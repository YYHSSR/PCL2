using System.Globalization;

namespace PCLCS;

public static class McUtils {

    /// <summary>
    /// Compares two version names; returns a positive number if the left is newer, 0 if equal, and a negative number if the right is newer.
    /// Cannot compare two pre-release versions.
    /// Supported formats: 未知版本, 26.1-snapshot-1, 1.13.2, 1.7.10-pre4, 1.8_pre, 1.14 Pre-Release 2, 1.14.4 C6
    /// </summary>
    public static int CompareVersion(string left, string right) {
        if (left == right) return 0;
        if (left == "未知版本" || right == "未知版本") return left == "未知版本" ? 1 : -1;
        left = left.Lower();
        right = right.Lower();
        var leftParts = left.Replace("快照", "snapshot").Replace("预览版", "pre").RegexSearch("[a-z]+|[0-9]+").ToList();
        var rightParts = right.Replace("快照", "snapshot").Replace("预览版", "pre").RegexSearch("[a-z]+|[0-9]+").ToList();
        for (int i = 0; ; i++) {
            if (i >= leftParts.Count && i >= rightParts.Count) return string.Compare(left, right, StringComparison.Ordinal); // Both sides are missing, probably the same thing
            string leftValue = i < leftParts.Count ? leftParts[i] : "0";
            string rightValue = i < rightParts.Count ? rightParts[i] : "0";
            if (leftValue == rightValue) continue;
            int leftNumber = leftValue switch {
                "rc" => -1,
                "pre" => -2,
                "snapshot" => -3,
                "experimental" => -4,
                _ => int.TryParse(leftValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int leftResult) ? leftResult : 0
            };
            int rightNumber = rightValue switch {
                "rc" => -1,
                "pre" => -2,
                "snapshot" => -3,
                "experimental" => -4,
                _ => int.TryParse(rightValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rightResult) ? rightResult : 0
            };
            if (leftNumber == 0 && rightNumber == 0) return string.Compare(leftValue, rightValue, StringComparison.Ordinal);
            if (leftNumber != rightNumber) return leftNumber.CompareTo(rightNumber);
        }
    }
    /// <summary>
    /// Compares two version names; returns True if the left is not older than the right.
    /// </summary>
    public static bool CompareVersionGE(string left, string right) => CompareVersion(left, right) >= 0;

}
