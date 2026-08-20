using Discord;
using Discord.Interactions;
using Tendo.Bot.Components;
using Tendo.Bot.Preconditions;
using Tendo.Bot.Services;
using Tendo.Data.Repositories;
using Tendo.Game.Master;
using Tendo.Game.Rendering;
using Tendo.Game.State;

namespace Tendo.Bot.Commands;

/// <summary>
/// 旧 <c>emod [-サブコマンド]</c> の移植。管理者専用。
///
/// 他は 1 コマンド 1 ファイルにしているが、スラッシュコマンドの「グループ」は
/// 1 つのモジュールにまとまっている必要がある (<c>/mod</c> は 12 個のコマンドではなく
/// サブコマンドを持つ 1 個のコマンド)。そのためここだけまとめてある。
///
/// 旧 <c>-eval</c> は任意の JavaScript を実行するもので、安全な代替が無いため移植しない。
/// 旧 <c>-macro</c> はマクロ検知に紐づくもので、検知ごと移植しないため削除した。
/// </summary>
[Group("mod", "管理者用コマンド")]
[RequireBotOwner]
public sealed class ModCommands : GameModuleBase
{
    private readonly IPlayerRepository _players;
    private readonly IBattleRepository _battles;
    private readonly IBanRepository _bans;
    private readonly BattleService _battle;
    private readonly MasterData _data;
    private readonly PaginationService _pagination;

    public ModCommands(
        IPlayerRepository players,
        IBattleRepository battles,
        IBanRepository bans,
        BattleService battle,
        MasterData data,
        PaginationService pagination)
    {
        _players = players;
        _battles = battles;
        _bans = bans;
        _battle = battle;
        _data = data;
        _pagination = pagination;
    }

    /// <summary>旧 <c>-addeff [状態異常名] [レベル] [残りターン]</c>。</summary>
    [SlashCommand("addeff", "自分に状態異常を付与します。")]
    public async Task AddEffectAsync(
        [Summary("状態異常", "付与する状態異常")]
        [Autocomplete(typeof(EffectAutocompleteHandler))]
        string name,
        [Summary("レベル", "既定 1")] int level = 1,
        [Summary("残りターン", "既定 3")] int turns = 3)
    {
        await DeferAsync();

        var effect = _data.FindEffect(name);
        if (effect is null)
        {
            // 旧実装は存在しない名前を黙って無視していた。
            await FollowupAsync($"「{name}」という状態異常はありません。", ephemeral: true);
            return;
        }

        var player = await _players.GetOrCreateAsync(Context.User.Id);
        player.Effects.Add(new ActiveEffect(effect.Name, level, turns));
        await _players.SaveAsync(player);

        await FollowupAsync(embed: Simple(
            $"{Context.User.Mention}は管理者用コマンドで{effect.Name}状態を付与しました。"));
    }

    /// <summary>旧 <c>-remeff</c>。</summary>
    [SlashCommand("remeff", "自分の状態異常を全て解除します。")]
    public async Task RemoveEffectsAsync()
    {
        await DeferAsync();

        var player = await _players.GetOrCreateAsync(Context.User.Id);
        player.Effects.Clear();
        await _players.SaveAsync(player);

        await FollowupAsync(embed: Simple(
            $"{Context.User.Mention}は管理者用コマンドで全状態異常を解除しました。"));
    }

    /// <summary>旧 <c>-field [フィールド名] [難易度]</c>。</summary>
    [SlashCommand("field", "フィールドと難易度を強制的に変更します。")]
    public async Task FieldAsync(
        [Summary("フィールド", "移動先")]
        [Autocomplete(typeof(FieldAutocompleteHandler))]
        string field,
        [Summary("難易度", "既定 NORMAL")] string? difficulty = null)
    {
        await DeferAsync();

        if (_data.FindField(field) is null)
        {
            await FollowupAsync($"「{field}」というフィールドはありません。", ephemeral: true);
            return;
        }

        var resolved = difficulty is null
            ? _data.DefaultDifficulty
            : _data.FindDifficulty(difficulty) ?? _data.DefaultDifficulty;

        var battle = await _battle.EnsureBattleAsync(Context.Channel.Id);
        battle.Field = field;
        battle.Difficulty = resolved.Name;
        await _battles.SaveAsync(battle);

        await FollowupAsync(embed: Simple(
            $"`[DEBUG]`{field}(`DIF={resolved.Name}`)に移動します..."));

        foreach (var embed in await _battle.ResetAsync(Context.Channel.Id, absolute: true))
        {
            await FollowupAsync(embed: embed);
        }
    }

    /// <summary>旧 <c>-sum [敵名/コード]</c>。</summary>
    [SlashCommand("sum", "指定した敵を召還します。")]
    public async Task SummonAsync(
        [Summary("敵", "敵の名前かコード")]
        [Autocomplete(typeof(EnemyAutocompleteHandler))]
        string enemy)
    {
        await DeferAsync();

        if (_data.FindEnemyByNameOrCode(enemy) is null)
        {
            await FollowupAsync($"「{enemy}」という敵はいません。", ephemeral: true);
            return;
        }

        await FollowupAsync(embed: Simple($"`[DEBUG]`{enemy}を召還します..."));

        foreach (var embed in await _battle.ResetAsync(Context.Channel.Id, enemy, absolute: true))
        {
            await FollowupAsync(embed: embed);
        }
    }

    /// <summary>旧 <c>-g [ユーザーID] [額]</c>。</summary>
    [SlashCommand("gil", "指定したユーザーにギルを付与します。")]
    public Task GiveGilAsync(
        [Summary("相手", "付与する相手")] IUser target,
        [Summary("額", "付与するギル")] int amount)
        => GrantAsync(target, amount, isExperience: false);

    /// <summary>旧 <c>-exp [ユーザーID] [値]</c>。</summary>
    [SlashCommand("exp", "指定したユーザーに経験値を付与します。")]
    public Task GiveExpAsync(
        [Summary("相手", "付与する相手")] IUser target,
        [Summary("値", "付与する経験値")] int amount)
        => GrantAsync(target, amount, isExperience: true);

    /// <summary>旧 <c>-isk [技名]</c>。習得判定を飛ばして技を撃つ。</summary>
    [SlashCommand("isk", "習得判定を無視して技を発動します。")]
    public async Task ForceSkillAsync(
        [Summary("技名", "発動する技")]
        [Autocomplete(typeof(SkillAutocompleteHandler))]
        string name)
    {
        await DeferAsync();

        // 旧 sk.find(s => s.name==args[0]) || sk.find(s => s.name=="攻撃")
        var skill = _data.FindSkill(name) ?? _data.FindSkill("攻撃")!;

        var embeds = await _battle.ActAsync(
            channelId: Context.Channel.Id,
            userId: Context.User.Id,
            userMention: Context.User.Mention,
            displayName: DisplayName,
            resolveName: ResolveName,
            skill: skill,
            skipLearnCheck: true);

        if (embeds.Count == 0)
        {
            await FollowupAsync("この戦場は処理中です。", ephemeral: true);
            return;
        }

        await FollowupAsync(embed: embeds[0]);
        foreach (var embed in embeds.Skip(1))
        {
            await FollowupAsync(embed: embed);
        }
    }

    /// <summary>
    /// 旧 <c>-update [チャンネルID]</c>。
    /// 別のチャンネルの敵を出し直す (表示が壊れたときの復旧用)。
    /// </summary>
    [SlashCommand("update", "別のチャンネルの敵を出し直します。")]
    public async Task UpdateAsync([Summary("チャンネル", "対象チャンネル")] IChannel channel)
    {
        await DeferAsync();

        if (channel is not IMessageChannel messageChannel)
        {
            await FollowupAsync("メッセージを送れないチャンネルです。", ephemeral: true);
            return;
        }

        var battle = await _battles.FindAsync(channel.Id);
        if (battle is null)
        {
            await FollowupAsync("そのチャンネルには戦場がありません。", ephemeral: true);
            return;
        }

        // 旧実装は現在の敵コードをそのまま召喚し直していた。
        var embeds = await _battle.ResetAsync(channel.Id, battle.EnemyCode, absolute: true);

        foreach (var embed in embeds)
        {
            await messageChannel.SendMessageAsync(embed: embed);
        }

        await FollowupAsync($"<#{channel.Id}> の敵を出し直しました。", ephemeral: true);
    }

    /// <summary>旧 <c>-clist</c>。戦場の一覧。</summary>
    [SlashCommand("clist", "戦場の一覧を表示します。")]
    public async Task ChannelListAsync()
    {
        await DeferAsync();

        var battles = await _battles.ListByLevelDescendingAsync(200);
        var total = await _battles.CountAsync();

        if (battles.Count == 0)
        {
            await FollowupAsync("戦場がありません。", ephemeral: true);
            return;
        }

        var pages = new List<Embed>();

        for (var i = 0; i < battles.Count; i++)
        {
            var battle = battles[i];
            var channel = await Context.Client.GetChannelAsync(battle.ChannelId);
            var server = channel is IGuildChannel guild ? guild.Guild.Name : "DM";

            // 旧実装は snowflake の精度落ちを検出するためチェックサムを突き合わせ、
            // 一致しない行を「無効」として表示していた。
            // BIGINT UNSIGNED では欠落しないので、通常は常に CLEAR になる。
            var ok = battle.ChecksumChannelId is null
                     || battle.ChecksumChannelId == battle.ChannelId;

            var lines = new[]
            {
                $"[ CHANNEL DATA No.{i + 1} ]",
                $"[ID] チェックサム:{battle.ChecksumChannelId}\n" +
                $"取得:{battle.ChannelId} 判定:{battle.ChannelId} ({(ok ? "CLEAR" : "FAIL")})",
                $"[フィールド] {battle.Field}",
                $"[敵コード] {battle.EnemyCode}",
                $"[レベル] {battle.Level}",
                $"[体力] {battle.Hp} / {battle.MaxHp}",
                $"[サーバー] {server}",
            };

            var body = Fence.Code(lines, Fence.Css);
            if (!ok)
            {
                body += Fence.Code("[チェックサム通過失敗により無効です。]", Fence.Css);
            }

            pages.Add(new EmbedBuilder()
                .WithTitle($"[{i + 1} / {total}]")
                .WithDescription(body)
                .Build());
        }

        var (page, components) = _pagination.Create(Context.User.Id, pages);
        await FollowupAsync(embed: page, components: components);
    }

    /// <summary>旧 <c>-plist</c>。プレイヤーの一覧。</summary>
    [SlashCommand("plist", "プレイヤーの一覧を表示します。")]
    public async Task PlayerListAsync()
    {
        await DeferAsync();

        var players = await _players.ListByExperienceDescendingAsync(200);

        if (players.Count == 0)
        {
            await FollowupAsync("プレイヤーがいません。", ephemeral: true);
            return;
        }

        var pages = new List<Embed>();

        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var name = ResolveName(player.UserId);

            var info = Fence.Code(
                new[]
                {
                    $"[ User Info Name={name} ]",
                    $"[順位] {i + 1}位",
                    $"[レベル] {player.Level}",
                    $"[体力] {player.Hp} / {player.MaxHp}",
                    $"[魔力] {player.Mana} / {player.MaxMana}",
                },
                Fence.Css);

            var pet = player.Pet is { } p
                ? Fence.Code(
                    new[]
                    {
                        "[ Pet Info ]",
                        $"[名前] {p.Name}",
                        $"[種族] {_data.FindEnemy(p.EnemyCode)?.Name ?? "不明"}",
                        $"[レベル] {p.Level}",
                        $"[経験値] {p.Experience}",
                        $"[攻撃確率] {p.AttackChance}",
                    },
                    Fence.Css)
                : Fence.Code("[ペットは飼っていないようです。]", Fence.Css);

            pages.Add(new EmbedBuilder()
                .WithTitle($"[{i + 1} / {players.Count}]")
                .WithDescription(info + pet)
                .Build());
        }

        var (page, components) = _pagination.Create(Context.User.Id, pages);
        await FollowupAsync(embed: page, components: components);
    }

    /// <summary>旧 <c>-ban [ユーザーID/タグ]</c>。</summary>
    [SlashCommand("ban", "ユーザーの利用を禁止します。")]
    public async Task BanAsync([Summary("相手", "BAN する相手")] IUser target)
    {
        await DeferAsync();

        await _bans.AddAsync(target.Id);
        await FollowupAsync(embed: Simple($"[DEBUG] `{target.Username}`をBANしました。"));
    }

    /// <summary>旧実装には無いが、BAN を解除する手段が無いと運用できないので追加した。</summary>
    [SlashCommand("unban", "ユーザーの利用禁止を解除します。")]
    public async Task UnbanAsync([Summary("相手", "解除する相手")] IUser target)
    {
        await DeferAsync();

        await _bans.RemoveAsync(target.Id);
        await FollowupAsync(embed: Simple($"[DEBUG] `{target.Username}`のBANを解除しました。"));
    }

    /// <summary>旧 <c>-banlist</c>。</summary>
    [SlashCommand("banlist", "BAN 中のユーザーを表示します。")]
    public async Task BanListAsync()
    {
        await DeferAsync();

        var banned = await _bans.ListAsync();

        var lines = banned
            .Select((id, i) => $"[{i + 1}] {ResolveName(id)}")
            .ToList();

        if (lines.Count == 0)
        {
            lines.Add("[BAN中のユーザーはいません。]");
        }

        await FollowupAsync(embed: Simple(Fence.Code(lines, Fence.Css)));
    }

    /// <summary>旧 <c>-g</c> / <c>-exp</c> の共通部分。</summary>
    private async Task GrantAsync(IUser target, int amount, bool isExperience)
    {
        await DeferAsync();

        // 旧 if( !pg )return; — 0 は何もしない。
        if (amount == 0)
        {
            await FollowupAsync("0 は指定できません。", ephemeral: true);
            return;
        }

        // 旧 if( m.id==d.user.id )return; — 自分自身には付与できない。
        if (target.Id == Context.User.Id)
        {
            await FollowupAsync("自分自身には付与できません。", ephemeral: true);
            return;
        }

        var player = await _players.FindAsync(target.Id);
        if (player is null)
        {
            await FollowupAsync("相手のデータが見つかりませんでした。", ephemeral: true);
            return;
        }

        if (isExperience)
        {
            player.Experience += amount;
        }
        else
        {
            player.Gil += amount;
        }

        await _players.SaveAsync(player);

        var message = isExperience
            ? $"{Context.User.Mention}は{target.Mention}に`{amount}`経験値を付与した。\n" +
              "次戦闘後から反映されます。"
            : $"{Context.User.Mention}は{target.Mention}に`{amount}`ギルを付与した。";

        await FollowupAsync(embed: Simple(message));
    }
}
