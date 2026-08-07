using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tendo.Bot.Configuration;

namespace Tendo.Bot.Events;

/// <summary>
/// 旧 <c>ea.on("ready", ...)</c> の移植。
///
/// 旧実装はここでプレゼンス設定と BAN リストのメモリ読み込みを行っていた。
/// プレゼンス文字列 <c>"Stopped Developing"</c> は見た目に出るのでそのまま維持する。
/// BAN リストは Phase 6 で DB 由来の precondition に置き換わる (メモリ配列 <c>BANLIST</c> の廃止)。
/// </summary>
public sealed class ReadyHandler : IDiscordEventHandler
{
    /// <summary>旧 <c>ea.user.setPresence({ game: { name: "Stopped Developing" } })</c>。</summary>
    private const string PresenceText = "Stopped Developing";

    private readonly ILogger<ReadyHandler> _logger;
    private readonly InteractionService _interactions;
    private readonly DiscordOptions _options;

    /// <summary>
    /// コマンド登録を済ませたか。<c>Ready</c> は再接続のたびに発火するため、
    /// 素直に書くと接続が揺れるたびに全コマンドの再登録が走ってしまう。
    /// グローバル登録には 1 日あたりの回数制限があるので初回だけに絞る。
    /// </summary>
    private int _commandsRegistered;

    public ReadyHandler(
        ILogger<ReadyHandler> logger,
        InteractionService interactions,
        IOptions<DiscordOptions> options)
    {
        _logger = logger;
        _interactions = interactions;
        _options = options.Value;
    }

    public void Attach(DiscordSocketClient client)
    {
        client.Ready += () => OnReadyAsync(client);
    }

    private async Task OnReadyAsync(DiscordSocketClient client)
    {
        // プレゼンスは接続ごとに設定し直す必要がある。
        await client.SetGameAsync(PresenceText);

        // 旧実装の "ea start..." 相当。再接続でも出す。
        _logger.LogInformation(
            "ea start... {User} としてログイン、{GuildCount} サーバーに参加中",
            client.CurrentUser?.Username ?? "(unknown)",
            client.Guilds.Count);

        // コマンド登録は初回のみ。
        if (Interlocked.Exchange(ref _commandsRegistered, 1) == 1)
        {
            return;
        }

        if (_options.TestGuildId is { } guildId)
        {
            // 開発用: 反映が即時なギルドコマンドとして登録する。
            await _interactions.RegisterCommandsToGuildAsync(guildId);
            _logger.LogInformation(
                "スラッシュコマンドをギルド {GuildId} に登録しました ({Count} 件)",
                guildId,
                _interactions.SlashCommands.Count);
        }
        else
        {
            await _interactions.RegisterCommandsGloballyAsync();
            _logger.LogInformation(
                "スラッシュコマンドをグローバル登録しました ({Count} 件)。反映まで最大 1 時間かかります。",
                _interactions.SlashCommands.Count);
        }
    }
}
