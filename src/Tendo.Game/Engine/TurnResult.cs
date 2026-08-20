namespace Tendo.Game.Engine;

/// <summary>ターン終了後に何をすべきか。</summary>
public enum TurnOutcome
{
    /// <summary>戦闘継続。</summary>
    Continue,

    /// <summary>技が未習得で何も起きなかった (旧「まだ習得していません」)。</summary>
    SkillNotLearned,

    /// <summary>別チャンネルで戦闘中のため行動できない。</summary>
    BusyElsewhere,

    /// <summary>既に倒れているため行動できない。</summary>
    AlreadyDown,

    /// <summary>敵を倒した。報酬処理へ進む。</summary>
    EnemyDefeated,

    /// <summary>敵が次の形態に変身する (旧 <c>einfo.next</c>)。</summary>
    EnemyTransforms,

    /// <summary>「吸い込み」で別フィールドへ引きずり込まれた。戦場をリセットする。</summary>
    PulledToAnotherField,
}

/// <summary>
/// 1 ターンの結果。表示に必要な文字列は全てここに入っており、
/// Discord 層は embed に詰めるだけでよい。
/// </summary>
public sealed record TurnResult
{
    public required TurnOutcome Outcome { get; init; }

    /// <summary>旧 <c>description</c>。コードブロックが連結済みの本文。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>旧 embed のタイトル <c>Turn ${enemy.turn}</c> に使う。</summary>
    public int Turn { get; init; }

    /// <summary>プレイヤー側フィールドの値 (体力/魔力 + 状態異常)。</summary>
    public string PlayerStatus { get; init; } = string.Empty;

    /// <summary>敵側フィールドの値。</summary>
    public string EnemyStatus { get; init; } = string.Empty;

    /// <summary>
    /// 行動できなかったとき (未習得・戦闘中・戦闘不能) に表示する一文。
    /// <see cref="Outcome"/> が <see cref="TurnOutcome.Continue"/> 系なら null。
    /// </summary>
    public string? RejectionMessage { get; init; }

    /// <summary>
    /// <see cref="TurnOutcome.EnemyTransforms"/> のときに召喚する敵の code。
    /// </summary>
    public string? TransformInto { get; init; }

    /// <summary>行動できず、ターンそのものが成立しなかったか。</summary>
    public bool IsRejected => Outcome is TurnOutcome.SkillNotLearned
        or TurnOutcome.BusyElsewhere
        or TurnOutcome.AlreadyDown;
}
