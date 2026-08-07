using System.Text.Json.Serialization;

namespace Tendo.Game.Master;

/// <summary>敵の技と発動確率。旧 <c>enemy.skill</c> の <c>{ name, per }</c>。</summary>
public sealed record EnemySkillRef
{
    /// <summary>
    /// 技名。旧データには「双頭龍」の 3 つ目のように <c>{ name:"" }</c> という
    /// 書きかけの要素が 1 件あり、名前が空でも許容する必要がある。
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// 抽選確率 (%)。旧 <c>skillselect</c> は先頭から順に判定し、最初に当たったものを使う。
    ///
    /// 未指定 (旧データの <c>undefined</c>) は 0 になる。旧実装の
    /// <c>random(1,100) &lt;= undefined</c> は常に false で、乱数は必ず 1 以上なので
    /// <c>&lt;= 0</c> も常に false。よって挙動は一致する。
    /// </summary>
    [JsonPropertyName("per")]
    public int Percent { get; init; }
}

/// <summary>倒すと別の敵に変身する指定。旧 <c>enemy.next</c>。</summary>
public sealed record EnemyNext
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("msg")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>逃走 (リセット・移動・難易度変更) を禁止する指定。旧 <c>enemy.noescape</c>。</summary>
public sealed record EnemyNoEscape
{
    [JsonPropertyName("msg")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// 旧 <c>mmo/enemy.js</c> の 1 件。全 77 体。
/// </summary>
public sealed record EnemyDef
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>データ上の識別子。DB にはこれが入る。旧既定値は <c>name</c> と同じ。</summary>
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    /// <summary>HP 補正倍率。実 HP は <c>round((lv*hp*10 + fix.enemy) * 難易度倍率)</c>。</summary>
    [JsonPropertyName("hp")]
    public double HpMultiplier { get; init; } = 1;

    /// <summary>最大魔力。</summary>
    [JsonPropertyName("mp")]
    public int MaxMana { get; init; } = 1000;

    /// <summary>攻撃補正。</summary>
    [JsonPropertyName("atk")]
    public double AttackMultiplier { get; init; } = 1;

    /// <summary>敏捷補正。先攻後攻は <c>player.spd*player.lv - enemy.spd*enemy.lv</c> で決まる。</summary>
    [JsonPropertyName("spd")]
    public double SpeedMultiplier { get; init; } = 1;

    [JsonPropertyName("skill")]
    public IReadOnlyList<EnemySkillRef> Skills { get; init; } = [];

    [JsonPropertyName("zokusei")]
    public string Element { get; init; } = "無";

    [JsonPropertyName("des")]
    public string Description { get; init; } = "-";

    /// <summary>出現時に最初から付いている状態異常。</summary>
    [JsonPropertyName("effect")]
    public IReadOnlyList<StatusState> InitialEffects { get; init; } = [];

    /// <summary>出現するフィールド名。空配列なら通常出現しない (召喚専用)。</summary>
    [JsonPropertyName("field")]
    public IReadOnlyList<string> Fields { get; init; } = [];

    /// <summary>経験値倍率。</summary>
    [JsonPropertyName("exp")]
    public double ExpMultiplier { get; init; } = 1;

    /// <summary>ギル倍率。獲得ギルは <c>round(g * round(lv/3))</c>。</summary>
    [JsonPropertyName("g")]
    public double GilMultiplier { get; init; } = 1;

    /// <summary>ドロップ品。旧 <c>enemy.i</c>。</summary>
    [JsonPropertyName("i")]
    public IReadOnlyList<ItemDrop> Drops { get; init; } = [];

    /// <summary>盗めるアイテム。定義のみで旧実装は参照していない。</summary>
    [JsonPropertyName("hunt")]
    public IReadOnlyList<ItemDrop> Huntable { get; init; } = [];

    /// <summary>
    /// 画像ファイル名。旧実装は読み込み時に固定 URL を前置していたが、
    /// 配信元が失われても差し替えられるよう、ここではファイル名だけを持ち
    /// ベース URL は設定値 (<c>Discord:EnemyImageBaseUrl</c>) と連結する。
    /// </summary>
    [JsonPropertyName("pic")]
    public string? Picture { get; init; }

    /// <summary>
    /// レア度。0=通常 1=レア 2=超レア 3=激レア 4=最レア。
    /// 出現抽選と、出現メッセージの装飾 (<c>["C","fix","fix","fix"][rare]</c>) に使う。
    /// rare=4 はこの 4 要素配列の範囲外なので既定色になる — 旧挙動なので維持すること。
    /// </summary>
    [JsonPropertyName("rare")]
    public int Rarity { get; init; }

    /// <summary>ペットにしたとき抽選対象になるアビリティのレア度。</summary>
    [JsonPropertyName("ability")]
    public IReadOnlyList<string> AbilityRarities { get; init; } = [];

    /// <summary>種族固有アビリティ名。ペット攻撃時にダメージ倍率が乗る。</summary>
    [JsonPropertyName("only")]
    public string? OnlyAbility { get; init; }

    /// <summary>1 ターンの攻撃回数。</summary>
    [JsonPropertyName("repeat")]
    public int Repeat { get; init; } = 1;

    /// <summary>倒すと次の形態に変身する。</summary>
    [JsonPropertyName("next")]
    public EnemyNext? Next { get; init; }

    /// <summary>設定されていると <c>/reset</c> <c>/go</c> <c>/dchange</c> を拒否する。</summary>
    [JsonPropertyName("noescape")]
    public EnemyNoEscape? NoEscape { get; init; }

    /// <summary>出現する難易度。定義のみで旧実装は参照していない。</summary>
    [JsonPropertyName("mode")]
    public IReadOnlyList<string> Modes { get; init; } = [];
}
