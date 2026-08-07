using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Tendo.Bot.Rendering;
using Tendo.Data;

namespace Tendo.Bot.Commands;

/// <summary>
/// 旧 <c>einfo</c> の移植。
///
/// <code>
/// ctrl.multi(["mmo_user","mmo_channel"], key => ctrl.all(key)).then(rows => {
///   let info = [`[導入サーバー] ${ea.guilds.size}鯖`,
///               `[認識しているユーザー] ${ea.users.size}人`,
///               `[参加しているユーザー] ${rows[0].length}人`,
///               `[認識しているチャンネル] ${ea.channels.size}個`,
///               `[戦場の数] ${rows[1].length}個`];
///   d.channel.send(embed({ title:"EA Info", description: code(info.join("\n"), "CSS") }));
/// });
/// </code>
/// </summary>
public sealed class InfoCommand : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IGameStatisticsRepository _statistics;

    public InfoCommand(IGameStatisticsRepository statistics)
    {
        _statistics = statistics;
    }

    [SlashCommand("info", "Bot の稼働状況を表示します。")]
    public async Task InfoAsync()
    {
        await DeferAsync();

        var stats = await _statistics.GetAsync();
        var client = Context.Client;

        var lines = new[]
        {
            $"[導入サーバー] {client.Guilds.Count}鯖",
            $"[認識しているユーザー] {CountKnownUsers(client)}人",
            $"[参加しているユーザー] {stats.PlayerCount}人",
            $"[認識しているチャンネル] {CountKnownChannels(client)}個",
            $"[戦場の数] {stats.BattleCount}個",
        };

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("EA Info")
            .WithDescription(Fence.Code(lines, Fence.Css))
            .Build());
    }

    /// <summary>
    /// 旧 <c>ea.users.size</c>。
    ///
    /// 旧 discord.js v11 は全インテント既定だったのでユーザーキャッシュが埋まっていたが、
    /// 現行 API で同じ値を得るには GuildMembers 特権インテントが要る。
    /// 本移植は特権インテントなし方針なので、各サーバーの公称メンバー数の合計で代替する
    /// (特権インテントなしで取得できる唯一の人数)。表示書式は変えていない。
    /// </summary>
    private static int CountKnownUsers(DiscordSocketClient client)
        => client.Guilds.Sum(g => g.MemberCount);

    /// <summary>旧 <c>ea.channels.size</c>。</summary>
    private static int CountKnownChannels(DiscordSocketClient client)
        => client.Guilds.Sum(g => g.Channels.Count);
}
