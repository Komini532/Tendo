using Discord;
using Discord.Interactions;
using Tendo.Game.Master;

namespace Tendo.Bot.Commands;

/// <summary>
/// <c>/mod</c> 用の補完。旧実装は名前を直接打ち込ませていたので、
/// 同じ入力ができるようにしたうえで候補を出す。
/// </summary>
public abstract class MasterDataAutocompleteHandler : AutocompleteHandler
{
    /// <summary>Discord の候補上限。</summary>
    private const int MaxSuggestions = 25;

    protected MasterDataAutocompleteHandler(MasterData data) => Data = data;

    protected MasterData Data { get; }

    protected abstract IEnumerable<string> Candidates { get; }

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var typed = interaction.Data.Current.Value?.ToString() ?? string.Empty;

        var matches = Candidates
            .Where(c => c.Contains(typed, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSuggestions)
            .Select(c => new AutocompleteResult(c, c));

        return Task.FromResult(AutocompletionResult.FromSuccess(matches));
    }
}

/// <summary>状態異常名の補完。</summary>
public sealed class EffectAutocompleteHandler : MasterDataAutocompleteHandler
{
    public EffectAutocompleteHandler(MasterData data) : base(data)
    {
    }

    protected override IEnumerable<string> Candidates => Data.Effects.Select(e => e.Name);
}

/// <summary>フィールド名の補完。</summary>
public sealed class FieldAutocompleteHandler : MasterDataAutocompleteHandler
{
    public FieldAutocompleteHandler(MasterData data) : base(data)
    {
    }

    protected override IEnumerable<string> Candidates => Data.Fields.Select(f => f.Name);
}

/// <summary>敵名の補完。旧実装は名前とコードのどちらでも指定できた。</summary>
public sealed class EnemyAutocompleteHandler : MasterDataAutocompleteHandler
{
    public EnemyAutocompleteHandler(MasterData data) : base(data)
    {
    }

    protected override IEnumerable<string> Candidates => Data.Enemies.Select(e => e.Name);
}
