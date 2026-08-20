using System.Reflection;
using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Tendo.Data;

/// <summary>
/// 埋め込みリソースの <c>Migrations/*.sql</c> をファイル名順に適用する。
///
/// 旧実装は起動のたびに <c>create table if not exists</c> を 3 本流すだけだった。
/// テーブルが増えたので、適用済みを記録して二重実行を防ぐ形にしてある。
/// </summary>
public sealed class MigrationRunner
{
    private const string ResourcePrefix = "Tendo.Data.Migrations.";

    private readonly IDbConnectionFactory _connections;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(IDbConnectionFactory connections, ILogger<MigrationRunner> logger)
    {
        _connections = connections;
        _logger = logger;
    }

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                name       VARCHAR(128) NOT NULL,
                applied_at DATETIME(3)  NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
                PRIMARY KEY (name)
            ) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4
            """);

        var applied = (await connection.QueryAsync<string>("SELECT name FROM schema_migrations"))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (name, sql) in ReadMigrations())
        {
            if (applied.Contains(name))
            {
                continue;
            }

            _logger.LogInformation("マイグレーション {Name} を適用します", name);

            // MySQL は 1 コマンドずつしか実行できないので文単位に分けて流す。
            foreach (var statement in SplitStatements(sql))
            {
                await connection.ExecuteAsync(statement);
            }

            await connection.ExecuteAsync(
                "INSERT INTO schema_migrations (name) VALUES (@name)",
                new { name });
        }
    }

    private static IEnumerable<(string Name, string Sql)> ReadMigrations()
    {
        var assembly = typeof(MigrationRunner).Assembly;

        var names = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Where(n => n.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal);

        foreach (var resource in names)
        {
            using var stream = assembly.GetManifestResourceStream(resource)
                               ?? throw new InvalidOperationException(
                                   $"埋め込みリソース {resource} を開けませんでした。");
            using var reader = new StreamReader(stream);

            yield return (resource[ResourcePrefix.Length..], reader.ReadToEnd());
        }
    }

    /// <summary>
    /// セミコロン区切りで文に分ける。
    /// このプロジェクトの SQL にはストアドプロシージャや文字列中のセミコロンがないので、
    /// 単純な分割で足りる。行コメント (<c>--</c>) は落としてから判定する。
    /// </summary>
    internal static IEnumerable<string> SplitStatements(string sql)
    {
        var withoutComments = string.Join('\n', sql
            .Split('\n')
            .Select(line =>
            {
                var index = line.IndexOf("--", StringComparison.Ordinal);
                return index >= 0 ? line[..index] : line;
            }));

        return withoutComments
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0);
    }
}

/// <summary>MySQL 接続を作る。テストで差し替えられるようインターフェースにしてある。</summary>
public interface IDbConnectionFactory
{
    Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class MySqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public MySqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
