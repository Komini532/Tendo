using Discord;
using Discord.Interactions;
using Tendo.Bot.Services;
using Tendo.Data.Repositories;
using Tendo.Game.Master;
using Tendo.Game.Rendering;

namespace Tendo.Bot.Commands;

/// <summary>
/// 旧 <c>ego</c>。フィールド移動。
///
/// 旧実装は移動先を一覧表示したうえで、フィールド名を発言させて選ばせていた
/// (<c>ctrl.collector</c>)。ここでは Select Menu に置き換える。
/// 条件を満たさないフィールドも一覧には出す (どこへ行けるようになるか分かるように)。
/// </summary>
public sealed class GoCommand : GameModuleBase
{
    private readonly BattleService _battle;
    private readonly IPlayerRepository _players;
    private readonly MasterData _data;

    public GoCommand(BattleService battle, IPlayerRepository players, MasterData data)
    {
        _battle = battle;
        _players = players;
        _data = data;
    }

    [SlashCommand("go", "フィールドを移動します。")]
    public async Task GoAsync()
    {
        await DeferAsync();

        var battle = await _battle.EnsureBattleAsync(Context.Channel.Id);
        var player = await _players.GetOrCreateAsync(Context.User.Id);
        var enemy = _data.FindEnemy(battle.EnemyCode);

        // 逃走を許さない敵と戦っている間は移動できない。
        if (enemy?.NoEscape is { } noEscape)
        {
            await FollowupAsync(embed: Simple(Fence.Code($"- {noEscape.Message}", Fence.Diff)));
            return;
        }

        // 今いるフィールドから繋がっている移動先。
        var requirements = _data.FieldRequirements
            .Where(f => f.ConnectedFrom.Contains(battle.Field, StringComparer.Ordinal))
            .ToList();

        var lines = new List<string>();
        var available = new List<string>();

        foreach (var requirement in requirements)
        {
            var ok = requirement.RequiredEnemyLevel <= battle.Level
                     && requirement.RequiredPlayerLevel <= player.Level;

            if (ok)
            {
                available.Add(requirement.Name);
                lines.Add(Fence.Code(
                    $"[{requirement.Name}] 敵Lv.{requirement.RequiredEnemyLevel} " +
                    $"解放者Lv.{requirement.RequiredPlayerLevel}\n条件が揃っています。",
                    Fence.Css));
            }
            else
            {
                // 旧実装は条件未達のフィールド名を伏せる。
                lines.Add(Fence.Code(
                    $"[？？] 敵Lv.{requirement.RequiredEnemyLevel} " +
                    $"解放者Lv.{requirement.RequiredPlayerLevel}\n条件が揃っていません。",
                    Fence.Css));
            }
        }

        if (available.Count == 0)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("移動")
                .WithDescription(
                    Fence.Code("現在、移動できるフィールドはありません。", Fence.BrainFuck)
                    + Fence.Join(lines))
                .Build());
            return;
        }

        var menu = new SelectMenuBuilder()
            .WithCustomId($"go:{Context.User.Id}")
            .WithPlaceholder("移動先を選んでください")
            .WithMinValues(1)
            .WithMaxValues(1);

        foreach (var name in available.Take(25))
        {
            menu.AddOption(name, name);
        }

        await FollowupAsync(
            embed: new EmbedBuilder()
                .WithTitle("移動")
                .WithDescription(
                    Fence.Code("移動先を選んでください", Fence.Css) + Fence.Join(lines))
                .Build(),
            components: new ComponentBuilder().WithSelectMenu(menu).Build());
    }
}

/// <summary>移動先 Select Menu の受け口。</summary>
public sealed class GoComponents : InteractionModuleBase<SocketInteractionContext>
{
    private readonly BattleService _battle;

    public GoComponents(BattleService battle) => _battle = battle;

    [ComponentInteraction("go:*", ignoreGroupNames: true)]
    public async Task SelectAsync(string userId, string[] selected)
    {
        if (!ulong.TryParse(userId, out var owner) || owner != Context.User.Id)
        {
            await RespondAsync("この操作は実行した人だけが使えます。", ephemeral: true);
            return;
        }

        var field = selected.FirstOrDefault();
        if (field is null)
        {
            await DeferAsync();
            return;
        }

        await DeferAsync();

        // 選択後はメニューを畳む。
        await ModifyOriginalResponseAsync(m => m.Components = new ComponentBuilder().Build());

        var battle = await _battle.EnsureBattleAsync(Context.Channel.Id);
        battle.Field = field;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithDescription($"「{field}」に移動しました！")
            .Build());

        // 旧実装は移動後に fn(d, {do:"rs", absolute:true}) で敵を出し直す。
        foreach (var embed in await _battle.ResetAsync(Context.Channel.Id, absolute: true))
        {
            await FollowupAsync(embed: embed);
        }
    }
}
