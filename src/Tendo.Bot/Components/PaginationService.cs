using System.Collections.Concurrent;
using Discord;

namespace Tendo.Bot.Components;

/// <summary>
/// 旧 <c>ctrl.page</c> の置き換え。
///
/// 旧実装は ◀ ▶ のリアクションを押させ、押されるたびにメッセージを編集していた。
/// <code>
/// REACTION.set(M.id, (R, U) => {
///   if (U != user) return;                       // 呼び出した本人だけ
///   if (movetime - c.lastmoved > 15000) {        // 15 秒無操作で打ち切り
///     REACTION.delete(M.id); return;
///   }
///   ...
///   if (c.nowpage > pages.length - 1) c.nowpage = 0;   // 端で折り返す
///   if (c.nowpage < 0) c.nowpage = pages.length - 1;
/// });
/// </code>
///
/// ボタン interaction に置き換えたが、「本人のみ」「15 秒無操作で打ち切り」
/// 「端で折り返す」という挙動はそのまま残している。
/// </summary>
public sealed class PaginationService
{
    /// <summary>旧 <c>movetime - c.lastmoved > 15000</c>。</summary>
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(15);

    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    /// <summary>ページ送りの一式を作る。戻り値をそのまま送信する。</summary>
    public (Embed Page, MessageComponent Components) Create(
        ulong userId,
        IReadOnlyList<Embed> pages)
    {
        ArgumentOutOfRangeException.ThrowIfZero(pages.Count);

        var id = Guid.NewGuid().ToString("N")[..16];
        _sessions[id] = new Session(userId, pages);

        return (pages[0], BuildComponents(id, pages.Count > 1));
    }

    /// <summary>
    /// ページを移動する。押した人が違う、期限切れ、といった場合は null を返す。
    /// </summary>
    public PageMove? Move(string sessionId, ulong userId, int delta)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            // 再起動などで失われた場合。旧実装でも無反応になっていた。
            return new PageMove(null, Expired: true);
        }

        if (session.UserId != userId)
        {
            // 旧 if (U != user) return; — 他人が押しても何も起きない。
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - session.LastMoved > IdleTimeout)
        {
            _sessions.TryRemove(sessionId, out _);
            return new PageMove(null, Expired: true);
        }

        var page = session.Current + delta;

        // 端で折り返す。
        if (page > session.Pages.Count - 1)
        {
            page = 0;
        }

        if (page < 0)
        {
            page = session.Pages.Count - 1;
        }

        session.Current = page;
        session.LastMoved = now;

        return new PageMove(session.Pages[page], Expired: false);
    }

    /// <summary>期限切れになったセッションを掃除する。</summary>
    public void Sweep()
    {
        var threshold = DateTimeOffset.UtcNow - IdleTimeout;

        foreach (var (id, session) in _sessions)
        {
            if (session.LastMoved < threshold)
            {
                _sessions.TryRemove(id, out _);
            }
        }
    }

    /// <summary>
    /// ボタンのカスタム ID の接頭辞。
    ///
    /// **ワイルドカードは必ずパターンの末尾に置くこと。**
    /// 以前は <c>page:{id}:prev</c> という形にしていたが、受け側のパターンが
    /// <c>page:*:prev</c> とワイルドカードを中間に持つ形になり、
    /// どのハンドラにも一致せずボタンが完全に無反応になっていた。
    /// 接頭辞で種類を分け、可変部分を末尾だけにする。
    /// </summary>
    public const string PreviousPrefix = "page-prev:";

    /// <inheritdoc cref="PreviousPrefix" />
    public const string NextPrefix = "page-next:";

    public static MessageComponent BuildComponents(string sessionId, bool enabled)
        => new ComponentBuilder()
            .WithButton("◀", PreviousPrefix + sessionId, ButtonStyle.Secondary, disabled: !enabled)
            .WithButton("▶", NextPrefix + sessionId, ButtonStyle.Secondary, disabled: !enabled)
            .Build();

    /// <param name="Page">移動先。期限切れなら null。</param>
    /// <param name="Expired">15 秒の無操作で打ち切られたか。</param>
    public sealed record PageMove(Embed? Page, bool Expired);

    private sealed class Session
    {
        public Session(ulong userId, IReadOnlyList<Embed> pages)
        {
            UserId = userId;
            Pages = pages;
        }

        public ulong UserId { get; }

        public IReadOnlyList<Embed> Pages { get; }

        public int Current { get; set; }

        public DateTimeOffset LastMoved { get; set; } = DateTimeOffset.UtcNow;
    }
}
