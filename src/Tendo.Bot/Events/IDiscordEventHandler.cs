using Discord.WebSocket;

namespace Tendo.Bot.Events;

/// <summary>
/// 旧 <c>ea.js</c> の <c>ea.on("ready")</c> / <c>ea.on("message")</c> /
/// <c>ea.on("messageReactionAdd")</c> は 1 ファイルに直書きされていた。
/// 本移植では 1 イベント = 1 ファイルとし、DI で集めて一括購読する。
/// </summary>
public interface IDiscordEventHandler
{
    /// <summary>起動時に一度だけ呼ばれる。ここでクライアントのイベントに購読する。</summary>
    void Attach(DiscordSocketClient client);
}
