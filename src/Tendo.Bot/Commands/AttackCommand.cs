using Tendo.Bot.Services;
using Tendo.Game.Master;

namespace Tendo.Bot.Commands;

/// <summary>旧 <c>eattack</c> / <c>eatk</c>。</summary>
public sealed class AttackCommand : BattleCommandBase
{
    public AttackCommand(BattleService battle, MasterData data) : base(battle, data)
    {
    }

    [Discord.Interactions.SlashCommand("attack", "敵に攻撃します。")]
    public Task AttackAsync() => ActAsync(Data.FindSkill("攻撃")!, skipLearnCheck: false);
}
