using Discord;
using Discord.Interactions;
using Tendo.Data.Repositories;
using Tendo.Game.Master;

namespace Tendo.Bot.Commands;

/// <summary>
/// 旧 <c>egive [アイテム] [個数] [@メンション]</c>。
/// アイテムのほか「ギル」も渡せる。旧実装はサーバー内でのみ使えた。
/// </summary>
public sealed class GiveCommand : GameModuleBase
{
    /// <summary>旧実装ではアイテム ID の代わりに文字列 "ギル" を指定していた。</summary>
    private const string GilKeyword = "ギル";

    private readonly IPlayerRepository _players;
    private readonly MasterData _data;

    public GiveCommand(IPlayerRepository players, MasterData data)
    {
        _players = players;
        _data = data;
    }

    [SlashCommand("give", "誰かにアイテムかギルを渡します。")]
    public async Task GiveAsync(
        [Summary("アイテム", "渡すアイテム、または「ギル」")]
        [Autocomplete(typeof(GiveTargetAutocompleteHandler))]
        string item,
        [Summary("個数", "渡す数")] [MinValue(1)] int quantity,
        [Summary("相手", "渡す相手")] IUser target)
    {
        // 旧 if( !Message.guild )return;
        if (Context.Guild is null)
        {
            await RespondAsync("このコマンドはサーバー内でのみ使えます。", ephemeral: true);
            return;
        }

        await DeferAsync();

        if (target.Id == Context.User.Id)
        {
            await FollowupAsync(embed: Simple(
                $"{Context.User.Mention}さん、自分自身にアイテムを渡すことはできません。"));
            return;
        }

        var isGil = item == GilKeyword;
        var itemId = isGil ? null : ItemAutocompleteHandler.Resolve(_data, item);

        if (!isGil && itemId is null)
        {
            await FollowupAsync($"「{item}」というアイテムはありません。", ephemeral: true);
            return;
        }

        var giver = await _players.GetOrCreateAsync(Context.User.Id);
        var displayName = isGil ? GilKeyword : _data.ItemNames.GetValueOrDefault(itemId!, itemId!);

        var held = isGil ? giver.Gil : giver.GetItem(itemId!);
        if (held < quantity)
        {
            await FollowupAsync(embed: Simple(
                $"{Context.User.Mention}さんは{displayName}を{quantity}個も持っていません！"));
            return;
        }

        var receiver = await _players.FindAsync(target.Id);
        if (receiver is null)
        {
            // 旧 if(!ud) return; — 相手が未登録なら何もしない。
            await FollowupAsync("相手のデータが見つかりませんでした。", ephemeral: true);
            return;
        }

        if (isGil)
        {
            giver.Gil -= quantity;
            receiver.Gil += quantity;
        }
        else
        {
            giver.AddItem(itemId!, -quantity);
            receiver.AddItem(itemId!, quantity);
        }

        await _players.SaveManyAsync([giver, receiver]);

        await FollowupAsync(embed: Simple(
            $"{Context.User.Mention}さんは{target.Mention}さんに{displayName}を{quantity}個渡しました。"));
    }
}

/// <summary>アイテム名に加えて「ギル」も候補に出す。</summary>
public sealed class GiveTargetAutocompleteHandler : AutocompleteHandler
{
    private readonly MasterData _data;

    public GiveTargetAutocompleteHandler(MasterData data) => _data = data;

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var typed = interaction.Data.Current.Value?.ToString() ?? string.Empty;

        var names = new[] { "ギル" }
            .Concat(_data.ItemNames.Where(kv => kv.Key != "exp").Select(kv => kv.Value));

        var matches = names
            .Where(n => n.Contains(typed, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(n => new AutocompleteResult(n, n));

        return Task.FromResult(AutocompletionResult.FromSuccess(matches));
    }
}
