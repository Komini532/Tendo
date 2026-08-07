using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Tendo.Bot.Events;

/// <summary>
/// 旧 <c>ea.on("message", ...)</c> の巨大な <c>switch(c)</c> に相当する入口。
///
/// 旧実装は接頭辞 <c>e</c> を自前で剥がしてコマンド名で分岐していたが、
/// 本移植は slash command のみ (MessageContent 特権インテント不要) なので、
/// 分岐は <see cref="InteractionService"/> の属性ベース解決に委ねる。
/// 個々のコマンド実装は <c>Commands/</c> に 1 コマンド 1 ファイルで置く。
/// </summary>
public sealed class InteractionCreatedHandler : IDiscordEventHandler
{
    private readonly ILogger<InteractionCreatedHandler> _logger;
    private readonly InteractionService _interactions;
    private readonly IServiceProvider _services;

    public InteractionCreatedHandler(
        ILogger<InteractionCreatedHandler> logger,
        InteractionService interactions,
        IServiceProvider services)
    {
        _logger = logger;
        _interactions = interactions;
        _services = services;
    }

    public void Attach(DiscordSocketClient client)
    {
        client.InteractionCreated += interaction => OnInteractionAsync(client, interaction);
        _interactions.SlashCommandExecuted += OnCommandExecutedAsync;
        _interactions.ComponentCommandExecuted += OnCommandExecutedAsync;
        _interactions.ModalCommandExecuted += OnCommandExecutedAsync;
    }

    private async Task OnInteractionAsync(DiscordSocketClient client, SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(client, interaction);

            // 各コマンドはスコープ付き DI (リポジトリ等) を要求するため、
            // interaction ごとにスコープを切る。
            await using var scope = _services.CreateAsyncScope();
            await _interactions.ExecuteCommandAsync(context, scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "interaction の処理中に例外が発生しました");
            await RespondWithErrorAsync(interaction);
        }
    }

    private async Task OnCommandExecutedAsync(
        ICommandInfo command,
        IInteractionContext context,
        IResult result)
    {
        if (result.IsSuccess)
        {
            return;
        }

        // 未処理の失敗のみログする。UnmetPrecondition は BAN 判定など想定内の拒否
        // (Phase 6 で precondition 側が利用者向けメッセージを出す)。
        if (result.Error == InteractionCommandError.UnmetPrecondition)
        {
            _logger.LogDebug(
                "コマンド {Command} が precondition で拒否されました: {Reason}",
                command.Name,
                result.ErrorReason);
            return;
        }

        _logger.LogError(
            "コマンド {Command} が失敗しました ({Error}): {Reason}",
            command.Name,
            result.Error,
            result.ErrorReason);

        await RespondWithErrorAsync(context.Interaction);
    }

    private async Task RespondWithErrorAsync(IDiscordInteraction interaction)
    {
        const string message = "処理中にエラーが発生しました。";

        try
        {
            if (interaction.HasResponded)
            {
                await interaction.FollowupAsync(message, ephemeral: true);
            }
            else
            {
                await interaction.RespondAsync(message, ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            // 応答期限切れなどで返せないことがある。ここで再送はしない。
            _logger.LogWarning(ex, "エラー応答の送信に失敗しました");
        }
    }
}
