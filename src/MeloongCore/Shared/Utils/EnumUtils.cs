using System.Globalization;

namespace MeloongCore;
public static class EnumUtils {

    /// <summary>
    /// Gets all values of the enum.
    /// </summary>
    private static IEnumerable<T> GetAllValues<T>() where T : struct, Enum
        => Enum.GetValues(typeof(T)).Cast<T>();

    /// <summary>
    /// Converts a string to an enum.
    /// Accepts the member name or its ordinal; returns 0 for an empty string or <see langword="null"/>.
    /// Throws if the conversion fails.
    /// </summary>
    public static T FromString<T>(string? value) where T : struct, Enum {
        if (string.IsNullOrEmpty(value)) {
            return (T) Enum.ToObject(typeof(T), 0);
        } else if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var number)) {
            return (T) Enum.ToObject(typeof(T), Convert.ToInt64(number));
        } else {
            return (T) Enum.Parse(typeof(T), value, true);
        }
    }

    public static bool HasFlagF<T>(this T value, T flag) where T : struct, Enum
        => value.HasFlag(flag) && !EqualityComparer<T>.Default.Equals(flag, default);

    /// <summary>
    /// For enums with <see cref="FlagsAttribute"/>, returns all of their flag values.
    /// Recommended only for positive enums without extreme values.
    /// </summary>
    /// <remarks>
    /// Skips: 0, values that are not powers of 2, numbers not defined in the enum, combined values, and multiple aliases of the same member.
    /// </remarks>
    public static List<T> GetAllFlags<T>() where T : struct, Enum {
        if (allFlagsCache.TryGetValue(typeof(T), out var cached)) return cached.Cast<T>().ToList();
        if (!typeof(T).IsDefined(typeof(FlagsAttribute), inherit: false)) throw new ArgumentException($"枚举 {typeof(T).FullName} 没有定义 FlagsAttribute。");
        var flags = new List<T>();
        foreach (var element in EnumUtils.GetAllValues<T>().Distinct()) {
            var number = Convert.ToInt64(element, CultureInfo.InvariantCulture);
            if (number == 0 || (number & (number - 1)) != 0) continue; // Skip 0 and values that are not powers of 2
            flags.Add(element);
        }
        allFlagsCache[typeof(T)] = flags.Cast<Enum>().ToList();
        return flags;
    }
    private static readonly ConcurrentDictionary<Type, List<Enum>> allFlagsCache = new();

}
