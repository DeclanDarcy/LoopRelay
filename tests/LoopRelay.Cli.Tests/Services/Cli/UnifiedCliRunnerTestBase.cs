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

// UnifiedCliRunnerTests was one xUnit collection (one class = strictly serial), so its wall-clock
// time was the whole assembly's binding constraint after Task 4 split LoopRelayCompositionRootTests
// (51.5s here vs. ~60s for the rest of the assembly combined). It has been split into three sibling
// classes sharing this base so xUnit can run them as separate, parallel collections.
//
// Grouping was built from measured per-method durations (a fresh trx run; see
// .superpowers/sdd/tperf-task-5-timings.txt), balanced by duration via a greedy
// longest-processing-time bin-pack across 3 bins -- NOT by the task brief's proposed
// storage-lifecycle / RunAsync_bounded_* / surface buckets, because those buckets, scored against
// real data, produce a 39.2s RunAsync_bounded_* class that would forfeit more than half of the
// achievable 3-way parallelism gain (ideal balance is 51.5s / 3 = 17.2s; the hard floor is 13.4s,
// set by RunAsync_bounded_eval_verifies_existing_eval_roadmap_products_through_canonical_runtime, a
// single indivisible test). The RunAsync_bounded_* family (7 methods) is therefore deliberately
// split across all three classes rather than kept in one.
//
// Final grouping (28 methods / 41 cases / 51.5s measured total):
//
//   UnifiedCliRunnerBoundedEvalAndExecuteTests              17.2s  (5 methods / 5 cases)
//     13.4s RunAsync_bounded_eval_verifies_existing_eval_roadmap_products_through_canonical_runtime
//           (hard floor, indivisible)
//      3.2s RunAsync_bounded_execute_rejects_workflow_exit_without_completion_authority_candidate
//      0.3s RunAsync_bounded_plan_stops_after_one_completed_workflow
//      0.2s Interaction_respond_acceptance_resolves_request_and_plans_scoped_commit_effect
//      0.1s RunAsync_status_reports_migration_required_without_mutating_pre_m2_database
//
//   UnifiedCliRunnerCancellationAndBoundedTraditionalTests  17.2s  (10 methods / 10 cases)
//      8.6s RunAsync_output_writer_cancellation_does_not_rewrite_the_application_run_outcome
//      6.5s RunAsync_bounded_traditional_verifies_existing_roadmap_products_through_canonical_runtime
//      0.5s RunAsync_storage_export_emits_verified_semantic_package_and_repeated_init_refuses
//      0.5s RunAsync_executes_workflow_chain_runner_for_run_commands
//      0.3s RunAsync_completed_chain_returns_success_without_old_execute_loop
//      0.3s RunAsync_storage_init_creates_canonical_workspace_schema
//      0.2s Non_runtime_commands_bypass_production_runtime_inspection
//      0.1s RunAsync_aborts_with_typed_outcome_when_a_runtime_prerequisite_is_missing
//      0.1s Quiet_startup_verifies_workspace_storage_once_not_twice
//      0.1s RunAsync_storage_import_fails_closed_for_unregistered_workspace_portfolio
//
//   UnifiedCliRunnerBoundedEvalSelectionAndPlanTests        17.1s  (13 methods / 26 cases)
//      8.2s RunAsync_bounded_eval_selects_evaluation_intent_through_canonical_runtime
//      6.8s RunAsync_bounded_plan_verifies_existing_execution_artifacts_through_canonical_runtime
//      0.8s RunAsync_bounded_execute_verifies_existing_execution_readiness_through_canonical_runtime
//      0.4s RunAsync_dirty_input_surface_stops_with_exit_4_and_prints_requirement_warnings
//      0.3s RunAsync_run_command_reenters_and_terminally_completes_matching_active_root
//      0.3s RunAsync_storage_migrate_is_the_only_command_that_upgrades_a_recognized_schema
//      0.2s RunAsync_previously_latched_blocked_workflow_runs_without_any_unblock_step
//      0.1s RunAsync_requires_recovery_when_active_run_catalog_is_unavailable
//      0.0s (x5 methods / 18 cases) all remaining zero-duration checks -- free to place anywhere;
//           grouped here because this class needed the most mass to reach 17.1s:
//           RunAsync_corrupt_workspace_database_fails_closed_without_crashing_or_leaking_paths,
//           Non_production_compositions_have_no_runtime_prerequisites_to_inspect,
//           RunAsync_status_stops_with_exit_4_for_future_schema_version_without_crashing,
//           RunAsync_storage_init_blocks_unsupported_existing_schema_without_repairing,
//           ExitCodeFor_maps_canonical_stop_reasons (Theory, 14 cases)
//
// None of these class names is a pure family bucket -- every class is a duration-balanced mix, and
// the RunAsync_bounded_* family is deliberately split 2/2/3 across all three classes rather than
// kept in one, per the measured-data override above. Each name reflects its two heaviest/anchor
// tests; the full membership above is the authoritative record. All 28 methods moved verbatim from
// the original UnifiedCliRunnerTests (deleted by this change); shared static helpers and the two
// nested private fakes below moved here unchanged except accessibility (private -> protected).
// Unlike Task 4's composition-root split, none of these helpers' signatures reference an `internal`
// production type (LoopRelayCompositionRoot/UnifiedCliRunner are only ever constructed inline
// inside each test method body here, never threaded through a shared helper's parameter list), so
// plain `protected` compiles; `private protected` was not required. See the task report for the
// verification.
public abstract class UnifiedCliRunnerTestBase
{
    protected sealed class CancelOnStopReasonWriter(CancellationTokenSource _source) : StringWriter
    {
        public override void WriteLine(string? value)
        {
            base.WriteLine(value);
            if (value is not null && value.StartsWith("Stop reason:", StringComparison.Ordinal))
            {
                _source.Cancel();
            }
        }
    }

    protected sealed class CountingStorageVerifier : IStorageVerifier
    {
        private readonly FileSystemStorageVerifier inner = new();
        private int verifications;

        public int Verifications => verifications;

        public Task<StorageVerificationResult> VerifyAsync(
            string repositoryPath,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref verifications);
            return inner.VerifyAsync(repositoryPath, cancellationToken);
        }
    }

    protected static ResolvedRuntimeHostProfile HostProfile() =>
        new(
            new RuntimeProfileIdentity("runtime_cli_test"),
            new AgentRuntimeCapabilities("codex", true, true, true));

    protected static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("cc-cli-unified-runner-").FullName;
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
    }

    protected static async Task GitAsync(string repositoryPath, params string[] arguments)
    {
        ProcessRunResult result = await new ProcessRunner().RunAsync("git", arguments, repositoryPath);
        Assert.Equal(0, result.ExitCode);
    }

    protected static async Task PersistCompletedChainAsync(CanonicalWorkflowPersistenceStore store)
    {
        await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
            WorkflowIdentity.TraditionalRoadmap,
            WorkflowResolutionState.Completed,
            null,
            RuntimeOutcomeKind.Completed,
            DateTimeOffset.UtcNow,
            ["traditional-complete.md"]));
        await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
            WorkflowIdentity.Plan,
            WorkflowResolutionState.Completed,
            null,
            RuntimeOutcomeKind.Completed,
            DateTimeOffset.UtcNow,
            ["plan-complete.md"]));
        await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
            WorkflowIdentity.Execute,
            WorkflowResolutionState.Completed,
            null,
            RuntimeOutcomeKind.Completed,
            DateTimeOffset.UtcNow,
            ["execute-complete.md"]));
        await store.UpsertProductAsync(Product(
            ProductIdentity.PreparedEpic,
            WorkflowIdentity.TraditionalRoadmap,
            WorkflowIdentity.Plan));
        await store.UpsertProductAsync(Product(
            ProductIdentity.MilestoneSpecificationSet,
            WorkflowIdentity.TraditionalRoadmap,
            WorkflowIdentity.Plan));
        await PersistPlanProductsAsync(store);
        await store.UpsertProductAsync(Product(
            ProductIdentity.CertifiedCompletion,
            WorkflowIdentity.Execute,
            WorkflowIdentity.Execute));
    }

    protected static async Task PersistPlanProductsAsync(CanonicalWorkflowPersistenceStore store)
    {
        await store.UpsertProductAsync(Product(
            ProductIdentity.ExecutablePlan,
            WorkflowIdentity.Plan,
            WorkflowIdentity.Execute));
        await store.UpsertProductAsync(Product(
            ProductIdentity.OperationalContext,
            WorkflowIdentity.Plan,
            WorkflowIdentity.Execute));
        await store.UpsertProductAsync(Product(
            ProductIdentity.ExecutionDetails,
            WorkflowIdentity.Plan,
            WorkflowIdentity.Execute));
        await store.UpsertProductAsync(Product(
            ProductIdentity.ExecutionMilestoneSet,
            WorkflowIdentity.Plan,
            WorkflowIdentity.Execute));
        await store.UpsertProductAsync(Product(
            ProductIdentity.ExecutionReadiness,
            WorkflowIdentity.Plan,
            WorkflowIdentity.Execute));
    }

    protected static ProductRecord Product(
        ProductIdentity identity,
        WorkflowIdentity producer,
        WorkflowIdentity consumer) =>
        new(
            identity,
            producer,
            new WorkflowTransitionIdentity($"Produce{identity}"),
            [consumer],
            "repository-owned test evidence",
            "test",
            [$"{identity}.md"],
            $"hash-{identity}",
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            [$"{identity}.md"]);

    protected static async Task WriteAsync(string root, string relativePath, string content)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    protected static async Task SeedEvalIntentAsync(Repository repository) =>
        await WriteAsync(repository.Path, ".agents/evals/e1.md", "# Eval");

    protected static async Task SeedRoadmapArtifactsAsync(Repository repository)
    {
        await SeedProjectContextAsync(repository);
        await WriteAsync(repository.Path, ".agents/epic.md", "# Epic");
        await WriteAsync(repository.Path, ".agents/specs/s1.md", "# Spec");
    }

    protected static async Task SeedProjectContextAsync(Repository repository)
    {
        foreach (string path in ProjectContextSourceContract.SourceFiles)
        {
            await WriteAsync(repository.Path, path, $"# {Path.GetFileNameWithoutExtension(path)}\n\nCanonical test project context.");
        }
    }

    protected static async Task SeedPlanArtifactsAsync(Repository repository)
    {
        await WriteAsync(repository.Path, ".agents/plan.md", "# Plan");
        await WriteAsync(repository.Path, ".agents/operational_context.md", "# Operational Context");
        await WriteAsync(repository.Path, ".agents/details.md", "# Details");
        await WriteAsync(repository.Path, ".agents/milestones/m1.md", "# Milestone\n\n- [ ] Implement capability.");
    }

    protected static async Task SeedCompletionArchiveAsync(Repository repository)
    {
        await WriteAsync(repository.Path, ".agents/archive/epics/1.md", "# Completed Epic\n\nSynthesized closure.");
        await WriteAsync(repository.Path, ".agents/archive/epics/1/archive-metadata.json", """{"SchemaVersion":"completed-epic-archive.v1"}""");
        await WriteAsync(repository.Path, ".agents/archive/epics/1/plan.md", "# Archived Plan");
        await WriteAsync(repository.Path, ".agents/archive/epics/1/milestones/m1.md", "# Archived Milestone");
    }

    protected static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    protected static async Task ExecuteAsync(SqliteConnection connection, string commandText)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    protected static async Task<string?> ScalarAsync(SqliteConnection connection, string commandText)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        object? value = await command.ExecuteScalarAsync();
        return value?.ToString();
    }
}
