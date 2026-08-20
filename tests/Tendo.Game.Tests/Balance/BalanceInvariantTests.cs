using System.Text;
using Tendo.Game.Master;
using Xunit;
using Xunit.Abstractions;

namespace Tendo.Game.Tests.Balance;

/// <summary>
/// バランス設計が満たすべき条件を機械的に確かめる。
///
/// 旧来この調整は手作業で、数値を触るたびに全フィールドを頭の中で検算していた。
/// ここが通ることが「調整が終わっている」ことの定義になる。
/// 数値を動かしたら <see cref="バランス表"/> の出力を見て、意図した形か確かめること。
///
/// ターン数・被ダメージは式を書き写さず <see cref="BalanceProbe"/> 経由で
/// 本物のエンジンから測っている。式を変えればここも自動で追随する。
///
/// 設計の考え方:
///   経験値/ターン ∝ フィールドexp / フィールドhp   (敵Lvは約分されて消える)
/// なので HP 倍率だけ上げても上位フィールドは「遅いだけ」になる。
/// 1 撃破は最短 1 ターンなので、フィールドの敵Lv上限が
///   経験値/ターン ≦ 上限 × フィールドexp
/// という硬い天井を作り、これが階層を成立させている。
/// </summary>
public sealed class BalanceInvariantTests
{
    private static readonly MasterData Data = MasterDataLoader.Load();

    /// <summary>
    /// 個体倍率 1.0 の敵を倒すのに許すターン数。
    /// 「適正スキル 2 回」が狙いだが、<see cref="BalanceProbe"/> は
    /// ダメージ乱数の最大値 (実戦平均の約 1.08 倍) で測るので、その分だけ下に幅を取る。
    /// </summary>
    private const double MinTurns = 1.2;
    private const double MaxTurns = 2.6;

    /// <summary>1 体倒すあいだに失ってよい体力の上限 (最大体力比)。</summary>
    private const double MaxDamageTakenPerKill = 0.65;

    private readonly ITestOutputHelper _output;

    public BalanceInvariantTests(ITestOutputHelper output) => _output = output;

    /// <summary>ティア順に並べたフィールドと、その参入Lv (= 一つ下のティアの上限)。</summary>
    private static IReadOnlyList<(FieldDef Field, int EntryLevel)> Tiers()
    {
        var caps = Data.Fields.Select(f => f.LevelCap).Distinct().OrderBy(c => c).ToList();
        return Data.Fields
            .OrderBy(f => f.LevelCap)
            .ThenBy(f => f.Name, StringComparer.Ordinal)
            .Select(f => (f, caps.IndexOf(f.LevelCap) == 0 ? 1 : caps[caps.IndexOf(f.LevelCap) - 1]))
            .ToList();
    }

    [Fact]
    public void バランス表()
    {
        _output.WriteLine(Table());
    }

    [Fact]
    public void 技の威力は習得レベル順に単調非減少()
    {
        // 「習得したのに前の技より弱い」= 死に技を作らない。
        // 旧データでは lv400 常闇 (112) と lv1000 シグルイ (444) が突出しており、
        // lv500〜650 と lv1100〜1600 の技が習得時点で下位互換になっていた。
        var byLevel = Data.Skills
            .Where(s => s.LearnLevel is not null && s.Attack > 0 && !BalanceProbe.ExcludedFromLadder(s))
            .GroupBy(s => s.LearnLevel!.Value)
            .OrderBy(g => g.Key)
            .Select(g => (Level: g.Key, Best: g.Max(s => s.Attack), Names: string.Join("/", g.Select(s => s.Name))))
            .ToList();

        var problems = new List<string>();
        var previous = Data.FindSkill("攻撃")!.Attack;
        var previousLabel = "攻撃";

        foreach (var (level, best, names) in byLevel)
        {
            if (best < previous)
            {
                problems.Add($"lv{level} の {names} (威力 {best}) が {previousLabel} (威力 {previous}) より弱い");
            }

            previous = best;
            previousLabel = $"lv{level} の {names}";
        }

        Assert.True(problems.Count == 0, Join("習得レベル順に威力が下がっている箇所がある", problems));
    }

    [Fact]
    public void どのフィールドでも適正スキル二回前後で倒せる()
    {
        var problems = new List<string>();

        foreach (var (field, entry) in Tiers())
        {
            foreach (var (label, level) in new[] { ("参入時", entry), ("上限時", field.LevelCap) })
            {
                var m = BalanceProbe.Measure(Data, field.Name, level, level);

                if (m.TurnsToKill < MinTurns || m.TurnsToKill > MaxTurns)
                {
                    problems.Add(
                        $"{field.Name} の{label} (敵Lv/自Lv {level}, 技 {m.Skill.Name} 威力 {m.Skill.Attack}) が " +
                        $"{m.TurnsToKill:F2} ターン ({MinTurns}〜{MaxTurns} の外)");
                }
            }
        }

        Assert.True(problems.Count == 0, Join("倒すのにかかるターン数が狙いから外れている", problems));
    }

    [Fact]
    public void 経験値ターン天井がティア順に増える()
    {
        // 下位フィールドで粘る意味を無くしている当の条件。
        // 1 撃破は最短 1 ターンなので「上限 × 経験値倍率」が経験値/ターンの上限になる。
        var problems = new List<string>();
        var ordered = Data.Fields.OrderBy(f => f.LevelCap).ToList();

        for (var i = 1; i < ordered.Count; i++)
        {
            var (lower, upper) = (ordered[i - 1], ordered[i]);
            if (upper.LevelCap == lower.LevelCap)
            {
                continue; // 同ティア
            }

            var below = lower.LevelCap * lower.ExpMultiplier;
            var above = upper.LevelCap * upper.ExpMultiplier;

            if (above <= below)
            {
                problems.Add(
                    $"{upper.Name} の天井 {above:F0} が {lower.Name} の天井 {below:F0} を上回っていない");
            }
        }

        Assert.True(problems.Count == 0, Join("上位フィールドの経験値/ターン天井が下位を超えていない", problems));
    }

    [Fact]
    public void 危険度はティア順に上がりかつ上限を超えない()
    {
        // 測るのは「そのフィールドへ入った直後」。上位へ上がるかどうかを決める瞬間であり、
        // 戦闘が最も厳しいのもここ (上限に近づくほど自分が育って楽になる)。
        var problems = new List<string>();
        double previous = 0;
        var previousName = "(なし)";
        var previousCap = 0;

        foreach (var (field, entry) in Tiers())
        {
            var m = BalanceProbe.Measure(Data, field.Name, entry, entry);
            var danger = m.DamageTakenPerKill;

            if (danger > MaxDamageTakenPerKill)
            {
                problems.Add(
                    $"{field.Name} は 1 体倒すのに最大体力の {danger:P0} を失う " +
                    $"(上限 {MaxDamageTakenPerKill:P0})");
            }

            // 同ティア内は同値になるので、ティアが上がったときだけ比べる。
            if (field.LevelCap != previousCap && danger < previous)
            {
                problems.Add($"{field.Name} の危険度 {danger:P0} が下位の {previousName} {previous:P0} より低い");
            }

            if (field.LevelCap != previousCap)
            {
                (previous, previousName, previousCap) = (danger, field.Name, field.LevelCap);
            }
        }

        Assert.True(problems.Count == 0, Join("危険度がティア順になっていない", problems));
    }

    [Theory]
    [InlineData(0, 1.0, 2.0, 0.7, 1.6, 1.0, 2.0)]
    [InlineData(1, 1.0, 3.0, 0.7, 2.5, 2.0, 4.0)]
    [InlineData(2, 1.0, 3.0, 0.7, 2.5, 5.0, 8.0)]
    [InlineData(3, 1.0, 3.0, 0.7, 2.5, 5.0, 8.0)]
    [InlineData(4, 1.0, 3.0, 0.7, 2.5, 10.0, 10.0)]
    public void 個体倍率はレア度ごとの帯に収まる(
        int rarity, double hpLo, double hpHi, double atkLo, double atkHi, double expLo, double expHi)
    {
        // 個体倍率は「そのフィールドの基準からのブレ」だけを表し、
        // スケールはフィールド側 (hp/atk/cap) が持つ。両方が伸びると調整できなくなる。
        //
        // 例外の 3 体は「逃げられる前に倒す」設計なので HP 下限を 0.5 まで許す。
        //   魔鏡     … 吸い込まれると地獄へ送られる
        //   踊る金貨 … 取り逃がすと大量のギルを失う
        //   ミミック … 同上
        string[] fragile = ["魔鏡", "踊る金貨", "ミミック"];
        var problems = new List<string>();

        foreach (var e in Data.Enemies.Where(e => e.Rarity == rarity))
        {
            var lo = fragile.Contains(e.Name, StringComparer.Ordinal) ? 0.5 : hpLo;

            if (e.HpMultiplier < lo || e.HpMultiplier > hpHi)
            {
                problems.Add($"{e.Name} ({e.Code}) の hp {e.HpMultiplier} が {lo}〜{hpHi} の外");
            }

            if (e.AttackMultiplier < atkLo || e.AttackMultiplier > atkHi)
            {
                problems.Add($"{e.Name} ({e.Code}) の atk {e.AttackMultiplier} が {atkLo}〜{atkHi} の外");
            }

            if (e.ExpMultiplier < expLo || e.ExpMultiplier > expHi)
            {
                problems.Add($"{e.Name} ({e.Code}) の exp {e.ExpMultiplier} が {expLo}〜{expHi} の外");
            }
        }

        Assert.True(problems.Count == 0, Join($"レア度 {rarity} の個体倍率が帯から外れている", problems));
    }

    [Fact]
    public void 難易度は上げるほど経験値効率が良くなる()
    {
        // 経験値/ターン ∝ exp/hp。旧データでは HARD がちょうど引き分け (1.2/1.2)、
        // LUNATIC は通常より効率が悪く (2.5/4.444 = 0.56 倍)、選ぶ理由が無かった。
        var problems = new List<string>();
        double previous = 0;
        var previousName = "(なし)";

        foreach (var d in Data.Difficulties)
        {
            var efficiency = d.ExpMultiplier / d.HpMultiplier;

            if (efficiency <= previous)
            {
                problems.Add(
                    $"{d.Name} の経験値/ターン比 {efficiency:F2} が {previousName} の {previous:F2} を超えていない");
            }

            // 届かない解放条件は無いこと (旧 LUNATIC は 99999 で永久に解放不能だった)。
            if (d.RequiredEnemyLevel > Data.Fields.Max(f => f.LevelCap))
            {
                problems.Add(
                    $"{d.Name} の解放条件 敵Lv{d.RequiredEnemyLevel} はどのフィールドの上限よりも高く、到達できない");
            }

            (previous, previousName) = (efficiency, d.Name);
        }

        Assert.True(problems.Count == 0, Join("難易度の割に合い方が単調になっていない", problems));
    }

    [Fact]
    public void 移動条件は移動元の敵Lv上限で満たせる()
    {
        // 敵Lvはフィールドの上限で頭打ちになるので、移動元の上限より高い elv を要求すると
        // そのフィールドへは永久に行けなくなる。
        var problems = new List<string>();

        foreach (var requirement in Data.FieldRequirements)
        {
            var reachable = requirement.ConnectedFrom
                .Select(name => Data.FindField(name))
                .Where(f => f is not null)
                .Select(f => f!.LevelCap)
                .DefaultIfEmpty(0)
                .Max();

            if (requirement.RequiredEnemyLevel > reachable)
            {
                problems.Add(
                    $"{requirement.Name} は 敵Lv{requirement.RequiredEnemyLevel} を要求するが、" +
                    $"移動元 ({string.Join("・", requirement.ConnectedFrom)}) の上限は {reachable} までしか上がらない");
            }
        }

        Assert.True(problems.Count == 0, Join("到達できない移動条件がある", problems));
    }

    [Fact]
    public void 効き目の無い状態異常が無い()
    {
        // 旧データの「被ダメージ上昇」はレベル配列のキーだけ effect (単数) になっており、
        // 読み込み時に空扱いされて完全に無効だった (メルトンの主効果ごと死んでいた)。
        var problems = new List<string>();

        foreach (var effect in Data.Effects)
        {
            if (effect.Levels.Count == 0)
            {
                problems.Add($"{effect.Name} はレベルが 1 件も定義されていない");
                continue;
            }

            var inert = effect.Levels.All(l =>
                l is { AttackMultiplier: 1, DefenseDivisor: 1, Heal: 0, Damage: 0, ManaHeal: 0, NoMovePercent: 0 });

            // 「天邪鬼」だけはデータに数値を持たず、HitEvent が名前を直接見て
            // 倍率の掛け算を割り算に反転させる (HitEvent.cs の Amanojaku)。
            var hardCodedInEngine = effect.Name == "天邪鬼";

            var hasOtherRole = effect.Protects.Count > 0
                               || effect.NoDamageElements.Count > 0
                               || effect.BlocksPhysical
                               || effect.BlocksMagic
                               || effect.Reflects
                               || effect.TriggersOnExpiry
                               || hardCodedInEngine;

            if (inert && !hasOtherRole)
            {
                problems.Add($"{effect.Name} は数値も耐性も持たず、付与されても何も起きない");
            }
        }

        Assert.True(problems.Count == 0, Join("効き目の無い状態異常がある", problems));
    }

    /// <summary>失敗メッセージの末尾に付ける現状の一覧。これを見れば手で検算せずに済む。</summary>
    private static string Table()
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            $"{"フィールド",-8}{"上限",6}{"HP倍",6}{"攻倍",6}{"経験倍",7}  " +
            $"{"参入Lv",6}{"適正技",-12}{"参入turn",9}{"上限turn",9}{"被ダメ/体(参入)",10}{"exp/turn天井",13}");

        foreach (var (field, entry) in Tiers())
        {
            var a = BalanceProbe.Measure(Data, field.Name, entry, entry);
            var b = BalanceProbe.Measure(Data, field.Name, field.LevelCap, field.LevelCap);
            sb.AppendLine(
                $"{field.Name,-8}{field.LevelCap,6}{field.HpMultiplier,6:F2}{field.AttackMultiplier,6:F2}" +
                $"{field.ExpMultiplier,7:F1}  {entry,6}{a.Skill.Name,-12}" +
                $"{a.TurnsToKill,9:F2}{b.TurnsToKill,9:F2}{a.DamageTakenPerKill,10:P0}" +
                $"{field.LevelCap * field.ExpMultiplier,13:F0}");
        }

        return sb.ToString();
    }

    private static string Join(string headline, IReadOnlyList<string> problems)
        => $"{headline} ({problems.Count} 件):" + Environment.NewLine
           + string.Join(Environment.NewLine, problems.Select(p => "  - " + p))
           + Environment.NewLine + Environment.NewLine + Table();
}
