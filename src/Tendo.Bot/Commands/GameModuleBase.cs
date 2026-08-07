using Discord;
using Discord.Interactions;

namespace Tendo.Bot.Commands;

/// <summary>
/// コマンド実装が共通で使う小物。
/// 旧 <c>ea.js</c> がコマンド分岐の外側で用意していた <c>d</c> オブジェクト
/// (<c>channel</c> / <c>user</c> / <c>guild</c> / <c>member</c>) に相当する。
/// </summary>
public abstract class GameModuleBase : InteractionModuleBase<SocketInteractionContext>
{
    /// <summary>旧 <c>d.member ? d.member.displayName : d.user.username</c>。</summary>
    protected string DisplayName
        => Context.User is IGuildUser member ? member.DisplayName : Context.User.Username;

    /// <summary>
    /// 表示名の解決。旧実装は <c>m.displayName || m.username</c> を直接読んでおり、
    /// 対象がサーバーを抜けていると例外になっていた。引けなければ ID を返す。
    /// </summary>
    protected string ResolveName(ulong userId)
    {
        if (Context.Guild?.GetUser(userId) is { } guildUser)
        {
            return guildUser.DisplayName;
        }

        return Context.Client.GetUser(userId)?.Username ?? userId.ToString();
    }

    protected static Embed Simple(string description)
        => new EmbedBuilder().WithDescription(description).Build();

    /// <summary>まだ応答していなければ応答し、済んでいれば追送する。</summary>
    protected async Task ReplyEmbedAsync(Embed embed, bool ephemeral = false)
    {
        if (Context.Interaction.HasResponded)
        {
            await FollowupAsync(embed: embed, ephemeral: ephemeral);
        }
        else
        {
            await RespondAsync(embed: embed, ephemeral: ephemeral);
        }
    }

    protected Task ReplyEmbedAsync(string description, bool ephemeral = false)
        => ReplyEmbedAsync(Simple(description), ephemeral);
}
