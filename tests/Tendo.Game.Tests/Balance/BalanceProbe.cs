using Tendo.Game.Engine;
using Tendo.Game.Master;
using Tendo.Game.State;

namespace Tendo.Game.Tests.Balance;

/// <summary>
/// バランス調整の「物差し」。式をテスト側に書き写すと調整のたびに二重管理になるので、
/// 実際の <see cref="EncounterSpawner"/> と <see cref="TurnRunner"/> を走らせて測る。
///
/// 乱数は常に 0.999 を返す固定源を使う。<c>JsMath.Random(a,b)</c> は
/// <c>round(rnd*(b-a))+a</c> なので、
/// <list type="bullet">
///   <item><c>Random(85,100)</c> → 100 (ダメージ乱数は最大)</item>
///   <item><c>Random(1,100)</c> → 100 (確率 100% の効果以外は発動しない)</item>
/// </list>
/// になる。状態異常が絡まないので 1 発分の素のダメージがそのまま測れる。
/// 自分に付く 100% の反動 (シグルイの「ダメージ低下」など) はダメージ適用より後なので、
/// 1 ターン分の計測には影響しない。
/// </summary>
public static class BalanceProbe
{
    /// <summary>常に 0.999。1.0 にすると <see cref="JsMath.Choice{T}"/> が範囲外を引く。</summary>
    private sealed class MaxRoll : IRandomSource
    {
        public double NextDouble() => 0.999;
    }

    /// <summary>
    /// 計測用の敵。個体倍率はすべて 1.0、属性は無 (どの技とも相性が付かない)、
    /// 技は効果を持たない「攻撃」だけ。フィールド倍率だけを見たいのでこの形にしてある。
    /// </summary>
    private static EnemyDef Dummy(string field) => new()
    {
        Name = "計測用",
        Code = "__probe__",
        HpMultiplier = 1.0,
        AttackMultiplier = 1.0,
        SpeedMultiplier = 1.0,
        Element = "無",
        MaxMana = int.MaxValue,
        Skills = [new EnemySkillRef { Name = "攻撃", Percent = 100 }],
        Fields = [field],
    };

    /// <summary>1 撃破ぶんの測定結果。</summary>
    public sealed record Measurement
    {
        /// <summary>フィールド倍率込みの敵の最大 HP。</summary>
        public required long EnemyMaxHp { get; init; }

        /// <summary>1 ターンでプレイヤーが与えるダメージ (ペットなし)。</summary>
        public required long PlayerDamage { get; init; }

        /// <summary>1 ターンでプレイヤーが受けるダメージ。</summary>
        public required long DamageTaken { get; init; }

        public required long PlayerMaxHp { get; init; }

        /// <summary>使った技。</summary>
        public required SkillDef Skill { get; init; }

        /// <summary>個体倍率 1.0 の敵を倒すのに要するターン数。</summary>
        public double TurnsToKill => (double)EnemyMaxHp / PlayerDamage;

        /// <summary>1 体倒すあいだに失う体力の、最大体力に対する割合。</summary>
        public double DamageTakenPerKill => TurnsToKill * DamageTaken / PlayerMaxHp;
    }

    /// <summary>
    /// <paramref name="field"/> で 敵Lv <paramref name="enemyLevel"/>・
    /// プレイヤー Lv <paramref name="playerLevel"/> のときの戦闘を 1 ターンだけ走らせて測る。
    /// </summary>
    public static Measurement Measure(
        MasterData data,
        string field,
        int enemyLevel,
        int playerLevel,
        string difficulty = "NORMAL")
    {
        var random = new MaxRoll();
        var enemy = Dummy(field);
        var skill = StrongestSkillAt(data, playerLevel);

        var fieldDef = data.FindField(field) ?? data.DefaultField;
        var difficultyDef = data.FindDifficulty(difficulty) ?? data.DefaultDifficulty;

        // 本物の HP 式で「倒すべき体力」を出す。
        var real = NewBattle(field, difficulty);
        new EncounterSpawner(data, random).Spawn(real, enemy, enemyLevel);

        // 別の戦場を用意し、敵の体力だけ十分大きくして 1 ターン走らせる。
        // 敵が死なないので後攻も必ず動き、与ダメと被ダメを同時に測れる。
        var probe = NewBattle(field, difficulty);
        new EncounterSpawner(data, random).Spawn(probe, enemy, enemyLevel);
        probe.MaxHp = long.MaxValue / 4;
        probe.Hp = probe.MaxHp;

        var player = new PlayerState
        {
            UserId = 1,
            Level = playerLevel,
            MaxHp = (playerLevel * 10) + data.Fix.Player,
            Hp = (playerLevel * 10) + data.Fix.Player,
            MaxMana = int.MaxValue,
            Mana = int.MaxValue,
            Speed = 1,
        };
        player.Skills.Add(skill.Name);

        var context = new TurnContext
        {
            Battle = probe,
            Player = player,
            Enemy = enemy,
            Difficulty = difficultyDef,
            Field = fieldDef,
            PlayerName = "計測",
        };

        new TurnRunner(data, random).Execute(context, skill);

        return new Measurement
        {
            EnemyMaxHp = real.MaxHp,
            PlayerDamage = probe.MaxHp - probe.Hp,
            DamageTaken = player.MaxHp - player.Hp,
            PlayerMaxHp = player.MaxHp,
            Skill = skill,
        };
    }

    /// <summary>
    /// そのレベルで使える最も威力の高い技。「攻撃」は未習得でも常に使えるので下限になる。
    /// ギル依存の「銭投げ」とデバフ主体の「メルトン」は素の威力カーブから外れているので除く。
    /// </summary>
    public static SkillDef StrongestSkillAt(MasterData data, int playerLevel)
        => data.Skills
            .Where(s => s.Name == "攻撃"
                        || (s.LearnLevel is { } learn && learn <= playerLevel && !ExcludedFromLadder(s)))
            .Where(s => s.Attack > 0)
            .OrderByDescending(s => s.Attack)
            .First();

    /// <summary>素の威力ラダーに乗せていない技。</summary>
    public static bool ExcludedFromLadder(SkillDef skill)
        => skill.Name is "銭投げ" or "メルトン";

    private static BattleState NewBattle(string field, string difficulty) => new()
    {
        ChannelId = 1,
        Field = field,
        Difficulty = difficulty,
        ChecksumChannelId = 1,
    };
}
