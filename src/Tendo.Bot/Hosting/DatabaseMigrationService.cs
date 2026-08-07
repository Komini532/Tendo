using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tendo.Data;

namespace Tendo.Bot.Hosting;

/// <summary>
/// 起動時にスキーマを適用する。旧実装が毎回 <c>create table if not exists</c> を
/// 流していたのと同じ位置づけだが、こちらは適用済みを記録して二重実行を防ぐ。
///
/// <see cref="DiscordBotService"/> より先に登録してあるので、
/// スキーマが整う前に Discord からのコマンドを受け付けることはない。
/// </summary>
public sealed class DatabaseMigrationService : IHostedService
{
    private readonly MigrationRunner _runner;
    private readonly ILogger<DatabaseMigrationService> _logger;

    public DatabaseMigrationService(MigrationRunner runner, ILogger<DatabaseMigrationService> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _runner.ApplyAsync(cancellationToken);
            _logger.LogInformation("データベースのスキーマを確認しました");
        }
        catch (Exception ex)
        {
            // 接続できない・権限がない等はここで気付けるようにする。
            _logger.LogError(
                ex,
                "データベースに接続できませんでした。TENDO_Database__ConnectionString を確認してください。");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
