using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Options;
using Tendo.Bot.Configuration;
using Tendo.Bot.Rendering;
using Tendo.Data.Repositories;
using Tendo.Game.Master;
using Tendo.Game.Rendering;

namespace Tendo.Bot.Commands;

/// <summary>旧 <c>epstatus</c> / <c>epst</c>。</summary>
public sealed class PetStatusCommand : GameModuleBase
{
    private readonly IPlayerRepository _players;
    private readonly MasterData _data;
    private readonly IGameClock _clock;
    private readonly DiscordOptions _options;

    public PetStatusCommand(
        IPlayerRepository players,
        MasterData data,
        IGameClock clock,
        IOptions<DiscordOptions> options)
    {
        _players = players;
        _data = data;
        _clock = clock;
        _options = options.Value;
    }

    [SlashCommand("pstatus", "ペットのステータスを確認します。")]
    public async Task PetStatusAsync()
    {
        await DeferAsync();

        var player = await _players.GetOrCreateAsync(Context.User.Id);

        if (player.Pet is not { } pet)
        {
            await FollowupAsync(embed: Simple($"{Context.User.Mention}さんはペットを飼っていません..."));
            return;
        }

        var species = _data.FindEnemy(pet.EnemyCode);
        var ability = _data.FindAbility(pet.Ability) ?? _data.DefaultAbility;

        if (species is null)
        {
            await FollowupAsync("ペットの種族データが見つかりませんでした。", ephemeral: true);
            return;
        }

        // 旧 oinfo = ab.find(a => a.name == xinfo.only) || ab[0]
        var speciesAbility = (species.OnlyAbility is null
            ? null
            : _data.FindAbility(species.OnlyAbility)) ?? _data.DefaultAbility;

        var description = Fence.Code($"[PET STATUS] {DisplayName}", Fence.Fix)
            + Fence.Code(
                new[]
                {
                    $"[名前] {pet.Name}",
                    $"[種族] {species.Name}",
                    $"[レベル] {pet.Level}",
                    $"[経験値] {pet.Experience}",
                    $"[属性] {species.Element}",
                    $"[個体能力] {pet.Ability} #{ability.Rarity}",
                    $" - {ability.Description}",
                    $"[固有能力] {speciesAbility.Name}",
                    $" - {speciesAbility.Description}",
                    $"[攻撃確率] {pet.AttackChance}%",
                },
                Fence.Css);

        var builder = new EmbedBuilder()
            .WithDescription(description)
            .WithFooter(_clock.Now());

        if (species.Picture is { Length: > 0 } picture)
        {
            builder.WithThumbnailUrl(_options.EnemyImageBaseUrl + picture);
        }

        await FollowupAsync(embed: builder.Build());
    }
}
