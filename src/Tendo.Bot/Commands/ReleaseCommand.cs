using Discord.Interactions;
using Tendo.Bot.Components;
using Tendo.Data.Repositories;

namespace Tendo.Bot.Commands;

/// <summary>旧 <c>erelease</c> / <c>erel</c>。ペットを逃がす。</summary>
public sealed class ReleaseCommand : GameModuleBase
{
    private readonly IPlayerRepository _players;
    private readonly PromptService _prompts;

    public ReleaseCommand(IPlayerRepository players, PromptService prompts)
    {
        _players = players;
        _prompts = prompts;
    }

    [SlashCommand("release", "ペットを逃がします。")]
    public async Task ReleaseAsync()
    {
        await DeferAsync();

        var player = await _players.GetOrCreateAsync(Context.User.Id);

        if (player.Pet is not { } pet)
        {
            await FollowupAsync(embed: Simple($"{Context.User.Mention}さんはペットを飼っていません..."));
            return;
        }

        var name = pet.Name;
        var userId = Context.User.Id;
        var mention = Context.User.Mention;

        var components = _prompts.Create(
            userId,
            onConfirm: async () =>
            {
                var current = await _players.GetOrCreateAsync(userId);

                // 旧 player.p[0] = undefined
                current.Pet = null;
                await _players.SaveAsync(current);

                return $"{mention}は`{name}`を逃がしました...";
            },
            onCancel: () => Task.FromResult($"{mention}は`{name}`を逃がしませんでした。"));

        await FollowupAsync(
            embed: Simple($"本当に`{name}`を逃がしますか...？"),
            components: components);
    }
}
