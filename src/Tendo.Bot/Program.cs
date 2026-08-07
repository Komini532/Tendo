using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Tendo.Game.Master;
using Tendo.Game.State;
using Tendo.Bot.Configuration;
using Tendo.Bot.Events;
using Tendo.Bot.Hosting;
using Tendo.Bot.Rendering;
using Tendo.Bot.Services;
using Tendo.Data;

var builder = Host.CreateApplicationBuilder(args);

// 設定の優先順位: appsettings.json < user-secrets < 環境変数 (TENDO_ 前置詞)。
// 旧実装は mmo/config.json にトークンを直書きしていたが、本移植では
// トークン・接続文字列はリポジトリに一切含めない。
builder.Configuration
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
    .AddEnvironmentVariables("TENDO_");

builder.Services
    .AddOptions<DiscordOptions>()
    .Bind(builder.Configuration.GetSection(DiscordOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Services.AddSerilog();

builder.Services.AddSingleton(new DiscordSocketConfig
{
    // slash command のみで動くため特権インテントは要求しない。
    // Guilds だけでサーバー/チャンネル情報とスラッシュコマンドの配信が成立する。
    GatewayIntents = GatewayIntents.Guilds,
    AlwaysDownloadUsers = false,
    LogGatewayIntentWarnings = false,
    MessageCacheSize = 0,
});
builder.Services.AddSingleton<DiscordSocketClient>();

builder.Services.AddSingleton(new InteractionServiceConfig
{
    DefaultRunMode = RunMode.Async,
    UseCompiledLambda = true,
});
builder.Services.AddSingleton(sp => new InteractionService(
    sp.GetRequiredService<DiscordSocketClient>(),
    sp.GetRequiredService<InteractionServiceConfig>()));

// 1 イベント 1 ファイル。DiscordBotService が起動時に全て購読する。
builder.Services.AddSingleton<IDiscordEventHandler, LogHandler>();
builder.Services.AddSingleton<IDiscordEventHandler, ReadyHandler>();
builder.Services.AddSingleton<IDiscordEventHandler, InteractionCreatedHandler>();

builder.Services.AddSingleton<IGameClock, GameClock>();

// マスターデータ (敵 77 / 技 90 / 状態異常 37 …) は起動時に一度だけ読んで共有する。
// 参照が壊れていればここで例外になり、不整合を抱えたまま起動しない。
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("MasterData");
    var data = MasterDataLoader.Load(directory: null, out var warnings);

    foreach (var warning in warnings)
    {
        logger.LogWarning("マスターデータの警告: {Warning}", warning);
    }

    logger.LogInformation(
        "マスターデータを読み込みました (敵 {Enemies} / 技 {Skills} (習得可能 {Learnable}) / 状態異常 {Effects})",
        data.Enemies.Count,
        data.Skills.Count,
        data.LearnableSkills.Count,
        data.Effects.Count);

    return data;
});

// 新規プレイヤー / 戦場 / ペットの初期値 (旧 mmo/newdata.js)。
builder.Services.AddSingleton(_ => GameDefaults.Load());

// 旧 AWAIT Map。チャンネル単位の「処理中」フラグ。
builder.Services.AddSingleton<BattleGate>();

// MySQL 永続化 (旧 db.js / SQLite の置き換え)。
builder.Services.AddTendoData();

// 旧 fn(d, act) の周辺処理。コマンドから呼ばれる。
builder.Services.AddScoped<BattleService>();

// スキーマ適用は Discord にログインする前に済ませる。
builder.Services.AddHostedService<DatabaseMigrationService>();
builder.Services.AddHostedService<DiscordBotService>();

var host = builder.Build();

try
{
    // シングルトンは遅延生成なので、ここで一度触って起動時に検証させる。
    // 不整合を抱えたまま Discord にログインしないようにするため。
    _ = host.Services.GetRequiredService<MasterData>();

    await host.RunAsync();
}
catch (MasterDataException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
catch (OptionsValidationException ex)
{
    // 設定漏れは運用時に一番よく踏むので、スタックトレースではなく
    // 何をどこに設定すればよいかだけを出す。
    Console.Error.WriteLine("設定が不正なため起動できません:");
    foreach (var failure in ex.Failures)
    {
        Console.Error.WriteLine($"  - {failure}");
    }

    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;
