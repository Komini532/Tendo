using Discord;
using Tendo.Bot.Services;
using Tendo.Game.Master;

namespace Tendo.Bot.Commands;

/// <summary>
/// 1 ターン進める系のコマンド (<c>/attack</c> <c>/skill</c> <c>/wait</c>) の共通部分。
/// </summary>
public abstract class BattleCommandBase : GameModuleBase
{
    private readonly BattleService _battle;

    protected BattleCommandBase(BattleService battle, MasterData data)
    {
        _battle = battle;
        Data = data;
    }

    protected MasterData Data { get; }

    protected async Task ActAsync(SkillDef skill, bool skipLearnCheck)
    {
        await DeferAsync();

        var embeds = await _battle.ActAsync(
            channelId: Context.Channel.Id,
            userId: Context.User.Id,
            userMention: Context.User.Mention,
            displayName: DisplayName,
            resolveName: ResolveName,
            skill: skill,
            skipLearnCheck: skipLearnCheck);

        if (embeds.Count == 0)
        {
            // 旧実装は処理中の連打を無反応で捨てていた。
            // slash command は必ず応答が要るので、本人にだけ返す。
            await FollowupAsync(
                "この戦場は処理中です。少し待ってからもう一度お試しください。",
                ephemeral: true);
            return;
        }

        await FollowupAsync(embed: embeds[0]);

        foreach (var embed in embeds.Skip(1))
        {
            await FollowupAsync(embed: embed);
        }
    }
}
