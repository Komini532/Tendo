using Discord;
using Discord.Interactions;

namespace Tendo.Bot.Components;

/// <summary>👍 / 👎 ボタンの受け口。</summary>
public sealed class PromptComponents : InteractionModuleBase<SocketInteractionContext>
{
    private readonly PromptService _prompts;

    public PromptComponents(PromptService prompts) => _prompts = prompts;

    // ワイルドカードは末尾のみ。接頭辞は PromptService 側と対で定義してある。
    [ComponentInteraction("confirm-yes:*", ignoreGroupNames: true)]
    public Task ConfirmAsync(string id) => ResolveAsync(id, confirmed: true);

    [ComponentInteraction("confirm-no:*", ignoreGroupNames: true)]
    public Task CancelAsync(string id) => ResolveAsync(id, confirmed: false);

    private async Task ResolveAsync(string id, bool confirmed)
    {
        var message = await _prompts.ResolveAsync(id, Context.User.Id, confirmed);

        if (message is null)
        {
            await RespondAsync("この確認は操作した人だけが使えます。", ephemeral: true);
            return;
        }

        await DeferAsync();

        // 確認は一度きり。押されたらボタンを消す。
        await ModifyOriginalResponseAsync(m => m.Components = new ComponentBuilder().Build());
        await FollowupAsync(embed: new EmbedBuilder().WithDescription(message).Build());
    }
}
