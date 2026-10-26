namespace MeloongCore;

public enum RateLimitMode {
    /// <summary>Throttle: runs once immediately, then merges calls within a period into a single trailing run.</summary>
    ImmediateThenMerge,
    /// <summary>Debounce (leading): runs once immediately, then ignores further calls within a period.</summary>
    ImmediateThenIgnore,
    /// <summary>Debounce (trailing): does not run immediately; runs once after no new calls have arrived for a period.</summary>
    WaitUnillNoNewInvoke,
}

/// <summary>
/// Rate-limits high-frequency calls.
/// See: <see href="https://aldaviva.com/portfolio.html#ratelimiting" />
/// </summary>
public class RateLimitedWorker(Action workload, double minimalIntervalMs, RateLimitMode mode) : IDisposable {

    private readonly RateLimitedAction action = mode switch {
        RateLimitMode.ImmediateThenMerge => Throttler.Throttle(workload, TimeSpan.FromMilliseconds(minimalIntervalMs), leading: true, trailing: true),
        RateLimitMode.WaitUnillNoNewInvoke => Debouncer.Debounce(workload, TimeSpan.FromMilliseconds(minimalIntervalMs), leading: false, trailing: true),
        _ => Debouncer.Debounce(workload, TimeSpan.FromMilliseconds(minimalIntervalMs), leading: true, trailing: false)
    };

    public void Start() {
        if (!disposed) action.Invoke();
    }

    private bool disposed = false;
    public virtual void Dispose() {
        if (disposed) return;
        disposed = true;
        action.Dispose();
    }

}
