using Discord;
using Discord.Interactions;

namespace Tendo.Bot.Components;

/// <summary>ページ送りボタンの受け口。</summary>
public sealed class PaginationComponents : InteractionModuleBase<SocketInteractionContext>
{
    private readonly PaginationService _pagination;

    public PaginationComponents(PaginationService pagination) => _pagination = pagination;

    [ComponentInteraction("page:*:prev", ignoreGroupNames: true)]
    public Task PreviousAsync(string sessionId) => MoveAsync(sessionId, -1);

    [ComponentInteraction("page:*:next", ignoreGroupNames: true)]
    public Task NextAsync(string sessionId) => MoveAsync(sessionId, +1);

    private async Task MoveAsync(string sessionId, int delta)
    {
        var move = _pagination.Move(sessionId, Context.User.Id, delta);

        if (move is null)
        {
            // 旧実装は呼び出した本人以外の操作を黙って無視していた。
            // interaction は必ず応答しないと失敗表示になるので、押した本人にだけ返す。
            await RespondAsync("このページ送りは操作した人だけが使えます。", ephemeral: true);
            return;
        }

        if (move.Expired || move.Page is null)
        {
            // 15 秒の無操作で打ち切り。旧実装は無反応になるだけだったが、
            // ボタンが残り続けると分かりにくいので無効化する。
            await DeferAsync();
            await ModifyOriginalResponseAsync(m =>
                m.Components = PaginationService.BuildComponents(sessionId, enabled: false));
            return;
        }

        await DeferAsync();
        await ModifyOriginalResponseAsync(m => m.Embed = move.Page);
    }
}
