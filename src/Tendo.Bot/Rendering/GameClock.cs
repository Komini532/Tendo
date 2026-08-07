using System.Globalization;

namespace Tendo.Bot.Rendering;

/// <summary>
/// 旧 <c>fn.js</c> の <c>r.date()</c> 相当。embed の footer に出る現在日時。
/// 元の実装はソースに残っていないため、呼び出し箇所 (<c>/status</c>、<c>/pstatus</c>、
/// <c>/cstatus</c>、<c>/help</c> の footer) から「日本時間の読める日時文字列」として復元した。
/// </summary>
public interface IGameClock
{
    /// <summary>旧 <c>r.date()</c>。</summary>
    string Now();
}

/// <inheritdoc />
public sealed class GameClock : IGameClock
{
    private static readonly TimeZoneInfo JapanStandardTime = ResolveJapanStandardTime();

    public string Now()
    {
        var jst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, JapanStandardTime);
        return jst.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static TimeZoneInfo ResolveJapanStandardTime()
    {
        // Linux は IANA 名、Windows は Windows 名。どちらでも動くよう両方試す。
        foreach (var id in new[] { "Asia/Tokyo", "Tokyo Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // 次の候補へ
            }
            catch (InvalidTimeZoneException)
            {
                // 次の候補へ
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("JST", TimeSpan.FromHours(9), "JST", "JST");
    }
}
