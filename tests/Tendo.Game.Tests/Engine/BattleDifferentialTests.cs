using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Tendo.Game.Engine;
using Tendo.Game.Master;
using Tendo.Game.State;
using Xunit;

namespace Tendo.Game.Tests.Engine;

/// <summary>
/// 戦闘計算の差分テスト。この移植で忠実性を担保する中心となる仕組み。
///
/// <c>tools/reference-battle.js</c> が ea.js から式と処理順をそのまま写した
/// 参照実装で、mulberry32 の固定種で 400 ケース分の結果を
/// <c>Fixtures/battle-reference.json</c> に書き出してある。
/// 同じ種・同じ初期状態を C# の <see cref="TurnRunner"/> に与え、
/// 最終的な体力・魔力・所持金・状態異常・フィールド、そして戦闘ログの
/// 全行が一致することを確かめる。
///
/// 乱数は「引かれる順序」まで一致していないとずれるので、
/// 処理順の取り違えもここで検出できる。
///
/// 再生成:
///   node tools/reference-battle.js 1000 400 &gt; tests/Tendo.Game.Tests/Fixtures/battle-reference.json
/// </summary>
public sealed class BattleDifferentialTests
{
    private static readonly MasterData Data = MasterDataLoader.Load();
    private static readonly IReadOnlyList<ReferenceCase> Cases = LoadCases();

    /// <summary>参照実装が使った種の基点。fixture の生成コマンドと揃えること。</summary>
    private const int BaseSeed = 1000;

    [Fact]
    public void 参照ケースが読み込めている()
    {
        Assert.Equal(400, Cases.Count);

        // 主要な経路を通っているケースが実際に含まれていること。
        // これが 0 になったら、テストが素通りしているだけの状態。
        Assert.Contains(Cases, c => c.ResultMoved);
        Assert.Contains(Cases, c => c.HasPet);
        Assert.Contains(Cases, c => c.ResultEnemyHp == 0);
        Assert.Contains(Cases, c => c.ResultPlayerHp <= 0);
        Assert.Contains(Cases, c => c.Log.Any(l => l.Contains("状態になった")));
        Assert.Contains(Cases, c => c.Log.Any(l => l.Contains("状態にならない")));
        Assert.Contains(Cases, c => c.Log.Any(l => l.Contains("で動けない")));
        Assert.Contains(Cases, c => c.Log.Any(l => l.Contains("魔力が足りない")));
        Assert.Contains(Cases, c => c.Log.Any(l => l.Contains("ギルを")));
        Assert.Contains(Cases, c => c.Log.Any(l => l.Contains("状態が切れた")));
    }

    [Fact]
    public void 全ケースで旧実装と同じ結果になる()
    {
        var failures = new List<string>();

        foreach (var expected in Cases)
        {
            var actual = Run(expected);
            var problems = Compare(expected, actual);

            if (problems.Count > 0)
            {
                failures.Add(
                    $"case #{expected.Index} (seed={expected.Seed}, 技={expected.Skill}, " +
                    $"敵={expected.EnemyCode}, 難易度={expected.Difficulty}):" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, problems.Select(p => "    " + p)));
            }
        }

        Assert.True(
            failures.Count == 0,
            $"{failures.Count}/{Cases.Count} 件が旧実装と食い違いました:" + Environment.NewLine +
            string.Join(Environment.NewLine, failures.Take(10)));
    }

    private static List<string> Compare(ReferenceCase expected, ActualOutcome actual)
    {
        var problems = new List<string>();

        void Check(string label, object? want, object? got)
        {
            if (!Equals(want, got))
            {
                problems.Add($"{label}: 期待 {want} / 実際 {got}");
            }
        }

        Check("プレイヤー体力", expected.ResultPlayerHp, actual.PlayerHp);
        Check("プレイヤー魔力", expected.ResultPlayerMp, actual.PlayerMana);
        Check("プレイヤー所持金", expected.ResultPlayerGil, actual.PlayerGil);
        Check("敵体力", expected.ResultEnemyHp, actual.EnemyHp);
        Check("敵魔力", expected.ResultEnemyMp, actual.EnemyMana);
        Check("フィールド", expected.ResultField, actual.Field);
        Check("吸い込み発生", expected.ResultMoved, actual.Moved);
        Check(
            "プレイヤー状態異常",
            string.Join(" | ", expected.ResultPlayerEffects),
            string.Join(" | ", actual.PlayerEffects));
        Check(
            "敵状態異常",
            string.Join(" | ", expected.ResultEnemyEffects),
            string.Join(" | ", actual.EnemyEffects));

        if (!expected.Log.SequenceEqual(actual.Log, StringComparer.Ordinal))
        {
            problems.Add(
                "ログ不一致:" + Environment.NewLine +
                "      期待: " + string.Join(" / ", expected.Log) + Environment.NewLine +
                "      実際: " + string.Join(" / ", actual.Log));
        }

        return problems;
    }

    /// <summary>
    /// 参照実装の <c>buildCase(index)</c> と同じ初期状態を組み立てて 1 ターン走らせる。
    /// 条件式を変えると両者がずれるので、必ず reference-battle.js と対応させること。
    /// </summary>
    private static ActualOutcome Run(ReferenceCase reference)
    {
        var index = reference.Index;
        var random = new Mulberry32((uint)reference.Seed);

        var enemyDef = Data.Enemies[index % Data.Enemies.Count];
        var skill = Data.Skills[index * 7 % Data.Skills.Count];
        var difficulty = Data.Difficulties[index % Data.Difficulties.Count];

        var enemyLevel = 1 + (index * 13 % 400);
        var playerLevel = 1 + (index * 17 % 300);
        var enemyHp = JsMath.RoundToLong(
            ((enemyLevel * enemyDef.HpMultiplier * 10) + Data.Fix.Enemy) * difficulty.HpMultiplier);

        var field = enemyDef.Fields.Count > 0 ? enemyDef.Fields[0] : "草原";
        var mana = index % 2 == 0 ? 999999 : JsMath.RoundToLong(playerLevel * 2.22);

        var player = new PlayerState
        {
            UserId = 1,
            Hp = (playerLevel * 10) + Data.Fix.Player,
            MaxHp = (playerLevel * 10) + Data.Fix.Player,
            Mana = mana,
            MaxMana = mana,
            Speed = 1,
            Level = playerLevel,
            Experience = 0,
            Gil = 100000,
            BattleChannelId = null,
        };

        if (index % 3 == 0)
        {
            player.Effects.Add(new ActiveEffect("毒", 1 + (index % 5), 3 + (index % 4)));
        }

        if (index % 7 == 0)
        {
            player.Effects.Add(new ActiveEffect("天邪鬼", 1, 5));
        }

        if (index % 11 == 0)
        {
            player.Effects.Add(new ActiveEffect("再生", 1 + (index % 3), 4));
        }

        if (index % 5 == 0)
        {
            var species = Data.Enemies[index * 3 % Data.Enemies.Count];
            player.Pet = new PetState
            {
                Name = "ペット" + index,
                EnemyCode = species.Code,
                Level = 1 + (index % 50),
                Experience = 0,
                AttackChance = 100,
                Ability = Data.Abilities[index % Data.Abilities.Count].Name,
            };
        }

        var battle = new BattleState
        {
            ChannelId = 1,
            Field = field,
            EnemyCode = enemyDef.Code,
            Level = enemyLevel,
            MaxHp = enemyHp,
            Hp = enemyHp,
            MaxMana = enemyDef.MaxMana,
            Mana = enemyDef.MaxMana,
            Difficulty = difficulty.Name,
            ChecksumChannelId = 1,
        };

        foreach (var initial in enemyDef.InitialEffects)
        {
            battle.Effects.Add(ActiveEffect.From(initial));
        }

        var context = new TurnContext
        {
            Battle = battle,
            Player = player,
            Enemy = enemyDef,
            Difficulty = difficulty,
            PlayerName = "テスト",
        };

        // 参照実装は習得判定より後の処理だけを写しているので、判定は飛ばす。
        var result = new TurnRunner(Data, random).Execute(context, skill, skipLearnCheck: true);

        return new ActualOutcome
        {
            PlayerHp = player.Hp,
            PlayerMana = player.Mana,
            PlayerGil = player.Gil,
            EnemyHp = battle.Hp,
            EnemyMana = battle.Mana,
            Field = battle.Field,
            Moved = context.Moved,
            PlayerEffects = [.. player.Effects.Select(e => $"{e.Name}:{e.Level}:{e.TurnsLeft}")],
            EnemyEffects = [.. battle.Effects.Select(e => $"{e.Name}:{e.Level}:{e.TurnsLeft}")],
            Log = ExtractLogLines(result.Description),
        };
    }

    /// <summary>
    /// C# 側はコードブロックで装飾済みの文字列を返すので、中身だけ取り出して比較する。
    /// 参照実装は装飾しない生のログ行を持っている。
    /// </summary>
    private static IReadOnlyList<string> ExtractLogLines(string description)
        => FencePattern.Matches(description).Select(m => m.Groups[1].Value).ToList();

    private static readonly Regex FencePattern =
        new("```[A-Za-z]*\n(.*?)\n```", RegexOptions.Singleline | RegexOptions.Compiled);

    private static IReadOnlyList<ReferenceCase> LoadCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "battle-reference.json");

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"参照ケースが見つかりません: {path}{Environment.NewLine}" +
                $"node tools/reference-battle.js {BaseSeed} 400 > tests/Tendo.Game.Tests/Fixtures/battle-reference.json");
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<List<ReferenceCase>>(stream)
               ?? throw new InvalidOperationException("参照ケースの読み込みに失敗しました。");
    }

    private sealed record ActualOutcome
    {
        public required long PlayerHp { get; init; }

        public required long PlayerMana { get; init; }

        public required long PlayerGil { get; init; }

        public required long EnemyHp { get; init; }

        public required long EnemyMana { get; init; }

        public required string Field { get; init; }

        public required bool Moved { get; init; }

        public required IReadOnlyList<string> PlayerEffects { get; init; }

        public required IReadOnlyList<string> EnemyEffects { get; init; }

        public required IReadOnlyList<string> Log { get; init; }
    }

    private sealed record ReferenceCase
    {
        [JsonPropertyName("index")]
        public int Index { get; init; }

        [JsonPropertyName("seed")]
        public int Seed { get; init; }

        [JsonPropertyName("skill")]
        public string Skill { get; init; } = string.Empty;

        [JsonPropertyName("enemyCode")]
        public string EnemyCode { get; init; } = string.Empty;

        [JsonPropertyName("difficulty")]
        public string Difficulty { get; init; } = string.Empty;

        [JsonPropertyName("hasPet")]
        public bool HasPet { get; init; }

        [JsonPropertyName("resultPlayerHp")]
        public long ResultPlayerHp { get; init; }

        [JsonPropertyName("resultPlayerMp")]
        public long ResultPlayerMp { get; init; }

        [JsonPropertyName("resultPlayerGil")]
        public long ResultPlayerGil { get; init; }

        [JsonPropertyName("resultEnemyHp")]
        public long ResultEnemyHp { get; init; }

        [JsonPropertyName("resultEnemyMp")]
        public long ResultEnemyMp { get; init; }

        [JsonPropertyName("resultPlayerEffects")]
        public IReadOnlyList<string> ResultPlayerEffects { get; init; } = [];

        [JsonPropertyName("resultEnemyEffects")]
        public IReadOnlyList<string> ResultEnemyEffects { get; init; } = [];

        [JsonPropertyName("resultField")]
        public string ResultField { get; init; } = string.Empty;

        [JsonPropertyName("resultMoved")]
        public bool ResultMoved { get; init; }

        [JsonPropertyName("log")]
        public IReadOnlyList<string> Log { get; init; } = [];
    }
}
