using Tendo.Game.Engine;
using Xunit;

namespace Tendo.Game.Tests.Engine;

/// <summary>
/// JS の数値意味論の再現。期待値は Node で実際に評価して取得したもの
/// (<c>node -e 'console.log(Math.round(2.5))'</c> 等)。
/// </summary>
public sealed class JsMathTests
{
    [Theory]
    // JS は常に +∞ 方向へ丸める。C# 既定の銀行家丸めとは 2.5 と -2.5 で食い違う。
    [InlineData(0.5, 1)]
    [InlineData(-0.5, 0)]
    [InlineData(2.5, 3)]
    [InlineData(-2.5, -2)]
    [InlineData(1.5, 2)]
    [InlineData(3.4, 3)]
    [InlineData(3.6, 4)]
    [InlineData(-3.4, -3)]
    [InlineData(-3.6, -4)]
    [InlineData(0, 0)]
    // floor(x + 0.5) では 1 になってしまう値。JS は 0 を返す。
    [InlineData(0.49999999999999994, 0)]
    [InlineData(-0.49999999999999994, 0)]
    public void RoundはJavaScriptと同じ結果になる(double input, double expected)
        => Assert.Equal(expected, JsMath.Round(input));

    [Fact]
    public void Roundは銀行家丸めではない()
    {
        // これを間違えると全ダメージが静かにずれる。
        Assert.Equal(3, JsMath.Round(2.5));
        Assert.Equal(2, Math.Round(2.5)); // C# 既定 (比較用)
    }

    [Fact]
    public void Mulberry32はJavaScript版と同じ乱数列を返す()
    {
        // 期待値は tools/mulberry32.js を Node で実行して得たもの。
        // これが一致していないと差分テスト自体が意味を持たない。
        double[] expected =
        [
            0.97972826776094735,
            0.30675226449966431,
            0.48420542152598500,
            0.81793441250920296,
            0.50942836934700608,
            0.34747186047025025,
            0.07375754183158278,
            0.76639646734111011,
        ];

        var random = new Mulberry32(12345);

        foreach (var value in expected)
        {
            Assert.Equal(value, random.NextDouble(), precision: 15);
        }
    }

    [Fact]
    public void Randomは両端を含む()
    {
        // r.random(1,100) は 1 と 100 の両方を返しうる。
        var lowest = JsMath.Random(new FixedRandom(0.0), 1, 100);
        var highest = JsMath.Random(new FixedRandom(0.9999999), 1, 100);

        Assert.Equal(1, lowest);
        Assert.Equal(100, highest);
    }

    [Fact]
    public void Randomの両端は当選幅が半分になる()
    {
        // Math.round(rand * 99) + 1 なので、1 になるのは rand < 0.5/99 のときだけ。
        // 一様分布に「直す」とドロップ率や発動率が変わるため、この偏りを保つ。
        const int min = 1;
        const int max = 100;

        // 0.5/99 をわずかに下回る -> 1、わずかに上回る -> 2
        Assert.Equal(1, JsMath.Random(new FixedRandom(0.5 / 99 * 0.99), min, max));
        Assert.Equal(2, JsMath.Random(new FixedRandom(0.5 / 99 * 1.01), min, max));

        // 中央付近は幅 1/99 ぶんある。
        Assert.Equal(50, JsMath.Random(new FixedRandom(49.0 / 99), min, max));
    }

    [Fact]
    public void Choiceは添字が範囲内ならその要素を返す()
    {
        string[] source = ["a", "b", "c"];

        Assert.Equal("b", JsMath.Choice(new FixedRandom(0.0), source, 1));
        Assert.Equal("c", JsMath.Choice(new FixedRandom(0.0), source, 2));
    }

    [Fact]
    public void Choiceは添字が範囲外ならランダムに落ちる()
    {
        // 旧 this[c] || this[floor(random()*length)]
        string[] source = ["a", "b", "c"];

        Assert.Equal("a", JsMath.Choice(new FixedRandom(0.0), source, 99));
        Assert.Equal("c", JsMath.Choice(new FixedRandom(0.9), source, -1));
        Assert.Equal("b", JsMath.Choice(new FixedRandom(0.5), source, index: null));
    }

    [Fact]
    public void Choiceは空配列でnullを返す()
        => Assert.Null(JsMath.Choice(new FixedRandom(0.5), Array.Empty<string>(), 0));

    private sealed class FixedRandom : IRandomSource
    {
        private readonly double _value;

        public FixedRandom(double value) => _value = value;

        public double NextDouble() => _value;
    }
}
