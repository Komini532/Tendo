using Discord;
using Discord.Interactions;

namespace Tendo.Bot.Commands;

/// <summary>
/// 旧 <c>eping</c> の移植。
///
/// 旧実装は 2 通のメッセージを送って 2 つの遅延を測っていた。
/// <code>
/// case "ping":                                  // msg = 利用者のコマンド
///   d.channel.send(embed({description:`[PING]`}))// ping = 1 通目
///     .then(M => fn(d, {do:"pg", msg:Message, ping:M}));
/// case "pg":
///   d.channel.send(embed({description:`:ping_pong: \`計測中...\``}))  // M = 2 通目
///     .then(M => {
///       M.edit(... `:zero: ${ping.createdTimestamp - msg.createdTimestamp} ms`
///                  `:one:  ${M.createdTimestamp    - msg.createdTimestamp} ms` ));
///       act.ping.delete();                       // 1 通目は消す
///     });
/// </code>
///
/// slash 化しても「2 回の往復を測り、1 通目は消え、2 通目に :zero: と :one: が残る」
/// という見た目を保つ:
///   msg  → interaction の生成時刻
///   ping → 最初の応答メッセージ (計測後に削除)
///   M    → followup メッセージ (ここに結果を出す)
/// </summary>
public sealed class PingCommand : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ping", "応答速度を計測します。")]
    public async Task PingAsync()
    {
        var origin = Context.Interaction.CreatedAt;

        // 1 通目 (旧 `[PING]`)。計測用なので後で消す。
        await RespondAsync(embed: new EmbedBuilder().WithDescription("[PING]").Build());
        var first = await GetOriginalResponseAsync();

        // 2 通目 (旧 `計測中...`)。結果はこのメッセージに書き込む。
        var second = await FollowupAsync(
            embed: new EmbedBuilder().WithDescription(":ping_pong: `計測中...`").Build());

        var zero = (long)(first.CreatedAt - origin).TotalMilliseconds;
        var one = (long)(second.CreatedAt - origin).TotalMilliseconds;

        await second.ModifyAsync(m => m.Embed = new EmbedBuilder()
            .WithDescription(":ping_pong: `計測結果`")
            .AddField("ping", $":zero: `{zero} ms`\n:one: `{one} ms`")
            .Build());

        await DeleteOriginalResponseAsync();
    }
}
