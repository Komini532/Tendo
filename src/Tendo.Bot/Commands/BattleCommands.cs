using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Tendo.Bot.Services;
using Tendo.Game.Master;

namespace Tendo.Bot.Commands;

/// <summary>
/// 戦闘中の行動。旧 <c>eattack</c> / <c>eskill</c> / <c>ewait</c> / <c>ereset</c> / <c>efix</c>。
///
/// 1 コマンド 1 ファイルの方針だが、この 5 つは同じ 1 ターン処理を共有し、
/// 分けると却って追いにくくなるためまとめてある。
/// </summary>
public sealed class BattleCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly BattleService _battle;
    private readonly MasterData _data;

    public BattleCommands(BattleService battle, MasterData data)
    {
        _battle = battle;
        _data = data;
    }

    [SlashCommand("attack", "敵に攻撃します。")]
    public Task AttackAsync() => ActAsync(_data.FindSkill("攻撃")!, skipLearnCheck: false);

    [SlashCommand("wait", "「何もしない」をします。")]
    public Task WaitAsync()
        // 旧 ewait は do:"isk" なので習得判定を通らない。
        => ActAsync(_data.FindSkill("何もしない")!, skipLearnCheck: true);

    [SlashCommand("skill", "技を発動します。")]
    public async Task SkillAsync(
        [Summary("技名", "発動する技")]
        [Autocomplete(typeof(SkillAutocompleteHandler))]
        string name)
    {
        var skill = _data.FindSkill(name);
        if (skill is null)
        {
            // 旧実装は存在しない技名を黙って無視していた。
            // 何も返さないと interaction が失敗扱いになるので、本人にだけ知らせる。
            await RespondAsync($"「{name}」という技はありません。", ephemeral: true);
            return;
        }

        await ActAsync(skill, skipLearnCheck: false);
    }

    [SlashCommand("reset", "戦場をリセットします。")]
    public async Task ResetAsync()
    {
        await DeferAsync();
        var embeds = await _battle.ResetAsync(Context.Channel.Id);
        await SendAsync(embeds);
    }

    [SlashCommand("fix", "起動してるのに攻撃できなくなったら試してください。")]
    public async Task FixAsync()
    {
        _battle.ForceRelease(Context.Channel.Id);
        await RespondAsync(embed: new EmbedBuilder()
            .WithDescription("連続攻撃制限状態を解除しました。")
            .Build());
    }

    private async Task ActAsync(SkillDef skill, bool skipLearnCheck)
    {
        await DeferAsync();

        var embeds = await _battle.ActAsync(
            channelId: Context.Channel.Id,
            userId: Context.User.Id,
            userMention: Context.User.Mention,
            displayName: DisplayNameOf(Context.User),
            resolveName: ResolveName,
            skill: skill,
            skipLearnCheck: skipLearnCheck);

        await SendAsync(embeds);
    }

    private async Task SendAsync(IReadOnlyList<Embed> embeds)
    {
        if (embeds.Count == 0)
        {
            // 処理中で弾かれた場合。旧実装は無反応だったが、
            // slash command は必ず応答が要るので本人にだけ返す。
            await FollowupAsync("この戦場は処理中です。少し待ってからもう一度お試しください。",
                ephemeral: true);
            return;
        }

        await FollowupAsync(embed: embeds[0]);

        foreach (var embed in embeds.Skip(1))
        {
            await FollowupAsync(embed: embed);
        }
    }

    /// <summary>旧 <c>d.member ? d.member.displayName : d.user.username</c>。</summary>
    private static string DisplayNameOf(IUser user)
        => user is IGuildUser member ? member.DisplayName : user.Username;

    /// <summary>
    /// 報酬メッセージ用の表示名。
    ///
    /// 旧実装は <c>m.displayName || m.username</c> を直接読んでいたため、
    /// 参加者がサーバーを抜けていると報酬処理ごと例外で落ちていた。
    /// 引けない場合はユーザー ID を出して処理を続ける。
    /// </summary>
    private string ResolveName(ulong userId)
    {
        if (Context.Guild?.GetUser(userId) is { } guildUser)
        {
            return guildUser.DisplayName;
        }

        return Context.Client.GetUser(userId)?.Username ?? userId.ToString();
    }
}

/// <summary>
/// <c>/skill</c> の技名補完。旧実装は技名を直接打ち込ませていたので、
/// 同じ入力ができるよう全 90 技から前方一致で候補を出す。
/// </summary>
public sealed class SkillAutocompleteHandler : AutocompleteHandler
{
    private readonly MasterData _data;

    public SkillAutocompleteHandler(MasterData data) => _data = data;

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var typed = interaction.Data.Current.Value?.ToString() ?? string.Empty;

        var matches = _data.Skills
            .Where(s => s.Name.Contains(typed, StringComparison.OrdinalIgnoreCase))
            // Discord の候補は最大 25 件。
            .Take(25)
            .Select(s => new AutocompleteResult(s.Name, s.Name));

        return Task.FromResult(AutocompletionResult.FromSuccess(matches));
    }
}
