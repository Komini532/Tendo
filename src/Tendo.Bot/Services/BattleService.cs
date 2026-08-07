using Discord;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tendo.Bot.Configuration;
using Tendo.Data.Repositories;
using Tendo.Game.Engine;
using Tendo.Game.Master;
using Tendo.Game.Rendering;
using Tendo.Game.State;

namespace Tendo.Bot.Services;

/// <summary>
/// 旧 <c>fn(d, act)</c> の周辺処理 (データの読み書き・敵の出現・報酬配布) を束ねる。
///
/// 計算そのものは <see cref="Tendo.Game.Engine"/> にあり、ここは
/// 「DB から読む → エンジンに渡す → 結果を保存して Discord へ返す」だけを担う。
/// </summary>
public sealed class BattleService
{
    private readonly MasterData _data;
    private readonly GameDefaults _defaults;
    private readonly IPlayerRepository _players;
    private readonly IBattleRepository _battles;
    private readonly BattleGate _gate;
    private readonly DiscordOptions _options;
    private readonly ILogger<BattleService> _logger;
    private readonly IRandomSource _random = new SystemRandomSource();

    public BattleService(
        MasterData data,
        GameDefaults defaults,
        IPlayerRepository players,
        IBattleRepository battles,
        BattleGate gate,
        IOptions<DiscordOptions> options,
        ILogger<BattleService> logger)
    {
        _data = data;
        _defaults = defaults;
        _players = players;
        _battles = battles;
        _gate = gate;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 1 ターン進める。戻り値は Discord に送る embed の並び。
    ///
    /// 旧実装は <c>AWAIT</c> フラグを立てて処理し、完了時に降ろしていた。
    /// 例外で降ろし損ねると <c>efix</c> を打つまでそのチャンネルが操作不能になったので、
    /// ここでは必ず解放されるようにしている。
    /// </summary>
    public async Task<IReadOnlyList<Embed>> ActAsync(
        ulong channelId,
        ulong userId,
        string userMention,
        string displayName,
        Func<ulong, string> resolveName,
        SkillDef skill,
        bool skipLearnCheck,
        CancellationToken cancellationToken = default)
    {
        using var lease = _gate.Enter(channelId);
        if (!lease.Acquired)
        {
            // 旧 awaitfn()。処理中の連打は黙って捨てる。
            _logger.LogDebug("チャンネル {ChannelId} は処理中のため要求を無視しました", channelId);
            return [];
        }

        var battle = await EnsureBattleAsync(channelId, cancellationToken);
        var player = await _players.GetOrCreateAsync(userId, cancellationToken);

        var enemy = _data.FindEnemy(battle.EnemyCode);
        if (enemy is null)
        {
            // データが壊れている場合の保険。仕切り直して続行できるようにする。
            _logger.LogWarning(
                "チャンネル {ChannelId} の敵コード {Code} が見つかりません。再抽選します。",
                channelId,
                battle.EnemyCode);

            var respawned = await RespawnAsync(battle, cancellationToken);
            return respawned is null ? [] : [BuildEncounterEmbed(respawned)];
        }

        var difficulty = _data.FindDifficulty(battle.Difficulty) ?? _data.DefaultDifficulty;

        var context = new TurnContext
        {
            Battle = battle,
            Player = player,
            Enemy = enemy,
            Difficulty = difficulty,
            PlayerName = displayName,
        };

        var result = new TurnRunner(_data, _random).Execute(context, skill, skipLearnCheck);

        if (result.IsRejected)
        {
            // 旧実装は「${d.user}さんは…」と mention を頭に付けて返していた。
            return [Simple(userMention + result.RejectionMessage)];
        }

        await _battles.SaveAsync(battle, cancellationToken);
        await _players.SaveAsync(player, cancellationToken);

        var embeds = new List<Embed> { BuildTurnEmbed(result, displayName, enemy.Name) };

        switch (result.Outcome)
        {
            case TurnOutcome.EnemyDefeated:
                embeds.AddRange(await ResolveVictoryAsync(battle, enemy, resolveName, cancellationToken));
                break;

            case TurnOutcome.EnemyTransforms:
                // 旧 fn(d, {do:"rs", absolute:true, summon:einfo.next.code})
                var transformed = await ForceSpawnAsync(
                    battle, enemy, result.TransformInto, cancellationToken);
                if (transformed is not null)
                {
                    embeds.Add(BuildEncounterEmbed(transformed));
                }

                break;

            case TurnOutcome.PulledToAnotherField:
                embeds.Add(Simple($"{userMention}は{battle.Field}に吸い込まれた！"));
                var pulled = await ForceSpawnAsync(battle, enemy, summon: null, cancellationToken);
                if (pulled is not null)
                {
                    embeds.Add(BuildEncounterEmbed(pulled));
                }

                break;
        }

        return embeds;
    }

    /// <summary>
    /// 旧 <c>case "rs"</c>。戦場を仕切り直す。
    /// </summary>
    /// <param name="absolute">
    /// 内部からの強制リセット。false のときは誰も戦っていなければ何もしない
    /// (旧 <c>if( !enemy.n.length &amp;&amp; !act.absolute ) return;</c>)。
    /// </param>
    public async Task<IReadOnlyList<Embed>> ResetAsync(
        ulong channelId,
        string? summon = null,
        bool absolute = false,
        CancellationToken cancellationToken = default)
    {
        var battle = await EnsureBattleAsync(channelId, cancellationToken);
        var enemy = _data.FindEnemy(battle.EnemyCode);

        if (enemy is not null && !absolute)
        {
            if (battle.Participants.Count == 0)
            {
                return [];
            }

            // 逃走を許さない敵。
            if (enemy.NoEscape is { } noEscape)
            {
                return [Simple(Fence.Code($"- {noEscape.Message}", Fence.Diff))];
            }
        }

        var announcement = await ForceSpawnAsync(battle, enemy, summon, cancellationToken);
        return announcement is null ? [] : [BuildEncounterEmbed(announcement)];
    }

    /// <summary>旧 <c>efix</c>。詰まった処理中フラグを解放する。</summary>
    public void ForceRelease(ulong channelId) => _gate.ForceRelease(channelId);

    /// <summary>
    /// 戦場が無ければ作り、敵が入っていなければ草原の敵を出す。
    /// 旧 <c>fn()</c> 冒頭の「データが無ければ作ってやり直す」に相当。
    /// </summary>
    public async Task<BattleState> EnsureBattleAsync(
        ulong channelId,
        CancellationToken cancellationToken = default)
    {
        var battle = await _battles.GetOrCreateAsync(channelId, cancellationToken);

        if (!string.IsNullOrEmpty(battle.EnemyCode) && _data.FindEnemy(battle.EnemyCode) is not null)
        {
            return battle;
        }

        await RespawnAsync(battle, cancellationToken);
        return battle;
    }

    /// <summary>
    /// 旧 <c>fn()</c> の新規戦場生成。
    /// <code>en.filter(e => e.field.indexOf("草原")!=-1 &amp;&amp; e.rare!=2).choice()</code>
    /// レア度 2 だけを除いた草原の敵から選ぶ (レア度 1/3/4 は出うる)。
    /// </summary>
    private async Task<EncounterAnnouncement?> RespawnAsync(
        BattleState battle,
        CancellationToken cancellationToken)
    {
        var candidates = _data.Enemies
            .Where(e => e.Fields.Contains(battle.Field, StringComparer.Ordinal))
            .Where(e => e.Rarity != 2)
            .ToList();

        var chosen = JsMath.Choice(_random, candidates);
        if (chosen is null)
        {
            _logger.LogError("フィールド {Field} に出現できる敵がいません", battle.Field);
            return null;
        }

        var spawner = new EncounterSpawner(_data, _random);
        var announcement = spawner.Spawn(battle, chosen, battle.Level);

        await _battles.SaveAsync(battle, cancellationToken);
        return announcement;
    }

    /// <summary>敵を強制的に入れ替える (変身・吸い込み・リセット・召喚)。</summary>
    private async Task<EncounterAnnouncement?> ForceSpawnAsync(
        BattleState battle,
        EnemyDef? current,
        string? summon,
        CancellationToken cancellationToken)
    {
        // 参加者は全快して戦闘状態が解ける。
        if (battle.Participants.Count > 0)
        {
            var participants = await _players.FindManyAsync(battle.Participants, cancellationToken);
            RewardCalculator.ReleaseParticipants(participants);
            await _players.SaveManyAsync(participants, cancellationToken);
        }

        var spawner = new EncounterSpawner(_data, _random);

        var announcement = current is null
            ? await RespawnAsync(battle, cancellationToken)
            : spawner.Reset(battle, current, summon, absolute: true);

        if (announcement is not null)
        {
            await _battles.SaveAsync(battle, cancellationToken);
        }

        return announcement;
    }

    /// <summary>旧 <c>result()</c> + <c>nxevt()</c>。報酬を配って次の敵を出す。</summary>
    private async Task<IReadOnlyList<Embed>> ResolveVictoryAsync(
        BattleState battle,
        EnemyDef enemy,
        Func<ulong, string> resolveName,
        CancellationToken cancellationToken)
    {
        var participants = await _players.FindManyAsync(battle.Participants, cancellationToken);

        var reward = new RewardCalculator(_data, _random)
            .Apply(battle, enemy, participants, resolveName);

        await _players.SaveManyAsync(participants, cancellationToken);

        var embeds = new List<Embed> { Simple(reward.Description) };

        // 次の敵はレベルが 1 上がる。
        var spawner = new EncounterSpawner(_data, _random);
        var announcement = spawner.SpawnNext(battle);

        await _battles.SaveAsync(battle, cancellationToken);

        if (announcement is not null)
        {
            embeds.Add(BuildEncounterEmbed(announcement));
        }

        return embeds;
    }

    private Embed BuildTurnEmbed(TurnResult result, string playerName, string enemyName)
        => new EmbedBuilder()
            .WithTitle($"Turn {result.Turn}")
            .WithDescription(result.Description)
            .AddField(playerName, result.PlayerStatus, inline: true)
            .AddField(enemyName, result.EnemyStatus, inline: true)
            .Build();

    /// <summary>敵の出現メッセージ。画像はベース URL とファイル名を連結する。</summary>
    public Embed BuildEncounterEmbed(EncounterAnnouncement announcement)
    {
        var builder = new EmbedBuilder().WithDescription(announcement.Description);

        if (announcement.Picture is { Length: > 0 } picture)
        {
            builder.WithImageUrl(_options.EnemyImageBaseUrl + picture);
        }

        return builder.Build();
    }

    private static Embed Simple(string description)
        => new EmbedBuilder().WithDescription(description).Build();
}
