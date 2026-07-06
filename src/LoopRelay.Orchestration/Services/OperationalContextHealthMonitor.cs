using LoopRelay.Orchestration.Models;

namespace LoopRelay.Orchestration.Services;

/// <summary>
/// Pure classifier for the operational-context size health guard. Kept separate from the orchestrator so the
/// ratchet rule is unit-testable without driving a full transfer, and so the orchestrator only owns the (locked)
/// state — <see cref="OperationalContextHealth.GrowthStreak"/> is threaded back in as <c>previousStreak</c>.
/// </summary>
public static class OperationalContextHealthMonitor
{
    /// <summary>
    /// A single growth is noise; a SUSTAINED ratchet is the signal. Warn once the context has grown on this many
    /// consecutive transfers, so a normal fold-in (which usually grows a little then settles) does not cry wolf.
    /// </summary>
    public const int GrowthStreakWarningThreshold = 2;

    /// <summary>
    /// Classifies the newest operational-context size against the previous revision and the running growth streak.
    /// First transfer of a process's lifetime (<paramref name="previousSize"/> null) is a no-warning baseline.
    /// </summary>
    public static OperationalContextHealth Classify(int? previousSize, int newSize, int previousStreak)
    {
        if (previousSize is null)
        {
            return new OperationalContextHealth(newSize, PreviousSize: null, GrowthStreak: 0, Warning: false);
        }

        // Strict growth extends the ratchet; a plateau or shrink breaks it (equal is NOT growth).
        int streak = newSize > previousSize.Value ? previousStreak + 1 : 0;
        bool warning = streak >= GrowthStreakWarningThreshold;
        return new OperationalContextHealth(newSize, previousSize, streak, warning);
    }
}
