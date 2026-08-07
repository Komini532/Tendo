using Discord;
using Discord.Interactions;
using Tendo.Bot.Services;
using Tendo.Data.Repositories;
using Tendo.Game.Engine;
using Tendo.Game.Master;
using Tendo.Game.Rendering;

namespace Tendo.Bot.Commands;

/// <summary>
/// 旧 <c>eshop</c>。
///
/// 旧実装は品目を一覧表示して「番号 個数」と発言させ、90 秒のあいだ何度でも
/// 買えるようにしていた。ここでは Select Menu で品目を選び、個数はモーダルで
/// 受け取る。メニューは残るので、続けて買えるところも同じ。
///
/// なお旧実装の購入処理は未定義の <c>separate()</c> を呼んでおり (ea.js:1306)、
/// 実際には例外で必ず失敗していた。画面の案内 (`[番号] [個数] で購入　例： 1 10`)
/// が意図するとおりに動くよう直してある。
/// </summary>
public sealed class ShopCommand : GameModuleBase
{
    private readonly BattleService _battle;
    private readonly MasterData _data;

    public ShopCommand(BattleService battle, MasterData data)
    {
        _battle = battle;
        _data = data;
    }

    [SlashCommand("shop", "アイテムを購入するショップ画面を開きます。")]
    public async Task ShopAsync()
    {
        await DeferAsync();

        var battle = await _battle.EnsureBattleAsync(Context.Channel.Id);
        var shop = _data.FindShop(battle.Field);

        if (shop is null || shop.Items.Count == 0)
        {
            await FollowupAsync(embed: Simple("このフィールドにはショップがないようです。"));
            return;
        }

        var lines = new List<string> { Fence.Code("買いたい品を選んでください", Fence.Css) };
        var menu = new SelectMenuBuilder()
            .WithCustomId($"shop:{Context.User.Id}")
            .WithPlaceholder("品物を選んでください")
            .WithMinValues(1)
            .WithMaxValues(1);

        for (var i = 0; i < shop.Items.Count && i < 25; i++)
        {
            var item = shop.Items[i];
            var name = _data.ItemNames.GetValueOrDefault(item.ItemId, item.ItemId);

            lines.Add(Fence.Code($"[{i + 1}] {name} - {item.Price} ギル", Fence.Css));
            menu.AddOption(name, item.ItemId, $"{item.Price} ギル");
        }

        await FollowupAsync(
            embed: new EmbedBuilder()
                .WithTitle($"{battle.Field}のショップ")
                .WithDescription(Fence.Join(lines))
                .Build(),
            components: new ComponentBuilder().WithSelectMenu(menu).Build());
    }
}

/// <summary>ショップの品目選択と個数入力。</summary>
public sealed class ShopComponents : InteractionModuleBase<SocketInteractionContext>
{
    private readonly BattleService _battle;
    private readonly IPlayerRepository _players;
    private readonly MasterData _data;
    private readonly IRandomSource _random = new SystemRandomSource();

    public ShopComponents(BattleService battle, IPlayerRepository players, MasterData data)
    {
        _battle = battle;
        _players = players;
        _data = data;
    }

    [ComponentInteraction("shop:*", ignoreGroupNames: true)]
    public async Task SelectAsync(string userId, string[] selected)
    {
        if (!ulong.TryParse(userId, out var owner) || owner != Context.User.Id)
        {
            await RespondAsync("このショップは開いた人だけが使えます。", ephemeral: true);
            return;
        }

        var itemId = selected.FirstOrDefault();
        if (itemId is null)
        {
            await DeferAsync();
            return;
        }

        var name = _data.ItemNames.GetValueOrDefault(itemId, itemId);

        // 個数はモーダルで受け取る。メニューは残るので続けて買える。
        var modal = new ModalBuilder()
            .WithTitle("購入個数")
            .WithCustomId($"shopbuy:{itemId}")
            .AddTextInput($"{name} をいくつ買いますか？", "quantity", placeholder: "1", value: "1")
            .Build();

        await RespondWithModalAsync(modal);
    }

    [ModalInteraction("shopbuy:*", ignoreGroupNames: true)]
    public async Task BuyAsync(string itemId, ShopQuantityModal modal)
    {
        await DeferAsync();

        if (!int.TryParse(modal.Quantity, out var quantity) || quantity < 1)
        {
            // 旧実装は解釈できない入力を黙って無視していた。
            await FollowupAsync("個数は 1 以上の数値で入力してください。", ephemeral: true);
            return;
        }

        var battle = await _battle.EnsureBattleAsync(Context.Channel.Id);
        var shop = _data.FindShop(battle.Field);
        var entry = shop?.Items.FirstOrDefault(i => i.ItemId == itemId);

        if (entry is null)
        {
            await FollowupAsync("その品物はここでは売っていません。", ephemeral: true);
            return;
        }

        var player = await _players.GetOrCreateAsync(Context.User.Id);
        var price = (long)entry.Price * quantity;
        var name = _data.ItemNames.GetValueOrDefault(itemId, itemId);

        // 旧実装は所持金が足りないと無反応だった。
        if (price > player.Gil)
        {
            await FollowupAsync(
                $"所持金が足りません。({price} ギル必要 / 所持 {player.Gil} ギル)",
                ephemeral: true);
            return;
        }

        string message;

        if (itemId == "exp")
        {
            // 経験値は所持品にならず、その場で加算される。
            // 1 個ごとに 価格/2 〜 価格/1.5 の範囲で抽選する。
            long gained = 0;
            for (var i = 0; i < quantity; i++)
            {
                gained += JsMath.Random(
                    _random,
                    (int)JsMath.Round(entry.Price / 2.0),
                    (int)JsMath.Round(entry.Price / 1.5));
            }

            player.Experience += gained;
            player.Gil -= price;

            message = $"{Context.User.Mention}は「{name}」を買いました！\n" +
                      $"{gained}の経験値を手に入れた！(次戦闘後から反映)\n(所持金：{player.Gil})";
        }
        else
        {
            player.AddItem(itemId, quantity);
            player.Gil -= price;

            message = $"{Context.User.Mention}は「{name}」を{quantity}個買いました！\n" +
                      $"(所持金：{player.Gil})";
        }

        await _players.SaveAsync(player);

        await FollowupAsync(embed: new EmbedBuilder().WithDescription(message).Build());
    }
}

/// <summary>購入個数のモーダル。</summary>
public sealed class ShopQuantityModal : IModal
{
    public string Title => "購入個数";

    [InputLabel("個数")]
    [ModalTextInput("quantity", placeholder: "1", initValue: "1")]
    public string Quantity { get; set; } = "1";
}
