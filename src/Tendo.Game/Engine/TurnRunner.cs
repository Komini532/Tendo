using System.Text;
using Tendo.Game.Master;
using Tendo.Game.Rendering;
using Tendo.Game.State;

namespace Tendo.Game.Engine;

/// <summary>
/// 旧 <c>fn(d, act)</c> の <c>case "sk" / "isk"</c> の移植。1 ターン分の戦闘を進める。
///
/// 旧実装の流れ:
///   習得判定 → 他チャンネル戦闘中判定 → 戦闘不能判定
///   → ターン数加算 → 参加者登録 → 1 ターン目なら難易度の状態異常付与
///   → spdevent  (敏捷順に殴り合う。プレイヤー側はペット攻撃も続く)
///   → spdevent2 (敏捷順に状態異常の継続効果)
///   → 決着判定
/// </summary>
public sealed class TurnRunner
{
    private readonly MasterData _data;
    private readonly IRandomSource _random;
    private readonly HitEvent _hit;
    private readonly StatusTick _statusTick;

    public TurnRunner(MasterData data, IRandomSource random)
    {
        _data = data;
        _random = random;
        _hit = new HitEvent(data, random);
        _statusTick = new StatusTick(data, random);
    }

    /// <param name="skipLearnCheck">
    /// 旧 <c>do:"isk"</c>。<c>case "sk"</c> は習得判定を通ってから
    /// <c>case "isk"</c> へ fall through していた。<c>/wait</c> と
    /// <c>/mod isk</c> は判定を飛ばして直接 isk に入る。
    /// </param>
    public TurnResult Execute(
        TurnContext context,
        SkillDef skill,
        bool skipLearnCheck = false)
    {
        var battle = context.Battle;
        var player = context.Player;

        // --- 行動できるかの判定 ---------------------------------------------
        // 旧 case "sk": 「攻撃」だけは未習得でも常に使える。
        if (!skipLearnCheck
            && skill.Name != "攻撃"
            && !player.Skills.Contains(skill.Name, StringComparer.Ordinal))
        {
            return new TurnResult
            {
                Outcome = TurnOutcome.SkillNotLearned,
                RejectionMessage = $"さんはまだ「{skill.Name}」を習得していません。",
            };
        }

        if (player.BattleChannelId is { } fighting && fighting != battle.ChannelId)
        {
            // 旧実装はチャンネルが実在するときだけ拒否し、
            // 消えたチャンネルを指していた場合は HP を全快させて続行していた。
            return new TurnResult
            {
                Outcome = TurnOutcome.BusyElsewhere,
                RejectionMessage = $"さんは「<#{fighting}>」で戦闘中です。",
            };
        }

        if (player.Hp <= 0)
        {
            return new TurnResult
            {
                Outcome = TurnOutcome.AlreadyDown,
                RejectionMessage = "さんは既にやられています...",
            };
        }

        // --- ターン開始 ------------------------------------------------------
        battle.Turn++;
        player.BattleChannelId = battle.ChannelId;

        if (!battle.Participants.Contains(player.UserId))
        {
            battle.Participants.Add(player.UserId);
        }

        var log = new StringBuilder();

        // 1 ターン目だけ、難易度ごとの状態異常を敵に付与する試行を行う。
        if (battle.Turn == 1)
        {
            foreach (var application in context.Difficulty.Effects)
            {
                if (JsMath.Random(_random, 1, 100) <= application.Percent)
                {
                    battle.Effects.Add(ActiveEffect.From(application));
                }
            }
        }

        var attacker = new PlayerCombatant(player);
        var defender = new EnemyCombatant(battle);

        // --- 敏捷順に行動 ----------------------------------------------------
        // 旧: spd = player.spd*player.lv - einfo.spd*enemy.lv; jun = spd>=0 (true=先攻)
        var speed = (player.Speed * player.Level) - (context.Enemy.SpeedMultiplier * battle.Level);
        var playerFirst = speed >= 0;

        RunExchange(context, skill, attacker, defender, playerFirst, log);
        RunStatusTicks(context, attacker, defender, playerFirst, log);

        // --- 決着 -------------------------------------------------------------
        if (defender.Hp <= 0)
        {
            log.Append(context.Enemy.Next is { } next
                ? Fence.Code($"- {next.Message}", Fence.Diff)
                : Fence.Code($"` {context.Enemy.Name}を倒した！", Fence.Js));
        }
        else if (attacker.Hp <= 0)
        {
            log.Append(Fence.Code($"{context.PlayerName}は倒れた…", Fence.BrainFuck));
        }

        var outcome = ResolveOutcome(context, attacker, defender);

        return new TurnResult
        {
            Outcome = outcome,
            Description = log.ToString(),
            Turn = battle.Turn,
            PlayerStatus = FormatStatus(player.Hp, player.MaxHp, player.Mana, player.MaxMana, player.Effects),
            EnemyStatus = FormatStatus(battle.Hp, battle.MaxHp, battle.Mana, battle.MaxMana, battle.Effects),
            TransformInto = outcome == TurnOutcome.EnemyTransforms ? context.Enemy.Next!.Code : null,
        };
    }

    /// <summary>
    /// 旧 <c>spdevent</c>。速い方から 1 回ずつ行動する。
    ///
    /// 旧実装は相互再帰で 2 回呼ばれる形だったが、途中で <c>return</c> すると
    /// 後攻の呼び出し自体が行われない。つまり先攻が相手を倒したら後攻は動かない。
    /// この「打ち切り」を保つため、順に回して都度生死を確かめる。
    /// </summary>
    private void RunExchange(
        TurnContext context,
        SkillDef skill,
        PlayerCombatant player,
        EnemyCombatant enemy,
        bool playerFirst,
        StringBuilder log)
    {
        var order = playerFirst ? new[] { true, false } : [false, true];

        foreach (var isPlayerTurn in order)
        {
            if (isPlayerTurn)
            {
                // 旧 if( player.hp<=0 ) return; — 後続の行動も起きない。
                if (player.Hp <= 0)
                {
                    return;
                }

                PlayerAttack(context, skill, player, enemy, log);
            }
            else
            {
                if (enemy.Hp <= 0)
                {
                    return;
                }

                EnemyAttack(context, player, enemy, log);
            }
        }
    }

    private void PlayerAttack(
        TurnContext context,
        SkillDef skill,
        PlayerCombatant player,
        EnemyCombatant enemy,
        StringBuilder log)
    {
        var damage = JsMath.RoundToLong(
            skill.Attack * ((context.Player.Level * 10) + _data.Fix.Player) / 3.0
            / 30.0
            * (JsMath.Random(_random, 85, 100) / 100.0));

        _hit.Execute(
            new HitOptions
            {
                Prefix = "+ ",
                AttackerName = context.PlayerName,
                DefenderName = context.Enemy.Name,
                DefenderElement = context.Enemy.Element,
                Attacker = player,
                Defender = enemy,
                Skill = skill,
                Damage = damage,
            },
            log,
            context);

        PetAttack(context, enemy, log);
    }

    /// <summary>
    /// 旧 <c>spdevent</c> 内のペット攻撃。プレイヤーの行動に続けて発生する。
    ///
    /// ペットは敵時代の技をそのまま使う。アビリティの <c>repeat</c> 回だけ攻撃し、
    /// ダメージにアビリティ倍率と、元になった敵の固有アビリティ倍率が乗る。
    /// </summary>
    private void PetAttack(TurnContext context, EnemyCombatant enemy, StringBuilder log)
    {
        var pet = context.Player.Pet;
        if (pet is null)
        {
            return;
        }

        var ability = _data.FindAbility(pet.Ability);

        // 旧 if( r.random(1,100) <= pinfo.p && pab ) — 乱数は毎ターン必ず引かれる。
        var triggered = JsMath.Random(_random, 1, 100) <= pet.AttackChance;
        if (!triggered || ability is null)
        {
            return;
        }

        var petData = new PetCombatant(pet.Level);

        for (var i = 0; i < ability.Repeat; i++)
        {
            var species = _data.FindEnemy(pet.EnemyCode);
            if (species is null)
            {
                continue;
            }

            var skillName = EffectRules.SelectEnemySkill(_random, species.Skills);
            var skill = skillName is null ? null : _data.FindSkill(skillName);
            if (skill is null)
            {
                continue;
            }

            // ペットのダメージ基礎値は敵ではなくプレイヤー側の定数 (fix.player) を使う。
            var damage = JsMath.RoundToLong(
                skill.Attack * ((pet.Level * 10) + _data.Fix.Player) / 3.0
                / 30.0
                * (JsMath.Random(_random, 85, 100) / 100.0));

            damage = JsMath.RoundToLong(damage * ability.AttackMultiplier);

            // 元になった敵の固有アビリティ (only) があれば更に倍率が乗る。
            var speciesAbility = species.OnlyAbility is null
                ? null
                : _data.FindAbility(species.OnlyAbility);
            if (speciesAbility is not null)
            {
                damage = JsMath.RoundToLong(damage * speciesAbility.AttackMultiplier);
            }

            _hit.Execute(
                new HitOptions
                {
                    Prefix = "+ ",
                    AttackerName = pet.Name,
                    DefenderName = context.Enemy.Name,
                    DefenderElement = context.Enemy.Element,
                    Attacker = petData,
                    Defender = enemy,
                    Skill = skill,
                    Damage = damage,
                },
                log,
                context);
        }
    }

    private void EnemyAttack(
        TurnContext context,
        PlayerCombatant player,
        EnemyCombatant enemy,
        StringBuilder log)
    {
        var fallback = _data.FindSkill("攻撃")!;

        for (var i = 0; i < Math.Max(context.Enemy.Repeat, 1); i++)
        {
            var name = EffectRules.SelectEnemySkill(_random, context.Enemy.Skills);
            var skill = (name is null ? null : _data.FindSkill(name)) ?? fallback;

            var damage = JsMath.RoundToLong(
                skill.Attack * ((context.Battle.Level * 10) + _data.Fix.Enemy) / 3.0
                * context.Enemy.AttackMultiplier
                * context.Difficulty.AttackMultiplier
                / 30.0
                * (JsMath.Random(_random, 85, 100) / 100.0));

            _hit.Execute(
                new HitOptions
                {
                    Prefix = "- ",
                    AttackerName = context.Enemy.Name,
                    DefenderName = context.PlayerName,
                    DefenderElement = TurnContext.PlayerElement,
                    Attacker = enemy,
                    Defender = player,
                    Skill = skill,
                    Damage = damage,
                },
                log,
                context);
        }
    }

    /// <summary>旧 <c>spdevent2</c>。状態異常の継続効果も敏捷順で、同じ打ち切り方をする。</summary>
    private void RunStatusTicks(
        TurnContext context,
        PlayerCombatant player,
        EnemyCombatant enemy,
        bool playerFirst,
        StringBuilder log)
    {
        var order = playerFirst ? new[] { true, false } : [false, true];

        foreach (var isPlayerTurn in order)
        {
            if (isPlayerTurn)
            {
                if (player.Hp <= 0)
                {
                    return;
                }

                _statusTick.Execute(player, context.PlayerName, log);
            }
            else
            {
                if (enemy.Hp <= 0)
                {
                    return;
                }

                _statusTick.Execute(enemy, context.Enemy.Name, log);
            }
        }
    }

    private static TurnOutcome ResolveOutcome(
        TurnContext context,
        PlayerCombatant player,
        EnemyCombatant enemy)
    {
        if (enemy.Hp <= 0)
        {
            return context.Enemy.Next is not null
                ? TurnOutcome.EnemyTransforms
                : TurnOutcome.EnemyDefeated;
        }

        // 旧 if( player.hp && moved ) — 倒れていたら移動は起きない。
        if (player.Hp != 0 && context.Moved)
        {
            return TurnOutcome.PulledToAnotherField;
        }

        return TurnOutcome.Continue;
    }

    /// <summary>旧 embed のフィールド値。体力・魔力のブロックに状態異常のブロックを続ける。</summary>
    private static string FormatStatus(
        long hp,
        long maxHp,
        long mana,
        long maxMana,
        IReadOnlyList<ActiveEffect> effects)
    {
        var vitals = Fence.Code($"[体力] {hp}/{maxHp}\n[魔力] {mana}/{maxMana}", Fence.Css);

        var status = effects.Count > 0
            ? Fence.Code(string.Join("\n", effects.Select(e => e.ToString())), Fence.Css)
            : Fence.Code("状態異常なし", Fence.BrainFuck);

        return vitals + status;
    }
}
