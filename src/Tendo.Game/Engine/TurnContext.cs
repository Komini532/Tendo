using Tendo.Game.Master;
using Tendo.Game.State;

namespace Tendo.Game.Engine;

/// <summary>
/// 1 ターンのあいだ共有される状態。旧実装で <c>fn()</c> のクロージャ変数だったもの。
/// </summary>
public sealed class TurnContext
{
    /// <summary>
    /// 旧 <c>penaltyenemy = ["nightmare", "daydream"]</c>。
    ///
    /// 本来はマクロ検知のペナルティ用の敵一覧だが、「吸い込み」の対象外判定にも
    /// 使い回されている。マクロ検知は移植しないものの、この用途は残る。
    /// </summary>
    public static readonly IReadOnlyList<string> UnpullableEnemies = ["nightmare", "daydream"];

    public required BattleState Battle { get; init; }

    public required PlayerState Player { get; init; }

    public required EnemyDef Enemy { get; init; }

    public required DifficultyDef Difficulty { get; init; }

    /// <summary>現在のフィールド定義。敵HP・敵与ダメの倍率と敵Lv上限を持つ。</summary>
    public required FieldDef Field { get; init; }

    /// <summary>
    /// 有効敵Lv。<c>min(Battle.Level, Field.LevelCap)</c>。
    /// 敵の強さと報酬はすべてこの値で決まる (<see cref="MasterData.EffectiveLevel"/>)。
    /// </summary>
    public int EnemyLevel => Math.Min(Battle.Level, Field.LevelCap);

    /// <summary>旧 <c>tinfo.name</c>。サーバーでの表示名。</summary>
    public required string PlayerName { get; init; }

    /// <summary>
    /// 旧 <c>moved</c>。「吸い込み」でフィールドが変わったか。
    /// ターン後に戦場のリセットを走らせるかどうかの判断に使う。
    /// </summary>
    public bool Moved { get; set; }

    /// <summary>旧 <c>tinfo.zokusei</c>。プレイヤーは常に無属性。</summary>
    public const string PlayerElement = "無";
}
