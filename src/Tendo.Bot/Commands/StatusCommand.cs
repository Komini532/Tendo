using Discord;
using Discord.Interactions;
using Tendo.Bot.Rendering;
using Tendo.Data.Repositories;
using Tendo.Game.Engine;
using Tendo.Game.Master;
using Tendo.Game.Rendering;

namespace Tendo.Bot.Commands;

/// <summary>旧 <c>estatus</c> / <c>est</c>。</summary>
public sealed class StatusCommand : GameModuleBase
{
    /// <summary>旧 <c>let air = "** **";</c> — フィールド名を空に見せるための埋め草。</summary>
    private const string BlankFieldName = "** **";

    private readonly IPlayerRepository _players;
    private readonly MasterData _data;
    private readonly IGameClock _clock;

    public StatusCommand(IPlayerRepository players, MasterData data, IGameClock clock)
    {
        _players = players;
        _data = data;
        _clock = clock;
    }

    [SlashCommand("status", "自分のステータスを表示します。")]
    public async Task StatusAsync()
    {
        await DeferAsync();

        var player = await _players.GetOrCreateAsync(Context.User.Id);
        var rank = await _players.GetRankAsync(Context.User.Id);

        // 旧実装は所持数が 0 でないものだけを item.json の名前で並べる。
        var items = player.Items
            .Where(kv => kv.Value != 0)
            .Select(kv => $"{_data.ItemNames.GetValueOrDefault(kv.Key, kv.Key)} : {kv.Value}個");

        // 旧: 順位が取れないときは空文字が先頭に入り、結果として 1 行空く。
        var stats = new[]
        {
            rank is { } r ? $"[順位] {r}位" : string.Empty,
            $"[レベル] {player.Level}",
            $"[経験値] {player.Experience}",
            $"[ギル] {player.Gil}",
            $"[体力] {player.Hp}/{player.MaxHp}",
            $"[魔力] {player.Mana}/{player.MaxMana}",
            $"[攻撃力] {JsMath.RoundToLong(player.MaxHp / 3.0)}",
            $"[敏捷力] {player.Speed * player.Level}",
        };

        var effects = player.Effects.Count > 0
            ? Fence.Code(string.Join("\n", player.Effects.Select(e => e.ToString())), Fence.Css)
            : Fence.Code("状態異常なし", Fence.BrainFuck);

        var embed = new EmbedBuilder()
            .WithDescription(Fence.Code($"[USER STATUS] {DisplayName}", Fence.Fix))
            .AddField(BlankFieldName, Fence.Code(stats, Fence.Css), inline: true)
            .AddField(BlankFieldName, Fence.Code(new[] { "[アイテム]" }.Concat(items), Fence.Css), inline: true)
            .AddField(BlankFieldName, effects)
            .WithThumbnailUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
            .WithFooter(_clock.Now(), Context.Client.CurrentUser.GetAvatarUrl()
                                      ?? Context.Client.CurrentUser.GetDefaultAvatarUrl())
            .Build();

        await FollowupAsync(embed: embed);
    }
}
