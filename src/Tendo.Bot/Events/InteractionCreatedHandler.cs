using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tendo.Data.Repositories;

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
            // ここに来ているかどうかが切り分けの起点になるので必ず記録する。
            _logger.LogInformation(
                "interaction を受け付けました: {Type} / {User}",
                interaction.Type,
                interaction.User.Username);

            // BAN 判定はこの場で完結するので、専用のスコープを切って即座に閉じる。
            await using (var scope = _services.CreateAsyncScope())
            {
                if (await IsBannedAsync(scope.ServiceProvider, interaction))
                {
                    return;
                }
            }

            var context = new SocketInteractionContext(client, interaction);

            // ここでスコープを作って ExecuteCommandAsync に渡してはいけない。
            //
            // DefaultRunMode は Async なので ExecuteCommandAsync はコマンド本体を待たずに返る。
            // 呼び出し側で using していると、本体が動き出す前にスコープが破棄され、
            // Discord.Net 内部の services.CreateScope() が ObjectDisposedException になる。
            // しかもその呼び出しは try の外かつ投げっぱなしタスク (_ = Task.Run(...)) の中なので
            // どこにも捕捉されず、ログにも残らないまま interaction が ACK されない。
            // 結果として Discord には「アプリケーションが応答しませんでした」とだけ出る。
            //
            // Discord.Net はコマンド実行ごとに自前でスコープを切るので、ルートを渡すのが正しい。
            await _interactions.ExecuteCommandAsync(context, _services);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "interaction の処理中に例外が発生しました");
            await RespondWithErrorAsync(interaction);
        }
    }

    /// <summary>
    /// 旧 <c>ea.on("message")</c> 冒頭の BAN 判定。
    /// <code>
    /// if( BANLIST.indexOf(d.user.id)!=-1 )
    ///   return console.log(`013: [User Banning Name=${d.user.tag}]`);
    /// </code>
    ///
    /// 旧実装は起動時に読み込んだメモリ配列を見ていたので、複数プロセスで動かすと
    /// ずれ、<c>/mod banlist</c> も再起動するまで DB と食い違った。毎回 DB を引く。
    ///
    /// 旧実装は無反応で握り潰していたが、slash command は応答しないと
    /// 失敗表示が残る。本人にだけ見える形で返す。
    /// </summary>
    private async Task<bool> IsBannedAsync(
        IServiceProvider services,
        SocketInteraction interaction)
    {
        var bans = services.GetRequiredService<IBanRepository>();

        if (!await bans.IsBannedAsync(interaction.User.Id))
        {
            return false;
        }

        _logger.LogInformation(
            "BAN 中のユーザー {User} ({UserId}) の操作を拒否しました",
            interaction.User.Username,
            interaction.User.Id);

        try
        {
            await interaction.RespondAsync("このBotは利用できません。", ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "BAN 通知の送信に失敗しました");
        }

        return true;
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
