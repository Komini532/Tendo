using System.Collections.Concurrent;
using Discord;

namespace Tendo.Bot.Components;

/// <summary>
/// 旧 <c>ctrl.react</c> の 👍 / 👎 による確認を、ボタン interaction に置き換えたもの。
///
/// ペットの改名・解放と、捕獲の可否確認で使う。
/// 捕獲だけは 10 秒で自動的に「いいえ」になる (旧 <c>setTimeout(() =&gt; no(), 10000)</c>)。
/// </summary>
public sealed class PromptService
{
    private readonly ConcurrentDictionary<string, Pending> _pending = new();

    /// <summary>
    /// 確認を登録してボタンを返す。
    /// </summary>
    /// <param name="userId">押せる人。旧実装も呼び出した本人だけが反応できた。</param>
    /// <param name="onConfirm">「はい」のときに実行する処理。</param>
    /// <param name="onCancel">「いいえ」または期限切れのときに実行する処理。</param>
    /// <param name="timeout">期限。null なら無期限 (旧改名・解放と同じ)。</param>
    public MessageComponent Create(
        ulong userId,
        Func<Task<string>> onConfirm,
        Func<Task<string>> onCancel,
        TimeSpan? timeout = null)
    {
        var id = Guid.NewGuid().ToString("N")[..16];

        _pending[id] = new Pending(userId, onConfirm, onCancel)
        {
            ExpiresAt = timeout is { } t ? DateTimeOffset.UtcNow + t : null,
        };

        return new ComponentBuilder()
            .WithButton("👍", $"confirm:{id}:yes", ButtonStyle.Success)
            .WithButton("👎", $"confirm:{id}:no", ButtonStyle.Secondary)
            .Build();
    }

    /// <summary>
    /// 押された結果を処理する。押せない人・不明な ID なら null。
    /// </summary>
    public async Task<string?> ResolveAsync(string id, ulong userId, bool confirmed)
    {
        if (!_pending.TryGetValue(id, out var pending) || pending.UserId != userId)
        {
            return null;
        }

        if (!_pending.TryRemove(id, out _))
        {
            // 同時に押された場合。片方だけが処理する。
            return null;
        }

        if (pending.ExpiresAt is { } expiry && DateTimeOffset.UtcNow > expiry)
        {
            return await pending.OnCancel();
        }

        return confirmed ? await pending.OnConfirm() : await pending.OnCancel();
    }

    /// <summary>
    /// 期限切れの確認を「いいえ」として確定させる。
    /// 捕獲の 10 秒自動キャンセルはこれで実現する。
    /// </summary>
    public async Task<IReadOnlyList<string>> ExpireAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var messages = new List<string>();

        foreach (var (id, pending) in _pending)
        {
            if (pending.ExpiresAt is { } expiry && now > expiry && _pending.TryRemove(id, out _))
            {
                messages.Add(await pending.OnCancel());
            }
        }

        return messages;
    }

    private sealed class Pending
    {
        public Pending(ulong userId, Func<Task<string>> onConfirm, Func<Task<string>> onCancel)
        {
            UserId = userId;
            OnConfirm = onConfirm;
            OnCancel = onCancel;
        }

        public ulong UserId { get; }

        public Func<Task<string>> OnConfirm { get; }

        public Func<Task<string>> OnCancel { get; }

        public DateTimeOffset? ExpiresAt { get; init; }
    }
}
