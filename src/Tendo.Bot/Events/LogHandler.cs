using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Tendo.Bot.Events;

/// <summary>
/// Discord.Net 内部ログを <see cref="ILogger"/> に橋渡しする。
/// 旧実装は <c>console.log</c> 直書きだった。
/// </summary>
public sealed class LogHandler : IDiscordEventHandler
{
    private readonly ILogger<LogHandler> _logger;
    private readonly InteractionService _interactions;

    public LogHandler(ILogger<LogHandler> logger, InteractionService interactions)
    {
        _logger = logger;
        _interactions = interactions;
    }

    public void Attach(DiscordSocketClient client)
    {
        client.Log += OnLogAsync;
        _interactions.Log += OnLogAsync;
    }

    private Task OnLogAsync(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information,
        };

#pragma warning disable CA2254 // Discord.Net 側のメッセージは動的なのでテンプレート化できない
        _logger.Log(level, message.Exception, "[{Source}] {Message}", message.Source, message.Message);
#pragma warning restore CA2254

        return Task.CompletedTask;
    }
}
