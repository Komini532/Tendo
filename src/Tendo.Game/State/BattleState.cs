namespace Tendo.Game.State;

/// <summary>
/// チャンネル 1 つ分の戦場。旧 <c>mmo_channel</c> テーブルの 1 行を展開したもの。
///
/// 旧実装ではこのオブジェクトが「今いる敵」そのものでもあるため、
/// フィールド・難易度といった場の情報と、敵の HP といった個体の情報が同居している。
/// 構造を変えると保存内容が変わるので、その同居のまま移植する。
/// </summary>
public sealed class BattleState
{
    /// <summary>戦場のチャンネル ID。</summary>
    public required ulong ChannelId { get; init; }

    /// <summary>旧 <c>f</c>。現在のフィールド名。</summary>
    public string Field { get; set; } = "草原";

    /// <summary>旧 <c>d</c>。難易度名 (NORMAL / HARD / EXTREME / LUNATIC)。</summary>
    public string Difficulty { get; set; } = "NORMAL";

    /// <summary>旧 <c>c</c>。今出ている敵の code。</summary>
    public string EnemyCode { get; set; } = string.Empty;

    /// <summary>旧 <c>lv</c>。敵のレベル。倒すたびに 1 上がる。</summary>
    public int Level { get; set; } = 1;

    public long Hp { get; set; }

    public long MaxHp { get; set; }

    public long Mana { get; set; }

    public long MaxMana { get; set; }

    /// <summary>
    /// 旧 <c>turn</c>。<c>Turn ${enemy.turn}</c> として embed のタイトルに出る。
    ///
    /// 旧実装では敵が入れ替わるときに書き込むオブジェクトへ turn を含めないため、
    /// 新しい敵になるとカウントが 1 から振り直しになる。この挙動は維持する
    /// (<see cref="ResetForNewEnemy"/> で 0 に戻す)。
    /// </summary>
    public int Turn { get; set; }

    /// <summary>旧 <c>eff</c>。敵に付いている状態異常。表示順が出るので順序を保つ。</summary>
    public List<ActiveEffect> Effects { get; } = [];

    /// <summary>
    /// 旧 <c>n</c>。この敵に手を出したユーザー ID。
    /// 撃破時に経験値とドロップを配る対象であり、
    /// <c>/reset</c> 時に戦闘状態を解除する対象でもある。
    /// </summary>
    public List<ulong> Participants { get; } = [];

    /// <summary>
    /// 旧 <c>pet</c>。この敵にチョコレートを食べさせたユーザー ID。
    /// 撃破時にこの中から抽選して「懐いた」判定を行う。
    /// </summary>
    public List<ulong> ChocolateFeeders { get; } = [];

    /// <summary>
    /// 旧 <c>cs</c>。書き込み時のチャンネル ID を控えたもの。
    ///
    /// 旧実装では snowflake の精度落ちで id 列が信用できなかったため、
    /// <c>/mod clist</c> がこの値と突き合わせて CLEAR / FAIL を表示していた。
    /// 精度が落ちなくなったので常に一致するが、表示は残すので列も残す。
    /// </summary>
    public ulong? ChecksumChannelId { get; set; }

    /// <summary>
    /// 敵が入れ替わったときの初期化。旧実装が新しいオブジェクトを
    /// 書き込むことで暗黙にリセットしていた項目を明示的に戻す。
    /// </summary>
    public void ResetForNewEnemy()
    {
        Turn = 0;
        Effects.Clear();
        Participants.Clear();
        ChocolateFeeders.Clear();
    }
}
