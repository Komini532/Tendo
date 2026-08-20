using System.Text.Json.Serialization;

namespace Tendo.Game.Master;

/// <summary>旧 <c>skill.type</c>。表示は <c>["物理","魔法","特殊"][type]</c>。</summary>
public enum SkillType
{
    /// <summary>打撃。<c>透明化</c> (inv) で無効化される。</summary>
    Physical = 0,

    /// <summary>魔法。<c>魔法障壁</c> (minv) で無効化される。</summary>
    Magic = 1,

    /// <summary>変化。ダメージ 0 でも「効いていない」と表示せず、効果付与だけ行う。</summary>
    Special = 2,
}

/// <summary>
/// 旧 <c>mmo/skill.js</c> の 1 件。全 90 個。既定値は JS 側の末尾 forEach で適用済み。
/// </summary>
public sealed record SkillDef
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// 威力。ダメージ式 <c>atk*(lv*10+fix)/3/30*rand(85,100)/100</c> の分子。
    ///
    /// 整数に見えるが「プラント」「フローズン」は 57.5 なので double で保持すること。
    /// int に丸めるとこの 2 技のダメージが変わる。
    /// </summary>
    [JsonPropertyName("atk")]
    public double Attack { get; init; }

    /// <summary>消費魔力。足りないと「魔力が足りない！」で不発。</summary>
    [JsonPropertyName("mp")]
    public int ManaCost { get; init; }

    [JsonPropertyName("type")]
    public SkillType Type { get; init; }

    /// <summary>命中率。旧実装では既定 100 が入るだけで判定には使われていない。</summary>
    [JsonPropertyName("hit")]
    public int Hit { get; init; } = 100;

    /// <summary>属性 (無、地、雷、水、火、草、氷、光、闇、虚)。相性計算に使う。</summary>
    [JsonPropertyName("zokusei")]
    public string Element { get; init; } = "無";

    [JsonPropertyName("des")]
    public string Description { get; init; } = "-";

    /// <summary>相手に付与する状態異常。</summary>
    [JsonPropertyName("effect")]
    public IReadOnlyList<StatusApplication> Effects { get; init; } = [];

    /// <summary>自分に付与する状態異常。</summary>
    [JsonPropertyName("self")]
    public IReadOnlyList<StatusApplication> SelfEffects { get; init; } = [];

    /// <summary>相手の状態異常を解除する指定。</summary>
    [JsonPropertyName("pair")]
    public IReadOnlyList<EffectClear> Clears { get; init; } = [];

    /// <summary>
    /// 習得レベル。旧データの <c>Infinity</c> (習得不可) は JSON 化で null になる。
    /// null の技は敵専用で、<c>/skills</c> の一覧にも出ない。
    /// </summary>
    [JsonPropertyName("learn")]
    public int? LearnLevel { get; init; }

    /// <summary>
    /// 命中時に相手を強制的に別フィールドへ移動させる技 (「地獄」へ吸い込む等)。
    /// 旧データで実際に値が入るのは 1 件のみ。
    /// </summary>
    [JsonPropertyName("move")]
    public string? MoveField { get; init; }

    /// <summary>
    /// 消費ギル倍率。旧 <c>skill.g</c>。<c>reqg = g * round(lv/10)</c> ギルを消費し、
    /// ダメージが <c>damage*0.01*reqg</c> になる。所持金が足りないとダメージ 0。
    /// </summary>
    [JsonPropertyName("g")]
    public double? GilCost { get; init; }

    /// <summary>習得可能な技か (旧 <c>sk.filter(s =&gt; s.learn != Infinity)</c>)。</summary>
    [JsonIgnore]
    public bool IsLearnable => LearnLevel.HasValue;
}
