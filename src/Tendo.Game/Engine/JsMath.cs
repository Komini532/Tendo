namespace Tendo.Game.Engine;

/// <summary>
/// JavaScript の数値まわりの意味論を再現する。
///
/// 移植で最も壊れやすいのがここ。ダメージ式も抽選もすべてこの 3 つを通るので、
/// 1 つでも違うとゲームバランスが静かにずれる。全計算は必ずここを経由させること。
/// </summary>
public static class JsMath
{
    /// <summary>
    /// JavaScript の <c>Math.round</c>。
    ///
    /// C# の <see cref="Math.Round(double)"/> は銀行家丸め (偶数丸め) なので
    /// <c>Math.Round(2.5) == 2</c> になってしまう。JS は常に +∞ 方向へ丸めるため
    /// <c>Math.round(2.5) === 3</c>、<c>Math.round(-2.5) === -2</c>。
    ///
    /// よくある <c>floor(x + 0.5)</c> は使えない。<c>0.49999999999999994 + 0.5</c> は
    /// 浮動小数の丸めでちょうど <c>1.0</c> になってしまい、JS の <c>0</c> と食い違う
    /// (差を取って補正しようとしても、その差自体が 0.5 に丸まるので検出できない)。
    ///
    /// 代わりに小数部を直接見る。<c>value - floor(value)</c> は倍精度の範囲で
    /// 必ず正確に求まるので、境界の判定がずれない。
    /// </summary>
    public static double Round(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return value;
        }

        var floor = Math.Floor(value);
        var fraction = value - floor;

        return fraction >= 0.5 ? floor + 1 : floor;
    }

    /// <summary>ダメージや HP など、整数として扱う値へ丸める。</summary>
    public static long RoundToLong(double value)
    {
        var rounded = Round(value);

        // 桁あふれは旧実装 (JS の Number) では起きないが、long では折り返してしまう。
        // 実データで到達しうる範囲ではないものの、静かに壊れるより飽和させる。
        if (rounded >= long.MaxValue)
        {
            return long.MaxValue;
        }

        return rounded <= long.MinValue ? long.MinValue : (long)rounded;
    }

    /// <summary>
    /// 旧 <c>fn.js</c> の <c>r.random(a, b)</c>。
    /// <code>let c = b - a, d = Math.round(Math.random() * c), e = d + a; return e;</code>
    ///
    /// <c>a</c> 以上 <c>b</c> 以下を返すが一様分布ではない。
    /// <c>Math.random()</c> が [0,1) なので、丸めの結果として両端 (a と b) だけ
    /// 当選幅が半分になる。<c>r.random(1,100) &lt;= 確率</c> という判定が
    /// あらゆる発動率に使われているため、この偏りも含めて再現する。
    /// </summary>
    public static int Random(IRandomSource random, int min, int max)
        => (int)Round(random.NextDouble() * (max - min)) + min;

    /// <summary>
    /// 旧 <c>Array.prototype.choice</c>。
    /// <code>return this[c] || this[Math.floor(Math.random() * this.length)];</code>
    ///
    /// 添字が範囲内でも「値が falsy なら」ランダムに落ちる点に注意。
    /// 状態異常のレベル別効果 <c>effects.choice(level-1)</c> はこれを踏むため、
    /// 定義数を超えるレベルの状態異常は毎ターン効果がランダムに変わる。
    /// </summary>
    /// <param name="index">
    /// 旧実装の添字。null は JS の <c>undefined</c> (= 引数なし) に相当し、必ずランダムになる。
    /// </param>
    public static T? Choice<T>(IRandomSource random, IReadOnlyList<T> source, int? index = null)
        where T : class
    {
        if (source.Count == 0)
        {
            // JS では this[n] が undefined になる。呼び出し側で null を弾く。
            return null;
        }

        if (index is { } i && i >= 0 && i < source.Count && source[i] is { } hit)
        {
            return hit;
        }

        return source[(int)Math.Floor(random.NextDouble() * source.Count)];
    }
}

/// <summary>
/// <c>Math.random()</c> の供給元。[0, 1) を返す。
///
/// 差分テストで JS と同じ乱数列を流し込めるよう、インターフェースにしてある。
/// </summary>
public interface IRandomSource
{
    double NextDouble();
}

/// <summary>本番用。スレッドセーフな共有乱数を使う。</summary>
public sealed class SystemRandomSource : IRandomSource
{
    public double NextDouble() => System.Random.Shared.NextDouble();
}

/// <summary>
/// テスト用の決定的な乱数源。JavaScript 側と同じ mulberry32 を使うので、
/// 同じ種を与えれば Node の参照実装と完全に同じ乱数列になる。
/// </summary>
public sealed class Mulberry32 : IRandomSource
{
    private uint _state;

    public Mulberry32(uint seed) => _state = seed;

    public double NextDouble()
    {
        // mulberry32 (JS 参照実装 tools/reference-battle.js と同一)
        _state += 0x6D2B79F5u;
        var t = _state;
        t = (uint)((t ^ (t >> 15)) * (t | 1u));
        t ^= t + (uint)((t ^ (t >> 7)) * (t | 61u));
        return ((t ^ (t >> 14)) >>> 0) / 4294967296.0;
    }
}
