using Discord.Interactions;
using Tendo.Bot.Services;

namespace Tendo.Bot.Commands;

/// <summary>旧 <c>ereset</c> / <c>ere</c> / <c>ers</c>。</summary>
public sealed class ResetCommand : GameModuleBase
{
    private readonly BattleService _battle;

    public ResetCommand(BattleService battle) => _battle = battle;

    [SlashCommand("reset", "戦場をリセットします。")]
    public async Task ResetAsync()
    {
        await DeferAsync();

        var embeds = await _battle.ResetAsync(Context.Channel.Id);

        if (embeds.Count == 0)
        {
            // 旧実装は誰も戦っていないと無反応だった。
            await FollowupAsync("まだ誰もこの敵と戦っていません。", ephemeral: true);
            return;
        }

        await FollowupAsync(embed: embeds[0]);
        foreach (var embed in embeds.Skip(1))
        {
            await FollowupAsync(embed: embed);
        }
    }
}
