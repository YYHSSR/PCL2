namespace MeloongCore;

/// <summary>
/// An object that tracks task progress; the progress shown to the user is guaranteed to increase monotonically.
/// Supports retries, splitting into child progress, and marking part of the progress as skipped.
/// </summary>
public class ProgressProvider {

    /// <summary>
    /// Raised when the raw progress may have changed.
    /// </summary>
    public event Action<(double actual, double skiped)>? ProgressChanged;
    private event Action? ChildProgressChanged;
    private void InvokeProgressChanged() {
        ProgressChanged?.Invoke(GetRaw());
        ChildProgressChanged?.Invoke();
    }

    // ===================================== Main progress =====================================

    private (double actual, double skiped, double splited) progressParts = (0, 0, 0);
    private double progressSum => progressParts.actual + progressParts.skiped + progressParts.splited;

    private bool _Set(double value, bool skiped) {
        value = value.Clamp(0, 1);
        double delta = value - progressSum;
        if (delta < 0) { // Scale down proportionally
            progressParts = (progressParts.actual * value / progressSum,
                              progressParts.skiped * value / progressSum,
                              progressParts.splited * value / progressSum);
        } else if (skiped) {
            progressParts.skiped += delta;
        } else {
            progressParts.actual += delta;
        }
        return delta != 0;
    }

    /// <summary>
    /// Sets the current progress to the specified value.
    /// If <paramref name="skiped"/> is set, the increment is counted as skipped: the observed progress does not increase now, but will increase faster afterwards.
    /// </summary>
    public void Set(double value, bool skiped = false, ChildrenAction action = ChildrenAction.Finished) {
        bool changed;
        lock (this) {
            changed = _Set(value, skiped);
            changed |= AssumeChildrenAs(action);
        }
        if (changed) InvokeProgressChanged();
    }
    /// <summary>
    /// Increases the current progress by the specified value.
    /// If <paramref name="skiped"/> is set, the increment is counted as skipped: the observed progress does not increase now, but will increase faster afterwards.
    /// If <paramref name="markChildrenAsFinished"/> is set, all unfinished children are treated as finished.
    /// </summary>
    public void Add(double value, bool skiped = false, ChildrenAction action = ChildrenAction.Finished) {
        bool changed;
        lock (this) {
            changed = _Set(value + progressSum, skiped);
            changed |= AssumeChildrenAs(action);
        }
        if (changed) InvokeProgressChanged();
    }
    /// <summary>Finishes the remaining progress of this item.</summary>
    public void Finish(ChildrenAction action = ChildrenAction.Finished) => Set(1, skiped: false, action: action);
    /// <summary>Skips the remaining progress of this item.</summary>
    public void Skip(ChildrenAction action = ChildrenAction.Skiped) => Set(1, skiped: true, action: action);
    /// <summary>
    /// Resets the current progress and clears all children.
    /// </summary>
    public void Reset() {
        bool changed;
        lock (this) {
            changed = progressSum > 0;
            progressParts = (0, 0, 0);
            childrens.ForEach(c => c.child.ChildProgressChanged -= InvokeProgressChanged);
            childrens.Clear();
        }
        if (changed) InvokeProgressChanged();
    }

    // ===================================== Child progress =====================================

    private readonly List<(double percentage, ProgressProvider child)> childrens = [];

    /// <summary>
    /// Splits off a child: when it finishes, the progress increases from the current value to <paramref name="value"/>.
    /// </summary>
    public ProgressProvider SplitTo(double value) {
        lock (this) return SplitBy(value - progressSum).First();
    }
    /// <summary>
    /// Splits off multiple children, each taking the specified amount of progress.
    /// <para/>For example, <c>SplitBy(0.2, 0.3)</c> returns two children taking 20% and 30% of the total progress.
    /// <para/>If the total amount is > 1, the children's amounts are scaled down proportionally.
    /// </summary>
    public List<ProgressProvider> SplitBy(params double[] percentages) {
        lock (this) {
            if (percentages.Any(p => p <= 0)) throw new ArgumentException("子项进度必须为正数且");
            progressParts.splited += (percentages.Sum() + progressSum).Clamp(0, 1) - progressSum;
            return percentages.Select(percentage => {
                var sub = new ProgressProvider();
                sub.ChildProgressChanged += InvokeProgressChanged;
                childrens.Add((percentage, sub));
                return sub;
            }).ToList();
        }
    }

    public enum ChildrenAction {
        /// <summary>Does nothing.</summary>
        None,
        /// <summary>Treats the remaining progress of unfinished children as finished.</summary>
        Finished,
        /// <summary>Treats the remaining progress of unfinished children as skipped.</summary>
        Skiped
    }
    /// <summary>
    /// Modifies all unfinished children.
    /// </summary>
    /// <returns>Whether any child with progress below 1 was modified.</returns>
    private bool AssumeChildrenAs(ChildrenAction action) {
        if (action == ChildrenAction.None) return false;
        lock (this) {
            bool changed = false;
            if (childrens.Any(c => c.percentage > 0)) { // Add the children's progress to the main item
                var mult = progressParts.splited / childrens.Sum(c => c.percentage);
                childrens.ForEach(c => {
                    var (subActual, subSkiped) = c.child.GetRaw();
                    progressParts.actual += mult * c.percentage * (action == ChildrenAction.Finished ? (1 - subSkiped) : subActual);
                    progressParts.skiped += mult * c.percentage * (action == ChildrenAction.Skiped ? (1 - subActual) : subSkiped);
                    c.child.ChildProgressChanged -= InvokeProgressChanged;
                    if (subActual + subSkiped < 0.9999999) changed = true;
                });
            }
            progressParts.splited = 0;
            childrens.Clear();
            return changed;
        }
    }

    // ===================================== Observation =====================================

    /// <summary>
    /// Gets the actual total progress, including children.
    /// </summary>
    public (double actual, double skiped) GetRaw() {
        lock (this) {
            (double actual, double skiped) current = (progressParts.actual, progressParts.skiped);
            if (childrens.Any(c => c.percentage > 0)) { // Add the children's progress to the main item
                var mult = progressParts.splited / childrens.Sum(c => c.percentage);
                childrens.ForEach(c => {
                    var (subActual, subSkiped) = c.child.GetRaw();
                    current.actual += mult * c.percentage * subActual;
                    current.skiped += mult * c.percentage * subSkiped;
                });
            }
            return current;
        }
    }

}
