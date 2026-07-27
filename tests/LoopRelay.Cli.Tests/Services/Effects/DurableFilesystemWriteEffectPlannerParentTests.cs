using LoopRelay.Cli.Services.Effects;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Effects;

/// <summary>
/// The nested-planning handshake is an in-process fact, not a database one. These tests hold the
/// planner to ordering and depending on the effect that is actually executing, with no row in any
/// particular status -- which is what makes the <c>Started</c> marker removable.
/// </summary>
public sealed class DurableFilesystemWriteEffectPlannerParentTests
{
    [Fact]
    public async Task Scheduled_write_depends_on_the_passed_parent_with_no_started_row_present()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalEffectWorkStore(repository);
        CanonicalCausalContext causality = NewCausality();
        EffectIntent parent = ParentIntent(causality, order: 4);
        await store.AppendPlanAsync([parent], CancellationToken.None);

        // Deliberately left Planned. Nothing in this workspace is Started, so a planner that went
        // back to the database for its parent would find nothing at all.
        Assert.Equal(
            EffectLifecycle.Planned,
            (await store.ReadAsync(parent.Identity, CancellationToken.None))!.State);

        await new DurableFilesystemWriteEffectPlanner(repository).ScheduleAsync(
            causality,
            new EffectParent(parent.Identity, parent.Order),
            ".LoopRelay/evidence/scheduled.md",
            "scheduled body",
            CancellationToken.None);

        IReadOnlyList<EffectWorkItem> plan =
            await store.ReadPlanAsync(causality.TransitionRun, CancellationToken.None);
        EffectWorkItem child = Assert.Single(
            plan, item => item.Intent.Executor == WorkspaceEffectExecutorKeys.FilesystemWrite);
        Assert.Equal([parent.Identity], child.Intent.Dependencies);
        Assert.Equal(5, child.Intent.Order);
        Assert.StartsWith(
            $"filesystem-write:{parent.Identity.Value}:",
            child.Intent.IdempotencyKey,
            StringComparison.Ordinal);
    }

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("cc-cli-planner-parent-").FullName;
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
    }

    private static CanonicalCausalContext NewCausality() => new(
        WorkspaceIdentity.New(),
        RunIdentity.New(),
        WorkflowInstanceIdentity.New(),
        TransitionRunIdentity.New(),
        AttemptIdentity.New());

    private static EffectIntent ParentIntent(CanonicalCausalContext causality, int order) => new(
        EffectIntentIdentity.New(),
        causality,
        "canonical-feature-effect",
        TransitionalFeatureEffectExecutorKeys.For("SomeEffect"),
        "1",
        new EffectTargetDescriptor("Transition", "SomeEffect", "{\"effect\":\"SomeEffect\"}"),
        "{\"effect\":\"SomeEffect\"}",
        new string('a', 64),
        order,
        [],
        EffectRequiredness.BlockingLocal,
        new EffectCondition("none", "{}"),
        new EffectCondition("none", "{}"),
        "children-exist",
        $"feature:{causality.TransitionRun.Value}:SomeEffect",
        DateTimeOffset.UtcNow);
}
