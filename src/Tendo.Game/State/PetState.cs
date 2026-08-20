namespace Tendo.Game.State;

/// <summary>
/// ペット 1 体。旧 <c>player.p[0]</c>。
///
/// 旧データでは配列だったが、要素 0 しか読み書きされない
/// (捕獲で <c>p[0] = npet</c>、逃がすと <c>p[0] = undefined</c>) ため、
/// 「0 体か 1 体」として持つ。
/// </summary>
public sealed class PetState
{
    /// <summary>旧 <c>pet.n</c>。表示名。<c>/rename</c> で変更できる (50 文字まで、改行不可)。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>旧 <c>pet.c</c>。元になった敵の code。使う技と画像はここから引く。</summary>
    public string EnemyCode { get; set; } = string.Empty;

    public int Level { get; set; } = 1;

    public long Experience { get; set; }

    /// <summary>旧 <c>pet.p</c>。1 ターンに攻撃してくる確率 (%)。捕獲時に 5〜95 で決まる。</summary>
    public int AttackChance { get; set; }

    /// <summary>旧 <c>pet.a</c>。個体アビリティ名。</summary>
    public string Ability { get; set; } = string.Empty;

    public PetState Clone() => new()
    {
        Name = Name,
        EnemyCode = EnemyCode,
        Level = Level,
        Experience = Experience,
        AttackChance = AttackChance,
        Ability = Ability,
    };
}
