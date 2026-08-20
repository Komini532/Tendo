using Tendo.Game.Master;
using Tendo.Game.Rendering;
using Tendo.Game.State;

namespace Tendo.Game.Engine;

/// <summary>新しい敵が出現したときの表示内容。</summary>
public sealed record EncounterAnnouncement
{
    public required EnemyDef Enemy { get; init; }

    /// <summary>embed の description。</summary>
    public required string Description { get; init; }

    /// <summary>敵画像のファイル名。ベース URL は Discord 層で連結する。</summary>
    public string? Picture { get; init; }
}

/// <summary>
/// 旧 <c>nxevt</c> (撃破後の次の敵) と <c>case "rs"</c> (戦場のリセット) の移植。
/// </summary>
public sealed class EncounterSpawner
{
    private readonly MasterData _data;
    private readonly IRandomSource _random;

    public EncounterSpawner(MasterData data, IRandomSource random)
    {
        _data = data;
        _random = random;
    }

    /// <summary>
    /// 旧 <c>nxevt</c> の抽選部分。現在のフィールドに出る敵からレア度に応じて 1 体選ぶ。
    ///
    /// 旧実装は 4 つの乱数を「判定の前に全て」引いていた。順序も含めて再現しないと
    /// 以降の乱数列がずれるので、条件分岐より先にまとめて引く。
    /// <code>
    /// let random  = r.random(1,100);    // レア1 (3%)
    /// let random2 = r.random(1,777);    // レア2 (1/777)
    /// let random3 = r.random(1,100);    // レア3 (7%)
    /// let random4 = r.random(1,2048);   // レア4 (1/2048)
    /// </code>
    /// 判定順は レア2 → レア4 → レア3 → レア1 → レア0。
    /// </summary>
    public EnemyDef? ChooseForField(string field)
    {
        var candidates = _data.Enemies
            .Where(e => e.Fields.Contains(field, StringComparer.Ordinal))
            .ToList();

        var roll1 = JsMath.Random(_random, 1, 100);
        var roll2 = JsMath.Random(_random, 1, 777);
        var roll3 = JsMath.Random(_random, 1, 100);
        var roll4 = JsMath.Random(_random, 1, 2048);

        if (roll2 == 777 && PickRarity(candidates, 2) is { } superRare)
        {
            return superRare;
        }

        if (roll4 == 2048 && PickRarity(candidates, 4) is { } ultraRare)
        {
            return ultraRare;
        }

        if (roll3 <= 7 && PickRarity(candidates, 3) is { } veryRare)
        {
            return veryRare;
        }

        if (roll1 <= 3 && PickRarity(candidates, 1) is { } rare)
        {
            return rare;
        }

        return PickRarity(candidates, 0);
    }

    /// <summary>
    /// 旧 <c>nxevt</c>。敵を倒した後、レベルを 1 上げて次の敵を出す。
    ///
    /// フィールドの <see cref="FieldDef.LevelCap"/> に達したチャンネルではレベルが伸びない。
    /// 敵Lvが頭打ちになると経験値も敵HPも頭打ちになり、かつ 1 撃破は最短 1 ターンなので
    /// 「経験値/ターン ≦ 上限 × フィールド経験値倍率」という天井が生まれる。
    /// 下位フィールドが構造的に頭打ちになるのはこの仕組みによる。
    /// </summary>
    public EncounterAnnouncement? SpawnNext(BattleState battle)
    {
        var next = ChooseForField(battle.Field);
        if (next is null)
        {
            return null;
        }

        var cap = _data.FindField(battle.Field)?.LevelCap ?? int.MaxValue;
        var level = battle.Level < cap ? battle.Level + 1 : battle.Level;

        return Spawn(battle, next, level);
    }

    /// <summary>
    /// 旧 <c>case "rs"</c> の敵決定部分。
    ///
    /// レア度 0 の敵と戦っている最中に <c>/reset</c> しても敵は入れ替わらず、
    /// 体力と状態異常だけが仕切り直される。レア度が 0 以外なら逃げてしまい、
    /// 同じレベルのレア度 0 の敵に置き換わる (tips「ereset をすると
    /// レア度 2 以上の敵は逃げてしまいます」)。
    /// </summary>
    /// <param name="summon">
    /// 旧 <c>act.summon</c>。指定があればレア度に関係なくその敵を呼ぶ (<c>/mod sum</c>)。
    /// </param>
    /// <param name="absolute">
    /// 旧 <c>act.absolute</c>。難易度変更・フィールド移動・変身など、
    /// 内部から強制的に敵を入れ替えたいときに true。
    /// </param>
    public EncounterAnnouncement? Reset(
        BattleState battle,
        EnemyDef current,
        string? summon = null,
        bool absolute = false)
    {
        var selected = current;

        if (current.Rarity != 0 || absolute)
        {
            var summoned = summon is null ? null : _data.FindEnemyByNameOrCode(summon);

            selected = summoned
                       ?? JsMath.Choice(
                           _random,
                           _data.Enemies
                               .Where(e => e.Fields.Contains(battle.Field, StringComparer.Ordinal))
                               .Where(e => e.Rarity == 0)
                               .ToList());

            if (selected is null)
            {
                // そのフィールドにレア度 0 の敵がいない場合。旧実装は undefined を
                // 読んで落ちていたので、ここでは今の敵を維持する。
                selected = current;
            }
        }

        // レベルは据え置き。体力は満タン、状態異常は出現時の初期状態に戻る。
        return Spawn(battle, selected, battle.Level);
    }

    /// <summary>
    /// 戦場の状態を新しい敵で上書きし、出現メッセージを組み立てる。
    /// ターン数・状態異常・参加者・チョコがここで初期化される。
    /// </summary>
    public EncounterAnnouncement Spawn(BattleState battle, EnemyDef enemy, int level)
    {
        var difficulty = _data.FindDifficulty(battle.Difficulty);
        var field = _data.FindField(battle.Field);
        var effective = _data.EffectiveLevel(battle.Field, level);

        var hp = JsMath.RoundToLong(
            ((effective * enemy.HpMultiplier * 10) + _data.Fix.Enemy)
            * (field?.HpMultiplier ?? 1)
            * (difficulty?.HpMultiplier ?? 1));

        battle.ResetForNewEnemy();
        battle.EnemyCode = enemy.Code;
        battle.Level = level;
        battle.MaxHp = hp;
        battle.Hp = hp;
        battle.MaxMana = enemy.MaxMana;
        battle.Mana = enemy.MaxMana;
        battle.ChecksumChannelId = battle.ChannelId;

        foreach (var initial in enemy.InitialEffects)
        {
            battle.Effects.Add(ActiveEffect.From(initial));
        }

        return new EncounterAnnouncement
        {
            Enemy = enemy,
            Description = Fence.Code($"{enemy.Name}が現れた！", RarityFence(enemy.Rarity))
                          + Fence.Code($"[レベル] {effective}\n[体力] {hp}", Fence.Css),
            Picture = enemy.Picture,
        };
    }

    /// <summary>
    /// 旧 <c>["C", "fix", "fix", "fix"][ninfo.rare]</c>。
    ///
    /// 要素が 4 つしかないので、レア度 4 は範囲外の <c>undefined</c> になり
    /// <c>code()</c> の既定 (<c>"C"</c>) に落ちる。つまり最高レアの敵だけ
    /// 通常敵と同じ見た目で出てくる。旧挙動なので直さない。
    /// </summary>
    private static string RarityFence(int rarity)
    {
        string[] byRarity = [Fence.Default, Fence.Fix, Fence.Fix, Fence.Fix];
        return rarity >= 0 && rarity < byRarity.Length ? byRarity[rarity] : Fence.Default;
    }

    private EnemyDef? PickRarity(IReadOnlyList<EnemyDef> candidates, int rarity)
    {
        var matching = candidates.Where(e => e.Rarity == rarity).ToList();

        // 旧 selector.find(e => e.rare==N) で存在確認してから filter().choice()。
        return matching.Count == 0 ? null : JsMath.Choice(_random, matching);
    }
}
