using Discord.Interactions;
using Tendo.Bot.Services;

namespace Tendo.Bot.Commands;

/// <summary>
/// 旧 <c>efix</c>。処理中フラグが詰まったときに手動で解放する。
///
/// 旧実装は例外で解放し損ねると、そのチャンネルが操作不能になったため
/// これが必要だった。現在は必ず解放されるが、利用者に見えるコマンドなので残す。
/// </summary>
public sealed class FixCommand : GameModuleBase
{
    private readonly BattleService _battle;

    public FixCommand(BattleService battle) => _battle = battle;

    [SlashCommand("fix", "起動してるのに攻撃できなくなったら試してください。")]
    public async Task FixAsync()
    {
        _battle.ForceRelease(Context.Channel.Id);
        await ReplyEmbedAsync("連続攻撃制限状態を解除しました。");
    }
}
