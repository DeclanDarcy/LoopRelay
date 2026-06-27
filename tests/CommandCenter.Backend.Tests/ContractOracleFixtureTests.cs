using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Continuity.Models;
using CommandCenter.Continuity.Primitives;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Planning;
using CommandCenter.Core.Projections;
using CommandCenter.Core.Repositories;
using CommandCenter.Execution;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Primitives;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Backend.Tests;

public sealed class ContractOracleFixtureTests
{
    private static readonly JsonSerializerOptions BackendJsonOptions = CreateBackendJsonOptions();

    [Fact]
    public void RepositoryDashboardGoldenFixtureMatchesBackendSerialization()
    {
        RepositoryDashboardProjection[] dashboard = [CreateRepresentativeRepositoryDashboardProjection()];
        string actualJson = JsonSerializer.Serialize(dashboard, BackendJsonOptions);
        string expectedJson = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "ContractFixtures",
            "repository-dashboard.golden.json"));

        using JsonDocument expected = JsonDocument.Parse(expectedJson);
        using JsonDocument actual = JsonDocument.Parse(actualJson);

        JsonContractAssert.MatchesFixture(
            expected.RootElement,
            actual.RootElement,
            ContractDriftPolicy.NoCompatibilityDriftAllowed("Repository dashboard"));
    }

    [Fact]
    public void RepositoryWorkspaceGoldenFixtureMatchesBackendSerialization()
    {
        RepositoryWorkspaceProjection workspace = CreateRepresentativeRepositoryWorkspaceProjection();
        string actualJson = JsonSerializer.Serialize(workspace, BackendJsonOptions);
        string expectedJson = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "ContractFixtures",
            "repository-workspace.golden.json"));

        using JsonDocument expected = JsonDocument.Parse(expectedJson);
        using JsonDocument actual = JsonDocument.Parse(actualJson);

        JsonContractAssert.MatchesFixture(
            expected.RootElement,
            actual.RootElement,
            ContractDriftPolicy.NoCompatibilityDriftAllowed("Repository workspace"));
    }

    [Fact]
    public void WorkflowInstanceGoldenFixtureMatchesBackendSerialization()
    {
        WorkflowInstance workflow = CreateRepresentativeWorkflowInstance();
        string actualJson = JsonSerializer.Serialize(workflow, BackendJsonOptions);
        string expectedJson = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "ContractFixtures",
            "workflow-instance.golden.json"));

        using JsonDocument expected = JsonDocument.Parse(expectedJson);
        using JsonDocument actual = JsonDocument.Parse(actualJson);

        JsonContractAssert.MatchesFixture(
            expected.RootElement,
            actual.RootElement,
            ContractDriftPolicy.NoCompatibilityDriftAllowed("Workflow instance"));
    }

    [Fact]
    public void ContractOracleClassifiesMissingFieldAsStructuralDrift()
    {
        using JsonDocument expected = JsonDocument.Parse("""{"name":"Repository","summary":{"count":1}}""");
        using JsonDocument actual = JsonDocument.Parse("""{"summary":{"count":1}}""");

        ContractDrift[] drift = JsonContractAssert.Compare(expected.RootElement, actual.RootElement).ToArray();

        ContractDrift missingField = Assert.Single(drift);
        Assert.Equal(ContractDriftCategory.Structural, missingField.Category);
        Assert.Equal(ContractDriftKind.MissingField, missingField.Kind);
        Assert.Equal("$.name", missingField.Path);
    }

    [Fact]
    public void ContractOracleClassifiesAdditiveFieldAsCompatibilityReviewDrift()
    {
        using JsonDocument expected = JsonDocument.Parse("""{"name":"Repository"}""");
        using JsonDocument actual = JsonDocument.Parse("""{"name":"Repository","displayName":"Repository"}""");

        ContractOracleDriftException exception = Assert.Throws<ContractOracleDriftException>(() =>
            JsonContractAssert.MatchesFixture(
                expected.RootElement,
                actual.RootElement,
                ContractDriftPolicy.NoCompatibilityDriftAllowed("Repository dashboard")));

        Assert.Contains("compatibility review drift", exception.Message, StringComparison.Ordinal);
        Assert.Contains("$.displayName", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContractOracleAllowsReviewedCompatibilityAdditiveField()
    {
        using JsonDocument expected = JsonDocument.Parse("""{"name":"Repository"}""");
        using JsonDocument actual = JsonDocument.Parse("""{"name":"Repository","displayName":"Repository"}""");

        JsonContractAssert.MatchesFixture(
            expected.RootElement,
            actual.RootElement,
            ContractDriftPolicy.WithReviewedCompatibilityAdditions(
                "Repository dashboard",
                "$.displayName"));
    }

    private static RepositoryDashboardProjection CreateRepresentativeRepositoryDashboardProjection()
    {
        DateTimeOffset startedAt = new(2026, 06, 24, 9, 57, 0, TimeSpan.Zero);
        DateTimeOffset completedAt = new(2026, 06, 24, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset generatedAt = new(2026, 06, 24, 10, 0, 0, TimeSpan.Zero);
        var sessionId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var executionSummary = new ExecutionSessionSummary
        {
            SessionId = sessionId,
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.AwaitingAcceptance,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Duration = TimeSpan.FromMinutes(3),
            AcceptedAt = null,
            RejectedAt = null,
            DecisionNote = null,
            LastActivityAt = completedAt,
            ProviderName = "codex",
            ProviderExecutablePath = null,
            ProviderProcessId = null,
            ProviderStartedAt = null,
            HandoffPath = ".agents/handoffs/handoff.md",
            CommitSha = null,
            CommittedAt = null,
            CommitMessage = null,
            PreparationSnapshotId = "prep-0001",
            PushAttemptedAt = null,
            PushedAt = null,
            PushedCommitSha = null,
            PushRemoteName = null,
            PushBranchName = null,
            FailureReason = null
        };

        return new RepositoryDashboardProjection
        {
            Repository = new Repository
            {
                Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                Name = "FixtureRepository",
                Path = "C:/command-center-fixtures/FixtureRepository"
            },
            Availability = RepositoryAvailability.Available,
            Readiness = ExecutionReadiness.Ready,
            ExecutionState = RepositoryExecutionState.AwaitingAcceptance,
            ActiveExecutionSession = null,
            ExecutionSummary = executionSummary,
            ExecutionHistory = [executionSummary],
            MilestoneCount = 2,
            HasCurrentHandoff = true,
            HasCurrentDecisions = true,
            ContinuitySummary = new RepositoryContinuitySummary
            {
                OperationalContextExists = true,
                OperationalContextRevisionCount = 2,
                OperationalContextLastUpdatedAt = generatedAt.AddMinutes(-30),
                OpenQuestionCount = 1,
                ActiveRiskCount = 1,
                PendingProposalExists = false
            },
            ReasoningSummary = new RepositoryReasoningSummary
            {
                EventCount = 4,
                ThreadCount = 1,
                RelationshipCount = 1,
                HypothesisEventCount = 1,
                AlternativeEventCount = 1,
                ContradictionEventCount = 1,
                DirectionEventCount = 1,
                DecisionEvolutionEventCount = 0,
                AssumptionEvolutionEventCount = 0,
                ConstraintEvolutionEventCount = 0,
                EvidenceEventCount = 0,
                LastEventAt = generatedAt.AddMinutes(-20),
                LastThreadActivityAt = generatedAt.AddMinutes(-15),
                LastRelationshipAt = generatedAt.AddMinutes(-10),
                LastActivityAt = generatedAt.AddMinutes(-10),
                LastReconstructionAt = null,
                LastCertificationAt = null,
                CertificationResult = null
            },
            DecisionSessionSummary = new RepositoryDecisionSessionSummary
            {
                DecisionSessionId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                State = "Active",
                LifecycleDecision = "Transfer",
                TransferEligibilityStatus = "Eligible",
                EstimatedTokenCount = 250_000,
                EstimatedCacheTtl = TimeSpan.FromMinutes(12),
                CacheMissRisk = 0.42m,
                CoherenceScore = 0.67m,
                TransferPressure = 0.81m,
                HealthDimensions =
                [
                    new RepositoryDecisionSessionHealthDimension
                    {
                        Name = "Lifecycle",
                        Status = "Warning",
                        Findings = ["Transfer pressure is elevated."]
                    }
                ],
                RecentTransferLineage =
                [
                    new RepositoryDecisionSessionTransferSummary
                    {
                        TransferId = "transfer-1",
                        SourceSessionId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                        TargetSessionId = null,
                        ContinuityArtifactId = "artifact-1",
                        StartedAt = generatedAt.AddMinutes(-5),
                        CompletedAt = generatedAt.AddMinutes(-4),
                        Succeeded = true
                    }
                ],
                Diagnostics = ["registry warning"],
                GeneratedAt = generatedAt
            }
        };
    }

    private static RepositoryWorkspaceProjection CreateRepresentativeRepositoryWorkspaceProjection()
    {
        DateTimeOffset startedAt = new(2026, 06, 25, 8, 30, 0, TimeSpan.Zero);
        DateTimeOffset completedAt = new(2026, 06, 25, 8, 45, 0, TimeSpan.Zero);
        DateTimeOffset generatedAt = new(2026, 06, 25, 9, 0, 0, TimeSpan.Zero);
        var sessionId = Guid.Parse("22222222-3333-4444-5555-666666666666");

        var executionSummary = new ExecutionSessionSummary
        {
            SessionId = sessionId,
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.Ready,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Duration = TimeSpan.FromMinutes(15),
            AcceptedAt = completedAt.AddMinutes(5),
            RejectedAt = null,
            DecisionNote = "Accepted for workspace fixture coverage.",
            LastActivityAt = completedAt,
            ProviderName = "codex",
            ProviderExecutablePath = "C:/tools/codex.exe",
            ProviderProcessId = 4242,
            ProviderStartedAt = startedAt.AddMinutes(-1),
            HandoffPath = ".agents/handoffs/handoff.md",
            CommitSha = "abc1234",
            CommittedAt = completedAt.AddMinutes(10),
            CommitMessage = "Add workspace fixture",
            PreparationSnapshotId = "prep-workspace-0001",
            PushAttemptedAt = completedAt.AddMinutes(12),
            PushedAt = completedAt.AddMinutes(13),
            PushedCommitSha = "abc1234",
            PushRemoteName = "origin",
            PushBranchName = "main",
            FailureReason = null
        };

        var proposalSummary = new OperationalContextProposalSummary
        {
            PendingProposalExists = true,
            LatestProposalId = "proposal-workspace-0001",
            GeneratedAt = generatedAt.AddMinutes(-20),
            Status = OperationalContextProposalStatus.Pending,
            SourceInputCount = 3,
            ContentByteCount = 2048,
            ContentCharacterCount = 1900,
            LastPromotedAt = null,
            LastArchivedRelativePath = null
        };

        return new RepositoryWorkspaceProjection
        {
            Repository = new Repository
            {
                Id = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
                Name = "WorkspaceFixtureRepository",
                Path = "C:/command-center-fixtures/WorkspaceFixtureRepository"
            },
            Availability = RepositoryAvailability.Available,
            Readiness = ExecutionReadiness.Ready,
            ExecutionState = RepositoryExecutionState.Ready,
            ExecutionSummary = executionSummary,
            ExecutionHistory = [executionSummary],
            ArtifactInventory = new ArtifactInventory
            {
                Plan = CreateArtifact(".agents/plan.md", "plan.md", ArtifactType.Plan, ArtifactFamily.Plan, ArtifactVersionKind.Current),
                OperationalContext = CreateArtifact(".agents/operational_context.md", "operational_context.md", ArtifactType.OperationalContext, ArtifactFamily.OperationalContext, ArtifactVersionKind.Current),
                HistoricalOperationalContexts =
                [
                    CreateArtifact(".agents/operational_context.0001.md", "operational_context.0001.md", ArtifactType.OperationalContext, ArtifactFamily.OperationalContext, ArtifactVersionKind.Historical)
                ],
                Milestones =
                [
                    CreateArtifact(".agents/milestones/m0.2-contract-oracle.md", "m0.2-contract-oracle.md", ArtifactType.Milestone, ArtifactFamily.Milestone, ArtifactVersionKind.Current),
                    CreateArtifact(".agents/milestones/m0.2-repository-workspace-fixture-slice-0020.md", "m0.2-repository-workspace-fixture-slice-0020.md", ArtifactType.Milestone, ArtifactFamily.Milestone, ArtifactVersionKind.Current)
                ],
                CurrentHandoff = CreateArtifact(".agents/handoffs/handoff.md", "handoff.md", ArtifactType.Handoff, ArtifactFamily.Handoff, ArtifactVersionKind.Current),
                HistoricalHandoffs =
                [
                    CreateArtifact(".agents/handoffs/handoff.0019.md", "handoff.0019.md", ArtifactType.Handoff, ArtifactFamily.Handoff, ArtifactVersionKind.Historical)
                ],
                CurrentDecisions = null,
                HistoricalDecisions =
                [
                    CreateArtifact(".agents/decisions/decisions.0019.md", "decisions.0019.md", ArtifactType.Decision, ArtifactFamily.Decision, ArtifactVersionKind.Historical)
                ],
                ReasoningArtifacts =
                [
                    CreateArtifact(".agents/reasoning/events.jsonl", "events.jsonl", ArtifactType.Reasoning, ArtifactFamily.Reasoning, ArtifactVersionKind.Current)
                ]
            },
            MilestoneCount = 2,
            HasPlan = true,
            HasOperationalContext = true,
            HasCurrentHandoff = true,
            HasCurrentDecisions = false,
            OperationalContextProposalSummary = proposalSummary,
            OperationalContext = new OperationalContextProjection
            {
                Exists = true,
                CurrentRelativePath = ".agents/operational_context.md",
                RevisionCount = 2,
                CurrentRevisionNumber = 2,
                LastUpdatedAt = generatedAt.AddMinutes(-30),
                LastPromotionAt = generatedAt.AddHours(-2),
                CurrentUnderstandingSummary = ["The Contract Oracle owns serialized backend projection truth."],
                Architecture =
                [
                    CreateOperationalContextItem("arch-1", OperationalContextItemKind.Architecture, "Backend projection serialization is the contract authority.", "M0.2 Oracle decision.", ".agents/plan.md")
                ],
                AuthorityBoundaries =
                [
                    CreateOperationalContextItem("authority-1", OperationalContextItemKind.AuthorityBoundary, "Rust and TypeScript mirrors are consumers, not authorities.", null, ".agents/decisions/decisions.md")
                ],
                Constraints =
                [
                    CreateOperationalContextItem("constraint-1", OperationalContextItemKind.Constraint, "Fixtures require field inventory before capture.", null, null)
                ],
                StableDecisions =
                [
                    CreateOperationalContextItem("decision-1", OperationalContextItemKind.StableDecision, "Repository workspace is the second Oracle family.", "Repeatability before generalization.", ".agents/decisions/decisions.md")
                ],
                DecisionRationale =
                [
                    CreateOperationalContextItem("rationale-1", OperationalContextItemKind.DecisionRationale, "Workspace is less semantically complex than workflow or decisions.", null, null)
                ],
                OpenQuestions =
                [
                    CreateOperationalContextItem("question-1", OperationalContextItemKind.OpenQuestion, "Which command-body contracts should be verified first?", null, null)
                ],
                ActiveRisks =
                [
                    CreateOperationalContextItem("risk-1", OperationalContextItemKind.ActiveRisk, "Shell mirrors can silently drift until generated or pass-through transport replaces them.", null, "src/CommandCenter.Shell/src/main.rs")
                ],
                RecentUnderstandingChanges =
                [
                    CreateOperationalContextItem("change-1", OperationalContextItemKind.RecentChange, "Workspace fixture now exercises artifact inventory and operational context serialization.", null, ".agents/milestones/m0.2-repository-workspace-fixture-slice-0020.md")
                ],
                PendingProposalSummary = proposalSummary,
                LatestReviewState = OperationalContextReviewState.PendingReview,
                ContinuityWarnings = ["Pending proposal has not been promoted."]
            },
            ReasoningSummary = new RepositoryReasoningSummary
            {
                EventCount = 6,
                ThreadCount = 2,
                RelationshipCount = 2,
                HypothesisEventCount = 1,
                AlternativeEventCount = 1,
                ContradictionEventCount = 0,
                DirectionEventCount = 1,
                DecisionEvolutionEventCount = 1,
                AssumptionEvolutionEventCount = 1,
                ConstraintEvolutionEventCount = 1,
                EvidenceEventCount = 0,
                LastEventAt = generatedAt.AddMinutes(-12),
                LastThreadActivityAt = generatedAt.AddMinutes(-8),
                LastRelationshipAt = generatedAt.AddMinutes(-6),
                LastActivityAt = generatedAt.AddMinutes(-6),
                LastReconstructionAt = generatedAt.AddMinutes(-4),
                LastCertificationAt = null,
                CertificationResult = "Pending"
            },
            DecisionSessionSummary = new RepositoryDecisionSessionSummary
            {
                DecisionSessionId = "bbbbbbbb-cccc-dddd-eeee-ffffffffffff",
                State = "Active",
                LifecycleDecision = "Continue",
                TransferEligibilityStatus = "NotEligible",
                EstimatedTokenCount = 175_000,
                EstimatedCacheTtl = TimeSpan.FromMinutes(20),
                CacheMissRisk = 0.18m,
                CoherenceScore = 0.91m,
                TransferPressure = 0.22m,
                HealthDimensions =
                [
                    new RepositoryDecisionSessionHealthDimension
                    {
                        Name = "Coherence",
                        Status = "Healthy",
                        Findings = []
                    }
                ],
                RecentTransferLineage = [],
                Diagnostics = [],
                GeneratedAt = generatedAt
            }
        };
    }

    private static WorkflowInstance CreateRepresentativeWorkflowInstance()
    {
        Guid repositoryId = Guid.Parse("cccccccc-dddd-eeee-ffff-000000000000");
        Guid executionId = Guid.Parse("dddddddd-eeee-ffff-0000-111111111111");
        DateTimeOffset executionStartedAt = new(2026, 06, 26, 8, 0, 0, TimeSpan.Zero);
        DateTimeOffset executionCompletedAt = new(2026, 06, 26, 8, 12, 0, TimeSpan.Zero);
        DateTimeOffset handoffAcceptedAt = new(2026, 06, 26, 8, 20, 0, TimeSpan.Zero);
        DateTimeOffset decisionCreatedAt = new(2026, 06, 26, 8, 30, 0, TimeSpan.Zero);
        DateTimeOffset decisionResolvedAt = new(2026, 06, 26, 8, 45, 0, TimeSpan.Zero);
        DateTimeOffset contextCreatedAt = new(2026, 06, 26, 8, 50, 0, TimeSpan.Zero);
        DateTimeOffset contextReviewedAt = new(2026, 06, 26, 9, 5, 0, TimeSpan.Zero);
        DateTimeOffset timelineBase = new(2026, 06, 26, 9, 10, 0, TimeSpan.Zero);

        WorkflowCompletionEvaluation completion = new(
            repositoryId,
            false,
            "Commit approval is still required before workflow completion.",
            null,
            ["execution accepted", "decision resolved", "context reviewed"],
            ["push evidence is not available yet"]);

        WorkflowExecutionDiagnostics executionDiagnostics = new(
            repositoryId,
            [".agents/handoffs/handoff.md"],
            [],
            [],
            ["Completed execution was accepted and is no longer the active gate."]);

        WorkflowHandoffValidation handoffValidation = new(
            true,
            ["handoff exists", "handoff accepted"],
            []);

        WorkflowHandoffDiagnostics handoffDiagnostics = new(
            repositoryId,
            [".agents/handoffs/handoff.md"],
            [],
            [],
            ["Accepted handoff allows decision review to proceed."]);

        WorkflowDecisionDiagnostics decisionDiagnostics = new(
            repositoryId,
            ["decisions", "governance", "quality", "certification"],
            ["Resolved decision closes the decision gate."],
            ["No blocking governance findings."],
            ["Quality score mixed:61."],
            ["Decision lifecycle certification passed."],
            [],
            []);

        WorkflowOperationalContextDiagnostics operationalContextDiagnostics = new(
            repositoryId,
            ["operational-context-proposal", "decision DEC-0007"],
            ["Reviewed operational context is ready for promotion before commit."],
            ["Review accepted by human operator."],
            ["Promotion has not been recorded."],
            ["Proposal links to decision DEC-0007 and execution dddddddd-eeee-ffff-0000-111111111111."],
            []);

        WorkflowGitDiagnostics gitDiagnostics = new(
            repositoryId,
            ["git status", "commit preparation"],
            ["push result"],
            ["Pending changes require commit approval."],
            ["Push cannot run before commit."],
            ["Git stage is waiting on human commit approval."],
            []);

        WorkflowTransition decisionToContext = new(
            WorkflowStage.Decision,
            WorkflowStage.OperationalContext,
            WorkflowGateType.DecisionResolution,
            WorkflowBlockingCondition.UnresolvedDecision,
            "Decision resolution allows operational context review.");
        WorkflowTransition contextToCommit = new(
            WorkflowStage.OperationalContext,
            WorkflowStage.Commit,
            WorkflowGateType.OperationalContextReview,
            WorkflowBlockingCondition.PendingContextReview,
            "Reviewed operational context allows commit approval.");
        WorkflowTransition commitToPush = new(
            WorkflowStage.Commit,
            WorkflowStage.Push,
            WorkflowGateType.CommitApproval,
            WorkflowBlockingCondition.PendingCommitApproval,
            "Commit approval allows push approval.");

        WorkflowTransitionResult validTransition = new(
            contextToCommit,
            true,
            false,
            new WorkflowGateResolution(
                WorkflowGateType.OperationalContextReview,
                WorkflowBlockingCondition.PendingContextReview,
                "Review the operational context proposal.",
                true),
            null,
            "Operational context review is satisfied.");
        WorkflowTransitionResult blockedTransition = new(
            commitToPush,
            false,
            true,
            new WorkflowGateResolution(
                WorkflowGateType.CommitApproval,
                WorkflowBlockingCondition.PendingCommitApproval,
                "Approve the prepared commit.",
                false),
            WorkflowBlockingCondition.PendingCommitApproval,
            "Commit approval is required before push.");

        WorkflowGateEvidence openGateEvidence = new(
            "git",
            ".agents/workflow/gates.json",
            "Pending changes require a reviewed commit.",
            timelineBase.AddMinutes(1),
            "gate-open-fingerprint");
        WorkflowGate openGate = new(
            "commit-approval",
            WorkflowGateType.CommitApproval,
            repositoryId,
            WorkflowStage.Commit,
            WorkflowGateStatus.Open,
            "Approve the prepared commit.",
            "commit_execution_session",
            ["commit_execution_session"],
            "git",
            ".agents/workflow/gates.json",
            timelineBase.AddMinutes(1),
            null,
            null,
            "Pending changes are present.",
            [openGateEvidence]);

        WorkflowGateEvidence satisfiedGateEvidence = new(
            "decisions",
            ".agents/decisions/decisions.md",
            "Decision DEC-0007 was resolved.",
            decisionResolvedAt,
            "gate-satisfied-fingerprint");
        WorkflowGate satisfiedGate = new(
            "decision-resolution",
            WorkflowGateType.DecisionResolution,
            repositoryId,
            WorkflowStage.Decision,
            WorkflowGateStatus.Satisfied,
            "Resolve the generated decision.",
            "resolve_decision",
            ["resolve_decision"],
            "decisions",
            ".agents/decisions/decisions.md",
            decisionCreatedAt,
            decisionResolvedAt,
            "human",
            "Decision was resolved.",
            [satisfiedGateEvidence]);

        WorkflowGateDiagnostics gateDiagnostics = new(
            repositoryId,
            WorkflowGateType.CommitApproval,
            [openGate],
            [satisfiedGate],
            ["CommitApproval=commit_execution_session", "PushApproval=push_execution_session"],
            ["Commit approval is the only open gate."],
            [],
            []);

        WorkflowStateMachineDiagnostics stateMachineDiagnostics = new(
            repositoryId,
            WorkflowStage.Commit,
            WorkflowProgressState.AwaitingGate,
            WorkflowGateType.CommitApproval,
            [WorkflowStage.Push],
            [validTransition],
            [blockedTransition],
            ["Canonical graph selected Commit because context review is complete and commit approval is pending."],
            ["Push transition rejected until commit approval exists."]);

        WorkflowProjectionDiagnostics projectionDiagnostics = new(
            repositoryId,
            ["execution", "handoff", "decisions", "operational-context", "git"],
            WorkflowStage.Commit,
            WorkflowGateType.CommitApproval,
            [WorkflowStage.Push],
            [validTransition],
            [blockedTransition],
            stateMachineDiagnostics,
            ["Flattened workflow fields derive from nested backend projection state."],
            [],
            []);

        return new WorkflowInstance(
            repositoryId,
            WorkflowStage.Commit,
            WorkflowProgressState.AwaitingGate,
            WorkflowGateType.CommitApproval,
            "Approve the prepared commit.",
            new WorkflowExecutionProjection(
                repositoryId,
                executionId,
                WorkflowExecutionStatus.Completed,
                executionStartedAt,
                executionCompletedAt,
                null,
                handoffAcceptedAt,
                null,
                null,
                null,
                null,
                null,
                true,
                true,
                null,
                false,
                null,
                executionDiagnostics),
            WorkflowExecutionStatus.Completed,
            false,
            null,
            executionDiagnostics,
            new WorkflowHandoffProjection(
                repositoryId,
                executionId,
                "handoff-0007",
                ".agents/handoffs/handoff.md",
                WorkflowHandoffStatus.Accepted,
                executionCompletedAt,
                handoffAcceptedAt,
                null,
                true,
                "Execution accepted for workflow fixture coverage.",
                handoffValidation,
                handoffDiagnostics),
            WorkflowHandoffStatus.Accepted,
            handoffValidation,
            handoffDiagnostics,
            new WorkflowDecisionProjection(
                repositoryId,
                "DEC-0007",
                null,
                null,
                "proposal-0007",
                "package-0007",
                WorkflowDecisionStatus.Resolved,
                "ReadyForResolution",
                "Accepted",
                "MinorRefinement",
                decisionCreatedAt,
                decisionResolvedAt,
                true,
                false,
                "Clear",
                "Mixed:61",
                "Passed",
                null,
                decisionDiagnostics),
            WorkflowDecisionStatus.Resolved,
            true,
            false,
            decisionDiagnostics,
            new WorkflowOperationalContextProjection(
                repositoryId,
                "ctx-0007",
                WorkflowOperationalContextStatus.Accepted,
                "Accepted",
                "PendingPromotion",
                contextCreatedAt,
                contextReviewedAt,
                null,
                "operator",
                "Operational context reviewed for the workflow fixture.",
                "DEC-0007",
                executionId.ToString(),
                false,
                true,
                false,
                operationalContextDiagnostics),
            WorkflowOperationalContextStatus.Accepted,
            false,
            true,
            false,
            operationalContextDiagnostics,
            new WorkflowGitProjection(
                repositoryId,
                WorkflowGitStatus.AwaitingCommit,
                WorkflowGitStatus.NotReady,
                null,
                "feature/workflow-fixture",
                null,
                null,
                true,
                false,
                true,
                false,
                true,
                false,
                completion,
                gitDiagnostics),
            WorkflowGitStatus.AwaitingCommit,
            WorkflowGitStatus.NotReady,
            true,
            false,
            completion,
            gitDiagnostics,
            [WorkflowStage.Push],
            [validTransition],
            [blockedTransition],
            [
                new WorkflowTimelineEntry(
                    WorkflowTimelineEventType.ExecutionCompleted,
                    WorkflowStage.Handoff,
                    executionCompletedAt,
                    "Execution completed and produced handoff evidence.",
                    "execution",
                    ".agents/handoffs/handoff.md",
                    "timeline-execution-completed"),
                new WorkflowTimelineEntry(
                    WorkflowTimelineEventType.DecisionResolved,
                    WorkflowStage.Decision,
                    decisionResolvedAt,
                    "Decision DEC-0007 resolved.",
                    "decisions",
                    ".agents/decisions/decisions.md",
                    "timeline-decision-resolved"),
                new WorkflowTimelineEntry(
                    WorkflowTimelineEventType.OperationalContextAccepted,
                    WorkflowStage.OperationalContext,
                    contextReviewedAt,
                    "Operational context proposal accepted.",
                    "continuity",
                    ".agents/operational_context.md",
                    "timeline-context-accepted")
            ],
            [openGate],
            [satisfiedGate],
            [satisfiedGate],
            gateDiagnostics,
            projectionDiagnostics)
        {
            DecisionSession = null
        };
    }

    private static Artifact CreateArtifact(
        string relativePath,
        string name,
        ArtifactType type,
        ArtifactFamily family,
        ArtifactVersionKind versionKind)
    {
        return new Artifact
        {
            RelativePath = relativePath,
            Name = name,
            Type = type,
            Family = family,
            VersionKind = versionKind
        };
    }

    private static OperationalContextItem CreateOperationalContextItem(
        string id,
        OperationalContextItemKind kind,
        string text,
        string? rationale,
        string? sourceRelativePath)
    {
        return new OperationalContextItem
        {
            Id = id,
            Kind = kind,
            Text = text,
            Rationale = rationale,
            SourceRelativePath = sourceRelativePath
        };
    }

    private static JsonSerializerOptions CreateBackendJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static class JsonContractAssert
    {
        public static void MatchesFixture(JsonElement expected, JsonElement actual, ContractDriftPolicy policy)
        {
            ContractDrift[] drifts = Compare(expected, actual, policy).ToArray();
            if (drifts.Length == 0)
            {
                return;
            }

            throw new ContractOracleDriftException(policy.ContractName, drifts);
        }

        public static IEnumerable<ContractDrift> Compare(JsonElement expected, JsonElement actual)
        {
            return Compare(expected, actual, ContractDriftPolicy.NoCompatibilityDriftAllowed("Contract"));
        }

        private static IEnumerable<ContractDrift> Compare(JsonElement expected, JsonElement actual, ContractDriftPolicy policy)
        {
            return Compare(expected, actual, "$", policy);
        }

        private static IEnumerable<ContractDrift> Compare(
            JsonElement expected,
            JsonElement actual,
            string path,
            ContractDriftPolicy policy)
        {
            if (expected.ValueKind != actual.ValueKind)
            {
                yield return ContractDrift.Structural(
                    ContractDriftKind.ValueKindChanged,
                    path,
                    $"expected {expected.ValueKind}, actual {actual.ValueKind}");
                yield break;
            }

            switch (expected.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (ContractDrift drift in CompareObjects(expected, actual, path, policy))
                    {
                        yield return drift;
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (ContractDrift drift in CompareArrays(expected, actual, path, policy))
                    {
                        yield return drift;
                    }

                    break;
                case JsonValueKind.String:
                    if (!StringComparer.Ordinal.Equals(expected.GetString(), actual.GetString()))
                    {
                        yield return ContractDrift.Structural(
                            ContractDriftKind.ValueChanged,
                            path,
                            $"expected {expected.GetRawText()}, actual {actual.GetRawText()}");
                    }

                    break;
                case JsonValueKind.Number:
                    if (!StringComparer.Ordinal.Equals(expected.GetRawText(), actual.GetRawText()))
                    {
                        yield return ContractDrift.Structural(
                            ContractDriftKind.ValueChanged,
                            path,
                            $"expected {expected.GetRawText()}, actual {actual.GetRawText()}");
                    }

                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (expected.GetBoolean() != actual.GetBoolean())
                    {
                        yield return ContractDrift.Structural(
                            ContractDriftKind.ValueChanged,
                            path,
                            $"expected {expected.GetBoolean()}, actual {actual.GetBoolean()}");
                    }

                    break;
                case JsonValueKind.Null:
                    break;
                default:
                    if (!StringComparer.Ordinal.Equals(expected.GetRawText(), actual.GetRawText()))
                    {
                        yield return ContractDrift.Structural(
                            ContractDriftKind.ValueChanged,
                            path,
                            $"expected {expected.GetRawText()}, actual {actual.GetRawText()}");
                    }

                    break;
            }
        }

        private static IEnumerable<ContractDrift> CompareObjects(
            JsonElement expected,
            JsonElement actual,
            string path,
            ContractDriftPolicy policy)
        {
            Dictionary<string, JsonElement> expectedProperties = expected.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value);
            Dictionary<string, JsonElement> actualProperties = actual.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value);

            foreach (string missing in expectedProperties.Keys.Except(actualProperties.Keys, StringComparer.Ordinal))
            {
                yield return ContractDrift.Structural(
                    ContractDriftKind.MissingField,
                    BuildPropertyPath(path, missing),
                    "field exists in the fixture but not in backend serialization");
            }

            foreach (string unexpected in actualProperties.Keys.Except(expectedProperties.Keys, StringComparer.Ordinal))
            {
                string unexpectedPath = BuildPropertyPath(path, unexpected);
                if (!policy.IsReviewedCompatibilityAddition(unexpectedPath))
                {
                    yield return ContractDrift.CompatibilityReview(
                        ContractDriftKind.UnexpectedField,
                        unexpectedPath,
                        "backend serialization added a field that requires fixture and consumer review");
                }
            }

            foreach ((string name, JsonElement expectedValue) in expectedProperties)
            {
                if (actualProperties.TryGetValue(name, out JsonElement actualValue))
                {
                    foreach (ContractDrift drift in Compare(expectedValue, actualValue, BuildPropertyPath(path, name), policy))
                    {
                        yield return drift;
                    }
                }
            }
        }

        private static IEnumerable<ContractDrift> CompareArrays(
            JsonElement expected,
            JsonElement actual,
            string path,
            ContractDriftPolicy policy)
        {
            JsonElement[] expectedItems = expected.EnumerateArray().ToArray();
            JsonElement[] actualItems = actual.EnumerateArray().ToArray();
            if (expectedItems.Length != actualItems.Length)
            {
                yield return ContractDrift.Structural(
                    ContractDriftKind.ArrayLengthChanged,
                    path,
                    $"expected {expectedItems.Length}, actual {actualItems.Length}");
                yield break;
            }

            for (int i = 0; i < expectedItems.Length; i++)
            {
                foreach (ContractDrift drift in Compare(expectedItems[i], actualItems[i], $"{path}[{i}]", policy))
                {
                    yield return drift;
                }
            }
        }

        private static string BuildPropertyPath(string path, string propertyName)
        {
            return $"{path}.{propertyName}";
        }
    }

    private sealed class ContractOracleDriftException(string contractName, IReadOnlyList<ContractDrift> drifts)
        : Exception(CreateMessage(contractName, drifts))
    {
        private static string CreateMessage(string contractName, IReadOnlyList<ContractDrift> drifts)
        {
            string details = string.Join(
                Environment.NewLine,
                drifts.Select(drift => $"- {drift.Category} {drift.Kind} at {drift.Path}: {drift.Message}"));

            bool hasStructuralDrift = drifts.Any(drift => drift.Category == ContractDriftCategory.Structural);
            bool hasCompatibilityReviewDrift = drifts.Any(drift => drift.Category == ContractDriftCategory.CompatibilityReview);
            string summary = (hasStructuralDrift, hasCompatibilityReviewDrift) switch
            {
                (true, true) => "structural drift and compatibility review drift",
                (true, false) => "structural drift",
                (false, true) => "compatibility review drift",
                _ => "contract drift"
            };

            return $"{contractName} Contract Oracle detected {summary}:{Environment.NewLine}{details}";
        }
    }

    private sealed class ContractDriftPolicy
    {
        private readonly HashSet<string> _reviewedCompatibilityAdditions;

        private ContractDriftPolicy(string contractName, IEnumerable<string> reviewedCompatibilityAdditions)
        {
            ContractName = contractName;
            _reviewedCompatibilityAdditions = new HashSet<string>(
                reviewedCompatibilityAdditions,
                StringComparer.Ordinal);
        }

        public string ContractName { get; }

        public static ContractDriftPolicy NoCompatibilityDriftAllowed(string contractName)
        {
            return new ContractDriftPolicy(contractName, []);
        }

        public static ContractDriftPolicy WithReviewedCompatibilityAdditions(
            string contractName,
            params string[] reviewedCompatibilityAdditions)
        {
            return new ContractDriftPolicy(contractName, reviewedCompatibilityAdditions);
        }

        public bool IsReviewedCompatibilityAddition(string path)
        {
            return _reviewedCompatibilityAdditions.Contains(path);
        }
    }

    private sealed record ContractDrift(
        ContractDriftCategory Category,
        ContractDriftKind Kind,
        string Path,
        string Message)
    {
        public static ContractDrift Structural(ContractDriftKind kind, string path, string message)
        {
            return new ContractDrift(ContractDriftCategory.Structural, kind, path, message);
        }

        public static ContractDrift CompatibilityReview(ContractDriftKind kind, string path, string message)
        {
            return new ContractDrift(ContractDriftCategory.CompatibilityReview, kind, path, message);
        }
    }

    private enum ContractDriftCategory
    {
        Structural,
        CompatibilityReview
    }

    private enum ContractDriftKind
    {
        MissingField,
        UnexpectedField,
        ValueKindChanged,
        ValueChanged,
        ArrayLengthChanged
    }
}
