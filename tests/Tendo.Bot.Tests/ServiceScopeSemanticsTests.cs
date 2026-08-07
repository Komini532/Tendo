using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Tendo.Bot.Tests;

/// <summary>
/// 全スラッシュコマンドが無反応になっていた不具合の再発防止。
///
/// 原因は <c>InteractionCreatedHandler</c> が <c>ExecuteCommandAsync</c> に
/// 「自分で作って自分で破棄するスコープ」を渡していたこと。
/// <c>DefaultRunMode = RunMode.Async</c> では <c>ExecuteCommandAsync</c> がコマンド本体を
/// 待たずに返るため、本体が動き出す前にスコープが破棄されていた。
///
/// Discord.Net はコマンド実行ごとに、渡されたプロバイダから
/// <c>services.CreateScope()</c> で自前のスコープを切る。その呼び出しは
/// try の外かつ投げっぱなしタスク (<c>_ = Task.Run(...)</c>) の中にあるため、
/// ここで例外が出ても捕捉されずログにも残らず、interaction が ACK されないまま終わる。
/// Discord 側には「アプリケーションが応答しませんでした」とだけ表示される。
///
/// ここではその土台となる DI の意味論を固定しておく。
/// </summary>
public sealed class ServiceScopeSemanticsTests
{
    private static ServiceProvider BuildRoot()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedThing>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void 破棄済みスコープのプロバイダからはスコープを作れない()
    {
        // これが実際に起きていたこと。
        using var root = BuildRoot();

        var scope = root.CreateScope();
        var provider = scope.ServiceProvider;
        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => provider.CreateScope());
    }

    [Fact]
    public void 破棄済みスコープのプロバイダからはサービスも解決できない()
    {
        using var root = BuildRoot();

        var scope = root.CreateScope();
        var provider = scope.ServiceProvider;
        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => provider.GetRequiredService<ScopedThing>());
    }

    [Fact]
    public void ルートプロバイダからは何度でもスコープを切れる()
    {
        // 修正後の形。ハンドラはルートを渡し、スコープは Discord.Net が実行ごとに切る。
        using var root = BuildRoot();

        for (var i = 0; i < 3; i++)
        {
            using var scope = root.CreateScope();
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<ScopedThing>());
        }
    }

    [Fact]
    public void スコープごとに別のインスタンスになる()
    {
        // コマンドごとにリポジトリが使い回されないことの確認。
        using var root = BuildRoot();

        using var first = root.CreateScope();
        using var second = root.CreateScope();

        Assert.NotSame(
            first.ServiceProvider.GetRequiredService<ScopedThing>(),
            second.ServiceProvider.GetRequiredService<ScopedThing>());
    }

    private sealed class ScopedThing;
}
