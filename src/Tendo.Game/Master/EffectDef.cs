using System.Text.Json.Serialization;

namespace Tendo.Game.Master;

/// <summary>
/// 状態異常のレベル別効果。旧 <c>effect.effects[レベル-1]</c>。
/// </summary>
public sealed record EffectLevel
{
    /// <summary>ターンごとの体力回復比率 (最大 HP に対する割合)。</summary>
    [JsonPropertyName("heal")]
    public double Heal { get; init; }

    /// <summary>ターンごとのダメージ比率 (最大 HP に対する割合)。</summary>
    [JsonPropertyName("damage")]
    public double Damage { get; init; }

    /// <summary>ターンごとの魔力回復比率。旧実装では読まれていない。</summary>
    [JsonPropertyName("mheal")]
    public double ManaHeal { get; init; }

    /// <summary>与ダメージ倍率。「天邪鬼」状態だと除算に反転する。</summary>
    [JsonPropertyName("atk")]
    public double AttackMultiplier { get; init; } = 1;

    /// <summary>被ダメージ除数。大きいほど硬い。</summary>
    [JsonPropertyName("def")]
    public double DefenseDivisor { get; init; } = 1;

    /// <summary>行動不能になる確率 (%)。</summary>
    [JsonPropertyName("nomove")]
    public int NoMovePercent { get; init; }
}

/// <summary>
/// 旧 <c>mmo/effect.js</c> の 1 件。全 37 種。
/// </summary>
public sealed record EffectDef
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>絵文字 ID。旧実装では定義されているだけで表示に使われていない。</summary>
    [JsonPropertyName("emoji")]
    public string Emoji { get; init; } = string.Empty;

    /// <summary>
    /// レベル別効果。<c>n</c> 番目がレベル <c>n+1</c> の効果。
    ///
    /// 注意: 旧実装は <c>effects.choice(level-1)</c> で引く。<c>choice</c> は
    /// 添字が範囲外か値が falsy ならランダムな要素を返すため、
    /// 定義数を超えるレベルの状態異常は「毎ターン効果がランダムに変わる」。
    /// これは移植先でも再現する必要がある (JsArray.Choice)。
    /// </summary>
    [JsonPropertyName("effects")]
    public IReadOnlyList<EffectLevel> Levels { get; init; } = [];

    /// <summary>物理ダメージ無効 (旧 <c>inv</c>)。「透明化」など。</summary>
    [JsonPropertyName("inv")]
    public bool BlocksPhysical { get; init; }

    /// <summary>魔法ダメージ無効 (旧 <c>minv</c>)。「魔法障壁」など。</summary>
    [JsonPropertyName("minv")]
    public bool BlocksMagic { get; init; }

    /// <summary>魔法反射 (旧 <c>ref</c>)。定義のみで旧実装は参照していない。</summary>
    [JsonPropertyName("ref")]
    public bool Reflects { get; init; }

    /// <summary>この属性のダメージを 0 にする。</summary>
    [JsonPropertyName("nodamage")]
    public IReadOnlyList<string> NoDamageElements { get; init; } = [];

    /// <summary>これらの状態異常にかからなくなる (耐性)。</summary>
    [JsonPropertyName("protect")]
    public IReadOnlyList<string> Protects { get; init; } = [];

    /// <summary>
    /// true なら残りターンが 1 になった時だけ効果が出る (時限発動)。
    /// 「死の宣告」「豊穣の宣告」など。
    /// </summary>
    [JsonPropertyName("end")]
    public bool TriggersOnExpiry { get; init; }
}
