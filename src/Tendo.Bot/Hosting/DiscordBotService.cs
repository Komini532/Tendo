using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tendo.Bot.Configuration;
using Tendo.Bot.Events;

namespace Tendo.Bot.Hosting;

/// <summary>
/// 旧 <c>ea.login(config.token)</c> と <c>main.js</c> の HTTP keep-alive
/// (glitch.com がスリープしないようにするための残骸) の置き換え。
/// glitch は終了したので keep-alive サーバーは移植せず、通常の常駐サービスにする。
/// </summary>
public sealed class DiscordBotService : IHostedService
{
    private readonly ILogger<DiscordBotService> _logger;
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly IEnumerable<IDiscordEventHandler> _handlers;
    private readonly IServiceProvider _services;
    private readonly DiscordOptions _options;

    public DiscordBotService(
        ILogger<DiscordBotService> logger,
        DiscordSocketClient client,
        InteractionService interactions,
        IEnumerable<IDiscordEventHandler> handlers,
        IServiceProvider services,
        IOptions<DiscordOptions> options)
    {
        _logger = logger;
        _client = client;
        _interactions = interactions;
        _handlers = handlers;
        _services = services;
        _options = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var handler in _handlers)
        {
            handler.Attach(_client);
            _logger.LogDebug("イベントハンドラ {Handler} を購読しました", handler.GetType().Name);
        }

        // Commands/ 配下の InteractionModuleBase を全て拾う。
        var modules = await _interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);
        _logger.LogInformation(
            "コマンドモジュールを {ModuleCount} 個読み込みました (スラッシュ {SlashCount} / " +
            "コンポーネント {ComponentCount} / モーダル {ModalCount}): {Commands}",
            modules.Count(),
            _interactions.SlashCommands.Count,
            _interactions.ComponentCommands.Count,
            _interactions.ModalCommands.Count,
            string.Join(", ", _interactions.SlashCommands.Select(c => "/" + c.Name)));

        // ボタンや Select Menu のカスタム ID は、生成側と受け側のパターンが
        // ずれると一致せず無反応になる (ログにも出ない)。実際に一度踏んだので、
        // 登録されているパターンを起動時に見えるようにしておく。
        _logger.LogDebug(
            "コンポーネントのパターン: {Patterns}",
            string.Join(", ", _interactions.ComponentCommands.Select(c => c.Name)));

        await _client.LoginAsync(TokenType.Bot, _options.Token);
        await _client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Discord クライアントを停止します");
        await _client.StopAsync();
        await _client.LogoutAsync();
    }
}
