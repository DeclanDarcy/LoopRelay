using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives;
using LoopRelay.Agents.Primitives.Sessions;
using System.Security.Cryptography;
using System.Text;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models;
using LoopRelay.Completion.Models.Archive;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Models.Prompts;
using LoopRelay.Completion.Primitives;
using LoopRelay.Completion.Services;
using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Completion.Services.Certification;
using LoopRelay.Completion.Services.Prompts;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Abstractions.Persistence;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Services;
using LoopRelay.Projections.Abstractions;
using LoopRelay.Projections.Models;
using LoopRelay.Projections.Models.Context;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Primitives;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LoopRelay.Completion.Tests.Services;

public sealed class CompletionCertificationServiceTests
{
    [Fact]
    public async Task CloseWorthyCertification_ArchivesSynthesizesAndPassesCompletedEpicToUpdate()
    {
        Harness h = Harness.Create();
        await h.SeedExecutionWorkspaceAsync();

        h.Prompts.Handler = async invocation =>
        {
            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift)
            {
                return Evaluation("Fully Complete", "None", "Close Epic");
            }

            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.SynthesizeCompletedEpic)
            {
                await h.WriteAsync($".agents/archive/epics/{invocation.Label}.md", "# Completed Epic\n\nSynthesized.");
                return "synthesized";
            }

            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.UpdateRoadmapCompletionContext)
            {
                return "# Roadmap Completion Context\n\nUpdated.";
            }

            throw new InvalidOperationException(invocation.RuntimePromptName);
        };

        CompletionCertificationResult result = await h.Service.CertifyPlanCompletionAsync(new CompletionCertificationRequest(h.Repository));

        Assert.Equal(CompletionCertificationServiceOutcome.Completed, result.Outcome);
        Assert.Equal(1, result.CompletedEpicArchiveIndex);
        Assert.Equal(".agents/archive/epics/1.md", result.CompletedEpicSynthesisPath);
        Assert.Equal("# Roadmap Completion Context\n\nUpdated.", await h.ReadAsync(CompletionArtifactPaths.RoadmapCompletionContext));
        Assert.Equal("PLAN", await h.ReadAsync(".agents/archive/epics/1/plan.md"));
        Assert.Equal("DETAILS", await h.ReadAsync(".agents/archive/epics/1/details.md"));
        Assert.Equal("OPCTX", await h.ReadAsync(".agents/archive/epics/1/operational_context.md"));
        Assert.Equal("- [x] milestone", await h.ReadAsync(".agents/archive/epics/1/milestones/m1.md"));
        Assert.Equal("DECISION", await h.ReadAsync(".agents/archive/epics/1/decisions/decisions.md"));
        Assert.Null(await h.ReadAsync(CompletionArtifactPaths.ExecutionPlan));

        CompletionRuntimePromptInvocation update = Assert.Single(
            h.Prompts.Invocations,
            invocation => invocation.RuntimePromptName == CompletionRuntimePromptNames.UpdateRoadmapCompletionContext);
        Assert.Equal("# Completed Epic\n\nSynthesized.", update.SecondaryInput);
        Assert.Contains("# Completed Epic", update.ProjectContext, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CloseWorthyCertification_ArchivesSqliteBackedHistoriesAndExecutionEvidence()
    {
        using var repo = new TempFileRepo();
        await InitializeArchiveDatabaseAsync(repo.Repository);
        await SeedArchiveRecordAsync(repo.Repository, "loop_history", new Dictionary<string, object?>
        {
            ["kind"] = "Decisions",
            ["sequence"] = 1,
            ["logical_path"] = ".agents/decisions/decisions.0001.md",
            ["body"] = "decision history",
            ["content_hash"] = Sha256("decision history"),
            ["created_at"] = "2026-01-01T00:00:00.0000000+00:00",
        });
        await SeedArchiveRecordAsync(repo.Repository, "loop_history", new Dictionary<string, object?>
        {
            ["kind"] = "Handoff",
            ["sequence"] = 1,
            ["logical_path"] = ".agents/handoffs/handoff.0001.md",
            ["body"] = "handoff history",
            ["content_hash"] = Sha256("handoff history"),
            ["created_at"] = "2026-01-01T00:00:00.0000000+00:00",
        });
        await SeedArchiveRecordAsync(repo.Repository, "loop_history", new Dictionary<string, object?>
        {
            ["kind"] = "OperationalDelta",
            ["sequence"] = 1,
            ["logical_path"] = ".agents/deltas/operational_delta.0001.md",
            ["body"] = "delta history",
            ["content_hash"] = Sha256("delta history"),
            ["created_at"] = "2026-01-01T00:00:00.0000000+00:00",
        });
        await SeedArchiveRecordAsync(repo.Repository, "execution_evidence", new Dictionary<string, object?>
        {
            ["logical_path"] = ".agents/evidence/execution/execution.0001.md",
            ["stem"] = "execution",
            ["sequence"] = 1,
            ["body"] = "execution evidence",
            ["content_hash"] = Sha256("execution evidence"),
            ["created_at"] = "2026-01-01T00:00:00.0000000+00:00",
            ["writer"] = null,
            ["metadata_json"] = "{}",
        });
        await repo.SeedExecutionWorkspaceAsync();
        await repo.DeleteAsync(".agents/deltas/operational_delta.0001.md");
        var prompts = new FakePromptRunner();
        var service = new CompletionCertificationService(
            repo.Store,
            new FakeProjectionService(),
            prompts,
            new CompletedEpicArchiveService(
                repo.Store,
                prompts,
                _archiveMaterializer: new SqliteCompletedEpicArchiveMaterializer()),
            _executionEvidenceStore: new SqliteExecutionEvidenceStore(repo.Repository));
        prompts.Handler = async invocation =>
        {
            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift)
            {
                return Evaluation("Fully Complete", "None", "Close Epic");
            }

            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.SynthesizeCompletedEpic)
            {
                await repo.WriteAsync($".agents/archive/epics/{invocation.Label}.md", "# Completed Epic\n\nSynthesized.");
                return "synthesized";
            }

            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.UpdateRoadmapCompletionContext)
            {
                return "# Roadmap Completion Context\n\nUpdated.";
            }

            throw new InvalidOperationException(invocation.RuntimePromptName);
        };

        CompletionCertificationResult result = await service.CertifyPlanCompletionAsync(new CompletionCertificationRequest(repo.Repository));

        Assert.Equal(CompletionCertificationServiceOutcome.Completed, result.Outcome);
        Assert.Equal("decision history", await repo.ReadAsync(".agents/archive/epics/1/decisions/decisions.0001.md"));
        Assert.Equal("handoff history", await repo.ReadAsync(".agents/archive/epics/1/handoffs/handoff.0001.md"));
        Assert.Equal("delta history", await repo.ReadAsync(".agents/archive/epics/1/deltas/operational_delta.0001.md"));
        Assert.Equal("execution evidence", await repo.ReadAsync(".agents/archive/epics/1/evidence/execution/execution.0001.md"));
        Assert.Contains("# Main CLI Completion Claim", await repo.ReadAsync(".agents/archive/epics/1/evidence/execution/main-cli-completion-claim.0001.md"), StringComparison.Ordinal);
        Assert.Equal("PLAN", await repo.ReadAsync(".agents/archive/epics/1/plan.md"));
        Assert.Equal("OPCTX", await repo.ReadAsync(".agents/archive/epics/1/operational_context.md"));
        Assert.Null(await repo.ReadAsync(".agents/evidence/execution/execution.0001.md"));

        CompletedEpicArchiveRecoveryResult recovery =
            await new CompletedEpicArchiveRecoveryService(repo.Store, repo.Repository).LoadAsync(1);
        Assert.Contains(recovery.Records, record =>
            record.Domain == "loop_history" &&
            record.LogicalPath == ".agents/deltas/operational_delta.0001.md" &&
            record.ExportPath == ".agents/archive/epics/1/deltas/operational_delta.0001.md");
        Assert.Contains(recovery.Records, record =>
            record.Domain == "execution_evidence" &&
            record.LogicalPath == ".agents/evidence/execution/execution.0001.md");
    }

    [Fact]
    public async Task ArchiveMaterialization_UsesPersistedReferencesToSelectSqliteBackedRecords()
    {
        using var repo = new TempFileRepo();
        await InitializeArchiveDatabaseAsync(repo.Repository);
        await SeedArchiveRecordAsync(repo.Repository, "loop_history", new Dictionary<string, object?>
        {
            ["kind"] = "Decisions",
            ["sequence"] = 1,
            ["logical_path"] = ".agents/decisions/decisions.0001.md",
            ["body"] = "referenced decision",
            ["content_hash"] = Sha256("referenced decision"),
            ["created_at"] = "2026-01-01T00:00:00.0000000+00:00",
        });
        await SeedArchiveRecordAsync(repo.Repository, "loop_history", new Dictionary<string, object?>
        {
            ["kind"] = "OperationalDelta",
            ["sequence"] = 1,
            ["logical_path"] = ".agents/deltas/operational_delta.0001.md",
            ["body"] = "referenced delta",
            ["content_hash"] = Sha256("referenced delta"),
            ["created_at"] = "2026-01-01T00:00:00.0000000+00:00",
        });
        await SeedArchiveRecordAsync(repo.Repository, "loop_history", new Dictionary<string, object?>
        {
            ["kind"] = "Handoff",
            ["sequence"] = 1,
            ["logical_path"] = ".agents/handoffs/handoff.0001.md",
            ["body"] = "referenced handoff",
            ["content_hash"] = Sha256("referenced handoff"),
            ["created_at"] = "2026-01-01T00:00:00.0000000+00:00",
        });
        await SeedArchiveRecordAsync(repo.Repository, "loop_history", new Dictionary<string, object?>
        {
            ["kind"] = "Handoff",
            ["sequence"] = 2,
            ["logical_path"] = ".agents/handoffs/handoff.0002.md",
            ["body"] = "unreferenced handoff",
            ["content_hash"] = Sha256("unreferenced handoff"),
            ["created_at"] = "2026-01-01T00:00:00.0000000+00:00",
        });
        await SeedArchiveRecordAsync(repo.Repository, "execution_evidence", new Dictionary<string, object?>
        {
            ["logical_path"] = ".agents/evidence/execution/execution.0001.md",
            ["stem"] = "execution",
            ["sequence"] = 1,
            ["body"] = "referenced execution evidence",
            ["content_hash"] = Sha256("referenced execution evidence"),
            ["created_at"] = "2026-01-01T00:00:00.0000000+00:00",
            ["writer"] = null,
            ["metadata_json"] = "{}",
        });
        await SeedArchiveRecordAsync(repo.Repository, "execution_evidence", new Dictionary<string, object?>
        {
            ["logical_path"] = ".agents/evidence/execution/execution.0002.md",
            ["stem"] = "execution",
            ["sequence"] = 2,
            ["body"] = "unreferenced execution evidence",
            ["content_hash"] = Sha256("unreferenced execution evidence"),
            ["created_at"] = "2026-01-01T00:00:00.0000000+00:00",
            ["writer"] = null,
            ["metadata_json"] = "{}",
        });
        await SeedArchiveRecordAsync(repo.Repository, "roadmap_state", new Dictionary<string, object?>
        {
            ["id"] = 1,
            ["document_json"] = """
                {"lastTransition":{"output":".agents/deltas/operational_delta.0001.md"},"transitionIntent":{"evidencePaths":[".agents/handoffs/handoff.0001.md"]}}
                """,
            ["updated_at"] = "2026-01-01T00:00:00.0000000+00:00",
        });
        await SeedArchiveRecordAsync(repo.Repository, "transition_journal", new Dictionary<string, object?>
        {
            ["correlation_id"] = "correlation-1",
            ["event_name"] = "TransitionCompleted",
            ["recorded_at"] = "2026-01-01T00:00:00.0000000+00:00",
            ["from_state"] = "ExecutionPromptReady",
            ["to_state"] = "Complete",
            ["transition"] = "CompleteEpic",
            ["projection_path"] = ".agents/projections/select-next-epic.md",
            ["prompt_contract"] = "contract",
            ["input_hashes_json"] = "{}",
            ["output_paths_json"] = """[".agents/evidence/execution/execution.0001.md"]""",
            ["duration_milliseconds"] = 1,
            ["retry_count"] = 0,
            ["result"] = "Completed",
            ["decision"] = "Close Epic",
            ["error"] = null,
            ["input_snapshot_json"] = null,
        });
        await repo.WriteAsync(
            CompletionArtifactPaths.RoadmapCompletionContext,
            "Completion context references .agents/decisions/decisions.0001.md.");

        await new SqliteCompletedEpicArchiveMaterializer().MaterializeAsync(
            repo.Store,
            repo.Repository,
            ".agents/archive/epics/1");

        Assert.Equal("referenced decision", await repo.ReadAsync(".agents/archive/epics/1/decisions/decisions.0001.md"));
        Assert.Equal("referenced delta", await repo.ReadAsync(".agents/archive/epics/1/deltas/operational_delta.0001.md"));
        Assert.Equal("referenced handoff", await repo.ReadAsync(".agents/archive/epics/1/handoffs/handoff.0001.md"));
        Assert.Equal("referenced execution evidence", await repo.ReadAsync(".agents/archive/epics/1/evidence/execution/execution.0001.md"));
        Assert.Null(await repo.ReadAsync(".agents/archive/epics/1/handoffs/handoff.0002.md"));
        Assert.Null(await repo.ReadAsync(".agents/archive/epics/1/evidence/execution/execution.0002.md"));
    }

    [Fact]
    public async Task CompletionCertification_RendersSqliteBackedRecentHandoff()
    {
        using var repo = new TempFileRepo();
        await InitializeArchiveDatabaseAsync(repo.Repository);
        await SeedArchiveRecordAsync(repo.Repository, "loop_history", new Dictionary<string, object?>
        {
            ["kind"] = "Handoff",
            ["sequence"] = 1,
            ["logical_path"] = ".agents/handoffs/handoff.0001.md",
            ["body"] = "handoff from sqlite",
            ["content_hash"] = Sha256("handoff from sqlite"),
            ["created_at"] = "2026-01-01T00:00:00.0000000+00:00",
        });
        await repo.SeedExecutionWorkspaceAsync();
        var prompts = new FakePromptRunner();
        prompts.Handler = invocation =>
        {
            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift)
            {
                Assert.Contains("handoff from sqlite", invocation.ProjectContext, StringComparison.Ordinal);
                return Task.FromResult(Evaluation("Partially Complete", "None", "Continue Epic"));
            }

            throw new InvalidOperationException(invocation.RuntimePromptName);
        };
        var service = new CompletionCertificationService(
            repo.Store,
            new FakeProjectionService(),
            prompts,
            new CompletedEpicArchiveService(repo.Store, prompts));

        CompletionCertificationResult result = await service.CertifyPlanCompletionAsync(
            new CompletionCertificationRequest(repo.Repository));

        Assert.Equal(CompletionCertificationServiceOutcome.Blocked, result.Outcome);
    }

    [Fact]
    [Trait("Baseline", "KnownRisk")]
    public async Task ArchiveMaterialization_MissingRequiredMigratedRecordFailsBeforeRetainedMoves()
    {
        using var repo = new TempFileRepo();
        await InitializeArchiveDatabaseAsync(repo.Repository);
        await repo.SeedExecutionWorkspaceAsync();
        var prompts = new FakePromptRunner();
        var archive = new CompletedEpicArchiveService(
            repo.Store,
            prompts,
            _archiveMaterializer: new SqliteCompletedEpicArchiveMaterializer(
                [".agents/evidence/execution/missing.0001.md"]));

        await Assert.ThrowsAsync<CompletionCertificationException>(() =>
            archive.ArchiveAndSynthesizeAsync(new CompletedEpicArchiveRequest(repo.Repository)));

        Assert.Equal("PLAN", await repo.ReadAsync(CompletionArtifactPaths.ExecutionPlan));
        Assert.Null(await repo.ReadAsync(".agents/archive/epics/1/plan.md"));
        Assert.Empty(prompts.Invocations);
    }

    [Fact]
    [Trait("Baseline", "KnownRisk")]
    public async Task ArchiveMaterialization_RetainedTargetCollisionFailsBeforeRetainedDeletes()
    {
        using var repo = new TempFileRepo();
        await InitializeArchiveDatabaseAsync(repo.Repository);
        await SeedArchiveRecordAsync(repo.Repository, "loop_history", new Dictionary<string, object?>
        {
            ["kind"] = "OperationalDelta",
            ["sequence"] = 1,
            ["logical_path"] = ".agents/deltas/operational_delta.0001.md",
            ["body"] = "delta history",
            ["content_hash"] = Sha256("delta history"),
            ["created_at"] = "2026-01-01T00:00:00.0000000+00:00",
        });
        await repo.SeedExecutionWorkspaceAsync();
        var prompts = new FakePromptRunner();
        var archive = new CompletedEpicArchiveService(
            repo.Store,
            prompts,
            _archiveMaterializer: new SqliteCompletedEpicArchiveMaterializer());

        CompletionCertificationException exception = await Assert.ThrowsAsync<CompletionCertificationException>(() =>
            archive.ArchiveAndSynthesizeAsync(new CompletedEpicArchiveRequest(repo.Repository)));

        Assert.Contains("Archive target already exists", exception.Message, StringComparison.Ordinal);
        Assert.Equal("PLAN", await repo.ReadAsync(CompletionArtifactPaths.ExecutionPlan));
        Assert.Equal("DELTA", await repo.ReadAsync(".agents/deltas/operational_delta.0001.md"));
        Assert.Empty(prompts.Invocations);
    }

    [Fact]
    public async Task CompletedEpicEvidenceLoader_RendersArchiveMetadataWhenPresent()
    {
        using var repo = new TempFileRepo();
        await repo.WriteAsync(".agents/archive/epics/1.md", "# Completed Epic\n\nSynthesized.");
        await repo.WriteAsync(".agents/archive/epics/1/archive-metadata.json", """
            {
              "SchemaVersion": "completed-epic-archive.v1",
              "Records": [
                {
                  "Domain": "execution_evidence",
                  "LogicalPath": ".agents/evidence/execution/execution.0001.md",
                  "ExportPath": ".agents/archive/epics/1/evidence/execution/execution.0001.md",
                  "ContentHash": "hash"
                }
              ]
            }
            """);
        var loader = new CompletedEpicEvidenceLoader(new CompletionArtifacts(repo.Store, repo.Repository));

        string rendered = await loader.RenderAsync();

        Assert.Contains("Archive Metadata", rendered, StringComparison.Ordinal);
        Assert.Contains("Migrated Records | 1", rendered, StringComparison.Ordinal);
        Assert.Contains("execution_evidence", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonCloseCertification_ReturnsBlockedAndDoesNotArchiveOrUpdateContext()
    {
        Harness h = Harness.Create();
        await h.SeedExecutionWorkspaceAsync();
        h.Prompts.Handler = invocation =>
            Task.FromResult(invocation.RuntimePromptName == CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift
                ? Evaluation("Partially Complete", "None", "Continue Epic")
                : throw new InvalidOperationException(invocation.RuntimePromptName));

        CompletionCertificationResult result = await h.Service.CertifyPlanCompletionAsync(new CompletionCertificationRequest(h.Repository));

        Assert.Equal(CompletionCertificationServiceOutcome.Blocked, result.Outcome);
        Assert.Equal("Continue Epic", result.Decision?.ClosureRecommendation);
        Assert.NotNull(result.BlockedEvidencePath);
        Assert.Null(await h.ReadAsync(".agents/archive/epics/1/plan.md"));
        Assert.Equal("# Roadmap Completion Context\n\nCurrent.", await h.ReadAsync(CompletionArtifactPaths.RoadmapCompletionContext));
        Assert.DoesNotContain(h.Prompts.Invocations, invocation => invocation.RuntimePromptName == CompletionRuntimePromptNames.SynthesizeCompletedEpic);
        Assert.DoesNotContain(h.Prompts.Invocations, invocation => invocation.RuntimePromptName == CompletionRuntimePromptNames.UpdateRoadmapCompletionContext);
    }

    [Fact]
    public async Task ArchiveIndexCountsExistingArchiveDirectories()
    {
        Harness h = Harness.Create();
        await h.SeedExecutionWorkspaceAsync();
        await h.WriteAsync(".agents/archive/epics/1/existing.md", "existing archive");

        h.Prompts.Handler = async invocation =>
        {
            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift)
            {
                return Evaluation("Functionally Complete", "Mixed", "Close With Follow-Up");
            }

            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.SynthesizeCompletedEpic)
            {
                await h.WriteAsync($".agents/archive/epics/{invocation.Label}.md", "# Completed Epic 2");
                return "synthesized";
            }

            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.UpdateRoadmapCompletionContext)
            {
                return "# Roadmap Completion Context\n\nUpdated.";
            }

            throw new InvalidOperationException(invocation.RuntimePromptName);
        };

        CompletionCertificationResult result = await h.Service.CertifyPlanCompletionAsync(new CompletionCertificationRequest(h.Repository));

        Assert.Equal(CompletionCertificationServiceOutcome.Completed, result.Outcome);
        Assert.Equal(2, result.CompletedEpicArchiveIndex);
        Assert.Equal("2", Assert.Single(
            h.Prompts.Invocations,
            invocation => invocation.RuntimePromptName == CompletionRuntimePromptNames.SynthesizeCompletedEpic).Label);
        Assert.Equal("PLAN", await h.ReadAsync(".agents/archive/epics/2/plan.md"));
    }

    [Fact]
    public async Task CompletionEvaluationAndUpdateContextsIncludeReviewSummariesWhenPresent()
    {
        Harness h = Harness.Create();
        await h.SeedExecutionWorkspaceAsync();
        await h.WriteAsync(OrchestrationArtifactPaths.NonImplementationReview, "# Review\n\nUnresolved docs/report.md.");
        await h.WriteAsync(OrchestrationArtifactPaths.NonImplementationSynthesis, "# Synthesis\n\nUseful extracted context.");
        var promptsWithSummaries = new List<string>();

        h.Prompts.Handler = async invocation =>
        {
            if (invocation.RuntimePromptName is CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift
                or CompletionRuntimePromptNames.UpdateRoadmapCompletionContext)
            {
                Assert.Contains("Non-Implementation Review Summary", invocation.ProjectContext, StringComparison.Ordinal);
                Assert.Contains("Unresolved docs/report.md", invocation.ProjectContext, StringComparison.Ordinal);
                Assert.Contains("Non-Implementation Review Synthesis", invocation.ProjectContext, StringComparison.Ordinal);
                Assert.Contains("Useful extracted context", invocation.ProjectContext, StringComparison.Ordinal);
                promptsWithSummaries.Add(invocation.RuntimePromptName);
            }

            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift)
            {
                return Evaluation("Fully Complete", "None", "Close Epic");
            }

            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.SynthesizeCompletedEpic)
            {
                await h.WriteAsync($".agents/archive/epics/{invocation.Label}.md", "# Completed Epic\n\nSynthesized.");
                return "synthesized";
            }

            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.UpdateRoadmapCompletionContext)
            {
                return "# Roadmap Completion Context\n\nUpdated.";
            }

            throw new InvalidOperationException(invocation.RuntimePromptName);
        };

        CompletionCertificationResult result = await h.Service.CertifyPlanCompletionAsync(new CompletionCertificationRequest(h.Repository));

        Assert.Equal(CompletionCertificationServiceOutcome.Completed, result.Outcome);
        Assert.Contains(CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift, promptsWithSummaries);
        Assert.Contains(CompletionRuntimePromptNames.UpdateRoadmapCompletionContext, promptsWithSummaries);
    }

    [Fact]
    public async Task CompletionEvaluationContextReadsExecutionClaimThroughLogicalResolver()
    {
        var executionEvidence = new MemoryExecutionEvidenceStore();
        Harness h = Harness.Create(executionEvidence);
        await h.SeedExecutionWorkspaceAsync();

        h.Prompts.Handler = async invocation =>
        {
            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift)
            {
                Assert.Contains(
                    "Execution Completion Claim: .agents/evidence/execution/main-cli-completion-claim.0001.md",
                    invocation.ProjectContext,
                    StringComparison.Ordinal);
                Assert.Contains("# Main CLI Completion Claim", invocation.ProjectContext, StringComparison.Ordinal);
                Assert.Null(await h.ReadAsync(".agents/evidence/execution/main-cli-completion-claim.0001.md"));
                return Evaluation("Partially Complete", "None", "Continue Epic");
            }

            throw new InvalidOperationException(invocation.RuntimePromptName);
        };

        CompletionCertificationResult result = await h.Service.CertifyPlanCompletionAsync(new CompletionCertificationRequest(h.Repository));

        Assert.Equal(CompletionCertificationServiceOutcome.Blocked, result.Outcome);
        Assert.Equal(".agents/evidence/execution/main-cli-completion-claim.0001.md", Assert.Single(executionEvidence.Records).RelativePath);
    }

    [Fact]
    public async Task CompletionEvaluationContextReadsSqliteExecutionClaimAfterExportIsDeleted()
    {
        using var repo = new TempFileRepo();
        await InitializeExecutionEvidenceDatabaseAsync(repo.Repository);
        await repo.SeedExecutionWorkspaceAsync();
        var executionEvidence = new SqliteExecutionEvidenceStore(repo.Repository);
        var prompts = new FakePromptRunner();
        var service = new CompletionCertificationService(
            repo.Store,
            new FakeProjectionService(),
            prompts,
            new CompletedEpicArchiveService(repo.Store, prompts),
            _executionEvidenceStore: executionEvidence);

        prompts.Handler = async invocation =>
        {
            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift)
            {
                const string claimPath = ".agents/evidence/execution/main-cli-completion-claim.0001.md";
                Assert.Contains($"Execution Completion Claim: {claimPath}", invocation.ProjectContext, StringComparison.Ordinal);
                Assert.Contains("# Main CLI Completion Claim", invocation.ProjectContext, StringComparison.Ordinal);
                Assert.Null(await repo.ReadAsync(claimPath));
                return Evaluation("Partially Complete", "None", "Continue Epic");
            }

            throw new InvalidOperationException(invocation.RuntimePromptName);
        };

        CompletionCertificationResult result = await service.CertifyPlanCompletionAsync(new CompletionCertificationRequest(repo.Repository));

        Assert.Equal(CompletionCertificationServiceOutcome.Blocked, result.Outcome);
        ExecutionEvidenceRecord? record = await executionEvidence.ReadAsync(".agents/evidence/execution/main-cli-completion-claim.0001.md");
        Assert.NotNull(record);
    }

    [Fact]
    public async Task CompletionRuntimeContextRejectsRawProjectContextMarkers()
    {
        Harness h = Harness.Create(projectionService: new FakeProjectionService(
            "<!-- BEGIN PROJECT-CONTEXT FILE: 09-eval-details.md -->"));
        await h.SeedExecutionWorkspaceAsync();

        CompletionCertificationResult result = await h.Service.CertifyPlanCompletionAsync(new CompletionCertificationRequest(h.Repository));

        Assert.Equal(CompletionCertificationServiceOutcome.Failed, result.Outcome);
        Assert.Contains("raw Project Context markers", result.Message, StringComparison.Ordinal);
        Assert.Empty(h.Prompts.Invocations);
    }

    [Fact]
    public async Task AgentCompletionPromptRunner_AppendsImplementationFirstPolicy()
    {
        var runtime = new RecordingAgentRuntime(new AgentTurnResult(
            0,
            AgentTurnState.Completed,
            "ok",
            AgentTokenUsage.Zero));
        var repository = new Repository { Id = Guid.NewGuid(), Name = "repo", Path = "/repo" };
        var runner = new AgentCompletionPromptRunner(runtime, repository);

        string output = await runner.RunAsync(new CompletionRuntimePromptInvocation(
            CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift,
            ProjectContext: "context"));

        Assert.Equal("ok", output);
        string prompt = Assert.Single(runtime.Prompts);
        Assert.Contains("Repository growth is implementation-first", prompt, StringComparison.Ordinal);
        Assert.Contains("The HITL-requested exception is disabled", prompt, StringComparison.Ordinal);
    }

    private static string Evaluation(string completionStatus, string drift, string recommendation) => $$"""
        # Epic Completion and Drift Evaluation

        ## Evaluation Summary

        | Field | Value |
        |---|---|
        | Epic ID | EPIC-TEST |
        | Epic Name | Test Epic |
        | Overall Completion Status | {{completionStatus}} |
        | Overall Drift Classification | {{drift}} |
        | Evidence Strength | Strong |
        | Closure Recommendation | {{recommendation}} |
        | Primary Reason | Test |
        """;

    private static async Task InitializeArchiveDatabaseAsync(Repository repository)
    {
        string databasePath = DatabasePath(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS loop_history(
                kind text not null,
                sequence integer not null,
                logical_path text not null unique,
                body text not null,
                content_hash text not null,
                created_at text not null,
                primary key(kind, sequence)
            );

            CREATE TABLE IF NOT EXISTS execution_evidence(
                logical_path text primary key,
                stem text not null,
                sequence integer not null,
                body text not null,
                content_hash text not null,
                created_at text not null,
                writer text,
                metadata_json text not null,
                unique(stem, sequence)
            );

            CREATE TABLE IF NOT EXISTS roadmap_state(
                id integer primary key check (id = 1),
                document_json text not null,
                updated_at text not null
            );

            CREATE TABLE IF NOT EXISTS transition_journal(
                event_order integer primary key autoincrement,
                correlation_id text not null,
                event_name text not null,
                recorded_at text not null,
                from_state text not null,
                to_state text not null,
                transition text not null,
                projection_path text not null,
                prompt_contract text not null,
                input_hashes_json text not null,
                output_paths_json text not null,
                duration_milliseconds integer not null,
                retry_count integer not null,
                result text not null,
                decision text not null,
                error text,
                input_snapshot_json text
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedArchiveRecordAsync(
        Repository repository,
        string table,
        IReadOnlyDictionary<string, object?> values)
    {
        string databasePath = DatabasePath(repository);
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString());
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        string columns = string.Join(", ", values.Keys);
        string parameters = string.Join(", ", values.Keys.Select(key => "$" + key));
        command.CommandText = $"INSERT INTO {table} ({columns}) VALUES ({parameters});";
        foreach ((string key, object? value) in values)
        {
            command.Parameters.AddWithValue("$" + key, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static string DatabasePath(Repository repository) =>
        Path.Combine(repository.Path, ".LoopRelay", "persistence", "looprelay.sqlite3");

    private static string Sha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private sealed class Harness
    {
        private readonly MemoryArtifactStore store;

        private Harness(MemoryArtifactStore store, Repository repository, FakePromptRunner prompts, CompletionCertificationService service)
        {
            this.store = store;
            Repository = repository;
            Prompts = prompts;
            Service = service;
        }

        public Repository Repository { get; }

        public FakePromptRunner Prompts { get; }

        public CompletionCertificationService Service { get; }

        public static Harness Create(
            IExecutionEvidenceStore? executionEvidenceStore = null,
            IProjectContextProjectionService? projectionService = null)
        {
            var store = new MemoryArtifactStore();
            var repository = new Repository { Id = Guid.NewGuid(), Name = "repo", Path = "/repo" };
            var prompts = new FakePromptRunner();
            var archive = new CompletedEpicArchiveService(store, prompts);
            var service = new CompletionCertificationService(
                store,
                projectionService ?? new FakeProjectionService(),
                prompts,
                archive,
                _executionEvidenceStore: executionEvidenceStore);
            return new Harness(store, repository, prompts, service);
        }

        public async Task SeedExecutionWorkspaceAsync()
        {
            await WriteAsync(CompletionArtifactPaths.ActiveEpic, "# Epic\n\nIntent.");
            await WriteAsync(CompletionArtifactPaths.RoadmapCompletionContext, "# Roadmap Completion Context\n\nCurrent.");
            await WriteAsync(CompletionArtifactPaths.ExecutionPlan, "PLAN");
            await WriteAsync(CompletionArtifactPaths.Details, "DETAILS");
            await WriteAsync(CompletionArtifactPaths.OperationalContext, "OPCTX");
            await WriteAsync(".agents/milestones/m1.md", "- [x] milestone");
            await WriteAsync(".agents/decisions/decisions.md", "DECISION");
            await WriteAsync(".agents/deltas/operational_delta.0001.md", "DELTA");
            await WriteAsync(".agents/handoffs/handoff.md", "HANDOFF");
        }

        public Task WriteAsync(string relativePath, string content) =>
            store.WriteAsync(Resolve(relativePath), content);

        public Task<string?> ReadAsync(string relativePath) =>
            store.ReadAsync(Resolve(relativePath));

        private string Resolve(string relativePath) =>
            ArtifactPath.ResolveRepositoryPath(Repository, relativePath);
    }

    private sealed class FakePromptRunner : ICompletionPromptRunner
    {
        public List<CompletionRuntimePromptInvocation> Invocations { get; } = [];

        public Func<CompletionRuntimePromptInvocation, Task<string>> Handler { get; set; } =
            invocation => throw new InvalidOperationException(invocation.RuntimePromptName);

        public async Task<string> RunAsync(
            CompletionRuntimePromptInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add(invocation);
            return await Handler(invocation);
        }
    }

    private sealed class MemoryExecutionEvidenceStore : IExecutionEvidenceStore
    {
        private readonly List<ExecutionEvidenceRecord> records = [];

        public IReadOnlyList<ExecutionEvidenceRecord> Records => records;

        public Task<ExecutionEvidenceRecord> WriteAsync(string stem, string content)
        {
            int sequence = records
                .Where(record => string.Equals(record.Stem, stem, StringComparison.Ordinal))
                .Select(record => record.Sequence)
                .DefaultIfEmpty()
                .Max() + 1;
            var record = new ExecutionEvidenceRecord(
                stem,
                sequence,
                $".agents/evidence/execution/{stem}.{sequence:0000}.md",
                content);
            records.Add(record);
            return Task.FromResult(record);
        }

        public Task<string> NextPathAsync(string stem)
        {
            int sequence = records
                .Where(record => string.Equals(record.Stem, stem, StringComparison.Ordinal))
                .Select(record => record.Sequence)
                .DefaultIfEmpty()
                .Max() + 1;
            return Task.FromResult($".agents/evidence/execution/{stem}.{sequence:0000}.md");
        }

        public Task<ExecutionEvidenceRecord?> ReadAsync(string relativePath)
        {
            ExecutionEvidenceRecord? record = records.FirstOrDefault(item =>
                string.Equals(item.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(record);
        }

        public Task<IReadOnlyList<ExecutionEvidenceRecord>> ListAsync(string searchPattern = "*.md") =>
            Task.FromResult<IReadOnlyList<ExecutionEvidenceRecord>>(
                records
                    .Where(record => GlobMatches(Path.GetFileName(record.RelativePath), searchPattern))
                    .OrderBy(record => record.Stem, StringComparer.Ordinal)
                    .ThenBy(record => record.Sequence)
                    .ToArray());

        private static bool GlobMatches(string fileName, string searchPattern)
        {
            if (searchPattern == "*")
            {
                return true;
            }

            int star = searchPattern.IndexOf('*', StringComparison.Ordinal);
            if (star < 0)
            {
                return string.Equals(fileName, searchPattern, StringComparison.OrdinalIgnoreCase);
            }

            string prefix = searchPattern[..star];
            string suffix = searchPattern[(star + 1)..];
            return fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                fileName.Length >= prefix.Length + suffix.Length;
        }
    }

    private sealed class RecordingAgentRuntime(AgentTurnResult result) : IAgentRuntime
    {
        public List<string> Prompts { get; } = [];

        public Task<IAgentSession> OpenSessionAsync(
            AgentSessionSpec spec,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AgentTurnResult> RunOneShotAsync(
            AgentSessionSpec spec,
            string prompt,
            Func<AgentStreamChunk, Task>? onChunk = null,
            CancellationToken cancellationToken = default)
        {
            Prompts.Add(prompt);
            return Task.FromResult(result);
        }

        public ValueTask CloseSessionAsync(IAgentSession session) => ValueTask.CompletedTask;
    }

    private sealed class FakeProjectionService(string? projectionContent = null) : IProjectContextProjectionService
    {
        public Task<ProjectContextProjectionResult> EnsureFreshAsync(
            string runtimePromptName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProjectContextProjectionResult(
                new ProjectionDefinition(
                    runtimePromptName,
                    $"ProjectionFor{runtimePromptName}",
                    $".agents/projections/{runtimePromptName}.md",
                    "# Test Projection",
                    runtimePromptName),
                projectionContent ?? $"# Projection for {runtimePromptName}",
                Generated: false,
                ProjectionStaleStatus.Fresh,
                []));

        public Task<ProjectionFreshness> EvaluateFreshnessAsync(
            string runtimePromptName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ProjectionFreshness.Fresh);
    }

    private sealed class TempFileRepo : IDisposable
    {
        public TempFileRepo()
        {
            Root = Path.Combine(Path.GetTempPath(), "looprelay-completion-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Repository = new Repository
            {
                Id = Guid.NewGuid(),
                Name = "repo",
                Path = Root,
            };
            Store = new FileSystemArtifactStore();
        }

        public string Root { get; }

        public Repository Repository { get; }

        public FileSystemArtifactStore Store { get; }

        public async Task SeedExecutionWorkspaceAsync()
        {
            await WriteAsync(CompletionArtifactPaths.ActiveEpic, "# Epic\n\nIntent.");
            await WriteAsync(CompletionArtifactPaths.RoadmapCompletionContext, "# Roadmap Completion Context\n\nCurrent.");
            await WriteAsync(CompletionArtifactPaths.ExecutionPlan, "PLAN");
            await WriteAsync(CompletionArtifactPaths.Details, "DETAILS");
            await WriteAsync(CompletionArtifactPaths.OperationalContext, "OPCTX");
            await WriteAsync(".agents/milestones/m1.md", "- [x] milestone");
            await WriteAsync(".agents/decisions/decisions.md", "DECISION");
            await WriteAsync(".agents/deltas/operational_delta.0001.md", "DELTA");
            await WriteAsync(".agents/handoffs/handoff.md", "HANDOFF");
        }

        public Task WriteAsync(string relativePath, string content) =>
            Store.WriteAsync(Resolve(relativePath), content);

        public Task<string?> ReadAsync(string relativePath) =>
            Store.ReadAsync(Resolve(relativePath));

        public Task DeleteAsync(string relativePath) =>
            Store.DeleteAsync(Resolve(relativePath));

        private string Resolve(string relativePath) =>
            ArtifactPath.ResolveRepositoryPath(Repository, relativePath);

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private static async Task InitializeExecutionEvidenceDatabaseAsync(Repository repository)
    {
        string databasePath = Path.Combine(
            repository.Path,
            ".LoopRelay",
            "persistence",
            "looprelay.sqlite3");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS execution_evidence(
                logical_path text primary key,
                stem text not null,
                sequence integer not null,
                body text not null,
                content_hash text not null,
                created_at text not null,
                writer text,
                metadata_json text not null,
                unique(stem, sequence)
            );

            CREATE INDEX IF NOT EXISTS idx_execution_evidence_stem_sequence_desc
            ON execution_evidence(stem, sequence desc);
            """;
        await command.ExecuteNonQueryAsync();
    }
}
