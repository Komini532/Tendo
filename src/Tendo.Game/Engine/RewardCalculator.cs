using Tendo.Game.Master;
using Tendo.Game.Rendering;
using Tendo.Game.State;

namespace Tendo.Game.Engine;

/// <summary>撃破報酬の結果。</summary>
public sealed record RewardResult
{
    /// <summary>embed に出す本文 (報酬 + レベルアップ等の通知)。</summary>
    public required string Description { get; init; }

    public required long Experience { get; init; }

    public required long Gil { get; init; }

    /// <summary>抽選に当たったドロップ。参加者全員が同じものを受け取る。</summary>
    public required IReadOnlyList<ItemDrop> Drops { get; init; }
}

/// <summary>
/// 旧 <c>result(d, dat)</c> の移植。敵を倒したときの経験値・ギル・ドロップと、
/// レベルアップ・技習得・ペットの成長を処理する。
/// </summary>
public sealed class RewardCalculator
{
    /// <summary>
    /// 旧 <c>resetable = ["即死", "死の宣告"]</c>。
    /// 戦闘終了時に解除される状態異常はこの 2 つだけで、毒などは持ち越す。
    /// </summary>
    private static readonly string[] ClearedOnBattleEnd = ["即死", "死の宣告"];

    private readonly MasterData _data;
    private readonly IRandomSource _random;

    public RewardCalculator(MasterData data, IRandomSource random)
    {
        _data = data;
        _random = random;
    }

    /// <summary>
    /// 参加者に報酬を配る。<paramref name="participants"/> は呼び出し側で
    /// 直接書き換わるので、そのまま保存すればよい。
    /// </summary>
    /// <param name="resolveName">
    /// ユーザー ID から表示名を引く。旧実装は <c>m.displayName || m.username</c> を
    /// 直接読んでおり、サーバーを抜けた参加者がいると例外で報酬処理ごと落ちていた。
    /// 呼び出し側で名前が引けなければ代替文字列を返すこと。
    /// </param>
    public RewardResult Apply(
        BattleState battle,
        EnemyDef enemy,
        IReadOnlyList<PlayerState> participants,
        Func<ulong, string> resolveName)
    {
        var field = _data.FindField(battle.Field);
        var difficulty = _data.FindDifficulty(battle.Difficulty);

        // 敵Lvはフィールドの上限でクランプした有効敵Lvを使う。上限に達したフィールドでは
        // 経験値もギルも頭打ちになり、上位フィールドへ移る以外に伸ばす手が無くなる。
        var level = _data.EffectiveLevel(battle.Field, battle.Level);

        var experience = JsMath.RoundToLong(
            enemy.ExpMultiplier
            * level
            * (field?.ExpMultiplier ?? 1)
            * (difficulty?.ExpMultiplier ?? 1));

        // ギルはレベルの 1/3 (切り捨てではなく JS の丸め) で決まる。
        var gil = JsMath.RoundToLong(enemy.GilMultiplier * JsMath.Round(level / 3.0));

        var lines = new List<string>
        {
            Fence.Code("< RESULT >", Fence.Html),
            Fence.Code($"` {experience}の経験値を獲得！", Fence.Js),
            Fence.Code($"` {gil}のギルを獲得！", Fence.Js),
        };

        // ドロップ抽選は 1 回だけ行い、その結果を参加者全員に配る。
        var drops = new List<ItemDrop>();
        foreach (var drop in enemy.Drops)
        {
            if (JsMath.Random(_random, 1, 100) <= drop.Percent)
            {
                drops.Add(drop);
                var itemName = _data.ItemNames.GetValueOrDefault(drop.ItemId, drop.ItemId);
                lines.Add(Fence.Code($"` {itemName}を手に入れた！", Fence.Js));
            }
        }

        var updates = new List<string>();

        foreach (var player in participants)
        {
            var name = resolveName(player.UserId);

            foreach (var drop in drops)
            {
                player.AddItem(drop.ItemId, drop.Count);
            }

            player.Gil += gil;
            player.Experience += experience;

            // 旧 while(p.xp >= (p.lv+1)**2)。経験値は減らず、累計で閾値を越えるたびに上がる。
            var levelsGained = 0;
            while (player.Experience >= (long)(player.Level + 1) * (player.Level + 1))
            {
                player.Level += 1;
                levelsGained += 1;
                player.MaxHp = (player.Level * 10) + _data.Fix.Player;
                player.MaxMana = JsMath.RoundToLong(player.Level * 2.22);
            }

            // 技の習得通知はレベルアップ通知より先に出る (旧実装の順序)。
            foreach (var learnable in _data.LearnableSkills)
            {
                if (learnable.LearnLevel <= player.Level
                    && !player.Skills.Contains(learnable.Name, StringComparer.Ordinal))
                {
                    player.Skills.Add(learnable.Name);
                    updates.Add(Fence.Code($"+ {name}は{learnable.Name}を習得した！", Fence.Diff));
                }
            }

            if (levelsGained >= 1)
            {
                updates.Add(Fence.Code($"+ {name}が{player.Level}にレベルアップした！", Fence.Diff));
            }

            if (player.Pet is { } pet)
            {
                var petLevels = 0;
                pet.Experience += experience;
                while (pet.Experience >= (long)(pet.Level + 1) * (pet.Level + 1))
                {
                    pet.Level += 1;
                    petLevels += 1;
                }

                if (petLevels >= 1)
                {
                    updates.Add(Fence.Code($"+ {pet.Name}が{pet.Level}にレベルアップした！", Fence.Diff));
                }
            }

            // 勝利後は全快し、戦闘状態が解ける。
            player.Hp = player.MaxHp;
            player.BattleChannelId = null;
            player.Effects.RemoveAll(e => ClearedOnBattleEnd.Contains(e.Name, StringComparer.Ordinal));
        }

        return new RewardResult
        {
            Description = Fence.Join(lines.Concat(updates)),
            Experience = experience,
            Gil = gil,
            Drops = drops,
        };
    }

    /// <summary>
    /// 旧 <c>case "rs"</c> の参加者リセット部分。
    /// 戦場を仕切り直すとき、参加者は全快して戦闘状態が解ける。
    /// </summary>
    public static void ReleaseParticipants(IEnumerable<PlayerState> participants)
    {
        foreach (var player in participants)
        {
            player.BattleChannelId = null;
            player.Hp = player.MaxHp;
            player.Effects.RemoveAll(e => ClearedOnBattleEnd.Contains(e.Name, StringComparer.Ordinal));
        }
    }
}
