using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Services.Process;
using LoopRelay.Cli.Services.Cli;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Core.Services.ProjectContext;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Infrastructure.Services.Diagnostics;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Interactions;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Storage;
using LoopRelay.Orchestration.Workflows;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

// See the grouping comment block in UnifiedCliRunnerTestBase.cs for the full rationale and
// measured-second membership of all three sibling classes. This class: 17.2s / 10 methods / 10 cases.
public sealed class UnifiedCliRunnerCancellationAndBoundedTraditionalTests : UnifiedCliRunnerTestBase
{
    [Fact]
    public async Task RunAsync_output_writer_cancellation_does_not_rewrite_the_application_run_outcome()
    {
        Repository repository = CreateRepository();
        await SeedRoadmapArtifactsAsync(repository);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repository.Path);
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var source = new CancellationTokenSource();
        using var output = new CancelOnStopReasonWriter(source);
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(LoopRelayCompositionRoot.CreateForTests(repository), output, error);

        int? exitCode = null;
        try
        {
            exitCode = await runner.RunAsync(invocation, source.Token);
        }
        catch (OperationCanceledException)
        {
            // Cancellation may surface as a thrown OperationCanceledException or as a Cancelled stop reason.
        }

        var store = new CanonicalWorkflowPersistenceStore(repository);
        IReadOnlyList<RunRecord> runs = await store.ReadRunsAsync();
        RunRecord run = Assert.Single(runs);
        Assert.Equal(WorkflowStopReason.Failed.ToString(), run.Status);
        Assert.Equal(WorkflowStopReason.Failed.ToString(), run.StopReason);
        Assert.NotNull(run.CompletedAt);
        if (exitCode is not null)
        {
            Assert.Equal(1, exitCode);
        }
    }

    [Fact]
    public async Task RunAsync_bounded_traditional_verifies_existing_roadmap_products_through_canonical_runtime()
    {
        Repository repository = CreateRepository();
        await SeedRoadmapArtifactsAsync(repository);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repository.Path);
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedTraditional),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository);
        var runner = new UnifiedCliRunner(composition, output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.True(exitCode == 0, $"Output:\n{output}\nError:\n{error}");
        Assert.Contains("Workflow: TraditionalRoadmap", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Stop reason: TransitionCompleted", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Transition: VerifyPlanEntryContract", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
        RepositoryObservation observation = await composition.ObserveAsync(CancellationToken.None);
        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            workflow => workflow.Workflow == WorkflowIdentity.TraditionalRoadmap);
        Assert.Equal(WorkflowResolutionState.Completed, state.State);
    }

    [Fact]
    public async Task RunAsync_storage_export_emits_verified_semantic_package_and_repeated_init_refuses()
    {
        Repository repository = CreateRepository();
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository);
        var runner = new UnifiedCliRunner(composition, new StringWriter(), new StringWriter());
        var mode = new WorkflowInvocation(InvocationModeKind.DefaultChained);
        Assert.Equal(0, await runner.RunAsync(new TestApplicationInvocation(repository, mode,
            new TestApplicationCommand(TestApplicationCommandKind.StorageInit, [])), CancellationToken.None));
        byte[] initialized = await File.ReadAllBytesAsync(LoopRelayWorkspaceDatabase.Resolve(repository));
        Assert.Equal(4, await runner.RunAsync(new TestApplicationInvocation(repository, mode,
            new TestApplicationCommand(TestApplicationCommandKind.StorageInit, [])), CancellationToken.None));
        Assert.Equal(initialized, await File.ReadAllBytesAsync(LoopRelayWorkspaceDatabase.Resolve(repository)));
        const string exportPath = ".LoopRelay/exports/test.canonical.json";

        int exportExit = await runner.RunAsync(new TestApplicationInvocation(repository, mode,
            new TestApplicationCommand(TestApplicationCommandKind.StorageExport, [exportPath])), CancellationToken.None);

        Assert.Equal(0, exportExit);
        string json = await File.ReadAllTextAsync(Path.Combine(repository.Path,
            exportPath.Replace('/', Path.DirectorySeparatorChar)));
        CanonicalStorageExportPackage package = new CanonicalStorageExportCodec().Decode(json);
        Assert.Equal("looprelay-canonical-storage", package.Manifest.Codec);
        Assert.NotEmpty(package.Manifest.LogicalFingerprint);
        Assert.NotEmpty(package.Domains);
    }

    [Fact]
    public async Task RunAsync_executes_workflow_chain_runner_for_run_commands()
    {
        Repository repository = CreateRepository();
        await SeedProjectContextAsync(repository);
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(LoopRelayCompositionRoot.CreateForTests(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(1, exitCode);
        string text = output.ToString();
        Assert.Contains("Workflow: TraditionalRoadmap", text, StringComparison.Ordinal);
        Assert.Contains("Stop reason: Failed", text, StringComparison.Ordinal);
        Assert.Contains("Transition: BootstrapRoadmapCompletionContext", text, StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_completed_chain_returns_success_without_old_execute_loop()
    {
        Repository repository = CreateRepository();
        // Collaboration-file products are filesystem-authoritative (M3): the persisted rows
        // below only annotate them, so the actual files must exist to be observed.
        await SeedRoadmapArtifactsAsync(repository);
        await SeedPlanArtifactsAsync(repository);
        var store = new CanonicalWorkflowPersistenceStore(repository);
        await PersistCompletedChainAsync(store);
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(LoopRelayCompositionRoot.CreateForTests(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.True(exitCode == 0, $"Output:{Environment.NewLine}{output}{Environment.NewLine}Error:{Environment.NewLine}{error}");
        Assert.Contains("Stop reason: ChainCompleted", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_storage_init_creates_canonical_workspace_schema()
    {
        Repository repository = CreateRepository();
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            new TestApplicationCommand(TestApplicationCommandKind.StorageInit, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(LoopRelayCompositionRoot.CreateForTests(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Assert.True(File.Exists(databasePath));
        Assert.Contains("Storage operation: Initialize", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Lifecycle: Completed", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Storage health: Healthy", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync();
        Assert.Equal(LoopRelayWorkspaceDatabase.CurrentSchemaVersion.ToString(), await ScalarAsync(
            connection,
            "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
    }

    [Fact]
    public async Task Non_runtime_commands_bypass_production_runtime_inspection()
    {
        Repository repository = CreateRepository();
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            new TestApplicationCommand(TestApplicationCommandKind.StorageInit, []));
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository);
        composition.ProductionRuntime = true;
        composition.RuntimePrerequisiteProfile = HostProfile();
        composition.RuntimePrerequisiteDoctor = new RuntimePrerequisiteDoctor(_ => null, _ => false);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await new UnifiedCliRunner(composition, output, error)
            .RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("CODEX_EXECUTABLE", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("CODEX_EXECUTABLE", error.ToString(), StringComparison.Ordinal);
        Assert.Empty(await new CanonicalWorkflowPersistenceStore(repository).ReadRuntimePrerequisitesAsync());
    }

    [Fact]
    public async Task RunAsync_aborts_with_typed_outcome_when_a_runtime_prerequisite_is_missing()
    {
        // M7: a production run inspects the provider's runtime prerequisites before any agent
        // launches; an Error diagnostic aborts with the specific MissingRuntimePrerequisite
        // outcome (exit 4) instead of the raw resolver exception at the first send, and the
        // inspection is appended as an append-only fact.
        Repository repository = CreateRepository();
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository);
        composition.ProductionRuntime = true;
        composition.RuntimePrerequisiteProfile = HostProfile();
        composition.RuntimePrerequisiteDoctor = new RuntimePrerequisiteDoctor(_ => null, _ => false);
        var runner = new UnifiedCliRunner(composition, output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(4, exitCode);
        Assert.Contains("Stop reason: MissingRuntimePrerequisite", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("CODEX_EXECUTABLE is not set.", error.ToString(), StringComparison.Ordinal);
        var store = new CanonicalWorkflowPersistenceStore(repository);
        CanonicalRuntimePrerequisiteRecord check = Assert.Single(await store.ReadRuntimePrerequisitesAsync());
        Assert.StartsWith("pre_", check.PrerequisiteCheckId, StringComparison.Ordinal);
        Assert.Null(check.RunId);
        Assert.Contains("runtime.codex_executable.missing", check.DiagnosticsJson, StringComparison.Ordinal);
        Assert.Contains("MissingRequiredExecutable", check.DiagnosticsJson, StringComparison.Ordinal);
        Assert.Contains("runtime_cli_test", check.DiagnosticsJson, StringComparison.Ordinal);
        Assert.Contains("\"provider\":\"codex\"", check.DiagnosticsJson, StringComparison.Ordinal);
        Assert.Contains("Unsatisfied", check.DiagnosticsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quiet_startup_verifies_workspace_storage_once_not_twice()
    {
        // PERF-24b: startup observed, ran the effect worker, then re-observed unconditionally.
        // The worker's result is now read: a workspace with nothing unsettled cannot have been
        // mutated by it, so the first observation still holds and the second is skipped. This
        // fixture stops at the runtime-prerequisite gate, immediately after that step, so the
        // count isolates startup from anything the workflow chain would observe later.
        Repository repository = CreateRepository();
        var verifier = new CountingStorageVerifier();
        LoopRelayCompositionRoot composition =
            LoopRelayCompositionRoot.CreateForTests(repository, verifier);
        composition.ProductionRuntime = true;
        composition.RuntimePrerequisiteProfile = HostProfile();
        composition.RuntimePrerequisiteDoctor = new RuntimePrerequisiteDoctor(_ => null, _ => false);
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await new UnifiedCliRunner(composition, output, error)
            .RunAsync(invocation, CancellationToken.None);

        Assert.Equal(4, exitCode);
        Assert.Contains("Stop reason: MissingRuntimePrerequisite", output.ToString(), StringComparison.Ordinal);
        Assert.Empty(await new CanonicalEffectWorkStore(repository)
            .ScanUnsettledAsync(128, DateTimeOffset.UtcNow, CancellationToken.None));
        Assert.Equal(1, verifier.Verifications);
    }

    [Fact]
    public async Task Quiet_startup_with_only_dependency_blocked_effects_verifies_workspace_storage_once_not_twice()
    {
        // Task 3.8: `Discovered > 0` is not the same fact as "the effect worker wrote something".
        // A discovered intent whose dependency is not yet settled is skipped by a read-only check
        // (IEffectWorkStore.DependencySatisfiedAsync queries; it never writes) and never reaches a
        // lease, a lifecycle append, or a receipt. This fixture plants exactly one such intent -
        // its declared dependency identity was never planned, so the dependency gate reads "not
        // satisfied" immediately - so EffectWorker.RunOnceAsync discovers one item (Discovered == 1)
        // but performs zero durable writes (Dispatched == Succeeded == RecoveryRequired == 0). The
        // pre-existing `Quiet_startup_verifies_workspace_storage_once_not_twice` only covers the
        // Discovered == 0 case; this covers the Discovered > 0-but-write-free case the same
        // optimization must also carry the observation across.
        Repository repository = CreateRepository();
        var causality = new CanonicalCausalContext(
            WorkspaceIdentity.New(),
            RunIdentity.New(),
            WorkflowInstanceIdentity.New(),
            TransitionRunIdentity.New(),
            AttemptIdentity.New());
        var blockedIntent = new EffectIntent(
            EffectIntentIdentity.New(),
            causality,
            "test:dependency-blocked-effect",
            new EffectExecutorKey("test-executor"),
            "1",
            new EffectTargetDescriptor("Effect", "test-target", "{}"),
            "{}",
            "test-hash",
            0,
            [new EffectIntentIdentity("effect_never-planned-dependency")],
            EffectRequiredness.BlockingLocal,
            new EffectCondition("test-precondition", "{}"),
            new EffectCondition("test-postcondition", "{}"),
            "test-reconciliation-policy",
            $"test-blocked:{causality.TransitionRun.Value}",
            DateTimeOffset.UtcNow);
        var effectWorkStore = new CanonicalEffectWorkStore(repository);
        await effectWorkStore.AppendPlanAsync([blockedIntent], CancellationToken.None);
        var verifier = new CountingStorageVerifier();
        LoopRelayCompositionRoot composition =
            LoopRelayCompositionRoot.CreateForTests(repository, verifier);
        composition.ProductionRuntime = true;
        composition.RuntimePrerequisiteProfile = HostProfile();
        composition.RuntimePrerequisiteDoctor = new RuntimePrerequisiteDoctor(_ => null, _ => false);
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await new UnifiedCliRunner(composition, output, error)
            .RunAsync(invocation, CancellationToken.None);

        Assert.Equal(4, exitCode);
        Assert.Contains("Stop reason: MissingRuntimePrerequisite", output.ToString(), StringComparison.Ordinal);
        // The blocked intent was genuinely discovered (still scanned as unsettled afterward) and
        // genuinely untouched (still Planned, still exactly the one lifecycle event AppendPlanAsync
        // itself wrote) - proving Discovered > 0 without a single write from the worker pass.
        IReadOnlyList<EffectScanRow> stillUnsettled = await effectWorkStore
            .ScanUnsettledAsync(128, DateTimeOffset.UtcNow, CancellationToken.None);
        EffectScanRow remaining = Assert.Single(stillUnsettled);
        Assert.Equal(blockedIntent.Identity, remaining.Intent.Identity);
        Assert.Equal(EffectLifecycle.Planned, remaining.State);
        EffectWorkItem? persisted = await effectWorkStore.ReadAsync(blockedIntent.Identity, CancellationToken.None);
        EffectWorkItem item = Assert.IsType<EffectWorkItem>(persisted);
        Assert.Equal(EffectLifecycle.Planned, item.State);
        Assert.Single(item.Events);
        Assert.Equal(1, verifier.Verifications);
    }

    [Fact]
    public async Task RunAsync_storage_import_fails_closed_for_unregistered_workspace_portfolio()
    {
        Repository repository = CreateRepository();
        await SeedRoadmapArtifactsAsync(repository);
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            new TestApplicationCommand(TestApplicationCommandKind.StorageImport, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(LoopRelayCompositionRoot.CreateForTests(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(4, exitCode);
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Assert.False(File.Exists(databasePath));
        Assert.Contains("Import detection failed closed", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }
}
