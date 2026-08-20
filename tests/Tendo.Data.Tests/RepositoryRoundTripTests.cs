using Tendo.Data.Repositories;
using Tendo.Game.State;
using Xunit;

namespace Tendo.Data.Tests;

/// <summary>
/// 旧実装は「集約を丸ごと読む → 書き換える → 丸ごと書き戻す」形だった。
/// 正規化スキーマに展開しても同じ内容が往復することを確かめる。
/// </summary>
[Collection(MySqlCollection.Name)]
public sealed class RepositoryRoundTripTests
{
    private readonly MySqlFixture _fixture;
    private readonly GameDefaults _defaults = GameDefaults.Load();

    public RepositoryRoundTripTests(MySqlFixture fixture)
    {
        _fixture = fixture;
    }

    private PlayerRepository Players => new(_fixture.Connections, _defaults);

    private BattleRepository Battles => new(_fixture.Connections, _defaults);

    /// <summary>実際の Discord ID と同じ 64bit 域の値を使う (精度落ちを検出するため)。</summary>
    private static ulong NewSnowflake()
        => 1_000_000_000_000_000_000UL + (ulong)Random.Shared.NextInt64(0, 999_999_999_999_999L);

    [RequiresMySqlFact]
    public async Task プレイヤーを丸ごと往復できる()
    {
        var id = NewSnowflake();
        var player = _defaults.NewPlayer(id);

        player.Level = 42;
        player.Experience = 1_849;
        player.Gil = 9_999_999;
        player.Hp = 520;
        player.MaxHp = 520;
        player.Mana = 93;
        player.MaxMana = 93;
        player.BattleChannelId = NewSnowflake();
        player.Skills.Add("攻撃");
        player.Skills.Add("ファイア");
        player.SetItem("p", 7);
        player.SetItem("c", 3);
        player.Effects.Add(new ActiveEffect("毒", 2, 5));
        player.Effects.Add(new ActiveEffect("再生", 1, 3));
        player.Pet = new PetState
        {
            Name = "ぬし",
            EnemyCode = "seed",
            Level = 12,
            Experience = 144,
            AttackChance = 55,
            Ability = "荒くれ",
        };

        await Players.SaveAsync(player);
        var loaded = await Players.FindAsync(id);

        Assert.NotNull(loaded);
        Assert.Equal(player.UserId, loaded.UserId);
        Assert.Equal(42, loaded.Level);
        Assert.Equal(1_849, loaded.Experience);
        Assert.Equal(9_999_999, loaded.Gil);
        Assert.Equal(player.BattleChannelId, loaded.BattleChannelId);
        // 習得技は「集合」であって順序は意味を持たない。
        // 旧実装も indexOf による所持判定と push しかせず、/skills の一覧は
        // マスターデータ側の順で描画される (ea.js:383, 620, 1087)。
        Assert.Equal(["ファイア", "攻撃"], loaded.Skills);
        Assert.Equal(7, loaded.GetItem("p"));
        Assert.Equal(3, loaded.GetItem("c"));

        Assert.NotNull(loaded.Pet);
        Assert.Equal("ぬし", loaded.Pet.Name);
        Assert.Equal(55, loaded.Pet.AttackChance);
        Assert.Equal("荒くれ", loaded.Pet.Ability);
    }

    [RequiresMySqlFact]
    public async Task 状態異常の並び順が保たれる()
    {
        // 並び順は /status と /cstatus の表示順そのもの。入れ替わると見た目が変わる。
        var id = NewSnowflake();
        var player = _defaults.NewPlayer(id);
        player.Effects.Add(new ActiveEffect("猛毒", 3, 9));
        player.Effects.Add(new ActiveEffect("麻痺", 1, 2));
        player.Effects.Add(new ActiveEffect("再生", 2, 25));

        await Players.SaveAsync(player);
        var loaded = await Players.FindAsync(id);

        Assert.NotNull(loaded);
        Assert.Equal(["猛毒", "麻痺", "再生"], loaded.Effects.Select(e => e.Name));
        Assert.Equal([3, 1, 2], loaded.Effects.Select(e => e.Level));
        Assert.Equal([9, 2, 25], loaded.Effects.Select(e => e.TurnsLeft));
    }

    [RequiresMySqlFact]
    public async Task アイテム欄は常に既定の並びで揃う()
    {
        // 旧 newdata の i は 11 スロットを必ず持つ。表示の走査順もこの順。
        var id = NewSnowflake();
        await Players.SaveAsync(_defaults.NewPlayer(id));

        var loaded = await Players.FindAsync(id);

        Assert.NotNull(loaded);
        Assert.Equal(ItemSlots.Order, loaded.Items.Keys);
        Assert.All(loaded.Items.Values, v => Assert.Equal(0, v));
    }

    [RequiresMySqlFact]
    public async Task snowflakeが丸まらない()
    {
        // 旧 SQLite + JS Number では 2^53 を超えると精度が落ち、
        // それを誤魔化すために ctrl.nearest という回避策が入っていた。
        const ulong id = 18_446_744_073_709_551_000UL;
        await Players.SaveAsync(_defaults.NewPlayer(id));

        var loaded = await Players.FindAsync(id);

        Assert.NotNull(loaded);
        Assert.Equal(id, loaded.UserId);
    }

    [RequiresMySqlFact]
    public async Task 非戦闘中はnullとして往復する()
    {
        // 旧仕様の n:"0" (非戦闘中) は NULL。
        var id = NewSnowflake();
        var player = _defaults.NewPlayer(id);
        player.BattleChannelId = NewSnowflake();
        await Players.SaveAsync(player);

        var inBattle = await Players.FindAsync(id);
        Assert.NotNull(inBattle?.BattleChannelId);

        inBattle.BattleChannelId = null;
        await Players.SaveAsync(inBattle);

        var released = await Players.FindAsync(id);
        Assert.NotNull(released);
        Assert.Null(released.BattleChannelId);
    }

    [RequiresMySqlFact]
    public async Task ペットを逃がすと消える()
    {
        var id = NewSnowflake();
        var player = _defaults.NewPlayer(id);
        player.Pet = _defaults.NewPet();
        player.Pet.Name = "たろう";
        await Players.SaveAsync(player);

        Assert.NotNull((await Players.FindAsync(id))!.Pet);

        // 旧 player.p[0] = undefined。
        player.Pet = null;
        await Players.SaveAsync(player);

        Assert.Null((await Players.FindAsync(id))!.Pet);
    }

    [RequiresMySqlFact]
    public async Task 複数人をまとめて読むと引数の順序が保たれる()
    {
        var a = NewSnowflake();
        var b = NewSnowflake();
        var missing = NewSnowflake();

        await Players.SaveManyAsync([_defaults.NewPlayer(a), _defaults.NewPlayer(b)]);

        var loaded = await Players.FindManyAsync([b, missing, a]);

        // 存在しないユーザーは含まれない (旧 forEach の if(!ud) return と同じ)。
        Assert.Equal([b, a], loaded.Select(p => p.UserId));
    }

    [RequiresMySqlFact]
    public async Task 経験値の順位を求められる()
    {
        // 他のテストが同じ DB を共有するので、相対関係だけを確かめる。
        var low = _defaults.NewPlayer(NewSnowflake());
        var high = _defaults.NewPlayer(NewSnowflake());
        low.Experience = 10;
        high.Experience = long.MaxValue / 2;

        await Players.SaveManyAsync([low, high]);

        var highRank = await Players.GetRankAsync(high.UserId);
        var lowRank = await Players.GetRankAsync(low.UserId);

        Assert.NotNull(highRank);
        Assert.NotNull(lowRank);
        Assert.True(highRank < lowRank, $"経験値が多い方が上位のはず (high={highRank}, low={lowRank})");
        Assert.Null(await Players.GetRankAsync(NewSnowflake()));
    }

    [RequiresMySqlFact]
    public async Task 戦場を丸ごと往復できる()
    {
        var channelId = NewSnowflake();
        var battle = _defaults.NewBattle(channelId);

        battle.Field = "永遠悪夢";
        battle.Difficulty = "LUNATIC";
        battle.EnemyCode = "nightmare";
        battle.Level = 9_999;
        battle.MaxHp = 4_444_444;
        battle.Hp = 1_234_567;
        battle.MaxMana = 7_777;
        battle.Mana = 7_777;
        battle.Turn = 13;
        battle.Effects.Add(new ActiveEffect("完全耐性", 1, 9_999));
        battle.Effects.Add(new ActiveEffect("魔法障壁", 1, 9_999));
        battle.Participants.Add(NewSnowflake());
        battle.Participants.Add(NewSnowflake());
        battle.ChocolateFeeders.Add(battle.Participants[0]);

        await Battles.SaveAsync(battle);
        var loaded = await Battles.FindAsync(channelId);

        Assert.NotNull(loaded);
        Assert.Equal("永遠悪夢", loaded.Field);
        Assert.Equal("LUNATIC", loaded.Difficulty);
        Assert.Equal(9_999, loaded.Level);
        Assert.Equal(1_234_567, loaded.Hp);
        Assert.Equal(13, loaded.Turn);
        Assert.Equal(channelId, loaded.ChecksumChannelId);
        Assert.Equal(["完全耐性", "魔法障壁"], loaded.Effects.Select(e => e.Name));
        Assert.Equal(2, loaded.Participants.Count);
        Assert.Equal([battle.Participants[0]], loaded.ChocolateFeeders);
    }

    [RequiresMySqlFact]
    public async Task 敵が入れ替わると付随情報が消える()
    {
        // 旧実装は turn を含まない新しいオブジェクトを書き込むことで
        // ターン数・状態異常・参加者・チョコを暗黙にリセットしていた。
        var channelId = NewSnowflake();
        var battle = _defaults.NewBattle(channelId);
        battle.Turn = 7;
        battle.Effects.Add(new ActiveEffect("毒", 1, 3));
        battle.Participants.Add(NewSnowflake());
        battle.ChocolateFeeders.Add(NewSnowflake());
        await Battles.SaveAsync(battle);

        battle.ResetForNewEnemy();
        battle.EnemyCode = "seed";
        await Battles.SaveAsync(battle);

        var loaded = await Battles.FindAsync(channelId);

        Assert.NotNull(loaded);
        Assert.Equal(0, loaded.Turn);
        Assert.Empty(loaded.Effects);
        Assert.Empty(loaded.Participants);
        Assert.Empty(loaded.ChocolateFeeders);
        Assert.Equal("seed", loaded.EnemyCode);
    }

    [RequiresMySqlFact]
    public async Task 存在しなければ初期値で作られる()
    {
        // 旧 fn() の「データが無ければ ctrl.new してやり直す」に相当。
        var userId = NewSnowflake();
        var channelId = NewSnowflake();

        Assert.Null(await Players.FindAsync(userId));
        Assert.Null(await Battles.FindAsync(channelId));

        var player = await Players.GetOrCreateAsync(userId);
        var battle = await Battles.GetOrCreateAsync(channelId);

        // 旧 newdata("user") の初期値。
        Assert.Equal(100, player.Hp);
        Assert.Equal(100, player.MaxHp);
        Assert.Equal(1, player.Level);
        Assert.Equal(0, player.Gil);
        Assert.Null(player.BattleChannelId);
        Assert.Empty(player.Skills);

        // 旧 newdata("channel") の初期値。
        Assert.Equal("草原", battle.Field);
        Assert.Equal("NORMAL", battle.Difficulty);
        Assert.Equal(1, battle.Level);

        // 2 回目は作らず既存を返す。
        player.Gil = 1234;
        await Players.SaveAsync(player);
        Assert.Equal(1234, (await Players.GetOrCreateAsync(userId)).Gil);
    }

    [RequiresMySqlFact]
    public async Task BANの追加と解除ができる()
    {
        var repository = new BanRepository(_fixture.Connections);
        var id = NewSnowflake();

        Assert.False(await repository.IsBannedAsync(id));

        await repository.AddAsync(id);
        Assert.True(await repository.IsBannedAsync(id));
        Assert.Contains(id, await repository.ListAsync());

        // 二重登録しても壊れない。
        await repository.AddAsync(id);
        Assert.True(await repository.IsBannedAsync(id));

        await repository.RemoveAsync(id);
        Assert.False(await repository.IsBannedAsync(id));
    }

    [RequiresMySqlFact]
    public async Task 日本語のフィールド名と絵文字入りのペット名を保存できる()
    {
        // utf8mb4 でないと絵文字で落ちる。
        var id = NewSnowflake();
        var player = _defaults.NewPlayer(id);
        player.Pet = _defaults.NewPet();
        player.Pet.Name = "🐉りゅう🔥";
        player.Skills.Add("「::atk」");
        await Players.SaveAsync(player);

        var loaded = await Players.FindAsync(id);

        Assert.NotNull(loaded);
        Assert.Equal("🐉りゅう🔥", loaded.Pet!.Name);
        Assert.Contains("「::atk」", loaded.Skills);
    }
}
