using Tendo.Data;
using Xunit;

namespace Tendo.Data.Tests;

/// <summary>SQL の文分割。MySQL は 1 コマンドずつしか実行できないので必要になる。</summary>
public sealed class MigrationRunnerTests
{
    [Fact]
    public void 行コメントを落としてから文に分割する()
    {
        const string sql = """
            -- これはコメント。セミコロンを含んでいても無視される;
            CREATE TABLE a (id INT); -- 末尾コメント
            CREATE TABLE b (id INT);
            """;

        var statements = MigrationRunner.SplitStatements(sql).ToList();

        Assert.Equal(2, statements.Count);
        Assert.StartsWith("CREATE TABLE a", statements[0]);
        Assert.StartsWith("CREATE TABLE b", statements[1]);
    }

    [Fact]
    public void 末尾のセミコロンで空文を作らない()
    {
        var statements = MigrationRunner.SplitStatements("SELECT 1;\n\n  \n").ToList();

        Assert.Single(statements);
        Assert.Equal("SELECT 1", statements[0]);
    }

    [Fact]
    public void 初期スキーマが全テーブルを含む()
    {
        // 埋め込みリソースとして配布されていることの確認も兼ねる。
        var assembly = typeof(MigrationRunner).Assembly;
        var resource = Assert.Single(
            assembly.GetManifestResourceNames(),
            n => n.EndsWith("001_init.sql", StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        var sql = reader.ReadToEnd();

        string[] expected =
        [
            "players", "player_skills", "player_items", "player_effects", "pets",
            "battles", "battle_effects", "battle_participants", "battle_chocolate_feeders",
            "bans",
        ];

        foreach (var table in expected)
        {
            Assert.Contains($"CREATE TABLE IF NOT EXISTS {table}", sql, StringComparison.Ordinal);
        }

        // snowflake の精度を落とさないことがこの移植の要点のひとつ。
        Assert.DoesNotContain("user_id           INT ", sql, StringComparison.Ordinal);
        Assert.Contains("BIGINT UNSIGNED", sql, StringComparison.Ordinal);
    }
}
