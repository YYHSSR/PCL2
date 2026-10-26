using System.Text.RegularExpressions;

namespace MeloongCore.Extensions;
public static class StringExtensions {

    #region Culture / case-sensitivity shorthands

    /// <summary>
    /// Culture-invariant <see cref="string.StartsWith(string)"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool StartsWithF(this string? value, string prefix, bool ignoreCase = false) 
        => value?.StartsWith(prefix, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ?? false;
    /// <summary>
    /// Whether the string starts with <paramref name="prefix"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool StartsWithF(this string? value, char prefix, bool ignoreCase = false) 
        => value?.StartsWith(prefix.ToString(), ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ?? false;
    /// <summary>
    /// Culture-invariant <see cref="string.EndsWith(string)"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EndsWithF(this string? value, string suffix, bool ignoreCase = false) 
        => value?.EndsWith(suffix, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ?? false;
    /// <summary>
    /// Whether the string ends with <paramref name="suffix"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EndsWithF(this string? value, char suffix, bool ignoreCase = false) 
        => value?.EndsWith(suffix.ToString(), ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ?? false;

    /// <summary>
    /// Case-insensitive <see cref="string.Contains(string)"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsIgnoreCase(this string value, string subString) 
        => value.IndexOf(subString, StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>
    /// Culture-invariant <see cref="string.IndexOf(string)"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfF(this string value, string subString, bool ignoreCase = false) 
        => value.IndexOf(subString, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    /// <summary>
    /// Culture-invariant <see cref="string.IndexOf(string, int)"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfF(this string value, string subString, int startIndex, bool ignoreCase = false) 
        => value.IndexOf(subString, startIndex, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// Culture-invariant <see cref="string.LastIndexOf(string)"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LastIndexOfF(this string value, string subString, bool ignoreCase = false) 
        => value.LastIndexOf(subString, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    /// <summary>
    /// Culture-invariant <see cref="string.LastIndexOf(string, int)"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LastIndexOfF(this string value, string subString, int startIndex, bool ignoreCase = false) 
        => value.LastIndexOf(subString, startIndex, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// Shorthand for <see cref="string.ToLowerInvariant"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Lower(this string str) 
        => str.ToLowerInvariant();
    /// <summary>
    /// Shorthand for <see cref="string.ToUpperInvariant"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Upper(this string str) 
        => str.ToUpperInvariant();

    #endregion

    #region Split

    /// <summary>
    /// Splits the string.
    /// Returns {""} if the original string is empty.
    /// </summary>
    public static string[] Split(this string fullStr, string splitStr, bool removeEmptyEntries = false) {
        if (splitStr.IsSingle()) {
            return fullStr.Split(splitStr[0], removeEmptyEntries);
        } else {
            return fullStr.Split([splitStr], removeEmptyEntries);
        }
    }
    /// <summary>
    /// Splits the string.
    /// Returns {""} if the original string is empty.
    /// </summary>
    public static string[] Split(this string fullStr, char splitChar, bool removeEmptyEntries = false) 
        => fullStr.Split([splitChar], removeEmptyEntries);
    /// <summary>
    /// Splits the string.
    /// Returns {""} if the original string is empty.
    /// </summary>
    public static string[] Split(this string fullStr, string[] splitStrings, bool removeEmptyEntries = false)
        => fullStr.Split(splitStrings, removeEmptyEntries ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None);
    /// <summary>
    /// Splits the string.
    /// Returns {""} if the original string is empty.
    /// </summary>
    public static string[] Split(this string fullStr, char[] splitChars, bool removeEmptyEntries = false)
        => fullStr.Split(splitChars, removeEmptyEntries ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None);

    /// <summary>
    /// Splits the string into lines.
    /// </summary>
    public static string[] SplitLines(this string input, bool skipEmptyLines = false)
        => input.ReplaceLineEndings("\n").Split('\n', skipEmptyLines);

    #endregion

    #region Trimming

    /// <summary>
    /// Gets the part before the first occurrence of the substring; returns the string unchanged if not found.
    /// <code>"2024/11/08".BeforeFirst("/") => "2024"</code>
    /// </summary>
    public static string BeforeFirst(this string str, string text, bool ignoreCase = false) {
        int pos = string.IsNullOrEmpty(text) ? -1 : str.IndexOfF(text, ignoreCase);
        if (pos < 0) return str;
        return str[..pos];
    }
    /// <summary>
    /// Gets the part before the first occurrence of any of the substrings; returns the string unchanged if none is found.
    /// <code>"2024/11-08".BeforeFirstOfAny(["/", "-"]) => "2024"</code>
    /// </summary>
    public static string BeforeFirstOfAny(this string str, IEnumerable<string>? texts, bool ignoreCase = false) {
        if (texts == null) return str;
        int pos = -1;
        foreach (string text in texts) {
            if (string.IsNullOrEmpty(text)) continue;
            int p = str.IndexOfF(text, ignoreCase);
            if (p >= 0 && (pos < 0 || pos > p)) pos = p;
        }
        if (pos < 0) return str;
        return str[..pos];
    }

    /// <summary>
    /// Gets the part before the last occurrence of the substring; returns the string unchanged if not found.
    /// <code>"2024/11/08".BeforeLast("/") => "2024/11"</code>
    /// </summary>
    public static string BeforeLast(this string str, string text, bool ignoreCase = false) {
        int pos = string.IsNullOrEmpty(text) ? -1 : str.LastIndexOfF(text, ignoreCase);
        if (pos < 0) return str;
        return str[..pos];
    }
    /// <summary>
    /// Gets the part before the last occurrence of any of the substrings; returns the string unchanged if none is found.
    /// <code>"2024/11-08".BeforeLastOfAny(["/", "-"]) => "2024/11"</code>
    /// </summary>
    public static string BeforeLastOfAny(this string str, IEnumerable<string>? texts, bool ignoreCase = false) {
        if (texts == null) return str;
        int pos = -1;
        foreach (string text in texts) {
            if (string.IsNullOrEmpty(text)) continue;
            int p = str.LastIndexOfF(text, ignoreCase);
            if (p >= 0 && (pos < 0 || pos < p)) pos = p;
        }
        if (pos < 0) return str;
        return str[..pos];
    }

    /// <summary>
    /// Gets the part after the first occurrence of the substring; returns the string unchanged if not found.
    /// <code>"2024/11/08".AfterFirst("/") => "11/08"</code>
    /// </summary>
    public static string AfterFirst(this string str, string? text, bool ignoreCase = false) {
        int pos = string.IsNullOrEmpty(text) ? -1 : str.IndexOfF(text!, ignoreCase);
        if (pos < 0) return str;
        return str[(pos + text!.Length)..];
    }
    /// <summary>
    /// Gets the part after the first occurrence of any of the substrings; returns the string unchanged if none is found.
    /// <code>"2024/11-08".AfterFirstOfAny(["/", "-"]) => "11-08"</code>
    /// </summary>
    public static string AfterFirstOfAny(this string str, IEnumerable<string>? texts, bool ignoreCase = false) {
        if (texts == null) return str;
        int pos = -1;
        int len = 0;
        foreach (string text in texts) {
            if (string.IsNullOrEmpty(text)) continue;
            int p = str.IndexOfF(text, ignoreCase);
            if (p >= 0 && (pos < 0 || pos > p)) {
                pos = p;
                len = text.Length;
            }
        }
        if (pos < 0) return str;
        return str[(pos + len)..];
    }

    /// <summary>
    /// Gets the part after the last occurrence of the substring; returns the string unchanged if not found.
    /// <code>"2024/11/08".AfterLast("/") => "08"</code>
    /// </summary>
    public static string AfterLast(this string str, string? text, bool ignoreCase = false) {
        int pos = string.IsNullOrEmpty(text) ? -1 : str.LastIndexOfF(text!, ignoreCase);
        if (pos < 0) return str;
        return str[(pos + text!.Length)..];
    }
    /// <summary>
    /// Gets the part after the last occurrence of any of the substrings; returns the string unchanged if none is found.
    /// <code>"2024/11-08".AfterLastOfAny(["/", "-"]) => "08"</code>
    /// </summary>
    public static string AfterLastOfAny(this string str, IEnumerable<string>? texts, bool ignoreCase = false) {
        if (texts == null) return str;
        int pos = -1;
        int len = 0;
        foreach (string text in texts) {
            if (string.IsNullOrEmpty(text)) continue;
            int p = str.LastIndexOfF(text, ignoreCase);
            if (p >= 0 && (pos < 0 || pos < p)) {
                pos = p;
                len = text.Length;
            }
        }
        if (pos < 0) return str;
        return str[(pos + len)..];
    }

    /// <summary>
    /// Gets the part between two substrings, trimming as much as possible.
    /// Equivalent to AfterLast followed by BeforeFirst.
    /// Returns the string unchanged if a substring is not found.
    /// </summary>
    public static string Between(this string str, string after, string before, bool ignoreCase = false) {
        int startPos = string.IsNullOrEmpty(after) ? -1 : str.LastIndexOfF(after, ignoreCase);
        if (startPos >= 0) {
            startPos += after.Length;
        } else {
            startPos = 0;
        }

        int endPos = string.IsNullOrEmpty(before) ? -1 : str.IndexOfF(before, startPos, ignoreCase);
        if (endPos >= 0) {
            return str[startPos..endPos];
        } else if (startPos > 0) {
            return str[startPos..];
        } else {
            return str;
        }
    }

    #endregion

    #region Replacement

    /// <summary>
    /// Replaces content in the string.
    /// <paramref name="newValueGetter"/> is called to get the replacement only when a replacement is actually needed.
    /// </summary>
    public static string Replace(this string str, string oldValue, Func<string> newValueGetter) 
        => str.Contains(oldValue) ? str.Replace(oldValue, newValueGetter()) : str;

    /// <summary>
    /// Replaces all line breaks in the string with the specified string.
    /// If <paramref name="mergeMultiple"/> is set, consecutive line breaks are replaced by a single occurrence of the target.
    /// </summary>
    public static string ReplaceLineEndings(this string input, string newValue, bool mergeMultiple = false) 
        => (mergeMultiple ? regexLineEndingAndMerge : regexLineEnding).Replace(input, newValue.Replace("$", "$$")); // Escape $ so it is not treated as a capture group reference
    private static readonly Regex regexLineEndingAndMerge = new(@"(?:\r\n|[\n\r\f\u0085\u2028\u2029])+", RegexOptions.Compiled);
    private static readonly Regex regexLineEnding = new(@"\r\n|[\n\r\f\u0085\u2028\u2029]", RegexOptions.Compiled);

    #endregion

    #region Regex

    /// <summary>
    /// Finds all regex matches in the string.
    /// </summary>
    public static IEnumerable<string> RegexSearch(this string str, string pattern, RegexOptions options = RegexOptions.None) {
        try {
            return Regex.Matches(str, pattern, options).Cast<Match>().Select(m => m.Value);
        } catch (Exception ex) {
            Logger.Warn(ex, $"{nameof(RegexSearch)} 失败（{pattern}）");
            return [];
        }
    }

    /// <summary>
    /// Gets the first regex match in the string, or <see langword="null"/> if there is no match or the match fails.
    /// </summary>
    public static string? RegexSeek(this string str, string pattern, RegexOptions options = RegexOptions.None) {
        try {
            var match = Regex.Match(str, pattern, options);
            return match.Success ? match.Value : null;
        } catch (Exception ex) {
            Logger.Warn(ex, $"{nameof(RegexSeek)} 失败（{pattern}）");
            return null;
        }
    }

    /// <summary>
    /// Whether the string matches the regex pattern.
    /// </summary>
    public static bool RegexCheck(this string str, string pattern, RegexOptions options = RegexOptions.None) {
        try {
            return Regex.IsMatch(str, pattern, options);
        } catch (Exception ex) {
            Logger.Warn(ex, $"{nameof(RegexCheck)} 失败（{pattern}）");
            return false;
        }
    }

    /// <summary>
    /// Performs a regex replacement.
    /// Throws on failure.
    /// </summary>
    public static string RegexReplace(this string allContents, string searchRegex, string replaceTo, RegexOptions options = RegexOptions.None) {
        return Regex.Replace(allContents, searchRegex, replaceTo, options);
    }

    /// <summary>
    /// Replaces each regex match separately.
    /// Throws on failure.
    /// </summary>
    public static string RegexReplace(this string allContents, string searchRegex, MatchEvaluator replaceTo, RegexOptions options = RegexOptions.None) {
        return Regex.Replace(allContents, searchRegex, replaceTo, options);
    }

    #endregion

    /// <summary>
    /// Reads the content of a <see cref="Stream"/> and decodes it into a string.
    /// Resets the stream position to the beginning first.
    /// </summary>
    public static string ReadString(this Stream stream, Encoding? encoding = null) {
        using var memoryStream = new MemoryStream();
        if (stream.CanSeek && stream.Position != 0) stream.Seek(0, SeekOrigin.Begin);
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray().GetString(encoding);
    }

    /// <summary>
    /// Decodes <paramref name="bytes"/> into a string.
    /// </summary>
    public static string GetString(this byte[] bytes, Encoding? encoding = null) {
        if (encoding is not null) return encoding.GetString(bytes);
        int length = bytes.Length;
        // Detect encoding by BOM
        if (length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) {
            return Encoding.UTF8.GetString(bytes, 3, length - 3); // UTF8 with BOM
        } else if (length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, length - 2);
        } else if (length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) {
            return Encoding.Unicode.GetString(bytes, 2, length - 2);
        }
        // Files without BOM: GB18030 (ANSI) or UTF8
        try {
            return new UTF8Encoding(false, true).GetString(bytes); // UTF8 without BOM
        } catch (DecoderFallbackException) {
            return Encoding.GetEncoding("GB18030").GetString(bytes);
        }
    }

    /// <summary>
    /// Converts the first character to upper case and the rest to lower case.
    /// </summary>
    public static string Capitalize(this string word) {
        if (string.IsNullOrEmpty(word)) return word;
        return $"{word[..1].Upper()}{word[1..].Lower()}";
    }

    /// <summary>
    /// Normalizes the string to the given length.
    /// Pads it on the left with <paramref name="code"/> if too short, or keeps the leftmost characters if too long.
    /// </summary>
    public static string EnsureLength(this string? str, char code, int length) {
        str ??= "";
        return str.Length > length ? str[..length] : str.PadLeft(length, code);
    }

    /// <summary>
    /// Whether all characters in the string are ASCII.
    /// </summary>
    public static bool IsAsciiOnly(this string input) 
        => input.All(c => c < 128);

    /// <summary>
    /// Computes a hash of the string that stays consistent across environments.
    /// </summary>
    public static ulong GetStableHashCode(this string str) {
        ulong result = 5381;
        foreach (char v in str) result = (result << 5) ^ result ^ v;
        return result ^ 0xA98F501BC684032FUL;
    }

    /// <summary>
    /// Parses the string into a JSON object, throwing on failure.
    /// </summary>
    public static JToken? DeserializeJson(this string data) {
        try {
            return JsonConvert.DeserializeObject<JToken>(data, new JsonSerializerSettings {DateTimeZoneHandling = DateTimeZoneHandling.Local});
        } catch (Exception ex) {
            int length = (data ?? "").Length;
            throw new FormatException("JSON deserialize failed: " + 
                (length > 2000
                    ? $"{data![..500]}...(total {length} characters)...{data[^500..]}"
                    : (data ?? "")), ex);
        }
    }
    /// <summary>
    /// Parses the string into a JSON object, throwing on failure.
    /// </summary>
    public static T? DeserializeJson<T>(this string data) where T : JToken 
        => (T?) DeserializeJson(data);

}
