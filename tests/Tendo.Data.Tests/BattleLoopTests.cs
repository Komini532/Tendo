using Tendo.Data.Repositories;
using Tendo.Game.Engine;
using Tendo.Game.Master;
using Tendo.Game.State;
using Xunit;

namespace Tendo.Data.Tests;

/// <summary>
/// 戦闘 → 撃破 → 報酬 → 次の敵出現 を DB 越しに通しで動かす。
///
/// 単体テストは計算だけを見ているので、「毎ターン保存して読み直しても状態が保たれるか」
/// はここで確かめる。状態異常の残りターンや参加者リストは往復のたびに壊れやすい。
/// </summary>
[Collection(MySqlCollection.Name)]
public sealed class BattleLoopTests
{
    private readonly MySqlFixture _fixture;
    private readonly MasterData _data = MasterDataLoader.Load();
    private readonly GameDefaults _defaults = GameDefaults.Load();

    public BattleLoopTests(MySqlFixture fixture)
    {
        _fixture = fixture;
    }

    private PlayerRepository Players => new(_fixture.Connections, _defaults);

    private BattleRepository Battles => new(_fixture.Connections, _defaults);

    private static ulong NewSnowflake()
        => 1_000_000_000_000_000_000UL + (ulong)Random.Shared.NextInt64(0, 999_999_999_999_999L);

    [RequiresMySqlFact]
    public async Task 敵を倒すと報酬が入り次の敵が出る()
    {
        var random = new Mulberry32(4242);
        var channelId = NewSnowflake();
        var userId = NewSnowflake();

        // 高レベルのプレイヤーで弱い敵に挑み、確実に決着させる。
        var player = _defaults.NewPlayer(userId);
        player.Level = 200;
        player.MaxHp = (player.Level * 10) + _data.Fix.Player;
        player.Hp = player.MaxHp;
        player.MaxMana = 500;
        player.Mana = 500;
        await Players.SaveAsync(player);

        var battle = _defaults.NewBattle(channelId);
        var enemy = _data.FindEnemy("seed")!;
        new EncounterSpawner(_data, random).Spawn(battle, enemy, level: 1);
        await Battles.SaveAsync(battle);

        var attack = _data.FindSkill("攻撃")!;
        TurnResult? final = null;

        // 毎ターン保存して読み直す (実運用と同じ経路を通す)。
        for (var turn = 0; turn < 50; turn++)
        {
            var loadedBattle = await Battles.FindAsync(channelId);
            var loadedPlayer = await Players.FindAsync(userId);
            Assert.NotNull(loadedBattle);
            Assert.NotNull(loadedPlayer);

            var currentEnemy = _data.FindEnemy(loadedBattle.EnemyCode)!;

            var context = new TurnContext
            {
                Battle = loadedBattle,
                Player = loadedPlayer,
                Enemy = currentEnemy,
                Difficulty = _data.FindDifficulty(loadedBattle.Difficulty) ?? _data.DefaultDifficulty,
                Field = _data.FindField(loadedBattle.Field) ?? _data.DefaultField,
                PlayerName = "テスト",
            };

            final = new TurnRunner(_data, random).Execute(context, attack);

            await Battles.SaveAsync(loadedBattle);
            await Players.SaveAsync(loadedPlayer);

            if (final.Outcome == TurnOutcome.EnemyDefeated)
            {
                break;
            }

            Assert.Equal(TurnOutcome.Continue, final.Outcome);
        }

        Assert.NotNull(final);
        Assert.Equal(TurnOutcome.EnemyDefeated, final.Outcome);

        // --- 報酬 -----------------------------------------------------------
        var afterFight = await Battles.FindAsync(channelId);
        Assert.NotNull(afterFight);

        // 戦闘に参加したことが記録されている。
        Assert.Contains(userId, afterFight.Participants);

        var participants = await Players.FindManyAsync(afterFight.Participants);
        var reward = new RewardCalculator(_data, random)
            .Apply(afterFight, enemy, participants, _ => "テスト");
        await Players.SaveManyAsync(participants);

        Assert.True(reward.Experience > 0);

        var rewarded = await Players.FindAsync(userId);
        Assert.NotNull(rewarded);
        Assert.Equal(reward.Experience, rewarded.Experience);
        Assert.Equal(reward.Gil, rewarded.Gil);

        // 勝利すると全快し、戦闘状態が解ける。
        Assert.Equal(rewarded.MaxHp, rewarded.Hp);
        Assert.Null(rewarded.BattleChannelId);

        // レベル 200 なので、その時点までに習得できる技が全て入っている。
        var expectedSkills = _data.LearnableSkills
            .Where(s => s.LearnLevel <= rewarded.Level)
            .Select(s => s.Name)
            .OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(expectedSkills, rewarded.Skills.OrderBy(n => n, StringComparer.Ordinal));
        Assert.NotEmpty(rewarded.Skills);

        // --- 次の敵 ---------------------------------------------------------
        var announcement = new EncounterSpawner(_data, random).SpawnNext(afterFight);
        Assert.NotNull(announcement);
        await Battles.SaveAsync(afterFight);

        var next = await Battles.FindAsync(channelId);
        Assert.NotNull(next);

        // レベルが 1 上がり、体力は満タン、参加者とターン数は初期化される。
        Assert.Equal(2, next.Level);
        Assert.Equal(next.MaxHp, next.Hp);
        Assert.Equal(0, next.Turn);
        Assert.Empty(next.Participants);
        Assert.Contains("が現れた！", announcement.Description);
    }

    [RequiresMySqlFact]
    public async Task 状態異常の残りターンが保存を挟んでも減り続ける()
    {
        var random = new Mulberry32(77);
        var channelId = NewSnowflake();
        var userId = NewSnowflake();

        var player = _defaults.NewPlayer(userId);
        player.Level = 300;
        player.MaxHp = 100000;
        player.Hp = 100000;
        // 5 ターン持続の毒を仕込む。
        player.Effects.Add(new ActiveEffect("毒", 1, 5));
        await Players.SaveAsync(player);

        var battle = _defaults.NewBattle(channelId);
        var enemy = _data.FindEnemy("seed")!;
        new EncounterSpawner(_data, random).Spawn(battle, enemy, level: 500);
        await Battles.SaveAsync(battle);

        var wait = _data.FindSkill("何もしない")!;
        var observed = new List<int>();

        for (var turn = 0; turn < 3; turn++)
        {
            var loadedBattle = await Battles.FindAsync(channelId);
            var loadedPlayer = await Players.FindAsync(userId);

            var context = new TurnContext
            {
                Battle = loadedBattle!,
                Player = loadedPlayer!,
                Enemy = _data.FindEnemy(loadedBattle!.EnemyCode)!,
                Difficulty = _data.DefaultDifficulty,
                Field = _data.FindField(loadedBattle.Field) ?? _data.DefaultField,
                PlayerName = "テスト",
            };

            new TurnRunner(_data, random).Execute(context, wait, skipLearnCheck: true);

            await Battles.SaveAsync(loadedBattle);
            await Players.SaveAsync(loadedPlayer!);

            var poison = loadedPlayer!.Effects.FirstOrDefault(e => e.Name == "毒");
            observed.Add(poison?.TurnsLeft ?? 0);
        }

        // 毎ターン 1 ずつ減る。保存と読み直しを挟んでも巻き戻らない。
        Assert.Equal([4, 3, 2], observed);
    }
}
