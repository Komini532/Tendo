using System.Text;
using Tendo.Game.Master;
using Tendo.Game.Rendering;

namespace Tendo.Game.Engine;

/// <summary>
/// 旧 <c>joutai(options)</c> の移植。ターン終了時の状態異常処理。
/// 継続ダメージ・継続回復を適用し、残りターンを 1 減らして切れたものを外す。
/// </summary>
public sealed class StatusTick
{
    private const string Amanojaku = "天邪鬼";

    private readonly MasterData _data;
    private readonly IRandomSource _random;

    public StatusTick(MasterData data, IRandomSource random)
    {
        _data = data;
        _random = random;
    }

    public void Execute(ICombatant combatant, string displayName, StringBuilder log)
    {
        // 「天邪鬼」が付いていると回復とダメージが入れ替わる。
        var reversed = combatant.Effects.Any(e => e.Name == Amanojaku);

        // 走査中に要素を落とすので、旧実装と同じく「印を付けてから一括で除去」する。
        var expired = new List<State.ActiveEffect>();

        foreach (var active in combatant.Effects)
        {
            var definition = _data.FindEffect(active.Name);
            if (definition is null)
            {
                continue;
            }

            // 旧 effect.effects.choice(e[1]-1)。
            // hitevent 側と違ってここは choice なので、定義数を超えるレベルの状態異常は
            // 毎ターンランダムなレベルの効果になる。旧挙動なのでそのまま再現する。
            var level = JsMath.Choice(_random, definition.Levels, active.Level - 1);

            if (level is not null && ShouldTrigger(definition, active))
            {
                var heal = JsMath.RoundToLong(combatant.MaxHp * level.Heal);
                var damage = JsMath.RoundToLong(combatant.MaxHp * level.Damage);

                // 旧 if( attack.hp>=1 )。既に倒れていると継続効果は動かない。
                if (combatant.Hp >= 1)
                {
                    ApplyTick(combatant, displayName, active.Name, heal, damage, reversed, log);
                }
            }

            active.TurnsLeft--;

            if (active.TurnsLeft == 0)
            {
                log.Append(Fence.Code(
                    $"{displayName}の「{active.Name}」状態が切れた！", Fence.Fix));
                expired.Add(active);
            }
        }

        foreach (var effect in expired)
        {
            combatant.Effects.Remove(effect);
        }
    }

    /// <summary>
    /// 旧 <c>if( !effect.end || (effect.end &amp;&amp; !(e[2]-1)) )</c>。
    ///
    /// 通常の状態異常は毎ターン効果が出る。<c>end</c> 付き (「死の宣告」「豊穣の宣告」) は
    /// 残り 1 ターンになった回だけ発動する時限式。
    /// </summary>
    private static bool ShouldTrigger(EffectDef definition, State.ActiveEffect active)
        => !definition.TriggersOnExpiry || active.TurnsLeft - 1 == 0;

    private static void ApplyTick(
        ICombatant combatant,
        string displayName,
        string effectName,
        long heal,
        long damage,
        bool reversed,
        StringBuilder log)
    {
        // 旧実装は上限を掛けていない。「再生」で最大値を超えたままになるのも仕様。
        if (reversed)
        {
            if (heal != 0)
            {
                combatant.Hp -= heal;
                log.Append(Fence.Code(
                    $"+ {displayName}は{effectName}で{heal}ダメージ受けた！", Fence.Diff));
            }

            if (damage != 0)
            {
                combatant.Hp += damage;
                log.Append(Fence.Code(
                    $"- {displayName}は{effectName}で体力を{damage}回復した！", Fence.Diff));
            }

            return;
        }

        if (heal != 0)
        {
            combatant.Hp += heal;
            log.Append(Fence.Code(
                $"+ {displayName}は{effectName}で体力を{heal}回復した！", Fence.Diff));
        }

        if (damage != 0)
        {
            combatant.Hp -= damage;
            log.Append(Fence.Code(
                $"- {displayName}は{effectName}で{damage}ダメージ受けた！", Fence.Diff));
        }
    }
}
