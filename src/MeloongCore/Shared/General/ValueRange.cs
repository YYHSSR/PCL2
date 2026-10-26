namespace MeloongCore;

/// <summary>
/// A range made up of an optional lower bound and an optional upper bound.
/// </summary>
public sealed class ValueRange<T>(
    T? lower, T? upper,
    bool isLowerInclusive, bool isUpperInclusive,
    bool hasLower, bool hasUpper
) : IEquatable<ValueRange<T>?> where T : IComparable<T> {

    #region Basic fields

    public T? Lower { get; private set; } = hasLower && lower is null ? throw new ArgumentNullException(nameof(lower)) : lower;
    public T? Upper { get; private set; } = hasUpper && upper is null ? throw new ArgumentNullException(nameof(upper)) : upper;

    public bool HasLower { get; private set; } = hasLower;
    public bool HasUpper { get; private set; } = hasUpper;

    public void ClearLower() { Lower = default; HasLower = false; }
    public void ClearUpper() { Upper = default; HasUpper = false; }

    public void SetLower(T lower, bool isInclusive) {
        Lower = lower;
        HasLower = true;
        IsLowerInclusive = isInclusive;
    }
    public void SetUpper(T upper, bool isInclusive) {
        Upper = upper;
        HasUpper = true;
        IsUpperInclusive = isInclusive;
    }

    /// <summary> Whether the lower bound is inclusive. </summary>
    public bool IsLowerInclusive = isLowerInclusive;
    /// <summary> Whether the upper bound is inclusive. </summary>
    public bool IsUpperInclusive = isUpperInclusive;

    #endregion

    #region ClosedOpen, AtLeast and other factory methods

    /// <summary> Creates a closed range equivalent to <c>[lower, upper]</c>. </summary>
    public static ValueRange<T> Closed(T lower, T upper) => new(lower, upper, true, true, true, true);
    /// <summary> Creates a half-open range of the form <c>[lower, upper)</c>. </summary>
    public static ValueRange<T> ClosedOpen(T lower, T upper) => new(lower, upper, true, false, true, true);
    /// <summary> Creates a half-open range of the form <c>(lower, upper]</c>. </summary>
    public static ValueRange<T> OpenClosed(T lower, T upper) => new(lower, upper, false, true, true, true);
    /// <summary> Creates an open range of the form <c>(lower, upper)</c>. </summary>
    public static ValueRange<T> Open(T lower, T upper) => new(lower, upper, false, false, true, true);
    /// <summary> Creates a single-value closed range of the form <c>[value, value]</c>. </summary>
    public static ValueRange<T> Exactly(T value) => new(value, value, true, true, true, true);

    /// <summary> Creates a range of the form <c>[lower, +∞)</c>. </summary>
    public static ValueRange<T> AtLeast(T lower) => new(lower, default, true, true, true, false);
    /// <summary> Creates a range of the form <c>(lower, +∞)</c>. </summary>
    public static ValueRange<T> GreaterThan(T lower) => new(lower, default, false, true, true, false);
    /// <summary> Creates a range of the form <c>(-∞, upper]</c>. </summary>
    public static ValueRange<T> AtMost(T upper) => new(default, upper, true, true, false, true);
    /// <summary> Creates a range of the form <c>(-∞, upper)</c>. </summary>
    public static ValueRange<T> LessThan(T upper) => new(default, upper, true, false, false, true);
    /// <summary> Creates an unbounded range of the form <c>(-∞, +∞)</c>. </summary>
    public static ValueRange<T> All() => new(default, default, true, true, false, false);

    #endregion

    #region Bidirectional string conversion

    /// <summary>
    /// Parses a <see cref="ValueRange{T}"/> instance from a string.
    /// </summary>
    public static ValueRange<T> FromString(string str, Func<string, T>? parseFunc = null) {
        if (string.IsNullOrEmpty(str)) throw new ArgumentException("不能为空");
        var isLowerInclusive = str[0] == '[';
        var isUpperInclusive = str[^1] == ']';
        if (!isLowerInclusive && str[0] != '(') throw new ArgumentException("开头必须是 [ 或 (");
        if (!isUpperInclusive && str[^1] != ')') throw new ArgumentException("结尾必须是 ] 或 )");
        var commaIndex = str.IndexOfAny([',', '，']);
        if (commaIndex == -1) throw new ArgumentException("需要用逗号分隔两个值");
        var lowerStr = str[1..commaIndex].Trim();
        var upperStr = str[(commaIndex + 1)..^1].Trim();
        T? lower = default;
        T? upper = default;
        bool hasLower = false;
        bool hasUpper = false;
        if (lowerStr is not ("" or "-∞")) {
            lower = parseFunc != null ? parseFunc(lowerStr) : (T) Convert.ChangeType(lowerStr, typeof(T));
            hasLower = true;
        }
        if (upperStr is not ("" or "+∞" or "∞")) {
            upper = parseFunc != null ? parseFunc(upperStr) : (T) Convert.ChangeType(upperStr, typeof(T));
            hasUpper = true;
        }
        return new(lower, upper, isLowerInclusive, isUpperInclusive, hasLower, hasUpper);
    }

    public override string ToString()
        => $"{(HasLower && IsLowerInclusive ? "[" : "(")}{(HasLower ? Lower!.ToString() : "-∞")}, {(HasUpper ? Upper!.ToString() : "+∞")}{(HasUpper && IsUpperInclusive ? "]" : ")")}";

    #endregion

    /// <summary>
    /// Whether the specified value is within this range.
    /// </summary>
    public bool Contains(T value) {
        if (HasLower) {
            var compareLower = value.CompareTo(Lower!);
            if (compareLower < 0) return false;
            if (compareLower == 0 && !IsLowerInclusive) return false;
        }
        if (HasUpper) {
            var compareUpper = value.CompareTo(Upper!);
            if (compareUpper > 0) return false;
            if (compareUpper == 0 && !IsUpperInclusive) return false;
        }
        return true;
    }

    /// <summary>
    /// Whether this range contains no values.
    /// </summary>
    public bool IsEmpty() {
        if (!HasLower || !HasUpper) return false;
        int compared = Lower!.CompareTo(Upper!);
        return compared > 0 || (compared == 0 && (!IsLowerInclusive || !IsUpperInclusive));
    }

    /// <summary>
    /// Gets the intersection of this range and another range.
    /// </summary>
    /// <returns> The intersection if the two ranges overlap; otherwise <see langword="null" />. </returns>
    public ValueRange<T> Intersect(ValueRange<T> other) {
        T? lower = default;
        T? upper = default;
        var isLowerInclusive = true;
        var isUpperInclusive = true;

        if (HasLower && other.HasLower) {
            switch (Lower!.CompareTo(other.Lower!)) {
                case > 0:
                    lower = Lower;
                    isLowerInclusive = IsLowerInclusive;
                    break;
                case < 0:
                    lower = other.Lower;
                    isLowerInclusive = other.IsLowerInclusive;
                    break;
                default:
                    lower = Lower;
                    isLowerInclusive = IsLowerInclusive && other.IsLowerInclusive;
                    break;
            }
        } else if (HasLower) {
            lower = Lower;
            isLowerInclusive = IsLowerInclusive;
        } else if (other.HasLower) {
            lower = other.Lower;
            isLowerInclusive = other.IsLowerInclusive;
        }

        if (HasUpper && other.HasUpper) {
            switch (Upper!.CompareTo(other.Upper!)) {
                case < 0:
                    upper = Upper;
                    isUpperInclusive = IsUpperInclusive;
                    break;
                case > 0:
                    upper = other.Upper;
                    isUpperInclusive = other.IsUpperInclusive;
                    break;
                default:
                    upper = Upper;
                    isUpperInclusive = IsUpperInclusive && other.IsUpperInclusive;
                    break;
            }
        } else if (HasUpper) {
            upper = Upper;
            isUpperInclusive = IsUpperInclusive;
        } else if (other.HasUpper) {
            upper = other.Upper;
            isUpperInclusive = other.IsUpperInclusive;
        }

        var anyHasLower = HasLower || other.HasLower;
        var anyHasUpper = HasUpper || other.HasUpper;
        return new(lower, upper, isLowerInclusive, isUpperInclusive, anyHasLower, anyHasUpper);
    }

    #region Equality (auto-generated)

    public override bool Equals(object? obj) => Equals(obj as ValueRange<T>);
    public bool Equals(ValueRange<T>? other) {
        return other is not null&&
               EqualityComparer<T?>.Default.Equals(Lower, other.Lower)&&
               EqualityComparer<T?>.Default.Equals(Upper, other.Upper)&&
               HasLower==other.HasLower&&
               HasUpper==other.HasUpper&&
               IsLowerInclusive==other.IsLowerInclusive&&
               IsUpperInclusive==other.IsUpperInclusive;
    }
    public override int GetHashCode() {
        int hashCode = 169504101;
        hashCode=hashCode*-1521134295+EqualityComparer<T?>.Default.GetHashCode(Lower);
        hashCode=hashCode*-1521134295+EqualityComparer<T?>.Default.GetHashCode(Upper);
        hashCode=hashCode*-1521134295+HasLower.GetHashCode();
        hashCode=hashCode*-1521134295+HasUpper.GetHashCode();
        hashCode=hashCode*-1521134295+IsLowerInclusive.GetHashCode();
        hashCode=hashCode*-1521134295+IsUpperInclusive.GetHashCode();
        return hashCode;
    }
#pragma warning disable CS8604 // Possible null reference argument.
    public static bool operator ==(ValueRange<T>? left, ValueRange<T>? right) => EqualityComparer<ValueRange<T>>.Default.Equals(left, right);
#pragma warning restore CS8604 // Possible null reference argument.
    public static bool operator !=(ValueRange<T>? left, ValueRange<T>? right) => !(left==right);

    #endregion

}