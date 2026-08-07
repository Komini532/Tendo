using System.Collections.Concurrent;

namespace Tendo.Game.State;

/// <summary>
/// 旧 <c>const AWAIT = new Map()</c> の置き換え。チャンネル単位の「処理中」フラグ。
///
/// 旧実装はこうなっていた。
/// <code>
/// case "atk":
///   if( AWAIT.get(d.channel.id) && ... ) return awaitfn();  // 何も言わず無視
///   AWAIT.set(d.channel.id, true);
///   fn(d, { do:"sk", ... });
/// </code>
///
/// 待ち行列ではなく「取りこぼし」である点が重要。処理中に届いた攻撃は
/// 実行されずに捨てられる。順番待ちにすると連打が全部通ってしまい、
/// 1 ターンで複数回攻撃できるようになってしまうので、旧実装と同じ挙動にする。
///
/// 旧実装は例外が起きると解放されないままになり、そのチャンネルは
/// <c>efix</c> を打つまで操作を受け付けなくなった。<see cref="Release"/> を
/// finally で必ず呼べば詰まらないが、<c>/fix</c> は利用者に見えるコマンドなので残す。
///
/// プロセス内でのみ有効。旧実装も同じで、複数プロセスで動かすと共有されない。
/// </summary>
public sealed class BattleGate
{
    private readonly ConcurrentDictionary<ulong, byte> _busy = new();

    /// <summary>
    /// 処理を開始できるなら true。既に処理中なら false (呼び出し側は黙って諦める)。
    /// </summary>
    public bool TryEnter(ulong channelId) => _busy.TryAdd(channelId, 0);

    /// <summary>処理の終わりに必ず呼ぶ。旧 <c>AWAIT.set(id, false)</c>。</summary>
    public void Release(ulong channelId) => _busy.TryRemove(channelId, out _);

    /// <summary>旧 <c>efix</c> / <c>/fix</c>。詰まったフラグを手動で解放する。</summary>
    public void ForceRelease(ulong channelId) => Release(channelId);

    /// <summary>そのチャンネルが処理中かどうか。</summary>
    public bool IsBusy(ulong channelId) => _busy.ContainsKey(channelId);

    /// <summary>
    /// <c>using</c> で確実に解放するための入場。
    /// 取得できなかった場合は <see cref="BattleGateLease.Acquired"/> が false になる。
    /// </summary>
    public BattleGateLease Enter(ulong channelId) => new(this, channelId, TryEnter(channelId));
}

/// <summary>
/// <see cref="BattleGate.Enter"/> の戻り値。破棄時に自動で解放する。
/// </summary>
public readonly struct BattleGateLease : IDisposable
{
    private readonly BattleGate _gate;
    private readonly ulong _channelId;

    internal BattleGateLease(BattleGate gate, ulong channelId, bool acquired)
    {
        _gate = gate;
        _channelId = channelId;
        Acquired = acquired;
    }

    /// <summary>入場できたか。false なら他の処理が走っているので何もしない。</summary>
    public bool Acquired { get; }

    public void Dispose()
    {
        if (Acquired)
        {
            _gate.Release(_channelId);
        }
    }
}
