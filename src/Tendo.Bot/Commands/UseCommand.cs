using Discord;
using Discord.Interactions;
using Tendo.Bot.Services;
using Tendo.Data.Repositories;
using Tendo.Game.Engine;
using Tendo.Game.Master;
using Tendo.Game.State;

namespace Tendo.Bot.Commands;

/// <summary>
/// 旧 <c>euse [アイテム] [@メンション]</c>。
///
/// 旧実装は短縮 ID (<c>p</c>) と正式名 (<c>ポーション</c>) の両方を受け付けていた。
/// 選択肢方式にしたので、どちらの入力も表示名から選べる。
/// </summary>
public sealed class UseCommand : GameModuleBase
{
    private readonly IPlayerRepository _players;
    private readonly BattleService _battle;
    private readonly IBattleRepository _battles;
    private readonly MasterData _data;

    public UseCommand(
        IPlayerRepository players,
        BattleService battle,
        IBattleRepository battles,
        MasterData data)
    {
        _players = players;
        _battle = battle;
        _battles = battles;
        _data = data;
    }

    [SlashCommand("use", "アイテムを使用します。")]
    public async Task UseAsync(
        [Summary("アイテム", "使うアイテム")]
        [Autocomplete(typeof(ItemAutocompleteHandler))]
        string item,
        [Summary("対象", "使う相手 (省略すると自分)")] IUser? target = null)
    {
        await DeferAsync();

        var itemId = ItemAutocompleteHandler.Resolve(_data, item);
        if (itemId is null)
        {
            await FollowupAsync($"「{item}」というアイテムはありません。", ephemeral: true);
            return;
        }

        var itemName = _data.ItemNames.GetValueOrDefault(itemId, itemId);
        var user = await _players.GetOrCreateAsync(Context.User.Id);

        if (user.IsDown)
        {
            await FollowupAsync(embed: Simple($"{Context.User.Mention}さんは既にやられています..."));
            return;
        }

        if (user.GetItem(itemId) <= 0)
        {
            await FollowupAsync(embed: Simple($"{Context.User.Mention}さんは{itemName}を持っていません。"));
            return;
        }

        var targetUser = target ?? Context.User;
        var isSelf = targetUser.Id == Context.User.Id;
        var targetState = isSelf ? user : await _players.FindAsync(targetUser.Id);

        if (targetState is null)
        {
            // 旧 if(!ud) return; — 相手のデータが無ければ何もしない。
            await FollowupAsync("相手のデータが見つかりませんでした。", ephemeral: true);
            return;
        }

        var battle = await _battle.EnsureBattleAsync(Context.Channel.Id);
        var enemy = _data.FindEnemy(battle.EnemyCode);

        user.AddItem(itemId, -1);

        var messages = new List<string> { $"{Context.User.Mention}は`{itemName}`を使った！" };

        long healHp = 0;
        long healMana = 0;
        var effects = new List<ActiveEffect>();
        var reviveAllowed = false;
        var isChocolate = false;

        switch (itemId)
        {
            case "p":
                healHp = JsMath.RoundToLong(targetState.MaxHp * (1.0 / 5.0));
                break;
            case "t":
                healMana = JsMath.RoundToLong(targetState.MaxMana * (1.0 / 4.0));
                break;
            case "e":
                // エリクサーだけは倒れている相手にも使える。
                reviveAllowed = true;
                healHp = targetState.MaxHp;
                break;
            case "c":
                isChocolate = true;
                break;
            case "i":
                if (targetState.Effects.Any(e => e.Name == "透明化"))
                {
                    // 旧実装はここで return しており、アイテムは減ったまま何も起きない。
                    await _players.SaveAsync(user);
                    return;
                }

                messages.Add($"{targetUser.Mention}は透明化状態になった！");
                effects.Add(new ActiveEffect("透明化", 1, 8));
                break;
            case "a":
                messages.Add($"{targetUser.Mention}はダメージ上昇状態を得た！");
                effects.Add(new ActiveEffect("ダメージ上昇", 3, 8));
                break;
            case "d":
                messages.Add($"{targetUser.Mention}はダメージ軽減状態を得た！");
                effects.Add(new ActiveEffect("ダメージ軽減", 3, 8));
                break;

            // 旧実装には "r" (リフレク) の分岐が無く、消費されるだけで何も起きない。
            // 見た目の動作を変えないため、こちらでも何もしない。
        }

        if (targetState.IsDown && !reviveAllowed)
        {
            await FollowupAsync(embed: Simple($"{targetUser.Mention}さんは既にやられています..."));
            return;
        }

        if (isSelf)
        {
            if (isChocolate && enemy is not null)
            {
                messages.Add($"{enemy.Name}はチョコレートを食べている...");
                if (!battle.ChocolateFeeders.Contains(Context.User.Id))
                {
                    battle.ChocolateFeeders.Add(Context.User.Id);
                }
            }
        }
        else if (isChocolate)
        {
            // 他人に使うと再生が付く (旧実装の分岐)。
            messages.Add($"{targetUser.Mention}は再生状態を得た！");
            targetState.Effects.Add(new ActiveEffect("再生", 2, 25));
        }

        if (healHp != 0)
        {
            messages.Add($"{targetUser.Mention}の体力が`{healHp}`回復した！");
            targetState.Hp += healHp;
        }

        if (healMana != 0)
        {
            messages.Add($"{targetUser.Mention}の魔力が`{healMana}`回復した！");
            targetState.Mana += healMana;
        }

        targetState.Effects.AddRange(effects);

        // 回復系は最大値で頭打ちになる (状態異常の継続回復と違い、こちらは上限がある)。
        targetState.Hp = Math.Min(targetState.Hp, targetState.MaxHp);
        targetState.Mana = Math.Min(targetState.Mana, targetState.MaxMana);

        await _players.SaveAsync(user);
        if (!isSelf)
        {
            await _players.SaveAsync(targetState);
        }

        await _battles.SaveAsync(battle);

        await FollowupAsync(embed: Simple(string.Join("\n", messages)));
    }
}

/// <summary>アイテム名の補完。表示名で選び、内部では短縮 ID に戻す。</summary>
public sealed class ItemAutocompleteHandler : AutocompleteHandler
{
    private readonly MasterData _data;

    public ItemAutocompleteHandler(MasterData data) => _data = data;

    /// <summary>
    /// 旧実装と同じく、短縮 ID と表示名のどちらでも解決できるようにする。
    /// </summary>
    public static string? Resolve(MasterData data, string input)
    {
        if (data.ItemNames.ContainsKey(input))
        {
            return input;
        }

        return data.ItemNames.FirstOrDefault(kv => kv.Value == input).Key;
    }

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var typed = interaction.Data.Current.Value?.ToString() ?? string.Empty;

        // 「経験値」はショップ専用でインベントリには入らないので候補から外す。
        var matches = _data.ItemNames
            .Where(kv => kv.Key != "exp")
            .Where(kv => kv.Value.Contains(typed, StringComparison.OrdinalIgnoreCase)
                         || kv.Key.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(kv => new AutocompleteResult(kv.Value, kv.Value));

        return Task.FromResult(AutocompletionResult.FromSuccess(matches));
    }
}
