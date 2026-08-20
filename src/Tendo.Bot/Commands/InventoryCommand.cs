using Discord.Interactions;
using Tendo.Data.Repositories;
using Tendo.Game.Master;
using Tendo.Game.Rendering;

namespace Tendo.Bot.Commands;

/// <summary>
/// 旧 <c>einv</c> / <c>ei</c>。
///
/// 旧実装は <c>iteminfo.js</c> に説明がある品目しか表示しない。
/// <c>item.json</c> には 9 種あるが定義があるのは p/t/e/c の 4 種だけなので、
/// インビジブル等は所持していても一覧に出ない。旧挙動なので維持する。
/// </summary>
public sealed class InventoryCommand : GameModuleBase
{
    private readonly IPlayerRepository _players;
    private readonly MasterData _data;

    public InventoryCommand(IPlayerRepository players, MasterData data)
    {
        _players = players;
        _data = data;
    }

    [SlashCommand("inventory", "アイテムを確認します。")]
    public async Task InventoryAsync()
    {
        await DeferAsync();

        var player = await _players.GetOrCreateAsync(Context.User.Id);

        var blocks = new List<string>();
        foreach (var (itemId, quantity) in player.Items)
        {
            var info = _data.FindItemInfo(itemId);
            if (quantity == 0 || info is null)
            {
                continue;
            }

            blocks.Add(Fence.Code(
                $"[{info.Name}] 所持数：{quantity}個\n{info.Description}\n(短縮：{itemId})",
                Fence.Css));
        }

        if (blocks.Count == 0)
        {
            blocks.Add(Fence.Code("あなたはアイテムを持っていません。"));
        }

        await FollowupAsync(embed: Simple(Fence.Join(blocks)));
    }
}
