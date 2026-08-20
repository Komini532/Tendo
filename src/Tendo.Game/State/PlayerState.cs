namespace Tendo.Game.State;

/// <summary>
/// プレイヤー 1 人分の状態。旧 <c>mmo_user</c> テーブルの 1 行
/// (id = ユーザー ID、name = eval 可能な JS 文字列) を展開したもの。
///
/// 旧実装は「丸ごと読む → メモリ上で書き換える → 丸ごと書き戻す」形だったので、
/// 可変クラスにして同じ扱い方ができるようにしてある。
/// </summary>
public sealed class PlayerState
{
    /// <summary>
    /// Discord のユーザー ID。
    ///
    /// 旧 SQLite は <c>INT</c> 列に入れており、JS の Number (2^53) と合わせて
    /// snowflake の精度が落ちていた。そのせいで旧実装には
    /// 「一番近い ID を探す」<c>ctrl.nearest</c> という回避策があった。
    /// <c>ulong</c> + <c>BIGINT UNSIGNED</c> では精度が落ちないので不要になる。
    /// </summary>
    public required ulong UserId { get; init; }

    public long Hp { get; set; }

    public long MaxHp { get; set; }

    public long Mana { get; set; }

    public long MaxMana { get; set; }

    /// <summary>旧 <c>spd</c>。敏捷力は <c>spd * lv</c>。旧実装では 1 から変化しない。</summary>
    public double Speed { get; set; } = 1;

    public int Level { get; set; } = 1;

    public long Experience { get; set; }

    /// <summary>旧 <c>g</c>。所持ギル。</summary>
    public long Gil { get; set; }

    /// <summary>旧 <c>sk</c>。習得済みの技名。レベルアップ時に追加される。</summary>
    public List<string> Skills { get; } = [];

    /// <summary>旧 <c>eff</c>。付いている状態異常。表示順が出るので順序を保つ。</summary>
    public List<ActiveEffect> Effects { get; } = [];

    /// <summary>
    /// 旧 <c>i</c>。アイテム ID → 所持数。
    /// キーの並びは表示順になるので <see cref="ItemSlots.Order"/> の順で持つ。
    /// </summary>
    public Dictionary<string, int> Items { get; } = new(StringComparer.Ordinal);

    /// <summary>旧 <c>p[0]</c>。飼っているペット。いなければ null。</summary>
    public PetState? Pet { get; set; }

    /// <summary>
    /// 旧 <c>n</c>。戦闘中のチャンネル ID。旧仕様の <c>"0"</c> (非戦闘中) は null。
    /// 別チャンネルで戦闘中なら、そのチャンネルでの行動を拒否するために使う。
    /// </summary>
    public ulong? BattleChannelId { get; set; }

    /// <summary>旧 <c>p.hp &lt;= 0</c>。倒れている状態。</summary>
    public bool IsDown => Hp <= 0;

    /// <summary>
    /// 旧 <c>player.i[id]</c>。未定義キーは 0 として扱う
    /// (JS の <c>undefined</c> と違い例外にしない)。
    /// </summary>
    public int GetItem(string itemId) => Items.GetValueOrDefault(itemId);

    public void SetItem(string itemId, int quantity) => Items[itemId] = quantity;

    public void AddItem(string itemId, int delta) => Items[itemId] = GetItem(itemId) + delta;
}

/// <summary>
/// アイテム欄の並び。旧 <c>newdata("user").i</c> のキー順で、
/// <c>for (let a in player.i)</c> の走査順 = <c>/status</c> と <c>/inventory</c> の表示順になる。
///
/// <c>m</c> <c>q</c> <c>s</c> は旧コメントで「余り枠」とされている未使用スロットで、
/// 増える経路がないため常に 0。<c>item.json</c> にも名前がないので表示もされない。
/// 並びを変えると表示順が変わるので、data/defaults.json と一致していることを
/// テストで固定している。
/// </summary>
public static class ItemSlots
{
    public static readonly IReadOnlyList<string> Order =
        ["p", "t", "e", "i", "r", "a", "d", "c", "m", "q", "s"];
}
