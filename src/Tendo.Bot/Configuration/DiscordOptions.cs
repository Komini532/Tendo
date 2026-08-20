using System.ComponentModel.DataAnnotations;

namespace Tendo.Bot.Configuration;

/// <summary>
/// 旧 <c>mmo/config.json</c> の置き換え。
/// 旧ファイルには Bot トークンが直に書かれていたが、本移植ではトークンをリポジトリに
/// 含めない。値は環境変数 (<c>TENDO_Discord__Token</c>) か user-secrets から供給する。
/// </summary>
public sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    /// <summary>Bot トークン。appsettings.json には決して書かないこと。</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage =
        "Discord:Token が未設定です。環境変数 TENDO_Discord__Token か user-secrets で指定してください。")]
    public string Token { get; set; } = string.Empty;

    /// <summary>旧 <c>config.owner</c>。<c>/mod</c> 系コマンドを実行できる唯一のユーザー。</summary>
    public ulong OwnerId { get; set; }

    /// <summary>旧 <c>config.invite</c>。<c>/help</c> の「関連」フィールドに出る。</summary>
    public string InviteUrl { get; set; } = string.Empty;

    /// <summary>
    /// 旧 <c>mmo/enemy.js</c> の <c>base</c> 定数。敵画像 URL の前置詞。
    /// 元は <c>http://bgm.hisyaku.com/mmo/</c> 固定だったが、配信元が失われても
    /// 差し替えられるよう設定値にした。
    /// </summary>
    public string EnemyImageBaseUrl { get; set; } = "http://bgm.hisyaku.com/mmo/";

    /// <summary>
    /// 開発用。指定するとスラッシュコマンドをこのギルドにのみ登録する (反映が即時)。
    /// null ならグローバル登録 (反映に最大 1 時間)。
    /// </summary>
    public ulong? TestGuildId { get; set; }
}
