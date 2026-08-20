using System.Text.Json.Serialization;

namespace Tendo.Game.Master;

/// <summary>旧 <c>mmo/field.js</c>。フィールド 16 種。</summary>
public sealed record FieldDef
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>獲得経験値の倍率。</summary>
    [JsonPropertyName("exp")]
    public double ExpMultiplier { get; init; } = 1;
}

/// <summary>
/// 旧 <c>mmo/fieldrequire.js</c>。13 件。
/// 「今いるフィールドが <see cref="ConnectedFrom"/> に含まれていれば、
/// 条件を満たす限りこの <see cref="Name"/> へ移動できる」という向きで引く。
/// </summary>
public sealed record FieldRequirementDef
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>必要な敵レベル。</summary>
    [JsonPropertyName("elv")]
    public int RequiredEnemyLevel { get; init; }

    /// <summary>必要なプレイヤーレベル。</summary>
    [JsonPropertyName("plv")]
    public int RequiredPlayerLevel { get; init; }

    /// <summary>移動元として認められるフィールド名。</summary>
    [JsonPropertyName("require")]
    public IReadOnlyList<string> ConnectedFrom { get; init; } = [];
}

/// <summary>旧 <c>mmo/difficultity.js</c>。NORMAL / HARD / EXTREME / LUNATIC。</summary>
public sealed record DifficultyDef
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("des")]
    public string Description { get; init; } = string.Empty;

    /// <summary>敵 HP 倍率。</summary>
    [JsonPropertyName("hp")]
    public double HpMultiplier { get; init; } = 1;

    /// <summary>敵の与ダメージ倍率。</summary>
    [JsonPropertyName("atk")]
    public double AttackMultiplier { get; init; } = 1;

    /// <summary>獲得経験値倍率。</summary>
    [JsonPropertyName("exp")]
    public double ExpMultiplier { get; init; } = 1;

    /// <summary>戦闘 1 ターン目に敵へ付与を試みる状態異常。</summary>
    [JsonPropertyName("eff")]
    public IReadOnlyList<StatusApplication> Effects { get; init; } = [];

    /// <summary>解放に必要な敵レベル。旧データは要素 1 個の配列。</summary>
    [JsonPropertyName("require")]
    public IReadOnlyList<int> Require { get; init; } = [];

    /// <summary>旧 <c>d.require[0]</c>。</summary>
    [JsonIgnore]
    public int RequiredEnemyLevel => Require.Count > 0 ? Require[0] : 0;
}

/// <summary>旧 <c>mmo/aisyou.js</c>。属性相性 10 種。</summary>
public sealed record AffinityDef
{
    /// <summary>防御側の属性。</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>この属性で攻撃されると 1.333 倍になる (弱点)。</summary>
    [JsonPropertyName("high")]
    public IReadOnlyList<string> WeakTo { get; init; } = [];

    /// <summary>この属性で攻撃されると 0.666 倍になる (軽減)。</summary>
    [JsonPropertyName("low")]
    public IReadOnlyList<string> ResistantTo { get; init; } = [];
}

/// <summary>ショップの品目。</summary>
public sealed record ShopItemDef
{
    [JsonPropertyName("id")]
    public required string ItemId { get; init; }

    [JsonPropertyName("price")]
    public int Price { get; init; } = 1;
}

/// <summary>
/// 旧 <c>mmo/shop.js</c>。19 件。
///
/// 旧実装は重複除去の条件に存在しないプロパティ (<c>s.name</c>) を使っていたため、
/// 個別定義のある「湖」「秘境」「永遠悪夢」が既定品揃えの側にも重複して入る (16+3=19)。
/// 参照は <c>find</c> (先頭一致) なので個別定義が勝ち、実害はない。
/// 件数を 16 に直すと旧データと変わってしまうため、この重複は保持する。
/// </summary>
public sealed record ShopDef
{
    [JsonPropertyName("field")]
    public required string Field { get; init; }

    [JsonPropertyName("item")]
    public IReadOnlyList<ShopItemDef> Items { get; init; } = [];
}

/// <summary>
/// ペットのアビリティ。旧 <c>mmo/ability.js</c>。17 件。
///
/// 注意: 旧 <c>ea.js</c> が実際に読むのは <see cref="AttackMultiplier"/> と
/// <see cref="Repeat"/> だけ。<c>eff</c> (状態異常付与) と <c>appear</c> (レア出現補正) は
/// 一度も参照されていないため、「デッド・オア・ダイ」等の説明文どおりの効果は発生しない。
/// 説明文は <c>/pstatus</c> に表示されるので、データとしては保持する。
/// </summary>
public sealed record AbilityDef
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("des")]
    public string Description { get; init; } = "-";

    /// <summary>ダメージ倍率。</summary>
    [JsonPropertyName("atk")]
    public double AttackMultiplier { get; init; } = 1;

    /// <summary>連続攻撃回数。</summary>
    [JsonPropertyName("repeat")]
    public int Repeat { get; init; } = 1;

    /// <summary>レアリティ (EX / N / R / SR / UR)。捕獲時の抽選に使う。</summary>
    [JsonPropertyName("rare")]
    public string Rarity { get; init; } = "EX";

    /// <summary>状態異常付与。旧実装では未使用 (説明文のみ機能する)。</summary>
    [JsonPropertyName("eff")]
    public IReadOnlyList<StatusApplication> UnusedEffects { get; init; } = [];

    /// <summary>レア敵の出現確率補正。旧実装では未使用。</summary>
    [JsonPropertyName("appear")]
    public double? UnusedAppearBonus { get; init; }
}

/// <summary>
/// 旧 <c>mmo/iteminfo.js</c>。4 件。
///
/// 注意: <c>/inventory</c> はこの一覧に載っているアイテムしか表示しない。
/// <c>item.json</c> には 9 種あるが、ここに定義があるのは p/t/e/c の 4 種だけなので、
/// インビジブル等は所持していても一覧に出ない。旧挙動なので維持する。
/// </summary>
public sealed record ItemInfoDef
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("des")]
    public string Description { get; init; } = string.Empty;
}

/// <summary>旧 <c>mmo/fix.json</c>。ダメージ式と HP 式の基礎値。</summary>
public sealed record FixDef
{
    [JsonPropertyName("player")]
    public int Player { get; init; }

    [JsonPropertyName("enemy")]
    public int Enemy { get; init; }
}
