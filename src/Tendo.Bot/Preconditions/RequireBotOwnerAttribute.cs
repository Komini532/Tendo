using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tendo.Bot.Configuration;

namespace Tendo.Bot.Preconditions;

/// <summary>
/// 旧 <c>case "mod": if( d.user.id != config.owner )return;</c> の移植。
///
/// <c>Discord:OwnerId</c> に一致する 1 人だけが実行できる。
/// 未設定 (0) のときは誰も実行できない — 設定漏れで管理コマンドが
/// 誰にでも通ってしまうより安全側に倒す。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireBotOwnerAttribute : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        var ownerId = services.GetRequiredService<IOptions<DiscordOptions>>().Value.OwnerId;

        if (ownerId == 0)
        {
            return Task.FromResult(PreconditionResult.FromError(
                "Discord:OwnerId が未設定のため管理コマンドは使えません。"));
        }

        return Task.FromResult(context.User.Id == ownerId
            ? PreconditionResult.FromSuccess()
            : PreconditionResult.FromError("このコマンドは管理者専用です。"));
    }
}
