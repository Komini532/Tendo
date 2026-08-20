using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tendo.Bot.Components;

namespace Tendo.Bot.Hosting;

/// <summary>
/// 期限切れの確認とページ送りを定期的に片付ける。
///
/// 旧実装は <c>setTimeout</c> でペット捕獲の 10 秒自動キャンセルを行い、
/// ページ送りは放置されたハンドラがメモリに残り続けていた。
/// ここでは定期的に掃除して、溜まらないようにする。
/// </summary>
public sealed class PromptExpiryService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private readonly PromptService _prompts;
    private readonly PaginationService _pagination;
    private readonly ILogger<PromptExpiryService> _logger;

    public PromptExpiryService(
        PromptService prompts,
        PaginationService pagination,
        ILogger<PromptExpiryService> logger)
    {
        _prompts = prompts;
        _pagination = pagination;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await _prompts.ExpireAsync();
                _pagination.Sweep();
            }
            catch (Exception ex)
            {
                // 掃除で落ちても本体は動かし続ける。
                _logger.LogWarning(ex, "期限切れ処理に失敗しました");
            }
        }
    }
}
