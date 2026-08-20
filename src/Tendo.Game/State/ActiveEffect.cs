namespace Tendo.Game.State;

/// <summary>
/// 実際に付いている状態異常 1 件。旧 DB の <c>eff</c> 配列の要素
/// <c>[状態異常名, レベル, 残りターン]</c>。
///
/// 戦闘中に残りターンが減っていくので、マスターデータ側の
/// <see cref="Master.StatusState"/> (不変) とは別に可変の型にしてある。
/// </summary>
public sealed class ActiveEffect
{
    public ActiveEffect(string name, int level, int turnsLeft)
    {
        Name = name;
        Level = level;
        TurnsLeft = turnsLeft;
    }

    public string Name { get; }

    public int Level { get; }

    /// <summary>残りターン。毎ターン 1 減り、0 になると解除される。</summary>
    public int TurnsLeft { get; set; }

    public static ActiveEffect From(Master.StatusState state)
        => new(state.Name, state.Level, state.TurnsLeft);

    public static ActiveEffect From(Master.StatusApplication application)
        => new(application.Name, application.Level, application.Turns);

    public ActiveEffect Clone() => new(Name, Level, TurnsLeft);

    /// <summary>旧 <c>[${e[0]}] Lv.${e[1]} (${e[2]} left)</c> の表示。</summary>
    public override string ToString() => $"[{Name}] Lv.{Level} ({TurnsLeft} left)";
}
