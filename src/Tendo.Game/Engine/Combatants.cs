using Tendo.Game.State;

namespace Tendo.Game.Engine;

/// <summary>
/// 旧 <c>hitevent</c> / <c>joutai</c> が受け取る <c>attack</c> / <c>defence</c>。
/// プレイヤー・敵・ペットが同じ形で渡されていたので、その共通面を切り出したもの。
/// </summary>
public interface ICombatant
{
    long Hp { get; set; }

    long MaxHp { get; }

    long Mana { get; set; }

    int Level { get; }

    /// <summary>
    /// 旧 <c>attack.g</c>。ギル消費技 (「銭投げ」) だけが読む。
    ///
    /// 敵とペットのデータには <c>g</c> が無く、旧実装では <c>undefined</c> だった。
    /// <c>reqg &lt;= undefined</c> は常に false なので「持っていなかった」扱いになる。
    /// 0 で代用すると必要ギルが 0 のときだけ結果が変わってしまうため、null で区別する。
    /// </summary>
    long? Gil { get; set; }

    List<ActiveEffect> Effects { get; }
}

/// <summary>プレイヤーを戦闘参加者として見る。</summary>
public sealed class PlayerCombatant : ICombatant
{
    private readonly PlayerState _player;

    public PlayerCombatant(PlayerState player) => _player = player;

    public long Hp
    {
        get => _player.Hp;
        set => _player.Hp = value;
    }

    public long MaxHp => _player.MaxHp;

    public long Mana
    {
        get => _player.Mana;
        set => _player.Mana = value;
    }

    public int Level => _player.Level;

    public long? Gil
    {
        get => _player.Gil;
        set => _player.Gil = value ?? 0;
    }

    public List<ActiveEffect> Effects => _player.Effects;
}

/// <summary>敵 (= 戦場) を戦闘参加者として見る。</summary>
public sealed class EnemyCombatant : ICombatant
{
    private readonly BattleState _battle;

    public EnemyCombatant(BattleState battle) => _battle = battle;

    public long Hp
    {
        get => _battle.Hp;
        set => _battle.Hp = value;
    }

    public long MaxHp => _battle.MaxHp;

    public long Mana
    {
        get => _battle.Mana;
        set => _battle.Mana = value;
    }

    public int Level => _battle.Level;

    /// <summary>敵はギルを持たない (旧データに <c>g</c> が無い)。</summary>
    public long? Gil { get => null; set { } }

    public List<ActiveEffect> Effects => _battle.Effects;
}

/// <summary>
/// ペット。旧実装はその場でこういう仮のオブジェクトを作って <c>hitevent</c> に渡していた。
/// <code>let pdata = { lv: pinfo.lv, hp: Infinity, mp: Infinity, eff: [] };</code>
///
/// HP と MP が無限なので、ペットは減らないし魔力切れも起こさない。
/// 状態異常も常に空なので、攻撃側の補正も一切かからない。
/// </summary>
public sealed class PetCombatant : ICombatant
{
    public PetCombatant(int level) => Level = level;

    /// <summary>旧 <c>Infinity</c>。ペットは攻撃側にしかならないので減ることはない。</summary>
    public long Hp { get; set; } = long.MaxValue;

    public long MaxHp => long.MaxValue;

    /// <summary>旧 <c>Infinity</c>。どんな消費魔力の技でも撃てる。</summary>
    public long Mana { get; set; } = long.MaxValue;

    public int Level { get; }

    public long? Gil { get => null; set { } }

    public List<ActiveEffect> Effects { get; } = [];
}
