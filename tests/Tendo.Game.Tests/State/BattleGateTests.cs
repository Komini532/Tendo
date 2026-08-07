using Tendo.Game.State;
using Xunit;

namespace Tendo.Game.Tests.State;

/// <summary>旧 <c>AWAIT</c> Map の挙動。</summary>
public sealed class BattleGateTests
{
    [Fact]
    public void 処理中のチャンネルには入れない()
    {
        var gate = new BattleGate();

        Assert.True(gate.TryEnter(1UL));
        Assert.False(gate.TryEnter(1UL));
        Assert.True(gate.IsBusy(1UL));
    }

    [Fact]
    public void 別のチャンネルは互いに影響しない()
    {
        var gate = new BattleGate();

        Assert.True(gate.TryEnter(1UL));
        Assert.True(gate.TryEnter(2UL));
    }

    [Fact]
    public void 解放すればまた入れる()
    {
        var gate = new BattleGate();

        Assert.True(gate.TryEnter(1UL));
        gate.Release(1UL);

        Assert.False(gate.IsBusy(1UL));
        Assert.True(gate.TryEnter(1UL));
    }

    [Fact]
    public void usingを抜けると自動で解放される()
    {
        var gate = new BattleGate();

        using (var lease = gate.Enter(1UL))
        {
            Assert.True(lease.Acquired);

            using var denied = gate.Enter(1UL);
            Assert.False(denied.Acquired);
        }

        Assert.False(gate.IsBusy(1UL));
    }

    [Fact]
    public void 取得できなかったリースは解放しない()
    {
        // 入れなかった側が Dispose しても、入れた側のフラグを消してはいけない。
        var gate = new BattleGate();

        using var holder = gate.Enter(1UL);
        Assert.True(holder.Acquired);

        using (var denied = gate.Enter(1UL))
        {
            Assert.False(denied.Acquired);
        }

        Assert.True(gate.IsBusy(1UL));
    }

    [Fact]
    public void 詰まったフラグを手動で解放できる()
    {
        // 旧 efix / 現 /fix。
        var gate = new BattleGate();
        gate.TryEnter(1UL);

        gate.ForceRelease(1UL);

        Assert.False(gate.IsBusy(1UL));
    }

    [Fact]
    public void 同時に入れるのは一つだけ()
    {
        // 連打で複数ターン進んでしまわないことの確認。
        var gate = new BattleGate();
        var entered = 0;

        Parallel.For(0, 200, _ =>
        {
            if (gate.TryEnter(99UL))
            {
                Interlocked.Increment(ref entered);
            }
        });

        Assert.Equal(1, entered);
    }
}
