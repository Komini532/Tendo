using Discord;
using Discord.Interactions;
using Tendo.Bot.Components;
using Tendo.Data.Repositories;
using Tendo.Game.Master;
using Tendo.Game.Rendering;

namespace Tendo.Bot.Commands;

/// <summary>
/// 旧 <c>eskills</c> / <c>esl</c>。習得済みの技を一覧する。
///
/// 未習得の技は名前が伏せられ、文字数ぶんの「？」になる
/// (tips「eskills の？の数は、技の文字数です」)。
/// </summary>
public sealed class SkillsCommand : GameModuleBase
{
    /// <summary>旧実装は 1 ページ 5 件。</summary>
    private const int PerPage = 5;

    private readonly IPlayerRepository _players;
    private readonly MasterData _data;
    private readonly PaginationService _pagination;

    public SkillsCommand(IPlayerRepository players, MasterData data, PaginationService pagination)
    {
        _players = players;
        _data = data;
        _pagination = pagination;
    }

    [SlashCommand("skills", "習得した技一覧を表示します。")]
    public async Task SkillsAsync()
    {
        await DeferAsync();

        var player = await _players.GetOrCreateAsync(Context.User.Id);
        var blocks = new List<string>();

        // 一覧の並びは習得順ではなくマスターデータの順。
        foreach (var skill in _data.LearnableSkills)
        {
            var known = player.Skills.Contains(skill.Name, StringComparer.Ordinal);
            var kind = new[] { "物理", "魔法", "特殊" }[(int)skill.Type];

            if (!known)
            {
                blocks.Add(Fence.Code(
                    $"[{new string('？', skill.Name.Length)}] {skill.Element}属性 {kind} " +
                    $"威力？ 魔力？ 習得Lv.{skill.LearnLevel}",
                    Fence.BrainFuck));
                continue;
            }

            var selfs = skill.SelfEffects
                .Select(e => $"[自:{e.Name}] Lv.{e.Level} {e.Turns}ターン {e.Percent}%");
            var effects = skill.Effects
                .Select(e => $"[敵:{e.Name}] Lv.{e.Level} {e.Turns}ターン {e.Percent}%");
            var pairs = skill.Clears
                .Select(e => $"[解:{e.Name}] Lv.{e.Level} {e.Percent}%");

            var extra = string.Concat(
                Section(selfs), Section(effects), Section(pairs));

            blocks.Add(Fence.Code(
                $"[{skill.Name}] {skill.Element}属性 {kind} 威力{skill.Attack} " +
                $"魔力{skill.ManaCost} 習得Lv.{skill.LearnLevel}\n{skill.Description}" +
                (extra.Length > 0 ? "\n" : string.Empty) + extra,
                Fence.Css));
        }

        var pages = BuildPages(blocks, $"{DisplayName}の技一覧");
        var (page, components) = _pagination.Create(Context.User.Id, pages);

        await FollowupAsync(embed: page, components: components);
    }

    private static string Section(IEnumerable<string> lines)
    {
        var list = lines.ToList();
        return list.Count == 0 ? string.Empty : "\n" + string.Join("\n", list);
    }

    /// <summary>旧実装のページ組み立て。見出しに <c>[i / n]</c> が入る。</summary>
    internal static IReadOnlyList<Embed> BuildPages(
        IReadOnlyList<string> blocks,
        string heading,
        string? footer = null)
    {
        var total = Math.Max((int)Math.Ceiling(blocks.Count / (double)PerPage), 1);
        var pages = new List<Embed>(total);

        for (var i = 0; i < total; i++)
        {
            var builder = new EmbedBuilder().WithDescription(
                Fence.Code($"{heading} [{i + 1} / {total}]", Fence.Css)
                + Fence.Join(blocks.Skip(i * PerPage).Take(PerPage)));

            if (footer is not null)
            {
                builder.WithFooter(footer);
            }

            pages.Add(builder.Build());
        }

        return pages;
    }
}
