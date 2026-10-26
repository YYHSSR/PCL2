namespace MeloongCore;
public readonly struct OneOf<T1, T2> {
    private readonly int _index = -1; // -1 means uninitialized (this is a struct, so this keeps the default value from being 0)
    private readonly T1? _value1;
    private readonly T2? _value2;
    public static implicit operator OneOf<T1, T2>(T1 value) => new(1, value, default);
    public static implicit operator OneOf<T1, T2>(T2 value) => new(2, default, value);
    public static implicit operator T1(OneOf<T1, T2> value) => value.As<T1>();
    public static implicit operator T2(OneOf<T1, T2> value) => value.As<T2>();
    private OneOf(int index, T1? value1, T2? value2) {
        _index = index;
        _value1 = value1;
        _value2 = value2;
    }

    /// <summary>
    /// Performs a different action for each type and returns a single value.
    /// </summary>
    public TResult Switch<TResult>(Func<T1, TResult> f1, Func<T2, TResult> f2) => _index switch {
        1 => f1(_value1!), 
        2 => f2(_value2!),
        _ => throw new InvalidOperationException()
    };
    /// <summary>
    /// Performs a different action for each type.
    /// </summary>
    public void Switch(Action<T1> f1, Action<T2> f2) {
        if (_index == 1) f1(_value1!);
        else if (_index == 2) f2(_value2!);
        else throw new InvalidOperationException();
    }
    /// <summary>
    /// Whether the current type is T.
    /// </summary>
    public bool Is<T>() => _index switch {
        1 when typeof(T) == typeof(T1) => true, 
        2 when typeof(T) == typeof(T2) => true, 
        _ => false
    };
    public bool IsT1() => _index == 1;
    public bool IsT2() => _index == 2;
    /// <summary>
    /// Assumes the current type is T and returns the value.
    /// Throws <see cref="InvalidOperationException"/> if the current value is not of type T.
    /// </summary>
    public T As<T>() => _index switch {
        1 when typeof(T) == typeof(T1) => (T)(object)_value1!, 
        2 when typeof(T) == typeof(T2) => (T)(object)_value2!, 
        _ => throw new InvalidOperationException()
    };
    public T1 AsT1() => _index switch { 1 => _value1!, _ => throw new InvalidOperationException() };
    public T2 AsT2() => _index switch { 2 => _value2!, _ => throw new InvalidOperationException() };
}

public readonly struct OneOf<T1, T2, T3> {
    private readonly int _index = -1; // -1 means uninitialized (this is a struct, so this keeps the default value from being 0)
    private readonly T1? _value1;
    private readonly T2? _value2;
    private readonly T3? _value3;
    public static implicit operator OneOf<T1, T2, T3>(T1 value) => new(1, value, default, default);
    public static implicit operator OneOf<T1, T2, T3>(T2 value) => new(2, default, value, default);
    public static implicit operator OneOf<T1, T2, T3>(T3 value) => new(3, default, default, value);
    public static implicit operator T1(OneOf<T1, T2, T3> value) => value.As<T1>();
    public static implicit operator T2(OneOf<T1, T2, T3> value) => value.As<T2>();
    public static implicit operator T3(OneOf<T1, T2, T3> value) => value.As<T3>();
    private OneOf(int index, T1? value1, T2? value2, T3? value3) {
        _index = index;
        _value1 = value1;
        _value2 = value2;
        _value3 = value3;
    }

    /// <summary>
    /// Performs a different action for each type and returns a single value.
    /// </summary>
    public TResult Switch<TResult>(Func<T1, TResult> f1, Func<T2, TResult> f2, Func<T3, TResult> f3) => _index switch {
        1 => f1(_value1!), 
        2 => f2(_value2!), 
        3 => f3(_value3!),
        _ => throw new InvalidOperationException()
    };
    /// <summary>
    /// Performs a different action for each type.
    /// </summary>
    public void Switch(Action<T1> f1, Action<T2> f2, Action<T3> f3) {
        if (_index == 1) f1(_value1!);
        else if (_index == 2) f2(_value2!);
        else if (_index == 3) f3(_value3!);
        else throw new InvalidOperationException();
    }
    /// <summary>
    /// Whether the current type is T.
    /// </summary>
    public bool Is<T>() => _index switch {
        1 when typeof(T) == typeof(T1) => true,
        2 when typeof(T) == typeof(T2) => true,
        3 when typeof(T) == typeof(T3) => true,
        _ => false
    };
    public bool IsT1() => _index == 1;
    public bool IsT2() => _index == 2;
    public bool IsT3() => _index == 3;
    /// <summary>
    /// Assumes the current type is T and returns the value.
    /// Throws <see cref="InvalidOperationException"/> if the current value is not of type T.
    /// </summary>
    public T As<T>() => _index switch {
        1 when typeof(T) == typeof(T1) => (T) (object) _value1!,
        2 when typeof(T) == typeof(T2) => (T) (object) _value2!,
        3 when typeof(T) == typeof(T3) => (T) (object) _value3!,
        _ => throw new InvalidOperationException()
    };
    public T1 AsT1() => _index switch { 1 => _value1!, _ => throw new InvalidOperationException() };
    public T2 AsT2() => _index switch { 2 => _value2!, _ => throw new InvalidOperationException() };
    public T3 AsT3() => _index switch { 3 => _value3!, _ => throw new InvalidOperationException() };
}
