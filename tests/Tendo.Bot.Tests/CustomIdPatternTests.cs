using System.Reflection;
using Discord.Interactions;
using Tendo.Bot.Components;
using Xunit;

namespace Tendo.Bot.Tests;

/// <summary>
/// カスタム ID の書き方を固定する。
///
/// ページ送りと確認ボタンが完全に無反応になる不具合を踏んだ。原因は
/// <c>page:*:prev</c> のように**ワイルドカードをパターンの中間に置いていた**こと。
/// どのハンドラにも一致せず、Discord.Net は command に null を渡してくるため
/// (さらにこちらの null ガード漏れで NullReferenceException になっていた)、
/// ログにも意味のある情報が出ないまま黙って壊れる。
///
/// 同じ書き方が再び混入しないよう、アセンブリ全体を走査して禁止する。
/// </summary>
public sealed class CustomIdPatternTests
{
    private const string Wildcard = "*";

    /// <summary>
    /// 登録されている全パターン (属性の第 1 引数)。
    /// </summary>
    public static TheoryData<string, string> AllPatterns()
    {
        var data = new TheoryData<string, string>();

        foreach (var (owner, pattern) in EnumeratePatterns())
        {
            data.Add(owner, pattern);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllPatterns))]
    public void ワイルドカードは末尾にしか置けない(string owner, string pattern)
    {
        var index = pattern.IndexOf(Wildcard, StringComparison.Ordinal);

        if (index < 0)
        {
            // ワイルドカードなしの固定パターンは問題ない。
            return;
        }

        Assert.True(
            index == pattern.Length - 1,
            $"{owner} のパターン \"{pattern}\" はワイルドカードが中間にある。" +
            "末尾に置くこと (例: \"page:*:prev\" ではなく \"page-prev:*\")。" +
            "中間に置くとどのハンドラにも一致せず、ボタンが無反応になる。");

        Assert.False(
            pattern.LastIndexOf(Wildcard, StringComparison.Ordinal) != index,
            $"{owner} のパターン \"{pattern}\" はワイルドカードを複数含んでいる。");
    }

    [Fact]
    public void 走査対象のパターンが実際に見つかっている()
    {
        // 反射の条件を間違えて 0 件になると、上のテストが素通りしてしまう。
        var patterns = EnumeratePatterns().ToList();

        Assert.NotEmpty(patterns);
        Assert.Contains(patterns, p => p.Pattern.StartsWith("page-prev:", StringComparison.Ordinal));
        Assert.Contains(patterns, p => p.Pattern.StartsWith("confirm-yes:", StringComparison.Ordinal));
    }

    [Fact]
    public void 判定ロジックが中間ワイルドカードを検出できる()
    {
        // テスト自体が壊れていないことの確認。実際に踏んだ形を与えて落ちること。
        var broken = Record.Exception(() => ワイルドカードは末尾にしか置けない("dummy", "page:*:prev"));
        Assert.NotNull(broken);

        var ok = Record.Exception(() => ワイルドカードは末尾にしか置けない("dummy", "page-prev:*"));
        Assert.Null(ok);
    }

    [Fact]
    public void ページ送りが生成するIDは登録パターンに一致する()
    {
        // 生成側と受け側がずれると無反応になる。両者を突き合わせる。
        var registered = EnumeratePatterns().Select(p => p.Pattern).ToList();

        Assert.Contains(registered, p => Matches(p, PaginationService.PreviousPrefix + "abc123"));
        Assert.Contains(registered, p => Matches(p, PaginationService.NextPrefix + "abc123"));
    }

    [Fact]
    public void 確認ボタンが生成するIDは登録パターンに一致する()
    {
        var registered = EnumeratePatterns().Select(p => p.Pattern).ToList();

        Assert.Contains(registered, p => Matches(p, PromptService.ConfirmPrefix + "abc123"));
        Assert.Contains(registered, p => Matches(p, PromptService.CancelPrefix + "abc123"));
    }

    /// <summary>末尾ワイルドカードのパターンとカスタム ID の照合。</summary>
    private static bool Matches(string pattern, string customId)
        => pattern.EndsWith(Wildcard, StringComparison.Ordinal)
            ? customId.StartsWith(pattern[..^1], StringComparison.Ordinal)
            : string.Equals(pattern, customId, StringComparison.Ordinal);

    private static IEnumerable<(string Owner, string Pattern)> EnumeratePatterns()
    {
        var assembly = typeof(PaginationService).Assembly;

        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var owner = $"{type.Name}.{method.Name}";

                foreach (var attribute in method.GetCustomAttributes<ComponentInteractionAttribute>())
                {
                    yield return (owner, attribute.CustomId);
                }

                foreach (var attribute in method.GetCustomAttributes<ModalInteractionAttribute>())
                {
                    yield return (owner, attribute.CustomId);
                }
            }
        }
    }
}
