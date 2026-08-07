using System.Text;
using Tendo.Game.Master;
using Tendo.Game.Rendering;
using Tendo.Game.State;

namespace Tendo.Game.Engine;

/// <summary>1 回の攻撃の入力。旧 <c>hitevent(options)</c> の引数。</summary>
public sealed class HitOptions
{
    /// <summary>旧 <c>pre</c>。<c>"+ "</c> (味方側) か <c>"- "</c> (敵側)。diff の色分けに出る。</summary>
    public required string Prefix { get; init; }

    /// <summary>旧 <c>ainfo.name</c>。</summary>
    public required string AttackerName { get; init; }

    /// <summary>旧 <c>dinfo.name</c>。</summary>
    public required string DefenderName { get; init; }

    /// <summary>旧 <c>dinfo.zokusei</c>。属性相性の判定に使う (防御側の属性)。</summary>
    public required string DefenderElement { get; init; }

    public required ICombatant Attacker { get; init; }

    public required ICombatant Defender { get; init; }

    public required SkillDef Skill { get; init; }

    /// <summary>属性相性より前の素のダメージ。</summary>
    public required long Damage { get; init; }
}

/// <summary>
/// 旧 <c>hitevent</c> の移植。1 回の攻撃を解決してログを積む。
///
/// 処理順が結果に効くので、旧実装の順序を崩さないこと。
///   属性相性 → ギル消費 → 攻撃側の状態異常補正 (と行動不能判定)
///   → 防御側の状態異常補正 → 魔力判定 → ダメージ適用 → 効果付与 → 強制移動
/// </summary>
public sealed class HitEvent
{
    private readonly MasterData _data;
    private readonly IRandomSource _random;

    public HitEvent(MasterData data, IRandomSource random)
    {
        _data = data;
        _random = random;
    }

    public void Execute(HitOptions options, StringBuilder log, TurnContext context)
    {
        var skill = options.Skill;
        var attacker = options.Attacker;
        var defender = options.Defender;
        var pre = options.Prefix;
        var damage = options.Damage;
        string? noMove = null;

        // --- 属性相性 -------------------------------------------------------
        // 旧: zinfo は「防御側の属性」の相性表。high なら 1.333 倍、low なら 0.666 倍。
        var affinity = _data.FindAffinity(options.DefenderElement);
        if (affinity is not null)
        {
            if (affinity.WeakTo.Contains(skill.Element, StringComparer.Ordinal))
            {
                damage = JsMath.RoundToLong(damage * 1.333);
            }
            else if (affinity.ResistantTo.Contains(skill.Element, StringComparer.Ordinal))
            {
                damage = JsMath.RoundToLong(damage * 0.666);
            }
        }

        // --- ギル消費技 (「銭投げ」) ----------------------------------------
        if (skill.GilCost is { } gilCost)
        {
            var required = JsMath.RoundToLong(gilCost * JsMath.Round(attacker.Level / 10.0));

            // 旧 reqg <= attack.g。敵とペットは g が undefined なので必ず false。
            if (attacker.Gil is { } held && required <= held)
            {
                attacker.Gil = held - required;
                log.Append(Fence.Code($"{pre}{options.AttackerName}は{required}ギルを投げつけた！"));
                damage = JsMath.RoundToLong(damage * 0.01 * required);
            }
            else
            {
                log.Append(Fence.Code($"{pre}{options.AttackerName}は{required}ギルを持っていなかった！"));
                damage = 0;
            }
        }

        // --- 攻撃側の状態異常 -----------------------------------------------
        // 「天邪鬼」が付いていると倍率が乗算ではなく除算になる (効果が反転する)。
        var attackerReversed = attacker.Effects.Any(e => e.Name == Amanojaku);

        foreach (var active in attacker.Effects)
        {
            var definition = _data.FindEffect(active.Name);
            if (definition is null)
            {
                continue;
            }

            // ここは choice ではなく直接添字。定義数を超えるレベルなら効果なしで抜ける。
            // (joutai 側は choice なのでランダムに落ちる。両者の違いは意図的。)
            var level = LevelAt(definition, active.Level);
            if (level is null)
            {
                continue;
            }

            damage = attackerReversed
                ? JsMath.RoundToLong(damage / level.AttackMultiplier)
                : JsMath.RoundToLong(damage * level.AttackMultiplier);

            if (JsMath.Random(_random, 1, 100) <= level.NoMovePercent && noMove is null)
            {
                noMove = definition.Name;
            }
        }

        // --- 防御側の状態異常 -----------------------------------------------
        foreach (var active in defender.Effects)
        {
            var definition = _data.FindEffect(active.Name);
            if (definition is null)
            {
                continue;
            }

            var level = LevelAt(definition, active.Level);
            if (level is null)
            {
                continue;
            }

            damage = JsMath.RoundToLong(damage / level.DefenseDivisor);

            if (definition.NoDamageElements.Contains(skill.Element, StringComparer.Ordinal))
            {
                damage = 0;
            }

            if (definition.BlocksPhysical && skill.Type == SkillType.Physical)
            {
                damage = 0;
            }

            if (definition.BlocksMagic && skill.Type == SkillType.Magic)
            {
                damage = 0;
            }
        }

        if (noMove is not null)
        {
            log.Append(Fence.Code($"{pre}{options.AttackerName}は{noMove}で動けない！"));
            return;
        }

        // --- 魔力判定 -------------------------------------------------------
        // 旧: if( mp1 <= attack.mp || !skill.mp )
        var manaCost = skill.ManaCost;
        if (manaCost > attacker.Mana && manaCost != 0)
        {
            log.Append(Fence.Code(
                $"{pre}{options.AttackerName}の{skill.Name}！魔力が足りない！", Fence.Diff));
            return;
        }

        // --- ダメージ適用 ---------------------------------------------------
        defender.Hp -= damage;
        if (defender.Hp < 0)
        {
            defender.Hp = 0;
        }

        if (damage != 0)
        {
            log.Append(Fence.Code(
                $"{pre}{options.AttackerName}の{skill.Name}！{options.DefenderName}に{damage}ダメージ！",
                Fence.Diff));
        }
        else if (skill.Type != SkillType.Special)
        {
            log.Append(Fence.Code(
                $"{pre}{options.AttackerName}の{skill.Name}！{options.DefenderName}には効いていない！",
                Fence.Diff));
        }
        else
        {
            log.Append(Fence.Code($"{pre}{options.AttackerName}の{skill.Name}！", Fence.Diff));
        }

        // 旧: if( attack.mp ) attack.mp -= mp1;
        // 魔力が 0 のときは引かない (0 のまま)。消費 0 の技でも同じ結果になる。
        if (attacker.Mana != 0)
        {
            attacker.Mana -= manaCost;
        }

        // ダメージが 0 でも変化技 (type=2) なら効果は乗る。
        var noEffect = damage == 0 && skill.Type != SkillType.Special;
        if (noEffect)
        {
            return;
        }

        ApplyClears(options, log, defender);
        ApplySelfEffects(options, log, attacker);
        ApplyDefenderEffects(options, log, defender);
        ApplyForcedMove(options, log, context);
    }

    /// <summary>「天邪鬼」。効果の正負が反転する。</summary>
    private const string Amanojaku = "天邪鬼";

    /// <summary>
    /// 旧 <c>teff.effects[e[1]-1]</c>。範囲外なら undefined = null。
    /// <see cref="StatusTick"/> 側の <c>choice</c> とは違い、ここはランダムに落ちない。
    /// </summary>
    private static EffectLevel? LevelAt(EffectDef definition, int level)
    {
        var index = level - 1;
        return index >= 0 && index < definition.Levels.Count ? definition.Levels[index] : null;
    }

    /// <summary>旧 <c>_pair.forEach</c>。相手の状態異常を解除する。</summary>
    private void ApplyClears(HitOptions options, StringBuilder log, ICombatant defender)
    {
        foreach (var clear in options.Skill.Clears)
        {
            var hit = JsMath.Random(_random, 1, 100) <= clear.Percent;
            var target = defender.Effects.FirstOrDefault(e => e.Name == clear.Name);

            if (!hit || target is null)
            {
                continue;
            }

            // 相手の状態異常レベルが指定レベル以下なら解除できる。
            if (target.Level <= clear.Level)
            {
                defender.Effects.Remove(target);
                log.Append(Fence.Code(
                    $"{options.Prefix}{options.DefenderName}の{clear.Name}状態が解除された！", Fence.Css));
            }
            else
            {
                log.Append(Fence.Code(
                    $"{options.Prefix}{options.DefenderName}の{clear.Name}状態を解除できない！", Fence.Css));
            }
        }
    }

    /// <summary>旧 <c>_self.forEach</c>。自分に状態異常を付ける。</summary>
    private void ApplySelfEffects(HitOptions options, StringBuilder log, ICombatant attacker)
    {
        foreach (var application in options.Skill.SelfEffects)
        {
            if (JsMath.Random(_random, 1, 100) > application.Percent)
            {
                continue;
            }

            var protector = EffectRules.FindProtector(_data, attacker.Effects, application.Name);
            if (protector is not null)
            {
                log.Append(Fence.Code(
                    $"{options.Prefix}{options.AttackerName}は{protector}で{application.Name}状態にならない！",
                    Fence.Css));
            }
            else
            {
                attacker.Effects.Add(ActiveEffect.From(application));
                log.Append(Fence.Code(
                    $"{options.Prefix}{options.AttackerName}は{application.Name}状態になった！", Fence.Css));
            }
        }
    }

    /// <summary>
    /// 旧 <c>_effect.forEach</c>。相手に状態異常を付ける。
    /// self 側と違い、こちらのメッセージには言語指定が無い (既定の "C" になる)。
    /// </summary>
    private void ApplyDefenderEffects(HitOptions options, StringBuilder log, ICombatant defender)
    {
        foreach (var application in options.Skill.Effects)
        {
            if (JsMath.Random(_random, 1, 100) > application.Percent)
            {
                continue;
            }

            var protector = EffectRules.FindProtector(_data, defender.Effects, application.Name);
            if (protector is not null)
            {
                log.Append(Fence.Code(
                    $"{options.Prefix}{options.DefenderName}は{protector}で{application.Name}状態にならない！"));
            }
            else
            {
                defender.Effects.Add(ActiveEffect.From(application));
                log.Append(Fence.Code(
                    $"{options.Prefix}{options.DefenderName}は{application.Name}状態になった！"));
            }
        }
    }

    /// <summary>
    /// 旧 <c>if( skill.move &amp;&amp; enemy.f!=skill.move )</c>。
    ///
    /// 「吸い込み」で相手を別フィールドへ引きずり込む。敵 (魔鏡) もこの技を使うので、
    /// プレイヤーが地獄へ送られる経路でもある (tips「鏡に吸い込まれると地獄へ送られる」)。
    /// 移動先が現在地と同じなら何も起きない。
    /// </summary>
    private static void ApplyForcedMove(HitOptions options, StringBuilder log, TurnContext context)
    {
        var move = options.Skill.MoveField;
        if (string.IsNullOrEmpty(move) || context.Battle.Field == move)
        {
            return;
        }

        if (TurnContext.UnpullableEnemies.Contains(context.Battle.EnemyCode, StringComparer.Ordinal))
        {
            log.Append(Fence.Code($"{options.Prefix}{options.DefenderName}は吸いこめない！"));
            return;
        }

        context.Moved = true;
        context.Battle.Field = move;
        log.Append(Fence.Code($"{options.Prefix}{options.DefenderName}の体が{move}に吸い込まれる！"));
    }
}
