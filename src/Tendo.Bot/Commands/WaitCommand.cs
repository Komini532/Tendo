using Tendo.Bot.Services;
using Tendo.Game.Master;

namespace Tendo.Bot.Commands;

/// <summary>
/// 旧 <c>ewait</c> / <c>ewt</c>。
/// 旧実装は <c>do:"isk"</c> で呼ぶため習得判定を通らない。
/// </summary>
public sealed class WaitCommand : BattleCommandBase
{
    public WaitCommand(BattleService battle, MasterData data) : base(battle, data)
    {
    }

    [Discord.Interactions.SlashCommand("wait", "「何もしない」をします。")]
    public Task WaitAsync() => ActAsync(Data.FindSkill("何もしない")!, skipLearnCheck: true);
}
