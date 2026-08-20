using Discord;
using Discord.Interactions;
using Tendo.Data.Repositories;
using Tendo.Game.Rendering;

namespace Tendo.Bot.Commands;

/// <summary>
/// 旧 <c>eranking</c> / <c>erank</c>。敵レベルの高い順にサーバーを並べる。
///
/// 旧実装は全戦場を読み出して JS 側で並べ替えていたが、SQL で並べても結果は同じ。
/// 同じサーバーの複数チャンネルは最上位の 1 件だけを数える。
/// </summary>
public sealed class RankingCommand : GameModuleBase
{
    /// <summary>旧 <c>ranks.slice(0, 10)</c>。</summary>
    private const int DisplayCount = 10;

    /// <summary>重複除去で 10 件に届くよう、多めに読んでから絞る。</summary>
    private const int FetchCount = 300;

    private readonly IBattleRepository _battles;

    public RankingCommand(IBattleRepository battles) => _battles = battles;

    [SlashCommand("ranking", "サーバーランキングを表示します。")]
    public async Task RankingAsync()
    {
        await DeferAsync();

        var battles = await _battles.ListByLevelDescendingAsync(FetchCount);

        var seen = new HashSet<ulong>();
        var lines = new List<string>();

        foreach (var battle in battles)
        {
            if (lines.Count >= DisplayCount)
            {
                break;
            }

            // 旧実装は精度落ちした ID を補正するため checksum と突き合わせ、
            // 一致しない行を捨てていた。BIGINT UNSIGNED では必ず一致する。
            if (battle.ChecksumChannelId is { } checksum && checksum != battle.ChannelId)
            {
                continue;
            }

            var channel = await Context.Client.GetChannelAsync(battle.ChannelId);

            // Bot が見られなくなったチャンネルは一覧から外す (旧実装も表示できなかった)。
            if (channel is null)
            {
                continue;
            }

            var (key, name) = channel is IGuildChannel guildChannel
                ? (guildChannel.Guild.Id, guildChannel.Guild.Name)
                : (battle.ChannelId, $"[{ChannelLabel(channel)}]");

            // 同じサーバーは最上位の 1 件だけ。
            if (!seen.Add(key))
            {
                continue;
            }

            lines.Add($"[{lines.Count + 1}位] {name} (Lv.{battle.Level})");
        }

        if (lines.Count == 0)
        {
            lines.Add("まだ戦場がありません。");
        }

        await FollowupAsync(embed: Simple(Fence.Code(lines, Fence.Css)));
    }

    /// <summary>旧 <c>[${who.tag}'s DM Channel]</c> 相当。</summary>
    private static string ChannelLabel(IChannel channel)
        => channel is IDMChannel dm ? $"{dm.Recipient.Username}'s DM Channel" : "Unknown";
}
