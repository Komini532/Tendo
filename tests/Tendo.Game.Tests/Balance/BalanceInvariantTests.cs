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

    /// <summary>
    /// 通常進行のフィールド。<c>field-requirements.json</c> に「行き先」として載っているものが
    /// そのまま階層の一覧になる (階層をテストに二重に書かずに済む)。
    ///
    /// 地獄 (魔鏡の吸い込み専用)・竜洞 (未完成)・永遠悪夢 (イベント用) はここに入らない。
    /// </summary>
    private static IReadOnlyList<FieldDef> Ladder()
        => Data.FieldRequirements
            .Select(r => Data.FindField(r.Name))
            .Where(f => f is not null)
            .Select(f => f!)
            .DistinctBy(f => f.Name)
            .OrderBy(f => f.LevelCap)
            .ThenBy(f => f.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>通常進行の外にあるフィールド。/go の行き先にならない。</summary>
    private static IReadOnlyList<FieldDef> SpecialFields()
    {
        var ladder = Ladder().Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        return Data.Fields.Where(f => !ladder.Contains(f.Name)).ToList();
    }

    /// <summary>
    /// ティア順に並べた通常進行のフィールドと、その参入Lv (= 一つ下のティアの上限) と滞在撃破数。
    ///
    /// 敵Lv は 1 撃破 +1、自Lv も <c>field.exp</c> の選び方で 1:1 に追従するので、
    /// <b>滞在撃破数 = 上限 − 参入Lv</b> になる。
    /// </summary>
    private static IReadOnlyList<(FieldDef Field, int EntryLevel, int Kills)> Tiers()
    {
        var ladder = Ladder();
        var caps = ladder.Select(f => f.LevelCap).Distinct().OrderBy(c => c).ToList();

        return ladder
            .Select(f =>
            {
                var index = caps.IndexOf(f.LevelCap);
                var entry = index == 0 ? 1 : caps[index - 1];
                return (f, entry, f.LevelCap - entry);
            })
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

        foreach (var (field, entry, _) in Tiers())
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
        var ordered = Ladder();

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

        foreach (var (field, entry, _) in Tiers())
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
    [InlineData(0, 2.0, 1.6, 2.0)]
    [InlineData(1, 3.0, 2.5, 4.0)]
    [InlineData(2, 3.0, 2.5, 8.0)]
    [InlineData(3, 3.0, 2.5, 2.0)]
    [InlineData(4, 3.0, 2.5, 10.0)]
    public void 個体倍率はレア度ごとの上限に収まる(int rarity, double hpMax, double atkMax, double expMax)
    {
        // 個体倍率は「そのフィールドの基準からのブレ」だけを表し、
        // スケールはフィールド側 (hp/atk/cap) が持つ。両方が伸びると調整できなくなる。
        //
        // 下限は掛けない。掛けると弱い敵まで底上げされてしまう
        // (「踊る金貨」はギル用の敵なので exp 0.5 のままで正しい)。
        //
        // レア3 の exp 上限だけレア0 と同じ 2.0 なのは、レア3 の出現率が 7% と
        // レア1 の 3% より高く「レア」と呼べる頻度ではないため。8.0 のままだと
        // レア3 が経験値経済の 3 割を占め、エッグの居る火山・遺跡だけ突出する。
        var problems = new List<string>();

        foreach (var e in Data.Enemies.Where(e => e.Rarity == rarity))
        {
            if (PenaltyEnemies.Contains(e.Code, StringComparer.Ordinal))
            {
                continue;
            }

            if (e.HpMultiplier > hpMax)
            {
                problems.Add($"{e.Name} ({e.Code}) の hp {e.HpMultiplier} が上限 {hpMax} を超えている");
            }

            if (e.AttackMultiplier > atkMax)
            {
                problems.Add($"{e.Name} ({e.Code}) の atk {e.AttackMultiplier} が上限 {atkMax} を超えている");
            }

            if (e.ExpMultiplier > expMax)
            {
                problems.Add($"{e.Name} ({e.Code}) の exp {e.ExpMultiplier} が上限 {expMax} を超えている");
            }
        }

        Assert.True(problems.Count == 0, Join($"レア度 {rarity} の個体倍率が上限を超えている", problems));
    }

    /// <summary>
    /// 旧実装のマクロ検知で出していたペナルティエネミー。
    /// 法外な個体倍率 (Nightmare は hp 104 / atk 7.4) はこの出自によるもので、
    /// 入手経路が管理者コマンドと永遠悪夢に限られるため個体倍率の上限を課さない。
    /// 旧 <c>penaltyenemy = ["nightmare", "daydream"]</c> (「吸い込み」の対象外でもある)。
    /// </summary>
    private static readonly string[] PenaltyEnemies = ["nightmare", "omni-nightmare", "daydream"];

    [Fact]
    public void ペナルティエネミーは硬いが実入りが無い()
    {
        // 個体倍率の上限を外している 3 体。硬さは残す一方で、下位フィールドに召喚されたときに
        // そのフィールドの経験値天井を飛び越えないよう exp だけは通常の敵と同じ帯に収める。
        foreach (var code in PenaltyEnemies)
        {
            var enemy = Data.FindEnemy(code);
            Assert.NotNull(enemy);
            Assert.True(enemy.HpMultiplier >= 10, $"{enemy.Name} ({code}) の hp {enemy.HpMultiplier} が威圧として弱すぎる");
            Assert.True(enemy.ExpMultiplier <= 2.0, $"{enemy.Name} ({code}) の exp {enemy.ExpMultiplier} が高すぎる");
        }
    }

    [Fact]
    public void 特殊フィールドは通常進行の外にある()
    {
        // 地獄は魔鏡の「吸い込み」でのみ、竜洞は未完成で到達不可、
        // 永遠悪夢は管理者コマンドでのみ。いずれも /go の行き先にはしない。
        Assert.Equal(
            new HashSet<string>(["地獄", "竜洞", "永遠悪夢"], StringComparer.Ordinal),
            SpecialFields().Select(f => f.Name).ToHashSet(StringComparer.Ordinal));

        // 竜洞と永遠悪夢はどこの移動元にもならない (管理者が戻す)。
        // 地獄だけは移動元に残る — 理由は 地獄から出る経路が残っている() を参照。
        foreach (var name in new[] { "竜洞", "永遠悪夢" })
        {
            Assert.DoesNotContain(
                Data.FieldRequirements,
                r => r.ConnectedFrom.Contains(name, StringComparer.Ordinal));
        }

        // 地獄には居座る価値が無いこと。経験値/ターンは exp/hp に比例するので、
        // 通常進行の最上位より低ければ、吸い込まれた人は自然に出て行く。
        var hell = Data.FindField("地獄");
        Assert.NotNull(hell);
        var best = Ladder().Max(f => f.ExpMultiplier / f.HpMultiplier);
        Assert.True(
            hell.ExpMultiplier / hell.HpMultiplier < best,
            $"地獄の経験値/ターン比 {hell.ExpMultiplier / hell.HpMultiplier:F2} が " +
            $"通常進行の最良 {best:F2} を下回っていない");
    }

    [Fact]
    public void 地獄から出る経路が残っている()
    {
        // 地獄は /go の行き先から外してあるが、移動元として残しておかないと
        // 魔鏡に吸い込まれたプレイヤーが二度と出られなくなる。
        // 魔鏡の出るフィールドの敵Lv上限で満たせる移動先があることを確かめる。
        var mirrorFields = Data.Enemies
            .Where(e => e.Skills.Any(sk => Data.FindSkill(sk.Name)?.MoveField == "地獄"))
            .SelectMany(e => e.Fields)
            .Distinct()
            .ToList();

        Assert.NotEmpty(mirrorFields);

        var exits = Data.FieldRequirements
            .Where(r => r.ConnectedFrom.Contains("地獄", StringComparer.Ordinal))
            .ToList();

        foreach (var from in mirrorFields)
        {
            // 吸い込まれた時点の battle.Level は、元いたフィールドの上限が上限。
            var carried = Data.FindField(from)!.LevelCap;

            Assert.True(
                exits.Any(r => r.RequiredEnemyLevel <= carried),
                $"「{from}」から吸い込まれた人 (敵Lv 最大 {carried}) が地獄から出られない");
        }
    }

    /// <summary>
    /// 1 撃破あたりの 個体exp の期待値。<c>EncounterSpawner.ChooseForField</c> の抽選確率
    /// (レア2 が 1/777、レア4 が 1/2048、レア3 が 7%、レア1 が 3%、残りがレア0) で重み付けする。
    /// そのレア度の敵がフィールドに居なければ判定は素通りする。
    /// </summary>
    private static double ExpectedEnemyExp(string field)
    {
        var pool = Enumerable.Range(0, 5).ToDictionary(
            rare => rare,
            rare => Data.Enemies
                .Where(e => e.Rarity == rare && e.Fields.Contains(field, StringComparer.Ordinal))
                .Select(e => e.ExpMultiplier)
                .ToList());

        double total = 0, remaining = 1;

        foreach (var (rare, chance) in new[] { (2, 1 / 777.0), (4, 1 / 2048.0), (3, 0.07), (1, 0.03) })
        {
            if (pool[rare].Count == 0)
            {
                continue;
            }

            total += remaining * chance * pool[rare].Average();
            remaining -= remaining * chance;
        }

        return total + (remaining * (pool[0].Count > 0 ? pool[0].Average() : 1));
    }

    [Fact]
    public void 自レベルが有効敵レベルに追従し続ける()
    {
        // 最上位ティアは 9600 撃破に及ぶので、自Lv と 有効敵Lv のわずかなずれが
        // 滞在中に積み上がる。ずれると戦闘の重さが設計から外れるので、
        // field.exp はこれが 1.0 に近くなるよう選んである
        // (1 撃破で 敵Lv +1・自Lv +1 になるのは 個体exp × field.exp / 2 == 1 のとき)。
        //
        // レベルアップは累計xp >= (Lv+1)² なので 自Lv ≒ √(累計xp)。
        // 参入時は 自Lv == 有効敵Lv == 参入Lv から始まる。
        var problems = new List<string>();

        foreach (var (field, entry, kills) in Tiers())
        {
            var perKill = ExpectedEnemyExp(field.Name) * field.ExpMultiplier;
            double xp = (double)entry * entry;
            double worst = 1;

            for (var k = 1; k <= kills; k++)
            {
                var enemyLevel = entry + k;
                xp += perKill * enemyLevel;

                var ratio = Math.Sqrt(xp) / enemyLevel;
                if (Math.Abs(ratio - 1) > Math.Abs(worst - 1))
                {
                    worst = ratio;
                }
            }

            if (worst is < 0.85 or > 1.15)
            {
                problems.Add(
                    $"{field.Name} (参入Lv {entry} → 上限 {field.LevelCap}、{kills} 撃破) で " +
                    $"自Lv/有効敵Lv が {worst:F3} まで離れる " +
                    $"(1撃破あたりの経験値 {perKill:F2} / 狙いは 2.00)");
            }
        }

        Assert.True(problems.Count == 0, Join("階層の滞在中に自Lvが有効敵Lvから離れる", problems));
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
            $"{"フィールド",-8}{"上限",7}{"HP倍",6}{"攻倍",6}{"経験倍",7}  " +
            $"{"参入Lv",7}{"滞在撃破",9}{"目安",7}  {"適正技",-12}" +
            $"{"参入turn",9}{"上限turn",9}{"被ダメ/体",10}{"exp/turn天井",13}");

        foreach (var (field, entry, kills) in Tiers())
        {
            var a = BalanceProbe.Measure(Data, field.Name, entry, entry);
            var b = BalanceProbe.Measure(Data, field.Name, field.LevelCap, field.LevelCap);
            sb.AppendLine(
                $"{field.Name,-8}{field.LevelCap,7}{field.HpMultiplier,6:F2}{field.AttackMultiplier,6:F2}" +
                $"{field.ExpMultiplier,7:F2}  {entry,7}{kills,9}{KillsToHours(kills),6:F1}h  {a.Skill.Name,-12}" +
                $"{a.TurnsToKill,9:F2}{b.TurnsToKill,9:F2}{a.DamageTakenPerKill,10:P0}" +
                $"{field.LevelCap * field.ExpMultiplier,13:F0}");
        }

        sb.AppendLine();
        sb.AppendLine("通常進行の外 (/go の行き先にならないフィールド):");

        foreach (var f in SpecialFields())
        {
            sb.AppendLine(
                $"  {f.Name,-8} 上限 {f.LevelCap,6}  HP倍 {f.HpMultiplier,5:F2}  攻倍 {f.AttackMultiplier,5:F2}" +
                $"  経験倍 {f.ExpMultiplier,5:F2}  exp/hp {f.ExpMultiplier / f.HpMultiplier,5:F2}");
        }

        return sb.ToString();
    }

    /// <summary>撃破数を時間の目安へ。4 体/分 は「Lv100 分を 20〜30 分」という体感から。</summary>
    private static double KillsToHours(int kills) => kills / 4.0 / 60.0;

    private static string Join(string headline, IReadOnlyList<string> problems)
        => $"{headline} ({problems.Count} 件):" + Environment.NewLine
           + string.Join(Environment.NewLine, problems.Select(p => "  - " + p))
           + Environment.NewLine + Environment.NewLine + Table();
}
