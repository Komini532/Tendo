using System.Text;

namespace Tendo.Game.Rendering;

/// <summary>
/// 旧 <c>ea.js</c> の <c>const code = (a,b) =&gt; "```"+(b||"C")+"\n"+a+"\n```";</c> の移植。
///
/// このゲームの見た目はほぼ全てコードブロックの構文強調で作られている。
/// 言語名は「その言語として妥当か」ではなく「Discord がどう色付けするか」で選ばれており、
/// 旧コードで使われていた言語名をそのまま定数化してある。取り違えると配色が変わる。
/// </summary>
public static class Fence
{
    /// <summary>旧実装の既定言語 (<c>b || "C"</c>)。無色に近い表示。</summary>
    public const string Default = "C";

    /// <summary>ステータス値・一覧など。<c>[...]</c> が橙色になる。</summary>
    public const string Css = "CSS";

    /// <summary>増減の表現。行頭 <c>+</c> が緑、<c>-</c> が赤。</summary>
    public const string Diff = "DIFF";

    /// <summary>強調。旧コードではレア敵の出現メッセージなどに使われる。</summary>
    public const string Fix = "fix";

    /// <summary>灰色の打ち消し表現。「状態異常なし」「未開放」など。</summary>
    public const string BrainFuck = "BrainFuck";

    /// <summary>報酬行。行頭 <c>`</c> で始まる旧実装の書式に合わせる。</summary>
    public const string Js = "JS";

    /// <summary>見出し。<c>&lt; RESULT &gt;</c> など。</summary>
    public const string Html = "HTML";

    /// <summary>
    /// 旧 <c>code(text, lang)</c> と同じ文字列を組み立てる。
    /// 末尾に改行を含む点まで含めて一致させること (連結時の行間が変わるため)。
    /// </summary>
    public static string Code(string text, string language = Default)
        => "```" + language + "\n" + text + "\n```";

    /// <summary>複数行を <c>\n</c> で連結してから <see cref="Code"/> に渡す。</summary>
    public static string Code(IEnumerable<string> lines, string language = Default)
        => Code(string.Join("\n", lines), language);

    /// <summary>
    /// 旧実装が embed の description を作るときの
    /// <c>[code(...), code(...)].join("")</c> 相当。区切り文字を入れない連結。
    /// </summary>
    public static string Join(IEnumerable<string> blocks)
    {
        var sb = new StringBuilder();
        foreach (var block in blocks)
        {
            sb.Append(block);
        }

        return sb.ToString();
    }
}
