using Discord.Interactions;
using Tendo.Bot.Components;
using Tendo.Bot.Services;
using Tendo.Game.Master;
using Tendo.Game.Rendering;

namespace Tendo.Bot.Commands;

/// <summary>
/// 旧 <c>emlist</c> / <c>eml</c>。今いるフィールドに出る敵の一覧。
///
/// レア度 2 と 4 の敵は名前がスポイラーで隠される。
/// </summary>
public sealed class MonsterListCommand : GameModuleBase
{
    private readonly BattleService _battle;
    private readonly MasterData _data;
    private readonly PaginationService _pagination;

    public MonsterListCommand(BattleService battle, MasterData data, PaginationService pagination)
    {
        _battle = battle;
        _data = data;
        _pagination = pagination;
    }

    [SlashCommand("mlist", "フィールドに出る敵一覧を表示します。")]
    public async Task MonsterListAsync()
    {
        await DeferAsync();

        var battle = await _battle.EnsureBattleAsync(Context.Channel.Id);

        var enemies = _data.Enemies
            .Where(e => e.Fields.Contains(battle.Field, StringComparer.Ordinal))
            .ToList();

        if (enemies.Count == 0)
        {
            await FollowupAsync(embed: Simple("このフィールドには敵がいないようです。"));
            return;
        }

        var blocks = new List<string>();
        foreach (var enemy in enemies)
        {
            // 旧 ["★☆☆", "★★☆", "★★★", "★★☆", "★★★★"][rare]
            string[] stars = ["★☆☆", "★★☆", "★★★", "★★☆", "★★★★"];
            var rarity = enemy.Rarity >= 0 && enemy.Rarity < stars.Length
                ? stars[enemy.Rarity]
                : string.Empty;

            var text = Fence.Code($"[{enemy.Name}] {enemy.Element}属性 レア度:{rarity}", Fence.Css);

            // レア度 2 と 4 はスポイラーで伏せる。
            if (enemy.Rarity is 2 or 4)
            {
                text = $"||{text}||";
            }

            blocks.Add(text);
        }

        var pages = SkillsCommand.BuildPages(
            blocks,
            $"{battle.Field}の出現モンスター一覧",
            "レア度3以上のモンスターはスポイラーを開くと見ることが出来ます。");

        var (page, components) = _pagination.Create(Context.User.Id, pages);
        await FollowupAsync(embed: page, components: components);
    }
}
