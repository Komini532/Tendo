using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Options;
using Tendo.Bot.Components;
using Tendo.Bot.Configuration;
using Tendo.Bot.Rendering;
using Tendo.Game.Rendering;

namespace Tendo.Bot.Commands;

/// <summary>
/// 旧 <c>ehelp</c>。3 ページ構成のコマンド一覧。
///
/// 説明文は旧実装のものをそのまま使い、コマンド名だけ slash 形式に直してある。
/// 接頭辞コマンドは無くなったので「(短縮：…)」の行は落とした。
/// </summary>
public sealed class HelpCommand : GameModuleBase
{
    private readonly PaginationService _pagination;
    private readonly IGameClock _clock;
    private readonly DiscordOptions _options;

    public HelpCommand(
        PaginationService pagination,
        IGameClock clock,
        IOptions<DiscordOptions> options)
    {
        _pagination = pagination;
        _clock = clock;
        _options = options.Value;
    }

    /// <summary>旧 <c>commands</c> 配列。ページ分割も旧実装の区切りに合わせてある。</summary>
    private static readonly string[] Battle =
    [
        "/help - このメッセージを表示します。",
        "/attack - 敵に攻撃します。",
        "/skill [技名] - 技を発動します。",
        "/use [アイテム名] - アイテムを使用します。",
        "/wait - 「何もしない」をします。",
        "/inventory - アイテムを確認します。",
        "/status - 自分のステータスを表示します。",
        "/cstatus - 戦場のステータスを表示します。",
        "/skills - 習得した技一覧を表示します。",
        "/reset - 戦場をリセットします。",
    ];

    private static readonly string[] Other =
    [
        "/go - フィールドを移動します。",
        "/dchange - フィールドの難易度を変更します。",
        "/shop - アイテムを購入するショップ画面を開きます。",
        "/mlist - フィールドに出る敵一覧を表示します。",
        "/ranking - サーバーランキングを表示します。",
        "/give [アイテム] [個数] [相手]\n - 誰かにアイテムかギルを渡します。",
        "/fix - 起動してるのに攻撃出来なくなったら試してください。",
        "/tips [番号] - このBotに関する豆(ほどもない)知識です。",
    ];

    private static readonly string[] Pet =
    [
        "敵に「チョコレート」を与えて倒すと、\n敵が懐いてついてくることがあります。\nペットは敵の時と同じ技を使うことができます。\n",
        "/pstatus - ペットのステータスを確認します。",
        "/rename - ペットの名前を変更します。",
        "/release - ペットを逃がします。",
    ];

    [SlashCommand("help", "コマンド一覧を表示します。")]
    public async Task HelpAsync()
    {
        await DeferAsync();

        var links = new EmbedFieldBuilder()
            .WithName("関連")
            .WithValue($"[サーバーに招待]({_options.InviteUrl})");

        var footer = $"開発：Unknown｜現在日時：{_clock.Now()}";

        var pages = new List<Embed>
        {
            Page("コマンド一覧 1ページ目 (戦闘関連)", Battle, links, footer),
            Page("コマンド一覧 2ページ目 (戦闘以外)", Other, links, footer),
            Page("コマンド一覧 3ページ目 (ペット)", Pet, links, footer),
        };

        var (page, components) = _pagination.Create(Context.User.Id, pages);
        await FollowupAsync(embed: page, components: components);
    }

    private static Embed Page(
        string heading,
        IEnumerable<string> commands,
        EmbedFieldBuilder links,
        string footer)
        => new EmbedBuilder()
            .WithTitle("Extend Adventure")
            .AddField(heading, Fence.Code(string.Join("\n", commands), Fence.Css))
            .AddField(links)
            .WithFooter(footer)
            .Build();
}
