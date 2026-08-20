using Discord;
using Tendo.Bot.Components;
using Xunit;

namespace Tendo.Bot.Tests;

/// <summary>
/// 旧 <c>ctrl.page</c> の挙動 (本人のみ / 端で折り返す / 15 秒無操作で打ち切り) を固定する。
/// </summary>
public sealed class PaginationServiceTests
{
    private const ulong Owner = 100UL;
    private const ulong Stranger = 200UL;

    private static IReadOnlyList<Embed> Pages(int count)
        => Enumerable.Range(0, count)
            .Select(i => new EmbedBuilder().WithDescription($"page{i}").Build())
            .ToList();

    private static string Describe(Embed? embed) => embed?.Description ?? "(null)";

    [Fact]
    public void 最初は1ページ目を返す()
    {
        var service = new PaginationService();

        var (page, _) = service.Create(Owner, Pages(3));

        Assert.Equal("page0", page.Description);
    }

    [Fact]
    public void 次と前に移動できる()
    {
        var service = new PaginationService();
        var id = CreateAndExtractId(service, Pages(3));

        Assert.Equal("page1", Describe(service.Move(id, Owner, +1)!.Page));
        Assert.Equal("page2", Describe(service.Move(id, Owner, +1)!.Page));
        Assert.Equal("page1", Describe(service.Move(id, Owner, -1)!.Page));
    }

    [Fact]
    public void 端で折り返す()
    {
        // 旧: if (nowpage > pages.length-1) nowpage = 0;
        //     if (nowpage < 0) nowpage = pages.length-1;
        var service = new PaginationService();
        var id = CreateAndExtractId(service, Pages(3));

        service.Move(id, Owner, +1);
        service.Move(id, Owner, +1);
        Assert.Equal("page0", Describe(service.Move(id, Owner, +1)!.Page));

        Assert.Equal("page2", Describe(service.Move(id, Owner, -1)!.Page));
    }

    [Fact]
    public void 呼び出した本人以外は操作できない()
    {
        // 旧 if (U != user) return;
        var service = new PaginationService();
        var id = CreateAndExtractId(service, Pages(3));

        Assert.Null(service.Move(id, Stranger, +1));

        // 他人の操作でページが動いていないこと。
        Assert.Equal("page1", Describe(service.Move(id, Owner, +1)!.Page));
    }

    [Fact]
    public void 知らないセッションは期限切れ扱いになる()
    {
        var service = new PaginationService();

        var move = service.Move("そんなセッションはない", Owner, +1);

        Assert.NotNull(move);
        Assert.True(move.Expired);
        Assert.Null(move.Page);
    }

    [Fact]
    public void ページが一つだけならボタンは無効になる()
    {
        var service = new PaginationService();

        var (_, components) = service.Create(Owner, Pages(1));

        var buttons = components.Components
            .SelectMany(row => row.Components)
            .OfType<ButtonComponent>()
            .ToList();

        Assert.Equal(2, buttons.Count);
        Assert.All(buttons, b => Assert.True(b.IsDisabled));
    }

    private static string CreateAndExtractId(PaginationService service, IReadOnlyList<Embed> pages)
    {
        var (_, components) = service.Create(Owner, pages);

        var customId = components.Components
            .SelectMany(row => row.Components)
            .OfType<ButtonComponent>()
            .First()
            .CustomId;

        // "page-prev:{id}"
        return customId.Split(':')[1];
    }
}
