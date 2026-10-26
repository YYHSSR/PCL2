using System.Globalization;
using System.Text.RegularExpressions;

namespace MeloongCore;
public static class StringUtils {

    #region Escaping

    /// <summary>
    /// WPF XML escaping.
    /// </summary>
    public static string XmlEscape(string str) {
        if (str.StartsWithF("{")) str = "{}" + str; // #4187
        return str
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("'", "&apos;")
            .Replace("\"", "&quot;").ReplaceLineEndings("&#xa;");
    }

    /// <summary>
    /// Escaping for the VB.NET Like keyword.
    /// </summary>
    public static string LikePatternEscape(string input) {
        var builder = new StringBuilder();
        foreach (char c in input) {
            if (c is '[' or ']' or '*' or '?' or '#') {
                builder.Append('[').Append(c).Append(']');
            } else {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }

    /// <summary>
    /// Regex escaping.
    /// Same as <see cref="Regex.Escape"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string RegexEscape(string value) 
        => Regex.Escape(value);

    /// <summary>
    /// Regex unescaping.
    /// Same as <see cref="Regex.Unescape"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string RegexUnescape(string value) 
        => Regex.Unescape(value);

    /// <summary>
    /// URL escaping.
    /// Same as <see cref="Uri.EscapeDataString"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string UrlEscape(string value) 
        => Uri.EscapeDataString(value);

    /// <summary>
    /// URL unescaping.
    /// Same as <see cref="Uri.UnescapeDataString"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string UrlUnescape(string value) 
        => Uri.UnescapeDataString(value);

    /// <summary>
    /// Form escaping.
    /// Same as <see cref="WebUtility.UrlEncode"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FormUrlEscape(string value) 
        => WebUtility.UrlEncode(value)!;

    /// <summary>
    /// Form unescaping.
    /// Same as <see cref="WebUtility.UrlDecode"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FormUrlUnescape(string value) 
        => WebUtility.UrlDecode(value);

    /// <summary>
    /// HTML escaping.
    /// Same as <see cref="WebUtility.HtmlEncode"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string HtmlEscape(string value) 
        => WebUtility.HtmlEncode(value);

    /// <summary>
    /// HTML unescaping.
    /// Same as <see cref="WebUtility.HtmlDecode"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string HtmlUnescape(string value) 
        => WebUtility.HtmlDecode(value);

    #endregion

    #region Formatting

    /// <summary>
    /// Formats a byte size as human-readable text, such as <c>"1.28 M"</c> or <c>"128 G"</c>.
    /// </summary>
    public static string FormatByteSize(long byteSize) {
        var sign = byteSize < 0 ? "-" : "";
        if (byteSize < 0) byteSize *= -1;
        if (byteSize < 1000) { // B range
            return $"{sign}{byteSize} B";
        } else if (byteSize < 1024 * 1000) { // K range
            var roundResult = Math.Round(byteSize / 1024D).ToString(CultureInfo.InvariantCulture);
            return $"{sign}{Math.Round(byteSize / 1024D, (3 - roundResult.Length).Clamp(0, 2))} K";
        } else if (byteSize < 1024 * 1024 * 1000) { // M range
            var roundResult = Math.Round(byteSize / 1024D / 1024D).ToString(CultureInfo.InvariantCulture);
            return $"{sign}{Math.Round(byteSize / 1024D / 1024D, (3 - roundResult.Length).Clamp(0, 2))} M";
        } else { // G range
            var roundResult = Math.Round(byteSize / 1024D / 1024D / 1024D).ToString(CultureInfo.InvariantCulture);
            return $"{sign}{Math.Round(byteSize / 1024D / 1024D / 1024D, (3 - roundResult.Length).Clamp(0, 2))} G";
        }
    }

    /// <summary>
    /// Formats a time span as human-readable text.
    /// <para/>If <paramref name="isShort"/> is set, only one unit is used, such as <c>"5 分钟前"</c>; otherwise two units may be used, such as <c>"5 分 10 秒前"</c>.
    /// </summary>
    public static string FormatTimeSpan(TimeSpan span, bool isShort) {
        var suffix = span.TotalMilliseconds > 0 ? "后" : "前";
        if (span.TotalMilliseconds < 0) span = -span;
        var totalMonths = (int) Math.Floor(span.Days / 30D);
        if (isShort) {
            if (totalMonths >= 12) {
                // 1+ years, "3 年"
                return $"{Math.Floor(totalMonths / 12D)} 年{suffix}";
            } else if (totalMonths >= 2) {
                // 2~11 months, "5 个月"
                return $"{totalMonths} 个月{suffix}";
            } else if (span.TotalDays >= 2) {
                // 2 days ~ 2 months, "23 天"
                return $"{span.Days} 天{suffix}";
            } else if (span.TotalHours >= 1) {
                // 1 hour ~ 2 days, "15 小时"
                return $"{span.Hours} 小时{suffix}";
            } else if (span.TotalMinutes >= 1) {
                // 1 minute ~ 1 hour, "49 分钟"
                return $"{span.Minutes} 分钟{suffix}";
            } else if (span.TotalSeconds >= 1) {
                // 1 second ~ 1 minute, "23 秒"
                return $"{span.Seconds} 秒{suffix}";
            } else {
                // Less than 1 second
                return $"1 秒{suffix}";
            }
        } else {
            if (totalMonths >= 61) {
                // 5+ years, "5 年"
                return $"{Math.Floor(totalMonths / 12D)} 年{suffix}";
            } else if (totalMonths >= 12) {
                // 12~60 months, "1 年 2 个月"
                return $"{Math.Floor(totalMonths / 12D)} 年{(totalMonths % 12 > 0 ? $" {totalMonths % 12} 个月" : "")}{suffix}";
            } else if (totalMonths >= 4) {
                // 4~11 months, "5 个月"
                return $"{totalMonths} 个月{suffix}";
            } else if (totalMonths >= 1) {
                // 1~4 months, "2 个月 13 天"
                return $"{totalMonths} 月{(span.Days % 30 > 0 ? $" {span.Days % 30} 天" : "")}{suffix}";
            } else if (span.TotalDays >= 4) {
                // 4~30 days, "23 天"
                return $"{span.Days} 天{suffix}";
            } else if (span.TotalDays >= 1) {
                // 1~3 days, "2 天 20 小时"
                return $"{span.Days} 天{(span.Hours > 0 ? $" {span.Hours} 小时" : "")}{suffix}";
            } else if (span.TotalHours >= 10) {
                // 10 hours ~ 1 day, "15 小时"
                return $"{span.Hours} 小时{suffix}";
            } else if (span.TotalHours >= 1) {
                // 1~10 hours, "1 小时 20 分钟"
                return $"{span.Hours} 小时{(span.Minutes > 0 ? $" {span.Minutes} 分钟" : "")}{suffix}";
            } else if (span.TotalMinutes >= 10) {
                // 10 minutes ~ 1 hour, "49 分钟"
                return $"{span.Minutes} 分钟{suffix}";
            } else if (span.TotalMinutes >= 1) {
                // 1~10 minutes, "9 分 23 秒"
                return $"{span.Minutes} 分{(span.Seconds > 0 ? $" {span.Seconds} 秒" : "")}{suffix}";
            } else if (span.TotalSeconds >= 1) {
                // 1 second ~ 1 minute, "23 秒"
                return $"{span.Seconds} 秒{suffix}";
            } else {
                // Less than 1 second
                return $"1 秒{suffix}";
            }
        }
    }

    #endregion

    /// <summary>
    /// Parses a string into a version.
    /// Any number of segments may be missing; missing segments become 0 rather than -1. For example, "2" is parsed as 2.0.0.0.
    /// </summary>
    public static Version ParseVersionWithDefaults(string value) {
        while (value.Count(c => c == '.') < 3) value += ".0";
        return Version.Parse(value);
    }

}