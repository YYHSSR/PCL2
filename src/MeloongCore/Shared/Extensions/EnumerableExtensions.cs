namespace MeloongCore.Extensions;
public static class EnumerableExtensions {

    #region Search

    /// <summary>
    /// Tries each condition in priority order and returns the first element matched by the first condition that has any match.
    /// Returns default if no condition matches. The sequence is re-enumerated for each condition tried.
    /// </summary>
    public static TSource? FirstOrDefault<TSource>(this IEnumerable<TSource> source, params Func<TSource, bool>[] predicates) {
        foreach (var predicate in predicates) {
            foreach (var item in source) {
                if (predicate(item)) return item;
            }
        }
        return default;
    }

    #endregion

    #region Deduplication

    /// <summary>
    /// Removes duplicates using a comparer.
    /// This is inefficient; use it only on small lists, or use DistinctBy instead.
    /// </summary>
    public static IEnumerable<T> Distinct<T>(this IList<T> list, Func<T, T, bool> comparer) {
        var seen = new List<T>();
        foreach (var item in list) {
            if (!seen.Any(s => comparer(s, item))) {
                seen.Add(item);
                yield return item;
            }
        }
    }
    /// <summary>
    /// Removes duplicates by the specified value of each object.
    /// </summary>
    public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector) {
        var seen = new HashSet<TKey>();
        foreach (var element in source) {
            if (seen.Add(selector(element))) yield return element;
        }
    }
    /// <summary>
    /// Removes duplicates by the specified value of each object.
    /// </summary>
    public static ParallelQuery<TSource> DistinctBy<TSource, TKey>(this ParallelQuery<TSource> source, Func<TSource, TKey> selector) {
        var seen = new ConcurrentDictionary<TKey, bool>();
        return source.Where(element => seen.TryAdd(selector(element), true));
    }

    #endregion

    #region Max / Min

    /// <summary>
    /// Selects all objects that have the maximum value.
    /// Returns an empty list if there are no elements.
    /// </summary>
    public static List<TSource> MaxByAll<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector) where TKey : IComparable<TKey> {
        var results = new List<TSource>();
        using var enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext()) return results;
        TSource maxItem = enumerator.Current;
        TKey maxValue = selector(maxItem);
        results.Add(maxItem);
        while (enumerator.MoveNext()) {
            TSource currentItem = enumerator.Current;
            TKey currentValue = selector(currentItem);
            int comparisonResult = currentValue.CompareTo(maxValue);
            if (comparisonResult > 0) {
                maxValue = currentValue;
                results.Clear();
                results.Add(currentItem);
            } else if (comparisonResult == 0) {
                results.Add(currentItem);
            }
        }
        return results;
    }
    /// <summary>
    /// Selects all objects that have the maximum value.
    /// Returns an empty list if there are no elements.
    /// </summary>
    public static List<TSource> MaxByAll<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector, IComparer<TKey> comparer) {
        var results = new List<TSource>();
        using var enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext()) return results;
        TSource maxItem = enumerator.Current;
        TKey maxValue = selector(maxItem);
        results.Add(maxItem);
        while (enumerator.MoveNext()) {
            TSource currentItem = enumerator.Current;
            TKey currentValue = selector(currentItem);
            int comparisonResult = comparer.Compare(currentValue, maxValue);
            if (comparisonResult > 0) {
                maxValue = currentValue;
                results.Clear();
                results.Add(currentItem);
            } else if (comparisonResult == 0) {
                results.Add(currentItem);
            }
        }
        return results;
    }
    /// <summary>
    /// Selects all objects that have the minimum value.
    /// Returns an empty list if there are no elements.
    /// </summary>
    public static List<TSource> MinByAll<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector) where TKey : IComparable<TKey> {
        var results = new List<TSource>();
        using var enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext()) return results;
        TSource minItem = enumerator.Current;
        TKey minValue = selector(minItem);
        results.Add(minItem);
        while (enumerator.MoveNext()) {
            TSource currentItem = enumerator.Current;
            TKey currentValue = selector(currentItem);
            int comparisonResult = currentValue.CompareTo(minValue);
            if (comparisonResult < 0) {
                minValue = currentValue;
                results.Clear();
                results.Add(currentItem);
            } else if (comparisonResult == 0) {
                results.Add(currentItem);
            }
        }
        return results;
    }
    /// <summary>
    /// Selects all objects that have the minimum value.
    /// Returns an empty list if there are no elements.
    /// </summary>
    public static List<TSource> MinByAll<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector, IComparer<TKey> comparer) {
        var results = new List<TSource>();
        using var enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext()) return results;
        TSource minItem = enumerator.Current;
        TKey minValue = selector(minItem);
        results.Add(minItem);
        while (enumerator.MoveNext()) {
            TSource currentItem = enumerator.Current;
            TKey currentValue = selector(currentItem);
            int comparisonResult = comparer.Compare(currentValue, minValue);
            if (comparisonResult < 0) {
                minValue = currentValue;
                results.Clear();
                results.Add(currentItem);
            } else if (comparisonResult == 0) {
                results.Add(currentItem);
            }
        }
        return results;
    }

    /// <summary>
    /// Selects the object that has the maximum value.
    /// Returns null if there are no elements.
    /// </summary>
    public static TSource? MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector) where TKey : IComparable<TKey> {
        using var enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext()) return default;
        TSource maxItem = enumerator.Current;
        TKey maxValue = selector(maxItem);
        while (enumerator.MoveNext()) {
            TKey value = selector(enumerator.Current);
            if (value.CompareTo(maxValue) <= 0) continue;
            maxItem = enumerator.Current;
            maxValue = value;
        }
        return maxItem;
    }
    /// <summary>
    /// Selects the object that has the maximum value.
    /// Returns null if there are no elements.
    /// </summary>
    public static TSource? MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector, IComparer<TKey> comparer) {
        using var enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext()) return default;
        TSource maxItem = enumerator.Current;
        TKey maxValue = selector(maxItem);
        while (enumerator.MoveNext()) {
            TKey value = selector(enumerator.Current);
            if (comparer.Compare(value, maxValue) <= 0) continue;
            maxItem = enumerator.Current;
            maxValue = value;
        }
        return maxItem;
    }
    /// <summary>
    /// Selects the object that has the minimum value.
    /// Returns null if there are no elements.
    /// </summary>
    public static TSource? MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector) where TKey : IComparable<TKey> {
        using var enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext()) { return default; }
        TSource minItem = enumerator.Current;
        TKey minValue = selector(minItem);
        while (enumerator.MoveNext()) {
            TKey value = selector(enumerator.Current);
            if (value.CompareTo(minValue) >= 0) { continue; }
            minItem = enumerator.Current;
            minValue = value;
        }
        return minItem;
    }
    /// <summary>
    /// Selects the object that has the minimum value.
    /// Returns null if there are no elements.
    /// </summary>
    public static TSource? MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector, IComparer<TKey> comparer) {
        using var enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext()) { return default; }
        TSource minItem = enumerator.Current;
        TKey minValue = selector(minItem);
        while (enumerator.MoveNext()) {
            TKey value = selector(enumerator.Current);
            if (comparer.Compare(value, minValue) >= 0) { continue; }
            minItem = enumerator.Current;
            minValue = value;
        }
        return minItem;
    }

    #endregion

    #region Sorting

    /// <summary>
    /// Sorts the collection in ascending order using the default comparer.
    /// </summary>
    public static IEnumerable<T> Ordered<T>(this IEnumerable<T> source) where T : IComparable<T> 
        => source.OrderBy(x => x);

    /// <summary>
    /// Sorts the collection in descending order using the default comparer.
    /// </summary>
    public static IEnumerable<T> OrderedDescending<T>(this IEnumerable<T> source) where T : IComparable<T> 
        => source.OrderByDescending(x => x);

    #endregion

    #region Join

    /// <summary>
    /// Joins the collection into a string with the specified separator.
    /// </summary>
    public static string Join(this IEnumerable list, char split) {
        var builder = new StringBuilder();
        bool isFirst = true;
        foreach (var element in list) {
            if (isFirst)
                isFirst = false;
            else
                builder.Append(split);
            if (element is not null) builder.Append(element.ToString());
        }
        return builder.ToString();
    }
    /// <summary>
    /// Joins the collection into a string with the specified separator.
    /// </summary>
    public static string Join(this IEnumerable list, string split) {
        if (split.IsSingle()) return list.Join(split[0]);
        var builder = new StringBuilder();
        bool isFirst = true;
        foreach (var element in list) {
            if (isFirst)
                isFirst = false;
            else
                builder.Append(split);
            if (element is not null) builder.Append(element);
        }
        return builder.ToString();
    }

    #endregion

    #region Dictionary

    /// <summary>
    /// Removes the element with the specified key from a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// Returns whether an element was actually removed.
    /// </summary>
    public static bool Remove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dict, TKey key) 
        => dict.TryRemove(key, out _);

    /// <summary>
    /// Converts a two-dimensional array into a dictionary.
    /// </summary>
    public static Dictionary<T, T> ToDictionary<T>(this T[,] arr) where T : notnull {
        Dictionary<T, T> result = [];
        if (arr.Length == 0) return result;
        if (arr.GetLength(1) != 2) throw new ArgumentException("数组必须为两列，第一列为 Key，第二列为 Value。");
        for (int i = 0; i < arr.GetLength(0); i++) result[arr[i, 0]] = arr[i, 1];
        return result;
    }

    /// <summary>
    /// Tries to get an item from the dictionary, returning the default value if it does not exist.
    /// </summary>
    public static TValue? GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue? defaultValue = default) 
        => dict.TryGetValue(key, out var result) ? result : defaultValue;

    /// <summary>
    /// Adds a value to the list stored under the corresponding key of the dictionary.
    /// </summary>
    public static void AddIntoValueCollection<TKey, TValue>(this IDictionary<TKey, List<TValue>> dict, TKey key, TValue value) {
        if (dict.TryGetValue(key, out var collection)) {
            collection.Add(value);
        } else {
            dict.Add(key, [value]);
        }
    }

    #endregion

    /// <summary>
    /// Unpacks a <see cref="FlagsAttribute"/> enum value into the set of single-flag members it contains.
    /// Recommended only for positive enums without extreme values.
    /// </summary>
    /// <remarks>
    /// Skips: 0, values that are not powers of 2, numbers not defined in the enum, combined values, and multiple aliases of the same member.
    /// </remarks>
    public static IEnumerable<T> Flags<T>(this T value) where T : struct, Enum
        => EnumUtils.GetAllFlags<T>().Where(element => value.HasFlag(element));

    /// <summary>
    /// Removes the elements that satisfy the condition from the collection.
    /// </summary>
    public static void RemoveIf<T>(this ICollection<T> source, Func<T, bool> predicate) {
        var itemsToRemove = source.Where(predicate).ToList();
        foreach (var item in itemsToRemove) source.Remove(item);
    }

    /// <summary>
    /// Removes all elements of another collection from the collection.
    /// </summary>
    public static void RemoveAll<T>(this ICollection<T> source, IEnumerable<T> itemsToRemove) {
        foreach (var item in itemsToRemove) source.Remove(item);
    }

    /// <summary>
    /// Keeps only the elements that satisfy the condition and removes the rest.
    /// </summary>
    public static void KeepIf<T>(this ICollection<T> source, Func<T, bool> predicate) {
        var itemsToRemove = source.Where(item => !predicate(item)).ToList();
        foreach (var item in itemsToRemove) source.Remove(item);
    }

    /// <summary>
    /// Removes the specified element from the collection.
    /// </summary>
    public static IEnumerable<T> Except<T>(this IEnumerable<T> source, T element)
        => source.Except([element]);

    /// <summary>
    /// Whether the collection has exactly one element.
    /// Returns false for null input.
    /// </summary>
    public static bool IsSingle<T>(this IEnumerable<T>? source) {
        if (source is null) return false;
        if (source is IList<T> list) return list.Count == 1;
        using var enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext()) return false;
        if (enumerator.MoveNext()) return false;
        return true;
    }

    /// <summary>
    /// Performs the specified action on each element of the collection.
    /// </summary>
    public static IEnumerable<T> ForAll<T>(this IEnumerable<T> source, Action<T> action) {
        foreach (T item in source) action(item);
        return source;
    }

}
