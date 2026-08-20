using Discord;
using Discord.Interactions;
using Tendo.Bot.Rendering;
using Tendo.Bot.Services;
using Tendo.Game.Engine;
using Tendo.Game.Master;
using Tendo.Game.Rendering;

namespace Tendo.Bot.Commands;

/// <summary>旧 <c>ecstatus</c> / <c>ecst</c>。戦場の状況を表示する。</summary>
public sealed class ChannelStatusCommand : GameModuleBase
{
    private readonly BattleService _battle;
    private readonly MasterData _data;
    private readonly IGameClock _clock;

    public ChannelStatusCommand(BattleService battle, MasterData data, IGameClock clock)
    {
        _battle = battle;
        _data = data;
        _clock = clock;
    }

    [SlashCommand("cstatus", "戦場のステータスを表示します。")]
    public async Task ChannelStatusAsync()
    {
        await DeferAsync();

        var battle = await _battle.EnsureBattleAsync(Context.Channel.Id);
        var enemy = _data.FindEnemy(battle.EnemyCode);

        if (enemy is null)
        {
            await FollowupAsync("この戦場には敵がいないようです。", ephemeral: true);
            return;
        }

        var title = Fence.Code(
            $"[CHANNEL STATUS] {Context.Channel.Name ?? DisplayName}", Fence.Fix);

        // 敵の強さは生の battle.Level ではなくフィールドの上限でクランプした有効敵Lvで決まる。
        // 上限に達していることが見えないと「倒しても強くならない」のが理不尽に映るので、
        // 上限に届いているフィールドでは「Lv (上限)」の形で明示する。
        var field = _data.FindField(battle.Field) ?? _data.DefaultField;
        var level = _data.EffectiveLevel(battle.Field, battle.Level);
        var levelText = battle.Level >= field.LevelCap
            ? $"{level} (このフィールドの上限)"
            : $"{level}";

        var stats = Fence.Code(
            new[]
            {
                $"[フィールド] {battle.Field}",
                $"[難易度] {battle.Difficulty}",
                $"[名前] {enemy.Name}",
                $"[レベル] {levelText}",
                $"[体力] {battle.Hp}/{battle.MaxHp}",
                $"[魔力] {battle.Mana}/{battle.MaxMana}",
                $"[攻撃力] {JsMath.RoundToLong(((level * 10) + _data.Fix.Enemy) * enemy.AttackMultiplier * field.AttackMultiplier)}",
                $"[敏捷力] {enemy.SpeedMultiplier * level}",
                $"[属性] {enemy.Element}",
            },
            Fence.Css);

        // 旧実装は状態異常がある場合、未宣言のグローバル変数へ代入していたため
        // 一覧がどこにも出なかった (ea.js:1820)。/status と同じ形で表示する。
        var effects = battle.Effects.Count > 0
            ? Fence.Code(string.Join("\n", battle.Effects.Select(e => e.ToString())), Fence.Css)
            : Fence.Code("状態異常なし", Fence.BrainFuck);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithDescription(title + stats + effects)
            .WithFooter(_clock.Now())
            .Build());
    }
}
