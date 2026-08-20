using Tendo.Game.Master;
using Tendo.Game.State;

namespace Tendo.Game.Engine;

/// <summary>
/// 旧 <c>result()</c> 末尾のペット捕獲処理。
///
/// チョコレートを食べさせた敵を倒すと、与えた人の中から抽選で 1 人が
/// 「懐かれる」。承諾すると敵がペットになる。
/// </summary>
public sealed class PetTaming
{
    /// <summary>旧 <c>ctrl.perchoice(match, 12)</c>。1 人あたり 12%。</summary>
    private const int TameChancePercent = 12;

    private readonly MasterData _data;
    private readonly IRandomSource _random;

    public PetTaming(MasterData data, IRandomSource random)
    {
        _data = data;
        _random = random;
    }

    /// <summary>
    /// チョコレートを与えた人の中から、懐かれる相手を 1 人選ぶ。
    /// 先頭から順に 12% で判定し、最初に当たった人が対象。誰も当たらなければ null。
    /// </summary>
    public ulong? ChooseTamer(IReadOnlyList<ulong> feeders)
    {
        if (feeders.Count == 0)
        {
            return null;
        }

        var selected = EffectRules.SelectByEqualChance(
            _random,
            feeders.Select(f => f.ToString()).ToList(),
            TameChancePercent);

        return selected is null ? null : ulong.Parse(selected);
    }

    /// <summary>
    /// 敵をペットにする。既にペットがいる場合はレベルと経験値を引き継いで置き換える。
    /// </summary>
    public PetState CreatePet(EnemyDef enemy, GameDefaults defaults, PetState? previous)
    {
        // 乱数を引く順序が旧実装と一致していないと、以降の抽選が全てずれる。
        // ea.js:495-501 では アビリティ抽選 → 攻撃確率 の順。
        //   let aname = choice();            // ← 先
        //   npet.p = r.random(5, 95);        // ← 後
        var ability = ChooseAbility(enemy);

        var pet = defaults.NewPet();

        pet.Name = enemy.Name;
        pet.EnemyCode = enemy.Code;
        pet.AttackChance = JsMath.Random(_random, 5, 95);
        pet.Ability = ability?.Name ?? _data.DefaultAbility.Name;

        // 旧 if( beforepet ){ npet.lv = beforepet.lv; npet.xp = beforepet.xp; }
        // 育てたペットを逃がさずに乗り換えても、レベルは引き継がれる。
        if (previous is not null)
        {
            pet.Level = previous.Level;
            pet.Experience = previous.Experience;
        }

        return pet;
    }

    /// <summary>
    /// 旧 <c>choice()</c>。敵ごとに出うるレアリティが決まっており、その中から抽選する。
    ///
    /// 乱数は 1 回だけ引き、else if で順に判定する。
    /// UR は 5/1000、SR は 120/1000、R は 512/1000、それ以外は N。
    /// 「UR を持つ敵で乱数が 6」の場合は SR の判定へ落ちる (UR 用の枠が
    /// SR にも使われる) 点に注意。
    /// </summary>
    private AbilityDef? ChooseAbility(EnemyDef enemy)
    {
        var pool = _data.Abilities
            .Where(a => enemy.AbilityRarities.Contains(a.Rarity, StringComparer.Ordinal))
            .ToList();

        var roll = JsMath.Random(_random, 1, 1000);

        if (pool.Any(a => a.Rarity == "UR") && roll <= 5)
        {
            return PickRarity(pool, "UR");
        }

        if (pool.Any(a => a.Rarity == "SR") && roll <= 120)
        {
            return PickRarity(pool, "SR");
        }

        if (pool.Any(a => a.Rarity == "R") && roll <= 512)
        {
            return PickRarity(pool, "R");
        }

        if (pool.Any(a => a.Rarity == "N"))
        {
            return PickRarity(pool, "N");
        }

        return _data.DefaultAbility;
    }

    private AbilityDef? PickRarity(IReadOnlyList<AbilityDef> pool, string rarity)
        => JsMath.Choice(_random, pool.Where(a => a.Rarity == rarity).ToList());
}
