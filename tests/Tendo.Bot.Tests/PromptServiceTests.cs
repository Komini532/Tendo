using Discord;
using Tendo.Bot.Components;
using Xunit;

namespace Tendo.Bot.Tests;

/// <summary>
/// 旧 <c>ctrl.react</c> の 👍 / 👎 確認。
/// 本人のみ・一度きり・捕獲は 10 秒で自動キャンセル、という挙動を固定する。
/// </summary>
public sealed class PromptServiceTests
{
    private const ulong Owner = 100UL;
    private const ulong Stranger = 200UL;

    [Fact]
    public async Task はいを押すと確定処理が走る()
    {
        var service = new PromptService();
        var id = CreateAndExtractId(service, out _);

        var message = await service.ResolveAsync(id, Owner, confirmed: true);

        Assert.Equal("はい", message);
    }

    [Fact]
    public async Task いいえを押すとキャンセル処理が走る()
    {
        var service = new PromptService();
        var id = CreateAndExtractId(service, out _);

        Assert.Equal("いいえ", await service.ResolveAsync(id, Owner, confirmed: false));
    }

    [Fact]
    public async Task 本人以外は押せない()
    {
        var service = new PromptService();
        var id = CreateAndExtractId(service, out var invoked);

        Assert.Null(await service.ResolveAsync(id, Stranger, confirmed: true));

        // 他人の操作で処理が走っていないこと。
        Assert.False(invoked.Confirmed);
        Assert.False(invoked.Cancelled);
    }

    [Fact]
    public async Task 二度目は反応しない()
    {
        var service = new PromptService();
        var id = CreateAndExtractId(service, out _);

        Assert.NotNull(await service.ResolveAsync(id, Owner, confirmed: true));
        Assert.Null(await service.ResolveAsync(id, Owner, confirmed: true));
    }

    [Fact]
    public async Task 期限切れはキャンセル扱いになる()
    {
        // 旧: ペット捕獲は setTimeout(() => no(), 10000) で自動的に「いいえ」。
        var service = new PromptService();
        var flags = new Flags();

        service.Create(
            Owner,
            onConfirm: () => { flags.Confirmed = true; return Task.FromResult("はい"); },
            onCancel: () => { flags.Cancelled = true; return Task.FromResult("いいえ"); },
            timeout: TimeSpan.FromMilliseconds(-1));

        var expired = await service.ExpireAsync();

        Assert.Equal(["いいえ"], expired);
        Assert.True(flags.Cancelled);
        Assert.False(flags.Confirmed);
    }

    [Fact]
    public async Task 期限内なら自動キャンセルされない()
    {
        var service = new PromptService();
        var id = CreateAndExtractId(service, out _, TimeSpan.FromMinutes(10));

        Assert.Empty(await service.ExpireAsync());
        Assert.Equal("はい", await service.ResolveAsync(id, Owner, confirmed: true));
    }

    private sealed class Flags
    {
        public bool Confirmed { get; set; }

        public bool Cancelled { get; set; }
    }

    private static string CreateAndExtractId(
        PromptService service,
        out Flags flags,
        TimeSpan? timeout = null)
    {
        var captured = new Flags();
        flags = captured;

        var components = service.Create(
            Owner,
            onConfirm: () => { captured.Confirmed = true; return Task.FromResult("はい"); },
            onCancel: () => { captured.Cancelled = true; return Task.FromResult("いいえ"); },
            timeout);

        var customId = components.Components
            .SelectMany(row => row.Components)
            .OfType<ButtonComponent>()
            .First()
            .CustomId;

        // "confirm:{id}:yes"
        return customId.Split(':')[1];
    }
}
