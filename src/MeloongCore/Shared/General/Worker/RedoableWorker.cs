namespace MeloongCore;

public interface IWorker {
    /// <summary>Runs the workload on a worker thread.</summary>
    void Start(CancellationToken cancellationToken = default, ProgressProvider? progressOverride = null);
    /// <summary>Cancels the current run.</summary>
    void Cancel();
    /// <summary>If currently running, waits for it to finish; otherwise returns immediately.</summary>
    /// <returns>True if it did not time out.</returns>
    bool WaitIfRunning(int millisecondsTimeout = -1, CancellationToken cancellationToken = default);
    /// <summary>If currently running, asynchronously waits for it to finish; otherwise returns immediately.</summary>
    /// <returns>True if it did not time out.</returns>
    Task<bool> WaitIfRunningAsync(int millisecondsTimeout = -1, CancellationToken cancellationToken = default);

    /// <summary>Raised when transitioning from idle to running.</summary>
    event Action? Started;
    /// <summary>Raised when a run ends and the worker becomes idle.</summary>
    event Action? Stopped;
    /// <summary>Raised when the workload completes successfully.</summary>
    public event Action? Succeeded;
    /// <summary>Raised when the workload fails; the argument is the exception that occurred.</summary>
    public event Action<Exception>? Failed;
    /// <summary>Raised when a run is canceled.</summary>
    event Action? Canceled;

    /// <summary>Whether a run is currently in progress.</summary>
    bool Running { get; }
    /// <summary>Whether there has ever been a run that was neither canceled nor failed.</summary>
    bool HasSucceeded { get; }
    /// <summary>Indicates that the last transition to idle was a normal completion rather than a cancellation or failure.</summary>
    bool LastSucceeded { get; }
    /// <summary>Progress of the current workload.</summary>
    ProgressProvider Progress { get; }
}

/// <summary>
/// A worker whose runs can be merged and restarted.
/// <para/> Calling <see cref="Start(CancellationToken)"/> while the workload is running cancels it and reruns it with the latest token; multiple calls still cause only one rerun.
/// </summary>
public abstract class RedoableWorkerBase<TOut>(Func<CancellationToken?, ProgressProvider?, TOut> workload, 
    ProgressProvider? progress = null, [CallerMemberName] string creatorMemberName = "") : IWorker {

    // ============================================ State and events ============================================

    /// <inheritdoc/>
    public bool Running { get { lock (this) return running; } }
    private bool running;
    /// <inheritdoc/>
    public event Action? Started;
    /// <inheritdoc/>
    public event Action? Stopped;

    /// <inheritdoc/>
    public bool HasSucceeded { get { lock (this) return hasSucceeded; } }
    private bool hasSucceeded;
    /// <inheritdoc/>
    public event Action? Succeeded;
    /// <inheritdoc/>
    public event Action<Exception>? Failed;
    /// <inheritdoc/>
    public event Action? Canceled;

    /// <inheritdoc/>
    public bool LastSucceeded { get { lock (this) return lastSucceeded; } }
    private bool lastSucceeded;

    /// <summary>Return value of the workload from the last run that was neither canceled nor failed.</summary>
    public TOut LastResult {
        get {
            lock (this) {
                if (typeof(TOut) == typeof(NoType)) throw new InvalidOperationException("该 Worker 没有返回值。");
                if (!hasSucceeded) throw new InvalidOperationException("从未成功完成过。");
                return lastResult == null ? default! : (TOut) lastResult;
            }
        }
    }
    private object? lastResult;

    /// <inheritdoc/>
    public ProgressProvider Progress { get; private set; } = progress ?? new ProgressProvider();

    private bool pendingRedo;

    // =========================================== Run and cancel ===========================================

    private CancellationTokenSource? realCts;
    private CancellationToken lastToken;
    private readonly ManualResetEventSlim idleEvent = new(initialState: true);

    /// <summary>Runs the workload on a worker thread. If already running, cancels the current workload and restarts it with the latest token.</summary>
    public void Start(CancellationToken cancellationToken = default, ProgressProvider? progressOverride = null) {
        // Take over the running state
        lock (this) {
            lastToken = cancellationToken;
            if (progressOverride != null) Progress = progressOverride;
            if (running) {
                Logger.Info($"{creatorMemberName}：接到重启请求");
                pendingRedo = true;
                realCts?.Cancel();
                return;
            }
            running = true; idleEvent.Reset();
            realCts = CancellationTokenSource.CreateLinkedTokenSource(lastToken);
        }
        var th = new Thread(_Start) { IsBackground = true, Name = $"W/{creatorMemberName}" };
        th.Start();
    }
    private void _Start() {
        Logger.Info($"{creatorMemberName}：运行开始");
        Started?.Invoke();
        while (true) {
            try {
                realCts!.Token.ThrowIfCancellationRequested();
                Progress?.Reset();
                TOut result = workload(realCts.Token, Progress); // The actual execution
                realCts.Token.ThrowIfCancellationRequested();
                lock (this) {
                    realCts?.Dispose();
                    if (pendingRedo) { // Pick up the restart request
                        pendingRedo = false;
                        realCts = CancellationTokenSource.CreateLinkedTokenSource(lastToken);
                        continue;
                    }
                    realCts = null;
                    lastSucceeded = true;
                    lastResult = result;
                    hasSucceeded = true;
                    running = false; idleEvent.Set();
                    Progress?.Finish();
                }
                Logger.Info($"{creatorMemberName}：运行成功");
                Succeeded?.Invoke();
            } catch (Exception ex) {
                if (!ex.IsCanceled())
                    Logger.Log(ex, $"{creatorMemberName}：运行失败{(pendingRedo ? "，但即将重启，或可忽略" : "")}", 
                        pendingRedo ? LogLevel.Warn : LogLevel.Error, LogBehavior.ToastIfDebug);
                lock (this) {
                    realCts?.Dispose();
                    if (pendingRedo) { // Pick up the restart request
                        Logger.Info($"{creatorMemberName}：重启开始");
                        pendingRedo = false;
                        realCts = CancellationTokenSource.CreateLinkedTokenSource(lastToken);
                        continue;
                    }
                    realCts = null;
                    lastSucceeded = false;
                    running = false; idleEvent.Set();
                    Progress?.Skip();
                }
                if (ex.IsCanceled()) {
                    Logger.Info($"{creatorMemberName}：运行已取消");
                    Canceled?.Invoke();
                } else {
                    Failed?.Invoke(ex);
                }
            }
            break;
        }
        Stopped?.Invoke();
    }

    /// <inheritdoc/>
    public void Cancel() {
        lock (this) {
            pendingRedo = false;
            Logger.Info($"{creatorMemberName}：接到取消请求");
            realCts?.Cancel();
        }
    }

    /// <inheritdoc/>
    public bool WaitIfRunning(int millisecondsTimeout = -1, CancellationToken cancellationToken = default)
        => idleEvent.Wait(millisecondsTimeout, cancellationToken);
    /// <inheritdoc/>
    public async Task<bool> WaitIfRunningAsync(int millisecondsTimeout = -1, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waitHandle = ThreadPool.RegisterWaitForSingleObject(idleEvent.WaitHandle,
            (_, timedOut) => completionSource.TrySetResult(!timedOut), null, millisecondsTimeout, executeOnlyOnce: true);
        using var cancellationRegistration = cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken));
        try {
            return await completionSource.Task.NoCapture();
        } finally {
            waitHandle.Unregister(null);
        }
    }

}

/// <inheritdoc />
public class RedoableWorker<TOut> : RedoableWorkerBase<TOut> {
    public RedoableWorker(Func<CancellationToken?, ProgressProvider?, TOut> workload, [CallerMemberName] string creatorMemberName = "")
        : base(workload, creatorMemberName: creatorMemberName) { }
    public RedoableWorker(Func<CancellationToken?, TOut> workload, [CallerMemberName] string creatorMemberName = "")
        : base((c, _) => workload(c), creatorMemberName: creatorMemberName) { }
    public RedoableWorker(Func<TOut> workload, [CallerMemberName] string creatorMemberName = "")
        : base((_, _) => workload(), creatorMemberName: creatorMemberName) { }
}

/// <inheritdoc />
public class RedoableWorker : RedoableWorkerBase<NoType?> {
    public RedoableWorker(Action<CancellationToken?, ProgressProvider?> workload, [CallerMemberName] string creatorMemberName = "")
        : base((c, p) => { workload(c, p); return null; }, creatorMemberName: creatorMemberName) { }
    public RedoableWorker(Action<CancellationToken?> workload, [CallerMemberName] string creatorMemberName = "")
        : base((c, _) => { workload(c); return null; }, creatorMemberName: creatorMemberName) { }
    public RedoableWorker(Action workload, [CallerMemberName] string creatorMemberName = "")
        : base((_, _) => { workload(); return null; }, creatorMemberName: creatorMemberName) { }
}

/// <summary>Indicates the absence of a type, for use as a generic argument.</summary>
public class NoType { }
