using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend;
using CommandCenter.Backend.Services;
using CommandCenter.Continuity.Models;
using CommandCenter.Continuity.Primitives;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Modules;
using CommandCenter.Execution.Primitives;
using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Projections;
using CommandCenter.Reasoning.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionReasoningCaptureServiceTests
{
    [Fact]
    public async Task OperationalContextPromotionCaptureIsIdempotentAndSelective()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        var reasoningRepository = new FileSystemReasoningRepository(
            new FileSystemArtifactStore(),
            new ReasoningArtifactProjectionService());
        var captureService = new DecisionReasoningCaptureService(
            new StubRepositoryService(repository),
            decisionRepository,
            new FileSystemArtifactStore(),
            reasoningRepository);
        OperationalContextProposal proposal = CreatePromotedOperationalContextProposal(repository.Id);

        await captureService.CaptureOperationalContextPromotionAsync(repository.Id, proposal);
        await captureService.CaptureOperationalContextPromotionAsync(repository.Id, proposal);

        IReadOnlyList<ReasoningEvent> reasoningEvents = await reasoningRepository.ListEventsAsync(repository);
        Assert.Equal(2, reasoningEvents.Count);
        Assert.Contains(reasoningEvents, reasoningEvent =>
            reasoningEvent.Family == ReasoningEventFamily.ConstraintEvolution &&
            reasoningEvent.Type == ReasoningEventType.ConstraintIntroduced &&
            reasoningEvent.Provenance.SourceKind == "InferredOperationalContextPromotion" &&
            reasoningEvent.References.Any(reference =>
                reference.Kind == ReasoningReferenceKind.OperationalContextRevision &&
                reference.Id == "oc-proposal-1" &&
                reference.RelativePath == ".agents/operational_context/proposals/oc-proposal-1/metadata.json"));
        Assert.Contains(reasoningEvents, reasoningEvent =>
            reasoningEvent.Family == ReasoningEventFamily.DecisionEvolution &&
            reasoningEvent.Type == ReasoningEventType.EvidenceAdded &&
            reasoningEvent.Narrative.Details.Contains("Operational context remains authoritative current understanding", StringComparison.Ordinal));
        Assert.DoesNotContain(reasoningEvents, reasoningEvent =>
            reasoningEvent.Title.Contains("Should cache derived graph", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OperationalContextPromotionEndpointCapturesReasoningAfterPromotionPersists()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "plan");
        await WriteAsync(repository, ".agents/handoffs/handoff.md", "# Handoff\n\n- Keep backend workflow authority.");
        await WriteAsync(repository, ".agents/decisions/decisions.md", """
            # Decisions

            - Reasoning capture must observe promoted operational context because current understanding remains authoritative.
            """);
        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        OperationalContextProposal generated = (await (await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/operational-context/generate",
            null)).Content.ReadFromJsonAsync<OperationalContextProposal>(jsonOptions))!;
        await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/operational-context/proposals/{generated.ProposalId}/accept",
            new OperationalContextProposalReviewRequest { ReviewNote = "Promote durable understanding." },
            jsonOptions);

        HttpResponseMessage promoteResponse = await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/operational-context/proposals/{generated.ProposalId}/promote",
            null);
        ReasoningEvent[] reasoningEvents = (await (await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/events"))
            .Content.ReadFromJsonAsync<ReasoningEvent[]>(jsonOptions))!;

        Assert.Equal(HttpStatusCode.OK, promoteResponse.StatusCode);
        Assert.Contains(reasoningEvents, reasoningEvent =>
            reasoningEvent.Provenance.SourceKind == "InferredOperationalContextPromotion" &&
            reasoningEvent.Family == ReasoningEventFamily.DecisionEvolution);
        ReasoningEvent reasoningEvent = reasoningEvents.First(reasoningEvent =>
            reasoningEvent.Provenance.SourceKind == "InferredOperationalContextPromotion" &&
            reasoningEvent.Family == ReasoningEventFamily.DecisionEvolution);
        Assert.Contains("changed project understanding", reasoningEvent.Narrative.Summary);
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.Artifact &&
            reference.Id == ".agents/operational_context.md");
    }

    [Fact]
    public async Task OperationalContextPromotionEndpointDoesNotCaptureReasoningWhenPromotionFails()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "plan");
        await WriteAsync(repository, ".agents/handoffs/handoff.md", "# Handoff\n\n- Pending proposal only.");
        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        OperationalContextProposal generated = (await (await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/operational-context/generate",
            null)).Content.ReadFromJsonAsync<OperationalContextProposal>(jsonOptions))!;
        HttpResponseMessage promoteResponse = await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/operational-context/proposals/{generated.ProposalId}/promote",
            null);
        ReasoningEvent[] reasoningEvents = (await (await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/events"))
            .Content.ReadFromJsonAsync<ReasoningEvent[]>(jsonOptions))!;

        Assert.Equal(HttpStatusCode.Conflict, promoteResponse.StatusCode);
        Assert.Empty(reasoningEvents);
    }

    [Fact]
    public async Task ExecutionHandoffDecisionCaptureIsIdempotentAndSemantic()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/handoffs/handoff.md", """
            # Handoff

            ## New State

            - Direction shifted from workflow capture to semantic reasoning evidence.
            """);
        var reasoningRepository = new FileSystemReasoningRepository(
            new FileSystemArtifactStore(),
            new ReasoningArtifactProjectionService());
        var captureService = new DecisionReasoningCaptureService(
            new StubRepositoryService(repository),
            new InMemoryDecisionRepository(),
            new FileSystemArtifactStore(),
            reasoningRepository);
        ExecutionSession session = CreateDecidedExecutionSession(repository, accepted: true, "Direction shifted after review.");

        await captureService.CaptureExecutionHandoffDecisionAsync(session, accepted: true);
        await captureService.CaptureExecutionHandoffDecisionAsync(session, accepted: true);

        ReasoningEvent reasoningEvent = Assert.Single(await reasoningRepository.ListEventsAsync(repository));
        Assert.Equal(ReasoningEventFamily.Direction, reasoningEvent.Family);
        Assert.Equal(ReasoningEventType.DirectionShifted, reasoningEvent.Type);
        Assert.Equal("InferredExecutionHandoffAcceptance", reasoningEvent.Provenance.SourceKind);
        Assert.Equal(ReasoningCaptureMode.Inferred, reasoningEvent.CaptureProvenance?.Mode);
        Assert.Equal("ExecutionHandoffAcceptedReasoningObserved", reasoningEvent.CaptureProvenance?.SourceTransition);
        Assert.Equal(".agents/handoffs/handoff.md", reasoningEvent.CaptureProvenance?.SourceArtifact);
        Assert.Equal(reasoningEvent.CreatedAt, reasoningEvent.CaptureProvenance?.SourceTimestamp);
        Assert.Contains("Fingerprint ", reasoningEvent.CaptureProvenance?.DuplicateSignal);
        Assert.Contains("Execution remains workflow authority", reasoningEvent.Narrative.Details);
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.Handoff &&
            reference.Id == ".agents/handoffs/handoff.md");
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.ExecutionOutput &&
            reference.Id == session.Id.ToString("D"));
    }

    [Fact]
    public async Task ExecutionHandoffDecisionCaptureSkipsWorkflowOnlyTransitions()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/handoffs/handoff.md", "# Handoff\n\nCompleted requested work.");
        var reasoningRepository = new FileSystemReasoningRepository(
            new FileSystemArtifactStore(),
            new ReasoningArtifactProjectionService());
        var captureService = new DecisionReasoningCaptureService(
            new StubRepositoryService(repository),
            new InMemoryDecisionRepository(),
            new FileSystemArtifactStore(),
            reasoningRepository);
        ExecutionSession session = CreateDecidedExecutionSession(repository, accepted: true, "accepted");

        await captureService.CaptureExecutionHandoffDecisionAsync(session, accepted: true);

        Assert.Empty(await reasoningRepository.ListEventsAsync(repository));
    }

    [Fact]
    public async Task ExecutionHandoffRejectionCapturesSemanticReasonWhenPresent()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/handoffs/handoff.md", """
            # Handoff

            ## Review Outcome

            - Proposed direction conflicts with the repository authority boundary.
            """);
        var reasoningRepository = new FileSystemReasoningRepository(
            new FileSystemArtifactStore(),
            new ReasoningArtifactProjectionService());
        var captureService = new DecisionReasoningCaptureService(
            new StubRepositoryService(repository),
            new InMemoryDecisionRepository(),
            new FileSystemArtifactStore(),
            reasoningRepository);
        ExecutionSession session = CreateDecidedExecutionSession(repository, accepted: false, "Direction should be abandoned.");

        await captureService.CaptureExecutionHandoffDecisionAsync(session, accepted: false);

        ReasoningEvent reasoningEvent = Assert.Single(await reasoningRepository.ListEventsAsync(repository));
        Assert.Equal(ReasoningEventFamily.Direction, reasoningEvent.Family);
        Assert.Equal(ReasoningEventType.DirectionAbandoned, reasoningEvent.Type);
        Assert.Equal("InferredExecutionHandoffRejection", reasoningEvent.Provenance.SourceKind);
    }

    [Fact]
    public async Task ExecutionHandoffAcceptEndpointCapturesReasoningAfterAcceptancePersists()
    {
        Repository repository = CreateRepository();
        string storePath = Path.Combine(repository.Path, "execution-sessions.json");
        await WriteAsync(repository, ".agents/handoffs/handoff.md", """
            # Handoff

            ## New State

            - Assumption invalidated: handoff workflow state alone is not meaningful reasoning.
            """);
        ExecutionSession session = CreateAwaitingAcceptanceExecutionSession(repository);
        await new CommandCenter.Execution.Services.FileSystemExecutionSessionStore(storePath).SaveAsync([session]);
        await using WebApplication app = Program.CreateApp(
            [],
            services =>
            {
                services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository));
                services.AddSingleton<IExecutionProvider>(new FakeExecutionProvider());
                services.AddSingleton<IExecutionSessionStore>(
                    new CommandCenter.Execution.Services.FileSystemExecutionSessionStore(storePath));
                services.AddSingleton<IGitService>(new FakeGitService());
            });
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        HttpResponseMessage acceptResponse = await client.PostAsJsonAsync(
            $"{app.Urls.Single()}/api/execution-sessions/{session.Id}/accept",
            new ExecutionAcceptanceRequest { DecisionNote = "Assumption invalidated by the execution output." },
            jsonOptions);
        ReasoningEvent[] reasoningEvents = (await (await client.GetAsync(
            $"{app.Urls.Single()}/api/repositories/{repository.Id}/reasoning/events"))
            .Content.ReadFromJsonAsync<ReasoningEvent[]>(jsonOptions))!;
        ExecutionSession persistedSession = (await new CommandCenter.Execution.Services.FileSystemExecutionSessionStore(storePath)
                .LoadAsync())
            .Single(storedSession => storedSession.Id == session.Id);

        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        Assert.NotNull(persistedSession.AcceptedAt);
        ReasoningEvent reasoningEvent = Assert.Single(reasoningEvents);
        Assert.Equal(ReasoningEventFamily.AssumptionEvolution, reasoningEvent.Family);
        Assert.Equal(ReasoningEventType.AssumptionInvalidated, reasoningEvent.Type);
        Assert.Equal("InferredExecutionHandoffAcceptance", reasoningEvent.Provenance.SourceKind);
    }

    [Fact]
    public async Task FailedExecutionHandoffAcceptanceDoesNotCaptureReasoning()
    {
        Repository repository = CreateRepository();
        string storePath = Path.Combine(repository.Path, "execution-sessions.json");
        await WriteAsync(repository, ".agents/handoffs/handoff.md", "# Handoff\n\nDirection shifted.");
        ExecutionSession session = CreateDecidedExecutionSession(repository, accepted: true, "Already accepted.");
        await new CommandCenter.Execution.Services.FileSystemExecutionSessionStore(storePath).SaveAsync([session]);
        await using WebApplication app = Program.CreateApp(
            [],
            services =>
            {
                services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository));
                services.AddSingleton<IExecutionProvider>(new FakeExecutionProvider());
                services.AddSingleton<IExecutionSessionStore>(
                    new CommandCenter.Execution.Services.FileSystemExecutionSessionStore(storePath));
                services.AddSingleton<IGitService>(new FakeGitService());
            });
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        HttpResponseMessage acceptResponse = await client.PostAsJsonAsync(
            $"{app.Urls.Single()}/api/execution-sessions/{session.Id}/accept",
            new ExecutionAcceptanceRequest { DecisionNote = "Direction shifted." },
            jsonOptions);
        ReasoningEvent[] reasoningEvents = (await (await client.GetAsync(
            $"{app.Urls.Single()}/api/repositories/{repository.Id}/reasoning/events"))
            .Content.ReadFromJsonAsync<ReasoningEvent[]>(jsonOptions))!;

        Assert.Equal(HttpStatusCode.Conflict, acceptResponse.StatusCode);
        Assert.Empty(reasoningEvents);
    }

    [Fact]
    public async Task GovernanceContradictionCaptureIsIdempotentAndSelective()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        var reasoningRepository = new FileSystemReasoningRepository(
            new FileSystemArtifactStore(),
            new ReasoningArtifactProjectionService());
        var captureService = new DecisionReasoningCaptureService(
            new StubRepositoryService(repository),
            decisionRepository,
            new FileSystemArtifactStore(),
            reasoningRepository);
        DecisionGovernanceReport report = CreateGovernanceReport(repository.Id);

        await captureService.CaptureGovernanceContradictionsAsync(repository.Id, report);
        await captureService.CaptureGovernanceContradictionsAsync(repository.Id, report);

        ReasoningEvent reasoningEvent = Assert.Single(await reasoningRepository.ListEventsAsync(repository));
        Assert.Equal(ReasoningEventFamily.Contradiction, reasoningEvent.Family);
        Assert.Equal(ReasoningEventType.ContradictionIdentified, reasoningEvent.Type);
        Assert.Equal("InferredGovernanceContradiction", reasoningEvent.Provenance.SourceKind);
        Assert.Contains("Governance remains advisory", reasoningEvent.Narrative.Details);
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.GovernanceFinding &&
            reference.Id == "GOV-0001" &&
            reference.RelativePath == ".agents/decisions/governance/governance.202606230000000000000.json");
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.Decision &&
            reference.Id == "DEC-0001");
        Assert.DoesNotContain(reasoningEvent.Title, "Promoted candidate");
    }

    [Fact]
    public async Task GovernanceReportEndpointCapturesContradictionReasoningAfterReportPersists()
    {
        Repository repository = CreateRepository();
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        Decision first = CreateResolvedDecision(repository.Id);
        Decision second = CreateResolvedDecision(repository.Id, "DEC-0002", "Use local-only authority") with
        {
            Relationships =
            [
                new DecisionRelationship(
                    new DecisionId("DEC-0002"),
                    new DecisionId("DEC-0001"),
                    DecisionRelationshipType.ConflictsWith,
                    "Directions cannot both be active.")
            ]
        };
        await decisionRepository.SaveDecisionAsync(repository, first);
        await decisionRepository.SaveDecisionAsync(repository, second);
        await ProjectAllAsync(repository, decisionRepository, store);

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        HttpResponseMessage response = await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/governance/reports",
            null);
        ReasoningEvent[] reasoningEvents = (await (await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/events"))
            .Content.ReadFromJsonAsync<ReasoningEvent[]>(jsonOptions))!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ReasoningEvent reasoningEvent = Assert.Single(reasoningEvents, reasoningEvent =>
            reasoningEvent.Family == ReasoningEventFamily.Contradiction &&
            reasoningEvent.Type == ReasoningEventType.ContradictionIdentified &&
            reasoningEvent.Title.Contains("Conflicting resolved decisions", StringComparison.Ordinal));
        Assert.Equal("InferredGovernanceContradiction", reasoningEvent.Provenance.SourceKind);
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.GovernanceFinding);
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.Decision &&
            reference.Id == "DEC-0001");
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.Decision &&
            reference.Id == "DEC-0002");
    }

    [Fact]
    public async Task CurrentGovernanceReadDoesNotCaptureReasoning()
    {
        Repository repository = CreateRepository();
        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        HttpResponseMessage governanceResponse = await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/governance");
        ReasoningEvent[] reasoningEvents = (await (await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/events"))
            .Content.ReadFromJsonAsync<ReasoningEvent[]>(jsonOptions))!;

        Assert.Equal(HttpStatusCode.OK, governanceResponse.StatusCode);
        Assert.Empty(reasoningEvents);
    }

    private static DecisionGovernanceReport CreateGovernanceReport(Guid repositoryId)
    {
        return new DecisionGovernanceReport(
            "governance.202606230000000000000",
            repositoryId,
            DateTimeOffset.Parse("2026-06-23T00:00:00Z"),
            "governance-input",
            DecisionHealthAssessment.Blocked,
            new DecisionGovernanceSummary(0, 0, 0, 0, 0, 2, 1),
            [
                new DecisionGovernanceFinding(
                    "GOV-0001",
                    DecisionGovernanceCategory.Consistency,
                    DecisionGovernanceSeverity.Blocking,
                    true,
                    "Conflicting resolved decisions",
                    "Resolved decisions conflict with one another.",
                    [new DecisionSourceReference("Decision", ".agents/decisions/records/DEC-0001/decision.json", DecisionId: new DecisionId("DEC-0001"))],
                    ["DEC-0001", "DEC-0002"],
                    [],
                    []),
                new DecisionGovernanceFinding(
                    "GOV-0002",
                    DecisionGovernanceCategory.DecisionCoverage,
                    DecisionGovernanceSeverity.Warning,
                    false,
                    "Promoted candidate has no proposal",
                    "Candidate CAND-0001 is promoted but has no active or resolved proposal.",
                    [new DecisionSourceReference("DecisionCandidate", ".agents/decisions/candidates/CAND-0001/candidate.json", CandidateId: "CAND-0001")],
                    [],
                    ["CAND-0001"],
                    [])
            ],
            []);
    }

    private static OperationalContextProposal CreatePromotedOperationalContextProposal(Guid repositoryId)
    {
        return new OperationalContextProposal
        {
            ProposalId = "oc-proposal-1",
            RepositoryId = repositoryId,
            GeneratedAt = DateTimeOffset.Parse("2026-06-23T00:00:00Z"),
            Status = OperationalContextProposalStatus.Promoted,
            BaselineCurrentContextHash = "baseline",
            GeneratedContentHash = "generated",
            GeneratedContentRelativePath = ".agents/operational_context/proposals/oc-proposal-1/proposed.md",
            SemanticChanges =
            [
                new OperationalContextSemanticChange
                {
                    Type = OperationalContextSemanticChangeType.ConstraintAdded,
                    Section = "Constraints",
                    Description = "Human review remains required before materializing reasoning entities."
                },
                new OperationalContextSemanticChange
                {
                    Type = OperationalContextSemanticChangeType.ImportantDecisionIntroduced,
                    Section = "Stable Decisions",
                    Description = "Decision: Reasoning events explain current direction without owning authority."
                },
                new OperationalContextSemanticChange
                {
                    Type = OperationalContextSemanticChangeType.QuestionAdded,
                    Section = "Open Questions",
                    Description = "Should cache derived graph projections?"
                }
            ],
            Review = new OperationalContextReview
            {
                ProposalId = "oc-proposal-1",
                ReviewState = OperationalContextReviewState.Accepted,
                BaselineCurrentContextHash = "baseline",
                ReviewedContentHash = "promoted",
                ReviewedAt = DateTimeOffset.Parse("2026-06-23T00:01:00Z"),
                ReviewNote = "Promote semantic changes."
            },
            Promotion = new OperationalContextPromotion
            {
                ProposalId = "oc-proposal-1",
                PromotedAt = DateTimeOffset.Parse("2026-06-23T00:02:00Z"),
                PromotedContentHash = "promoted",
                PromotedContentSourceRelativePath = ".agents/operational_context/proposals/oc-proposal-1/proposed.md",
                RevisionNumber = 1,
                ArchivedRelativePath = ".agents/operational_context.0001.md"
            }
        };
    }

    private static Decision CreateResolvedDecision(
        Guid repositoryId,
        string decisionId = "DEC-0001",
        string title = "Use repository-backed decisions")
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var id = new DecisionId(decisionId);
        var proposal = new DecisionProposal(
            $"PROP-{decisionId[^4..]}",
            repositoryId,
            $"CAND-{decisionId[^4..]}",
            DecisionProposalState.ReadyForResolution,
            title,
            "Decision lifecycle state must be recoverable from repository artifacts.",
            [new DecisionOption("option-1", title, "Persist records under .agents/decisions.", [])],
            [new DecisionTradeoff("option-1", "Recoverable.", "Requires schema discipline.", [])],
            new DecisionRecommendation("option-1", "Matches repository authority.", []),
            [],
            [new DecisionEvidence("Plan requires repository authority.", [new DecisionSourceReference("Plan", ".agents/plan.md")])],
            []);
        var snapshot = new DecisionResolvedProposalSnapshot(
            proposal.Id,
            proposal.CandidateId,
            Fingerprint(proposal),
            proposal.State,
            proposal.Title,
            proposal.Context,
            proposal.Options,
            proposal.Tradeoffs,
            proposal.Recommendation,
            proposal.Assumptions,
            proposal.Evidence,
            proposal.History,
            []);
        return new Decision(
            id,
            DecisionState.Resolved,
            DecisionClassification.Architectural,
            title,
            "Decision lifecycle state must be recoverable from repository artifacts.",
            new DecisionMetadata(repositoryId, now, now),
            new DecisionResolution(
                DecisionOutcome.Accepted,
                "option-1",
                "Repository artifacts are the authoritative source.",
                "human-reviewer",
                false,
                now,
                [new DecisionSourceReference("DecisionProposal", $".agents/decisions/proposals/{proposal.Id}/proposal.json", DecisionId: id, ProposalId: proposal.Id)],
                snapshot),
            [],
            [new DecisionEvidence("Plan requires repository authority.", [new DecisionSourceReference("Plan", ".agents/plan.md")])],
            [new DecisionHistoryEntry(now, "Resolved", DecisionState.Open.ToString(), DecisionState.Resolved.ToString(), "Resolved by test.", [])]);
    }

    private static ExecutionSession CreateAwaitingAcceptanceExecutionSession(Repository repository)
    {
        DateTimeOffset startedAt = DateTimeOffset.Parse("2026-06-23T00:00:00Z");
        return new ExecutionSession
        {
            Id = Guid.NewGuid(),
            RepositoryId = repository.Id,
            RepositoryPath = repository.Path,
            MilestonePath = ".agents/milestones/m2-cross-artifact-capture.md",
            StartedAt = startedAt,
            CompletedAt = startedAt.AddMinutes(5),
            LastActivityAt = startedAt.AddMinutes(5),
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.AwaitingAcceptance,
            ProviderName = "fake",
            HandoffPath = ".agents/handoffs/handoff.md"
        };
    }

    private static ExecutionSession CreateDecidedExecutionSession(
        Repository repository,
        bool accepted,
        string decisionNote)
    {
        DateTimeOffset startedAt = DateTimeOffset.Parse("2026-06-23T00:00:00Z");
        DateTimeOffset decidedAt = DateTimeOffset.Parse("2026-06-23T00:06:00Z");
        return new ExecutionSession
        {
            Id = Guid.NewGuid(),
            RepositoryId = repository.Id,
            RepositoryPath = repository.Path,
            MilestonePath = ".agents/milestones/m2-cross-artifact-capture.md",
            StartedAt = startedAt,
            CompletedAt = startedAt.AddMinutes(5),
            LastActivityAt = decidedAt,
            State = ExecutionSessionState.Completed,
            RepositoryState = accepted ? RepositoryExecutionState.AwaitingCommit : RepositoryExecutionState.Ready,
            ProviderName = "fake",
            AcceptedAt = accepted ? decidedAt : null,
            RejectedAt = accepted ? null : decidedAt,
            DecisionNote = decisionNote,
            HandoffPath = ".agents/handoffs/handoff.md"
        };
    }

    private static string Fingerprint(DecisionProposal proposal)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(proposal, CreateJsonOptions()));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static async Task ProjectAllAsync(
        Repository repository,
        FileSystemDecisionRepository decisionRepository,
        FileSystemArtifactStore store)
    {
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        await projectionService.RefreshAllAsync(repository);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        return jsonOptions;
    }

    private static Repository CreateRepository()
    {
        string path = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, ".git"));
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path
        };
    }

    private static async Task WriteAsync(Repository repository, string relativePath, string content)
    {
        string path = Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content);
    }

    private sealed class StubRepositoryService(params Repository[] repositories) : IRepositoryService
    {
        public Task<IReadOnlyList<Repository>> GetAllAsync()
        {
            return Task.FromResult<IReadOnlyList<Repository>>(repositories);
        }

        public Task<Repository> RegisterAsync(string repositoryPath)
        {
            throw new NotSupportedException();
        }

        public Task RemoveAsync(Guid repositoryId)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeGitService : IGitService
    {
        public Task<ExecutionRepositorySnapshot> GetSnapshotAsync(Repository repository)
        {
            return Task.FromResult(new ExecutionRepositorySnapshot
            {
                Branch = "main",
                DirtyState = new RepositoryDirtyState
                {
                    IsClean = false,
                    ModifiedPaths = ["src/changed.cs"]
                },
                CapturedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<RepositoryGitStatus> GetStatusAsync(Repository repository)
        {
            return Task.FromResult(new RepositoryGitStatus());
        }

        public Task<CommitPreparation> PrepareCommitAsync(Repository repository, ExecutionSession session)
        {
            throw new NotSupportedException();
        }

        public Task<CommitStatusSnapshot> GetCommitStatusSnapshotAsync(Repository repository)
        {
            throw new NotSupportedException();
        }

        public Task<CommitResult> CommitAsync(
            Repository repository,
            string message,
            IReadOnlyList<string> selectedPaths,
            string preparationSnapshotId)
        {
            throw new NotSupportedException();
        }

        public Task<PushResult> PushAsync(Repository repository, string? commitSha)
        {
            throw new NotSupportedException();
        }
    }
}
