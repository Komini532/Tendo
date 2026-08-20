using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Tendo.Game.Engine;
using Tendo.Game.Master;
using Tendo.Game.State;
using Xunit;

namespace Tendo.Game.Tests.Engine;

/// <summary>
/// 撃破報酬・次の敵の抽選・ペット捕獲の差分テスト。
///
/// 参照実装は <c>tools/reference-reward.js</c>。
/// 特に次の敵の抽選は「判定より前に 4 つの乱数を全て引く」形なので、
/// 順序を取り違えると以降の乱数列がずれる。ここで検出する。
///
/// 再生成:
///   node tools/reference-reward.js 2000 200 &gt; tests/Tendo.Game.Tests/Fixtures/reward-reference.json
/// </summary>
public sealed class RewardDifferentialTests
{
    private static readonly MasterData Data = MasterDataLoader.Load();
    private static readonly GameDefaults Defaults = GameDefaults.Load();
    private static readonly IReadOnlyList<RewardCase> Cases = LoadCases();

    private static readonly Regex FencePattern =
        new("```[A-Za-z]*\n(.*?)\n```", RegexOptions.Singleline | RegexOptions.Compiled);

    [Fact]
    public void 参照ケースが主要な経路を通っている()
    {
        Assert.Equal(200, Cases.Count);
        Assert.Contains(Cases, c => c.UpdateLines.Any(l => l.Contains("レベルアップ")));
        Assert.Contains(Cases, c => c.UpdateLines.Any(l => l.Contains("習得した")));
        Assert.Contains(Cases, c => c.Drops.Count > 0);
        Assert.Contains(Cases, c => c.PetLevel is not null);
    }

    [Fact]
    public void 撃破報酬が旧実装と一致する()
    {
        var failures = new List<string>();

        foreach (var expected in Cases)
        {
            var problems = CompareReward(expected);
            if (problems.Count > 0)
            {
                failures.Add(
                    $"case #{expected.Index} (seed={expected.Seed}, 敵={expected.EnemyCode}):" +
                    Environment.NewLine + string.Join(Environment.NewLine, problems.Select(p => "    " + p)));
            }
        }

        Assert.True(
            failures.Count == 0,
            $"{failures.Count}/{Cases.Count} 件が食い違いました:" + Environment.NewLine +
            string.Join(Environment.NewLine, failures.Take(10)));
    }

    private static List<string> CompareReward(RewardCase expected)
    {
        var problems = new List<string>();
        var random = new Mulberry32((uint)expected.Seed);

        var enemy = Data.FindEnemy(expected.EnemyCode)!;
        var battle = new BattleState
        {
            ChannelId = 1,
            Field = expected.Field,
            Difficulty = expected.Difficulty,
            EnemyCode = enemy.Code,
            Level = expected.EnemyLevel,
        };

        var player = BuildPlayer(expected.Index);

        var reward = new RewardCalculator(Data, random)
            .Apply(battle, enemy, [player], _ => "テスト");

        void Check(string label, object? want, object? got)
        {
            if (!Equals(want, got))
            {
                problems.Add($"{label}: 期待 {want} / 実際 {got}");
            }
        }

        Check("経験値", expected.Exp, reward.Experience);
        Check("ギル", expected.Gil, reward.Gil);
        Check("ドロップ", string.Join(" | ", expected.Drops),
            string.Join(" | ", reward.Drops.Select(d => $"{d.ItemId}:{d.Count}")));
        Check("プレイヤーレベル", expected.PlayerLevel, player.Level);
        Check("経験値合計", expected.PlayerXp, player.Experience);
        Check("所持金", expected.PlayerGil, player.Gil);
        Check("最大体力", expected.PlayerMaxHp, player.MaxHp);
        Check("最大魔力", expected.PlayerMaxMp, player.MaxMana);
        Check("体力", expected.PlayerHp, player.Hp);
        Check("習得技", string.Join(" | ", expected.PlayerSkills), string.Join(" | ", player.Skills));
        Check("状態異常", string.Join(" | ", expected.PlayerEffects),
            string.Join(" | ", player.Effects.Select(e => $"{e.Name}:{e.Level}:{e.TurnsLeft}")));
        Check("ペットレベル", expected.PetLevel, player.Pet?.Level);
        Check("ペット経験値", expected.PetXp, player.Pet?.Experience);

        // 報酬本文 (< RESULT > と獲得行、続けてレベルアップ等の通知)。
        var actualLines = FencePattern.Matches(reward.Description).Select(m => m.Groups[1].Value).ToList();
        var expectedLines = expected.RewardLines.Concat(expected.UpdateLines).ToList();
        if (!expectedLines.SequenceEqual(actualLines, StringComparer.Ordinal))
        {
            problems.Add(
                "本文不一致:" + Environment.NewLine +
                "      期待: " + string.Join(" / ", expectedLines) + Environment.NewLine +
                "      実際: " + string.Join(" / ", actualLines));
        }

        // 報酬処理のあとに続けて次の敵を抽選する (乱数列が続いていることも確かめる)。
        var spawner = new EncounterSpawner(Data, random);
        var next = spawner.ChooseForField(battle.Field);
        Check("次の敵", expected.NextEnemyCode, next?.Code);

        if (next is not null && expected.NextEnemyHp is { } expectedHp)
        {
            var announcement = spawner.Spawn(battle, next, expected.NextEnemyLevel);
            Check("次の敵の体力", expectedHp, battle.MaxHp);
            Check("次の敵のレベル", expected.NextEnemyLevel, battle.Level);
            Assert.Equal(next.Code, announcement.Enemy.Code);

            // 新しい敵になるとターン数と参加者は初期化される。
            Check("ターン数", 0, battle.Turn);
            Check("参加者数", 0, battle.Participants.Count);
        }

        // 続けてペット捕獲の抽選。ここも同じ乱数列の続きになる。
        var taming = new PetTaming(Data, random);
        var pet = taming.CreatePet(enemy, Defaults, previous: null);
        Check("ペットアビリティ", expected.PetAbility, pet.Ability);
        Check("ペット攻撃確率", expected.PetChance, pet.AttackChance);

        return problems;
    }

    /// <summary>参照実装の初期プレイヤーと同じものを組み立てる。</summary>
    private static PlayerState BuildPlayer(int index)
    {
        var level = 1 + (index * 17 % 200);

        var player = new PlayerState
        {
            UserId = 1,
            Hp = 1,
            MaxHp = (level * 10) + Data.Fix.Player,
            Mana = 0,
            MaxMana = JsMath.RoundToLong(level * 2.22),
            Level = level,
            // 次のレベルまで僅かに足りない位置。
            Experience = ((long)(level + 1) * (level + 1)) - 1,
            Gil = 0,
        };

        player.Effects.Add(new ActiveEffect("毒", 1, 5));
        player.Effects.Add(new ActiveEffect("即死", 1, 1));
        player.Effects.Add(new ActiveEffect("死の宣告", 1, 3));

        if (index % 4 == 0)
        {
            player.Pet = new PetState
            {
                Name = "ペット" + index,
                EnemyCode = Data.Enemies[index * 3 % Data.Enemies.Count].Code,
                Level = 1 + (index % 30),
                Experience = 0,
                AttackChance = 50,
                Ability = Data.Abilities[0].Name,
            };
        }

        return player;
    }

    private static IReadOnlyList<RewardCase> LoadCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "reward-reference.json");
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<List<RewardCase>>(stream)
               ?? throw new InvalidOperationException("参照ケースの読み込みに失敗しました。");
    }

    private sealed record RewardCase
    {
        [JsonPropertyName("index")]
        public int Index { get; init; }

        [JsonPropertyName("seed")]
        public int Seed { get; init; }

        [JsonPropertyName("enemyCode")]
        public string EnemyCode { get; init; } = string.Empty;

        [JsonPropertyName("field")]
        public string Field { get; init; } = string.Empty;

        [JsonPropertyName("difficulty")]
        public string Difficulty { get; init; } = string.Empty;

        [JsonPropertyName("enemyLevel")]
        public int EnemyLevel { get; init; }

        [JsonPropertyName("exp")]
        public long Exp { get; init; }

        [JsonPropertyName("gil")]
        public long Gil { get; init; }

        [JsonPropertyName("drops")]
        public IReadOnlyList<string> Drops { get; init; } = [];

        [JsonPropertyName("rewardLines")]
        public IReadOnlyList<string> RewardLines { get; init; } = [];

        [JsonPropertyName("updateLines")]
        public IReadOnlyList<string> UpdateLines { get; init; } = [];

        [JsonPropertyName("playerLevel")]
        public int PlayerLevel { get; init; }

        [JsonPropertyName("playerXp")]
        public long PlayerXp { get; init; }

        [JsonPropertyName("playerGil")]
        public long PlayerGil { get; init; }

        [JsonPropertyName("playerMaxHp")]
        public long PlayerMaxHp { get; init; }

        [JsonPropertyName("playerMaxMp")]
        public long PlayerMaxMp { get; init; }

        [JsonPropertyName("playerHp")]
        public long PlayerHp { get; init; }

        [JsonPropertyName("playerSkills")]
        public IReadOnlyList<string> PlayerSkills { get; init; } = [];

        [JsonPropertyName("playerEffects")]
        public IReadOnlyList<string> PlayerEffects { get; init; } = [];

        [JsonPropertyName("petLevel")]
        public int? PetLevel { get; init; }

        [JsonPropertyName("petXp")]
        public long? PetXp { get; init; }

        [JsonPropertyName("nextEnemyCode")]
        public string? NextEnemyCode { get; init; }

        [JsonPropertyName("nextEnemyLevel")]
        public int NextEnemyLevel { get; init; }

        [JsonPropertyName("nextEnemyHp")]
        public long? NextEnemyHp { get; init; }

        [JsonPropertyName("petAbility")]
        public string? PetAbility { get; init; }

        [JsonPropertyName("petChance")]
        public int PetChance { get; init; }
    }
}
