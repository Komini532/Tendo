using Tendo.Game.State;
using Xunit;

namespace Tendo.Game.Tests.State;

/// <summary>旧 <c>mmo/newdata.js</c> の初期値が保たれていることを固定する。</summary>
public sealed class GameDefaultsTests
{
    private static readonly GameDefaults Defaults = GameDefaults.Load();

    [Fact]
    public void 新規プレイヤーの初期値が移植元どおり()
    {
        var player = Defaults.NewPlayer(123UL);

        Assert.Equal(123UL, player.UserId);
        Assert.Equal(100, player.Hp);
        Assert.Equal(100, player.MaxHp);
        Assert.Equal(0, player.Mana);
        Assert.Equal(0, player.MaxMana);
        Assert.Equal(1, player.Speed);
        Assert.Equal(1, player.Level);
        Assert.Equal(0, player.Experience);
        Assert.Equal(0, player.Gil);
        Assert.Empty(player.Skills);
        Assert.Empty(player.Effects);
        Assert.Null(player.Pet);

        // 旧 n:"0" は「非戦闘中」を表す番兵。null で持つ。
        Assert.Null(player.BattleChannelId);
    }

    [Fact]
    public void 新規戦場の初期値が移植元どおり()
    {
        var battle = Defaults.NewBattle(456UL);

        Assert.Equal(456UL, battle.ChannelId);
        Assert.Equal("草原", battle.Field);
        Assert.Equal("NORMAL", battle.Difficulty);
        Assert.Equal(1, battle.Level);
        Assert.Equal(0, battle.Turn);
        Assert.Empty(battle.Effects);
        Assert.Empty(battle.Participants);
        Assert.Equal(456UL, battle.ChecksumChannelId);
    }

    [Fact]
    public void 新規ペットの初期値が移植元どおり()
    {
        var pet = Defaults.NewPet();

        Assert.Equal(string.Empty, pet.Name);
        Assert.Equal(1, pet.Level);
        Assert.Equal(0, pet.Experience);
        Assert.Equal(0, pet.AttackChance);
    }

    [Fact]
    public void アイテム欄の並びがデータと一致する()
    {
        // 並びは /status と /inventory の表示順そのもの。
        // defaults.json 側が変わったら気付けるようにここで突き合わせる。
        Assert.Equal(Defaults.ItemSlotOrderFromData, ItemSlots.Order);
        Assert.Equal(
            ["p", "t", "e", "i", "r", "a", "d", "c", "m", "q", "s"],
            ItemSlots.Order);
    }

    [Fact]
    public void 新規プレイヤーは全アイテム枠を0で持つ()
    {
        var player = Defaults.NewPlayer(1UL);

        Assert.Equal(ItemSlots.Order, player.Items.Keys);
        Assert.All(player.Items.Values, v => Assert.Equal(0, v));

        // 未定義キーは例外ではなく 0 (旧 JS の undefined 相当の扱い)。
        Assert.Equal(0, player.GetItem("存在しない"));
    }

    [Fact]
    public void 敵の入れ替えでターン数と付随情報が戻る()
    {
        var battle = Defaults.NewBattle(1UL);
        battle.Turn = 9;
        battle.Effects.Add(new ActiveEffect("毒", 1, 3));
        battle.Participants.Add(2UL);
        battle.ChocolateFeeders.Add(3UL);

        battle.ResetForNewEnemy();

        // 旧実装は turn を含まない新オブジェクトを書くことで
        // Turn 表示を 1 から振り直していた。その挙動を明示的に再現する。
        Assert.Equal(0, battle.Turn);
        Assert.Empty(battle.Effects);
        Assert.Empty(battle.Participants);
        Assert.Empty(battle.ChocolateFeeders);
    }
}
