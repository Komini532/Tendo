using Discord.Interactions;
using Tendo.Bot.Components;
using Tendo.Data.Repositories;

namespace Tendo.Bot.Commands;

/// <summary>
/// 旧 <c>erename</c> / <c>eren</c>。ペットの改名。
/// 旧実装は 👍 / 👎 のリアクションで確認していたので、ボタンに置き換える。
/// </summary>
public sealed class RenameCommand : GameModuleBase
{
    /// <summary>旧 <c>String(name).length > 50</c>。</summary>
    private const int MaxNameLength = 50;

    private readonly IPlayerRepository _players;
    private readonly PromptService _prompts;

    public RenameCommand(IPlayerRepository players, PromptService prompts)
    {
        _players = players;
        _prompts = prompts;
    }

    [SlashCommand("rename", "ペットの名前を変更します。")]
    public async Task RenameAsync([Summary("名前", "新しい名前")] string name)
    {
        await DeferAsync();

        var player = await _players.GetOrCreateAsync(Context.User.Id);

        if (player.Pet is not { } pet)
        {
            await FollowupAsync(embed: Simple($"{Context.User.Mention}さんはペットを飼っていません..."));
            return;
        }

        if (name.Length > MaxNameLength)
        {
            await FollowupAsync(embed: Simple(
                $"{Context.User.Mention}さん、ペットの名前は{MaxNameLength}文字までにしてください。"));
            return;
        }

        if (name.Contains('\n') || name.Contains('\r'))
        {
            await FollowupAsync(embed: Simple(
                $"{Context.User.Mention}さん、ペットの名前に改行を含めることはできません。"));
            return;
        }

        var before = pet.Name;
        var userId = Context.User.Id;

        var components = _prompts.Create(
            userId,
            onConfirm: async () =>
            {
                // 確認までの間に変わっている可能性があるので読み直す。
                var current = await _players.GetOrCreateAsync(userId);
                if (current.Pet is null)
                {
                    return $"`{before}`はもういません。";
                }

                current.Pet.Name = name;
                await _players.SaveAsync(current);

                return $"`{before}`の名前を`{name}`に変更しました。";
            },
            onCancel: () => Task.FromResult($"`{before}`の名前を変更しませんでした。"));

        await FollowupAsync(
            embed: Simple($"`{before}`の名前を`{name}`に変更していいですか？"),
            components: components);
    }
}
