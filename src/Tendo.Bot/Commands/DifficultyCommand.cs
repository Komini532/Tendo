using Discord;
using Discord.Interactions;
using Tendo.Bot.Services;
using Tendo.Game.Master;
using Tendo.Game.Rendering;

namespace Tendo.Bot.Commands;

/// <summary>
/// 旧 <c>edchange</c> / <c>edc</c>。難易度変更。
/// 解放条件は敵レベルで決まる。未開放の難易度も一覧には出す。
/// </summary>
public sealed class DifficultyCommand : GameModuleBase
{
    private readonly BattleService _battle;
    private readonly MasterData _data;

    public DifficultyCommand(BattleService battle, MasterData data)
    {
        _battle = battle;
        _data = data;
    }

    [SlashCommand("dchange", "フィールドの難易度を変更します。")]
    public async Task DifficultyAsync()
    {
        await DeferAsync();

        var battle = await _battle.EnsureBattleAsync(Context.Channel.Id);
        var enemy = _data.FindEnemy(battle.EnemyCode);

        if (enemy?.NoEscape is { } noEscape)
        {
            await FollowupAsync(embed: Simple(Fence.Code($"- {noEscape.Message}", Fence.Diff)));
            return;
        }

        var lines = new List<string>();
        var available = new List<string>();

        foreach (var difficulty in _data.Difficulties)
        {
            if (battle.Level >= difficulty.RequiredEnemyLevel)
            {
                available.Add(difficulty.Name);
                lines.Add(Fence.Code(
                    $"[{difficulty.Name}] 必要Lv.{difficulty.RequiredEnemyLevel}\n" +
                    $"{difficulty.Description}\n[変更可能]",
                    Fence.Css));
            }
            else
            {
                lines.Add(Fence.Code(
                    $"[{difficulty.Name}] 必要Lv.{difficulty.RequiredEnemyLevel}\n" +
                    $"{difficulty.Description}\n[未開放]",
                    Fence.BrainFuck));
            }
        }

        if (available.Count == 0)
        {
            // 旧実装は選べるものが無いと何も出さなかったが、一覧だけは見せる。
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("難易度変更")
                .WithDescription(Fence.Join(lines))
                .Build());
            return;
        }

        var menu = new SelectMenuBuilder()
            .WithCustomId($"dchange:{Context.User.Id}")
            .WithPlaceholder("難易度を選んでください")
            .WithMinValues(1)
            .WithMaxValues(1);

        foreach (var name in available)
        {
            menu.AddOption(name, name);
        }

        await FollowupAsync(
            embed: new EmbedBuilder()
                .WithTitle("難易度変更")
                .WithDescription(
                    Fence.Code("難易度を選んでください", Fence.Css) + Fence.Join(lines))
                .Build(),
            components: new ComponentBuilder().WithSelectMenu(menu).Build());
    }
}

/// <summary>難易度 Select Menu の受け口。</summary>
public sealed class DifficultyComponents : InteractionModuleBase<SocketInteractionContext>
{
    private readonly BattleService _battle;

    public DifficultyComponents(BattleService battle) => _battle = battle;

    [ComponentInteraction("dchange:*", ignoreGroupNames: true)]
    public async Task SelectAsync(string userId, string[] selected)
    {
        if (!ulong.TryParse(userId, out var owner) || owner != Context.User.Id)
        {
            await RespondAsync("この操作は実行した人だけが使えます。", ephemeral: true);
            return;
        }

        var difficulty = selected.FirstOrDefault();
        if (difficulty is null)
        {
            await DeferAsync();
            return;
        }

        await DeferAsync();
        await ModifyOriginalResponseAsync(m => m.Components = new ComponentBuilder().Build());

        var battle = await _battle.EnsureBattleAsync(Context.Channel.Id);
        battle.Difficulty = difficulty;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithDescription($"難易度を「{difficulty}」に変更しました！")
            .Build());

        // 難易度で敵の体力倍率が変わるので、旧実装同様その場で出し直す。
        foreach (var embed in await _battle.ResetAsync(Context.Channel.Id, absolute: true))
        {
            await FollowupAsync(embed: embed);
        }
    }
}
