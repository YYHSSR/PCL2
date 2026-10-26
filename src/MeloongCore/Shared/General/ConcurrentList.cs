namespace MeloongCore;

/// <summary>
/// A thread-safe <see cref="List{T}"/> implementation using a coarse-grained lock.
/// <see cref="ConcurrentBag{T}"/> or other thread-safe collections are usually preferable, as they perform better.
/// </summary>
public class ConcurrentList<T> : IList<T>, IList {

    private readonly List<T> _items = [];

    // Constructors
    public ConcurrentList() { }
    public ConcurrentList(IEnumerable<T> data) => _items = new(data);
    public static implicit operator ConcurrentList<T>(List<T> data) => new(data);
    public static implicit operator List<T>(ConcurrentList<T> data) { lock (data.SyncRoot) { return new(data._items); } }

    // Enumerator
    /// <summary>
    /// Returns a copy of this list.
    /// </summary>
    public IEnumerator<T> GetEnumerator() { lock (SyncRoot) return _items.ToList().GetEnumerator(); }
    /// <summary>
    /// Returns a copy of this list.
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator() { lock (SyncRoot) return _items.ToList().GetEnumerator(); }

    // Members
    public int Count { get { lock (SyncRoot) { return _items.Count; } } }
    public bool IsReadOnly => false;
    bool IList.IsFixedSize => false;
    bool ICollection.IsSynchronized => true;
    public readonly object SyncRoot = new();
    object ICollection.SyncRoot => SyncRoot;

    public T this[int index] {
        get { lock (SyncRoot) { return _items[index]; } }
        set { lock (SyncRoot) { _items[index] = value; } }
    }
    object? IList.this[int index] {
        get => this[index];
        set { lock (SyncRoot) { _items[index] = (T) value!; } }
    }

    public void Add(T item) { lock (SyncRoot) { _items.Add(item); } }
    int IList.Add(object? value) { lock (SyncRoot) { _items.Add((T) value!); return _items.Count - 1; } }

    public void Insert(int index, T item) { lock (SyncRoot) { _items.Insert(index, item); } }
    void IList.Insert(int index, object? value) { lock (SyncRoot) { _items.Insert(index, (T) value!); } }

    public bool Remove(T item) { lock (SyncRoot) { return _items.Remove(item); } }
    void IList.Remove(object? value) { lock (SyncRoot) { if (value is T t) _items.Remove(t); } }

    public void RemoveAt(int index) { lock (SyncRoot) { _items.RemoveAt(index); } }
    public void Clear() { lock (SyncRoot) { _items.Clear(); } }

    public bool Contains(T item) { lock (SyncRoot) { return _items.Contains(item); } }
    bool IList.Contains(object? value) { lock (SyncRoot) { return value is T t && _items.Contains(t); } }

    public int IndexOf(T item) { lock (SyncRoot) { return _items.IndexOf(item); } }
    int IList.IndexOf(object? value) { lock (SyncRoot) { return value is T t ? _items.IndexOf(t) : -1; } }

    public void CopyTo(T[] array, int arrayIndex) { lock (SyncRoot) { _items.CopyTo(array, arrayIndex); } }
    void ICollection.CopyTo(Array array, int index) { lock (SyncRoot) { ((ICollection) _items).CopyTo(array, index); } }

}