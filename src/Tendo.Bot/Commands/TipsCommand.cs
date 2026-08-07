using Discord;
using Discord.Interactions;
using Tendo.Game.Engine;
using Tendo.Game.Master;

namespace Tendo.Bot.Commands;

/// <summary>
/// 旧 <c>etips [番号]</c>。
///
/// 旧実装は <c>ti.choice(parseInt(k[0]) - 1)</c> で引いていた。
/// <c>choice</c> は添字が範囲外ならランダムに落ちるので、
/// 番号を省略しても、範囲外の番号を入れてもランダムな豆知識が出る。
/// </summary>
public sealed class TipsCommand : GameModuleBase
{
    private readonly MasterData _data;
    private readonly IRandomSource _random = new SystemRandomSource();

    public TipsCommand(MasterData data) => _data = data;

    [SlashCommand("tips", "このBotに関する豆(ほどもない)知識です。")]
    public async Task TipsAsync([Summary("番号", "見たい豆知識の番号")] int? number = null)
    {
        // 番号は 1 始まり。範囲外・未指定はランダム。
        var tip = JsMath.Choice(_random, _data.Tips, number is { } n ? n - 1 : null);

        await RespondAsync(embed: new EmbedBuilder()
            .WithTitle("--- tips ---")
            .WithDescription($">>> {tip}")
            .Build());
    }
}
