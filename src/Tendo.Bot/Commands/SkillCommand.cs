using Discord;
using Discord.Interactions;
using Tendo.Bot.Services;
using Tendo.Game.Master;

namespace Tendo.Bot.Commands;

/// <summary>旧 <c>eskill [技名]</c> / <c>esk</c>。</summary>
public sealed class SkillCommand : BattleCommandBase
{
    public SkillCommand(BattleService battle, MasterData data) : base(battle, data)
    {
    }

    [SlashCommand("skill", "技を発動します。")]
    public async Task SkillAsync(
        [Summary("技名", "発動する技")]
        [Autocomplete(typeof(SkillAutocompleteHandler))]
        string name)
    {
        var skill = Data.FindSkill(name);
        if (skill is null)
        {
            // 旧実装は存在しない技名を黙って無視していた。
            await RespondAsync($"「{name}」という技はありません。", ephemeral: true);
            return;
        }

        await ActAsync(skill, skipLearnCheck: false);
    }
}

/// <summary>
/// 技名の補完。旧実装は技名をそのまま打ち込ませていたので、
/// 同じ入力ができるよう全 90 技から部分一致で候補を出す。
/// </summary>
public sealed class SkillAutocompleteHandler : AutocompleteHandler
{
    private readonly MasterData _data;

    public SkillAutocompleteHandler(MasterData data) => _data = data;

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var typed = interaction.Data.Current.Value?.ToString() ?? string.Empty;

        var matches = _data.Skills
            .Where(s => s.Name.Contains(typed, StringComparison.OrdinalIgnoreCase))
            // Discord の候補上限は 25 件。
            .Take(25)
            .Select(s => new AutocompleteResult(s.Name, s.Name));

        return Task.FromResult(AutocompletionResult.FromSuccess(matches));
    }
}
