using System.ComponentModel.DataAnnotations;

namespace Tendo.Data;

/// <summary>
/// 旧 <c>db.js</c> (<c>sqlite3.Database("./db/mmo.sqlite3")</c>) の置き換え。
/// 接続文字列は資格情報を含むためリポジトリに書かない。
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// MySQL 9.7 への接続文字列。
    /// 例: <c>Server=localhost;Port=3306;Database=tendo;User ID=tendo;Password=...;CharSet=utf8mb4</c>
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage =
        "Database:ConnectionString が未設定です。環境変数 TENDO_Database__ConnectionString で指定してください。")]
    public string ConnectionString { get; set; } = string.Empty;
}
