using Microsoft.Extensions.Logging.Abstractions;
using Tendo.Data;
using Xunit;

namespace Tendo.Data.Tests;

/// <summary>
/// 統合テスト用の MySQL 接続。
///
/// 実際の MySQL が要るので、接続文字列が与えられたときだけ実行する。
/// <code>
///   docker compose up -d
///   export TENDO_TEST_MYSQL="Server=127.0.0.1;Port=3306;Database=tendo;User ID=tendo;Password=tendo;CharSet=utf8mb4"
///   dotnet test
/// </code>
/// 未設定なら該当テストは skip される (無言で成功したことにはしない)。
/// </summary>
public static class TestDatabase
{
    public const string EnvironmentVariable = "TENDO_TEST_MYSQL";

    public static string? ConnectionString
        => Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } value
            ? value
            : null;

    public static bool IsAvailable => ConnectionString is not null;
}

/// <summary>
/// MySQL が無い環境では skip される <see cref="FactAttribute"/>。
/// xUnit は Skip を検出時に読むので、属性のコンストラクタで判定すれば動的に切り替えられる。
/// </summary>
public sealed class RequiresMySqlFactAttribute : FactAttribute
{
    public RequiresMySqlFactAttribute()
    {
        if (!TestDatabase.IsAvailable)
        {
            Skip = $"環境変数 {TestDatabase.EnvironmentVariable} が未設定のため実行しません " +
                   "(docker compose up -d で MySQL を起動してから設定してください)。";
        }
    }
}

/// <summary>
/// スキーマ適用を 1 度だけ行う共有フィクスチャ。
/// </summary>
public sealed class MySqlFixture : IAsyncLifetime
{
    public IDbConnectionFactory Connections { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (!TestDatabase.IsAvailable)
        {
            return;
        }

        Connections = new MySqlConnectionFactory(TestDatabase.ConnectionString!);
        var runner = new MigrationRunner(Connections, NullLogger<MigrationRunner>.Instance);
        await runner.ApplyAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(Name)]
public sealed class MySqlCollection : ICollectionFixture<MySqlFixture>
{
    public const string Name = "mysql";
}
