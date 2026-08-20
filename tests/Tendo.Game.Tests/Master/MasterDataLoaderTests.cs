using Tendo.Game.Master;
using Xunit;

namespace Tendo.Game.Tests.Master;

/// <summary>
/// <c>data/*.json</c> が移植元 <c>legacy/mmo/*.js</c> と等価であることを固定する。
///
/// 件数が変わったら「dump スクリプトか legacy 側が変わった」ということなので、
/// 意図した変更かどうかを必ず確認すること。
/// </summary>
public sealed class MasterDataLoaderTests
{
    private static readonly MasterData Data = MasterDataLoader.Load();

    [Theory]
    [InlineData("敵", 77)]
    [InlineData("技", 90)]
    [InlineData("状態異常", 37)]
    [InlineData("アビリティ", 17)]
    [InlineData("フィールド", 16)]
    [InlineData("移動条件", 16)]
    [InlineData("難易度", 4)]
    [InlineData("属性相性", 10)]
    [InlineData("ショップ", 16)]
    [InlineData("tips", 12)]
    [InlineData("アイテム情報", 4)]
    [InlineData("アイテム名", 9)]
    public void 件数が移植元と一致する(string category, int expected)
    {
        var actual = category switch
        {
            "敵" => Data.Enemies.Count,
            "技" => Data.Skills.Count,
            "状態異常" => Data.Effects.Count,
            "アビリティ" => Data.Abilities.Count,
            "フィールド" => Data.Fields.Count,
            "移動条件" => Data.FieldRequirements.Count,
            "難易度" => Data.Difficulties.Count,
            "属性相性" => Data.Affinities.Count,
            "ショップ" => Data.Shops.Count,
            "tips" => Data.Tips.Count,
            "アイテム情報" => Data.ItemInfo.Count,
            "アイテム名" => Data.ItemNames.Count,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
        };

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void 習得可能な技は42個ある()
    {
        // 旧 `sk.filter(s => s.learn != Infinity)`。
        Assert.Equal(42, Data.LearnableSkills.Count);
        Assert.Equal(48, Data.Skills.Count(s => !s.IsLearnable));
    }

    [Fact]
    public void 参照整合性の検証を通る()
    {
        // 挙動が壊れる参照があれば MasterDataException で落ちる。
        var warnings = MasterDataLoader.Validate(Data);

        // 旧データ由来の無害な綴り間違いだけが警告として残る。
        // これが増えたら legacy 側に新しい壊れた参照が入ったということ。
        Assert.Equal(
            ["状態異常「火傷耐性」の protect が未定義の「大火傷」を含んでいます (一致しないため無視されます)。"],
            warnings);
    }

    [Fact]
    public void 存在しない状態異常への耐性は無視される()
    {
        // 「火傷耐性」は存在しない「大火傷」を守るよう書かれている。
        // 耐性判定は付与しようとしている状態異常名との一致で行うため、
        // 存在しない名前は決して一致せず無害。旧挙動どおりデータは残す。
        var resistance = Data.FindEffect("火傷耐性");
        Assert.NotNull(resistance);
        Assert.Contains("大火傷", resistance.Protects);
        Assert.Null(Data.FindEffect("大火傷"));
        Assert.NotNull(Data.FindEffect("火傷"));
    }

    [Fact]
    public void 入れ子になっていない状態異常指定は空になっている()
    {
        // 旧 skill.js の 2 件は外側の配列を書き忘れており、
        // 旧実装の forEach では e[3] が "宣"/undefined になるため一度も発動しない。
        // dump スクリプトで空配列に正規化し、その「効果なし」を保っている。
        Assert.Empty(Data.FindSkill("虚ろな悪夢")!.SelfEffects);
        Assert.Empty(Data.FindSkill("ノーライフ")!.Effects);

        // 同じ技の正しく書かれている側は残っていること。
        Assert.Equal(3, Data.FindSkill("虚ろな悪夢")!.Effects.Count);
    }

    [Fact]
    public void 倍率が小数のまま読めている()
    {
        // 倍率を int にすると 1.333 が 1 に潰れてダメージが変わる。
        // 習得技の威力はバランス調整で整数のラダーに乗せたので、
        // 小数が生き残っているのは敵の個体倍率とフィールド倍率の側。
        Assert.Equal(1.333, Data.FindEnemy("oboro")?.HpMultiplier ?? 0, 3);
        Assert.Equal(1.444, Data.FindEnemy("curst")?.HpMultiplier ?? 0, 3);

        Assert.Contains(Data.Enemies, e => e.AttackMultiplier % 1 != 0);
        Assert.Contains(Data.Fields, f => f.HpMultiplier % 1 != 0);
    }

    [Fact]
    public void 名前が空の敵技は抽選されない()
    {
        // 「双頭龍」の 3 つ目は書きかけの { name:"" }。per 未指定なので 0 になる。
        // 乱数は必ず 1 以上なので random(1,100) <= 0 は成立せず、選ばれない。
        var twins = Data.FindEnemy("twins");
        Assert.NotNull(twins);
        var blank = twins.Skills.Single(s => s.Name.Length == 0);
        Assert.Equal(0, blank.Percent);
    }

    [Fact]
    public void 技の既定値が移植元どおり適用されている()
    {
        // legacy/mmo/skill.js 末尾の forEach で埋まる既定値。
        var attack = Data.FindSkill("攻撃");
        Assert.NotNull(attack);
        Assert.Equal(25, attack.Attack);
        Assert.Equal(0, attack.ManaCost);
        Assert.Equal(SkillType.Physical, attack.Type);
        Assert.Equal("無", attack.Element);
        Assert.Equal(100, attack.Hit);
        Assert.False(attack.IsLearnable);
        Assert.Empty(attack.Effects);
    }

    [Fact]
    public void 状態異常付与の配列がタプルとして読める()
    {
        // 毒牙: effect: [["毒", 1, 2, 15]]
        var skill = Data.FindSkill("毒牙");
        Assert.NotNull(skill);
        var poison = Assert.Single(skill.Effects);
        Assert.Equal("毒", poison.Name);
        Assert.Equal(1, poison.Level);
        Assert.Equal(2, poison.Turns);
        Assert.Equal(15, poison.Percent);
    }

    [Fact]
    public void 状態異常のレベル別効果が小数のまま読める()
    {
        // 毒 Lv.1 は damage: 1/32。丸めるとダメージ計算がずれる。
        var poison = Data.FindEffect("毒");
        Assert.NotNull(poison);
        Assert.Equal(1.0 / 32.0, poison.Levels[0].Damage, precision: 12);
        Assert.Equal(1.0 / 28.0, poison.Levels[1].Damage, precision: 12);
        Assert.Equal(1, poison.Levels[0].AttackMultiplier);
        Assert.Equal(1, poison.Levels[0].DefenseDivisor);
    }

    [Fact]
    public void 敵の既定値と参照が移植元どおり()
    {
        var seed = Data.FindEnemy("seed");
        Assert.NotNull(seed);
        Assert.Equal("悪魔の種", seed.Name);
        Assert.Equal("草", seed.Element);
        Assert.Equal(100, seed.MaxMana);
        Assert.Equal(0.5, seed.SpeedMultiplier);
        Assert.Equal(1, seed.HpMultiplier);          // 既定値
        Assert.Equal(3, seed.Skills.Count);
        Assert.Contains("草原", seed.Fields);

        // 既定ドロップ [["p",1,5],["t",1,3],["e",1,1]]
        Assert.Equal(3, seed.Drops.Count);
        Assert.Equal("p", seed.Drops[0].ItemId);
        Assert.Equal(5, seed.Drops[0].Percent);
    }

    [Fact]
    public void 敵画像はファイル名のみでベースURLを含まない()
    {
        // ベース URL は設定値と連結するので、データ側には残っていてはいけない。
        Assert.All(Data.Enemies, e =>
            Assert.False(e.Picture?.StartsWith("http", StringComparison.OrdinalIgnoreCase) ?? false));
        Assert.Equal("seed.png", Data.FindEnemy("seed")!.Picture);
    }

    [Fact]
    public void 物理無効と魔法無効のフラグが読めている()
    {
        Assert.True(Data.FindEffect("透明化")!.BlocksPhysical);
        Assert.True(Data.FindEffect("魔法障壁")!.BlocksMagic);
        Assert.True(Data.FindEffect("ツインウォール")!.BlocksPhysical);
        Assert.True(Data.FindEffect("ツインウォール")!.BlocksMagic);
        Assert.True(Data.FindEffect("リフレク")!.Reflects);
    }

    [Fact]
    public void 難易度の解放条件と状態異常が読めている()
    {
        var normal = Data.FindDifficulty("NORMAL");
        Assert.NotNull(normal);
        Assert.Equal(1, normal.RequiredEnemyLevel);
        Assert.Empty(normal.Effects);

        var lunatic = Data.FindDifficulty("LUNATIC");
        Assert.NotNull(lunatic);

        // 解放条件は 99999 で永久に届かなかった。敵Lvがフィールドの上限で頭打ちになる
        // 今の設計では届く値でないと意味が無いので、地獄の上限 2800 に合わせてある。
        Assert.Equal(2800, lunatic.RequiredEnemyLevel);
        Assert.Equal(2.2, lunatic.HpMultiplier);
        Assert.Equal(4, lunatic.Effects.Count);
        Assert.Equal("ツインウォール", lunatic.Effects[0].Name);
    }

    [Fact]
    public void 属性相性が読めている()
    {
        var fire = Data.FindAffinity("火");
        Assert.NotNull(fire);
        Assert.Contains("水", fire.WeakTo);   // 火は水に弱い
        Assert.Contains("草", fire.ResistantTo);
    }

    [Fact]
    public void ショップはフィールドごとに1件へ統合されている()
    {
        // 旧 shop.js は重複除去の条件を誤っており「湖」「秘境」「永遠悪夢」が 2 回入っていた。
        // FindShop は先頭一致なので、この 3 フィールドでは個別品 (インビジブル等) だけが引かれ、
        // ポーション・エーテル・エリクサーが買えなくなっていた。統合して両方買えるようにした。
        Assert.Equal(
            Data.Shops.Select(s => s.Field).Distinct().Count(),
            Data.Shops.Count);

        var lake = Data.FindShop("湖");
        Assert.NotNull(lake);
        Assert.Contains(lake.Items, i => i.ItemId == "i");
        Assert.Contains(lake.Items, i => i.ItemId == "p");
        Assert.Contains(lake.Items, i => i.ItemId == "t");
        Assert.Contains(lake.Items, i => i.ItemId == "e");
    }

    [Fact]
    public void アイテム情報は4種のみでインベントリ表示範囲を決める()
    {
        // items.json には 9 種あるが iteminfo は 4 種だけ。
        // /inventory はこの 4 種しか表示しない (旧挙動)。
        Assert.Equal(["p", "t", "e", "c"], Data.ItemInfo.Select(i => i.Id));
        Assert.True(Data.ItemNames.ContainsKey("i"));
        Assert.Null(Data.FindItemInfo("i"));
    }

    [Fact]
    public void 変身する敵と逃走禁止の敵が存在する()
    {
        var withNext = Data.Enemies.Where(e => e.Next is not null).ToList();
        var single = Assert.Single(withNext);
        Assert.Equal("delta2nd", single.Next!.Code);
        Assert.NotNull(Data.FindEnemy("delta2nd"));

        Assert.Equal(3, Data.Enemies.Count(e => e.NoEscape is not null));
    }

    [Fact]
    public void ダメージ式の基礎値が読めている()
    {
        Assert.Equal(100, Data.Fix.Player);
        Assert.Equal(50, Data.Fix.Enemy);
    }
}
