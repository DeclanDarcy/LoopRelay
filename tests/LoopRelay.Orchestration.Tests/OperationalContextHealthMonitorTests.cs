using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Orchestration.Tests;

/// <summary>
/// The operational-context size health guard's ratchet rule (Stage 2). A single growth is noise; a SUSTAINED
/// upward trend across transfers is the signal that the renewal-reward document is bloating and destabilizing the
/// cost-aware router. Pinned as a pure function so the rule is unit-testable without driving a full transfer.
/// </summary>
public sealed class OperationalContextHealthMonitorTests
{
    [Fact]
    public void First_transfer_is_a_baseline_with_no_warning()
    {
        OperationalContextHealth health = OperationalContextHealthMonitor.Classify(previousSize: null, newSize: 5_000, previousStreak: 0);

        Assert.Equal(5_000, health.Size);
        Assert.Null(health.PreviousSize);
        Assert.Equal(0, health.GrowthStreak);
        Assert.False(health.Warning);
    }

    [Fact]
    public void A_single_growth_increments_the_streak_but_does_not_warn()
    {
        OperationalContextHealth health = OperationalContextHealthMonitor.Classify(previousSize: 5_000, newSize: 5_100, previousStreak: 0);

        Assert.Equal(5_100, health.Size);
        Assert.Equal(5_000, health.PreviousSize);
        Assert.Equal(1, health.GrowthStreak);
        Assert.False(health.Warning);
    }

    [Fact]
    public void Sustained_growth_warns_once_the_streak_reaches_the_threshold()
    {
        // Second consecutive growth (previousStreak 1 -> 2) reaches GrowthStreakWarningThreshold (2) and warns.
        OperationalContextHealth health = OperationalContextHealthMonitor.Classify(previousSize: 5_100, newSize: 5_200, previousStreak: 1);

        Assert.Equal(2, health.GrowthStreak);
        Assert.True(health.Warning);
    }

    [Fact]
    public void Growth_streak_keeps_warning_while_it_keeps_growing()
    {
        OperationalContextHealth health = OperationalContextHealthMonitor.Classify(previousSize: 5_200, newSize: 5_300, previousStreak: 2);

        Assert.Equal(3, health.GrowthStreak);
        Assert.True(health.Warning);
    }

    [Fact]
    public void A_shrink_resets_the_streak_and_clears_the_warning()
    {
        OperationalContextHealth health = OperationalContextHealthMonitor.Classify(previousSize: 5_300, newSize: 4_000, previousStreak: 5);

        Assert.Equal(0, health.GrowthStreak);
        Assert.False(health.Warning);
    }

    [Fact]
    public void An_unchanged_size_resets_the_streak()
    {
        // Equal is NOT growth: a plateau breaks the ratchet.
        OperationalContextHealth health = OperationalContextHealthMonitor.Classify(previousSize: 5_000, newSize: 5_000, previousStreak: 3);

        Assert.Equal(0, health.GrowthStreak);
        Assert.False(health.Warning);
    }

    [Fact]
    public void The_warning_threshold_is_two_consecutive_growths()
    {
        // Documents the pinned constant so a silent change to the ratchet sensitivity fails here.
        Assert.Equal(2, OperationalContextHealthMonitor.GrowthStreakWarningThreshold);
    }
}
