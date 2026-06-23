using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Projections;
using CommandCenter.Reasoning.Services;

namespace CommandCenter.Backend.Tests;

public sealed class ReasoningLongHorizonValidationTests
{
    [Fact]
    public async Task LongHorizonStrategyReconstructionSurvivesRepositoryRecovery()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        ReasoningServices initial = CreateServices(repository, store);
        LongHorizonFixture fixture = await CreateLongHorizonFixtureAsync(repository, store, initial.Repository);
        ReasoningQuery query = new(
            ReasoningQueryCategory.Direction,
            "Why does the current strategy exist?",
            new ReasoningReference(ReasoningReferenceKind.Decision, "DEC-STRATEGY-CURRENT"));

        ReasoningGraph graphBefore = await initial.Graph.GetGraphAsync(repository.Id);
        ReasoningQueryResult queryBefore = await initial.Query.RunQueryAsync(repository.Id, query);

        Repository recoveredRepository = new()
        {
            Id = repository.Id,
            Name = "Recovered Repo",
            Path = repository.Path
        };
        ReasoningServices recovered = CreateServices(recoveredRepository, store);
        ReasoningGraph graphAfter = await recovered.Graph.GetGraphAsync(recoveredRepository.Id);
        ReasoningQueryResult queryAfter = await recovered.Query.RunQueryAsync(recoveredRepository.Id, query);

        Assert.Equal(GraphSignature(graphBefore), GraphSignature(graphAfter));
        Assert.Equal(QuerySignature(queryBefore), QuerySignature(queryAfter));
        Assert.Contains(queryAfter.Reconstruction.Evidence, evidence => evidence.Id == fixture.SelectedAlternativeId);
        Assert.Contains(queryAfter.Reconstruction.Evidence, evidence => evidence.Id == fixture.InvalidatedAssumptionId);
        Assert.Contains(queryAfter.Reconstruction.Evidence, evidence => evidence.Id == fixture.RecurringContradictionId);
        Assert.Contains(queryAfter.Reconstruction.Evidence, evidence => evidence.Id == fixture.DirectionShiftId);
        Assert.Contains(queryAfter.Reconstruction.Evidence, evidence => evidence.Reference?.Kind == ReasoningReferenceKind.Decision);
        Assert.Contains(fixture.DirectionShiftId, queryAfter.Reconstruction.Narrative.Details, StringComparison.Ordinal);
        Assert.Equal("High", queryAfter.Reconstruction.Confidence);
        await AssertNoDerivedAuthorityArtifactsAsync(store, repository);
    }

    [Fact]
    public async Task LongHorizonAnswerLevelQueriesSurviveRepositoryRecovery()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        ReasoningServices initial = CreateServices(repository, store);
        LongHorizonFixture fixture = await CreateLongHorizonFixtureAsync(repository, store, initial.Repository);
        Repository recoveredRepository = new()
        {
            Id = repository.Id,
            Name = "Recovered Repo",
            Path = repository.Path
        };
        ReasoningServices recovered = CreateServices(recoveredRepository, store);

        await AssertRecoveredAnswerAsync(
            recovered,
            recoveredRepository,
            new ReasoningQuery(
                ReasoningQueryCategory.Decision,
                "Why was the repository event substrate chosen over provider-session continuity?",
                new ReasoningReference(ReasoningReferenceKind.Decision, "DEC-STRATEGY-CURRENT")),
            [
                fixture.SelectedAlternativeId,
                fixture.RejectedAlternativeId,
                fixture.DirectionShiftId
            ],
            [
                "event-led repository substrate was selected",
                "Provider-session reuse was rejected",
                "current strategy superseded session continuity"
            ]);

        await AssertRecoveredAnswerAsync(
            recovered,
            recoveredRepository,
            new ReasoningQuery(
                ReasoningQueryCategory.Alternative,
                "Which alternative was rejected, and why?",
                new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, fixture.SelectedAlternativeId)),
            [
                fixture.RejectedAlternativeId,
                fixture.SelectedAlternativeId
            ],
            [
                "Provider-session reuse was rejected",
                "repository artifacts must remain the continuity source"
            ]);

        await AssertRecoveredAnswerAsync(
            recovered,
            recoveredRepository,
            new ReasoningQuery(
                ReasoningQueryCategory.Assumption,
                "What assumption failed, and what challenged it?",
                new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, fixture.InvalidatedAssumptionId)),
            [
                fixture.InvalidatedAssumptionId,
                fixture.RecurringContradictionId
            ],
            [
                "Manual-only capture was invalidated",
                "Repeated materialization pressure conflicted"
            ]);

        await AssertRecoveredAnswerAsync(
            recovered,
            recoveredRepository,
            new ReasoningQuery(
                ReasoningQueryCategory.Contradiction,
                "What contradiction changed the direction of work?",
                new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, fixture.DirectionShiftId)),
            [
                fixture.RecurringContradictionId,
                fixture.DirectionShiftId
            ],
            [
                "Repeated materialization pressure conflicted",
                "strategy shifted toward proving equivalent answers after restart"
            ]);

        await AssertNoDerivedAuthorityArtifactsAsync(store, repository);
    }

    [Fact]
    public async Task LongHorizonReconstructionDetailsRemainUsableForUiConsumption()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        ReasoningServices initial = CreateServices(repository, store);
        LongHorizonFixture fixture = await CreateLongHorizonFixtureAsync(repository, store, initial.Repository);
        Repository recoveredRepository = new()
        {
            Id = repository.Id,
            Name = "Recovered Repo",
            Path = repository.Path
        };
        ReasoningServices recovered = CreateServices(recoveredRepository, store);
        ReasoningQueryResult result = await recovered.Query.RunQueryAsync(
            recoveredRepository.Id,
            new ReasoningQuery(
                ReasoningQueryCategory.Direction,
                "Why does the current strategy exist?",
                new ReasoningReference(ReasoningReferenceKind.Decision, "DEC-STRATEGY-CURRENT")));

        string details = result.Reconstruction.Narrative.Details;
        AssertSectionOrder(details, "Evidence summary:", "Events:", "Relationships:", "External References:", "Threads:");
        Assert.Contains("Evidence summary:", details, StringComparison.Ordinal);
        Assert.Contains("- None", details, StringComparison.Ordinal);
        Assert.Contains(fixture.SelectedAlternativeId, details, StringComparison.Ordinal);
        Assert.Contains(fixture.RejectedAlternativeId, details, StringComparison.Ordinal);
        Assert.Contains(fixture.InvalidatedAssumptionId, details, StringComparison.Ordinal);
        Assert.Contains(fixture.RecurringContradictionId, details, StringComparison.Ordinal);
        Assert.Contains(fixture.DirectionShiftId, details, StringComparison.Ordinal);
        Assert.Contains("Event EVT-", details, StringComparison.Ordinal);
        Assert.Contains("Relationship REL-", details, StringComparison.Ordinal);
        Assert.Contains("Reference DEC-STRATEGY-CURRENT", details, StringComparison.Ordinal);
        Assert.True(details.Split(Environment.NewLine).All(line => line.Length <= 220));
        Assert.Equal("High", result.Reconstruction.Confidence);
        await AssertNoDerivedAuthorityArtifactsAsync(store, repository);
    }

    private static async Task<LongHorizonFixture> CreateLongHorizonFixtureAsync(
        Repository repository,
        IArtifactStore store,
        IReasoningRepository reasoningRepository)
    {
        await store.WriteAsync(Path.Combine(repository.Path, ".agents", "plan.md"), "Milestone 7 long-horizon validation.");
        await store.WriteAsync(Path.Combine(repository.Path, ".agents", "handoffs", "handoff.0021.md"), "Prior handoff.");

        var events = new List<ReasoningEvent>();
        for (int index = 0; index < 8; index++)
        {
            await store.WriteAsync(
                Path.Combine(repository.Path, ".agents", "history", $"{index + 1:00}.md"),
                $"Historical source {index + 1:00}.");
            events.Add(await reasoningRepository.CreateEventAsync(repository, EventCommand(
                ReasoningEventFamily.Evidence,
                ReasoningEventType.EvidenceAdded,
                $"Historical evidence {index + 1:00}",
                $"Evidence slice {index + 1:00} preserved project history.",
                [new ReasoningReference(ReasoningReferenceKind.Artifact, $"history-{index + 1:00}", $".agents/history/{index + 1:00}.md")])));
        }

        ReasoningEvent rejectedAlternative = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Alternative,
            ReasoningEventType.AlternativeRejected,
            "Rejected session continuity alternative",
            "Provider-session reuse was rejected because repository artifacts must remain the continuity source.",
            [new ReasoningReference(ReasoningReferenceKind.Decision, "DEC-OLD-SESSION-CONTINUITY")],
            ["alternative", "rejected"]));
        ReasoningEvent selectedAlternative = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Alternative,
            ReasoningEventType.AlternativeSelected,
            "Selected repository event substrate",
            "The event-led repository substrate was selected because it preserves rationale without creating authority.",
            [new ReasoningReference(ReasoningReferenceKind.Decision, "DEC-REASONING-SUBSTRATE")],
            ["alternative", "selected"]));
        ReasoningEvent hypothesis = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Hypothesis,
            ReasoningEventType.HypothesisSupported,
            "Repository truth supports reconstruction",
            "Persisted events, threads, relationships, references, and provenance are enough to rebuild answers.",
            tags: ["hypothesis"]));
        ReasoningEvent invalidatedAssumption = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.AssumptionEvolution,
            ReasoningEventType.AssumptionInvalidated,
            "Manual capture is not enough",
            "Manual-only capture was invalidated as the mature model because objective domain transitions can be inferred.",
            tags: ["assumption", "failed"]));
        ReasoningEvent contradiction = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Contradiction,
            ReasoningEventType.ContradictionRecurred,
            "Derived read models imply authority",
            "Repeated materialization pressure conflicted with the requirement that reasoning remain explanatory.",
            tags: ["contradiction", "recurring"]));
        ReasoningEvent directionShift = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Direction,
            ReasoningEventType.DirectionShifted,
            "Shift toward repository recovery certification",
            "The strategy shifted toward proving equivalent answers after restart instead of adding caches.",
            [new ReasoningReference(ReasoningReferenceKind.Handoff, "handoff.0021", ".agents/handoffs/handoff.0021.md")],
            ["direction", "strategy"]));
        ReasoningEvent decisionSuperseded = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.DecisionEvolution,
            ReasoningEventType.DecisionSuperseded,
            "Current strategy supersedes session continuity",
            "The current strategy superseded session continuity because recovery must start from repository truth.",
            [new ReasoningReference(ReasoningReferenceKind.Decision, "DEC-STRATEGY-CURRENT")],
            ["decision"]));

        ReasoningThread strategyThread = await reasoningRepository.CreateThreadAsync(repository, new CreateReasoningThreadCommand(
            "Repository recovery strategy",
            ReasoningThreadTheme.StrategicMovement,
            "Tracks why the strategy moved toward repository-backed reasoning recovery.",
            events.Select(reasoningEvent => reasoningEvent.Id)
                .Concat([
                    rejectedAlternative.Id,
                    selectedAlternative.Id,
                    hypothesis.Id,
                    invalidatedAssumption.Id,
                    contradiction.Id,
                    directionShift.Id,
                    decisionSuperseded.Id
                ])
                .ToArray(),
            ["long-horizon", "strategy"]));

        await RelateAsync(reasoningRepository, repository, ReasoningRelationshipType.ComparesWith, rejectedAlternative, selectedAlternative);
        await RelateAsync(reasoningRepository, repository, ReasoningRelationshipType.Supports, hypothesis, selectedAlternative);
        await RelateAsync(reasoningRepository, repository, ReasoningRelationshipType.Invalidates, invalidatedAssumption, hypothesis);
        await RelateAsync(reasoningRepository, repository, ReasoningRelationshipType.Challenges, contradiction, invalidatedAssumption);
        await RelateAsync(reasoningRepository, repository, ReasoningRelationshipType.LeadsTo, selectedAlternative, directionShift);
        await RelateAsync(reasoningRepository, repository, ReasoningRelationshipType.LeadsTo, contradiction, directionShift);
        await RelateAsync(reasoningRepository, repository, ReasoningRelationshipType.CausedBy, directionShift, decisionSuperseded);
        await reasoningRepository.CreateRelationshipAsync(repository, new CreateReasoningRelationshipCommand(
            ReasoningRelationshipType.CausedBy,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, decisionSuperseded.Id),
            new ReasoningReference(ReasoningReferenceKind.Decision, "DEC-STRATEGY-CURRENT"),
            new ReasoningNarrative("The supersession event explains the current strategy decision."),
            Provenance()));
        await reasoningRepository.CreateRelationshipAsync(repository, new CreateReasoningRelationshipCommand(
            ReasoningRelationshipType.BelongsTo,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, directionShift.Id),
            new ReasoningReference(ReasoningReferenceKind.ReasoningThread, strategyThread.Id),
            new ReasoningNarrative("The direction shift belongs to the long-horizon strategy thread."),
            Provenance()));

        return new LongHorizonFixture(
            rejectedAlternative.Id,
            selectedAlternative.Id,
            invalidatedAssumption.Id,
            contradiction.Id,
            directionShift.Id);
    }

    private static async Task AssertRecoveredAnswerAsync(
        ReasoningServices recovered,
        Repository repository,
        ReasoningQuery query,
        IReadOnlyList<string> expectedEvidenceIds,
        IReadOnlyList<string> expectedNarrativeFragments)
    {
        ReasoningQueryResult result = await recovered.Query.RunQueryAsync(repository.Id, query);

        foreach (string evidenceId in expectedEvidenceIds)
        {
            Assert.Contains(result.Reconstruction.Evidence, evidence => evidence.Id == evidenceId);
        }

        foreach (string narrativeFragment in expectedNarrativeFragments)
        {
            Assert.Contains(narrativeFragment, result.Reconstruction.Narrative.Details, StringComparison.Ordinal);
        }

        Assert.Equal("High", result.Reconstruction.Confidence);
    }

    private static async Task RelateAsync(
        IReasoningRepository reasoningRepository,
        Repository repository,
        ReasoningRelationshipType type,
        ReasoningEvent source,
        ReasoningEvent target)
    {
        await reasoningRepository.CreateRelationshipAsync(repository, new CreateReasoningRelationshipCommand(
            type,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, source.Id),
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, target.Id),
            new ReasoningNarrative($"{source.Title} {type.ToString().ToLowerInvariant()} {target.Title}."),
            Provenance()));
    }

    private static CreateReasoningEventCommand EventCommand(
        ReasoningEventFamily family,
        ReasoningEventType type,
        string title,
        string summary,
        IReadOnlyList<ReasoningReference>? references = null,
        IReadOnlyList<string>? tags = null)
    {
        return new CreateReasoningEventCommand(
            family,
            type,
            title,
            new ReasoningNarrative(summary),
            references ?? [],
            Provenance(),
            [],
            tags ?? []);
    }

    private static string[] GraphSignature(ReasoningGraph graph)
    {
        return graph.Nodes
            .Select(node => $"node:{node.Id}:{node.Kind}:{node.ReferenceId}:{node.Resolved}")
            .Concat(graph.Relationships.Select(relationship =>
                $"relationship:{relationship.Id}:{relationship.Type}:{relationship.SourceNodeId}:{relationship.TargetNodeId}:{relationship.RelationshipId}"))
            .Concat(graph.Diagnostics.Select(diagnostic => $"diagnostic:{diagnostic}"))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] QuerySignature(ReasoningQueryResult result)
    {
        return result.Reconstruction.Trace.Nodes
            .Select(node => $"node:{node.Id}:{node.Resolved}")
            .Concat(result.Reconstruction.Trace.Relationships.Select(relationship =>
                $"relationship:{relationship.Id}:{relationship.Type}:{relationship.SourceNodeId}:{relationship.TargetNodeId}:{relationship.RelationshipId}"))
            .Concat(result.Reconstruction.Evidence.Select(evidence =>
                $"evidence:{evidence.Kind}:{evidence.Id}:{evidence.Title}:{evidence.Summary}"))
            .Concat(result.Reconstruction.Diagnostics.Select(diagnostic => $"reconstruction-diagnostic:{diagnostic}"))
            .Concat(result.Diagnostics.Select(diagnostic => $"query-diagnostic:{diagnostic}"))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertSectionOrder(string text, params string[] headings)
    {
        int previous = -1;
        foreach (string heading in headings)
        {
            int current = text.IndexOf(heading, StringComparison.Ordinal);
            Assert.True(current > previous, $"Expected heading '{heading}' after index {previous} in reconstruction details.");
            previous = current;
        }
    }

    private static async Task AssertNoDerivedAuthorityArtifactsAsync(
        IArtifactStore store,
        Repository repository)
    {
        foreach (string directory in new[]
        {
            "hypotheses",
            "alternatives",
            "contradictions",
            "directions",
            "graph",
            "queries"
        })
        {
            string path = Path.Combine(repository.Path, ".agents", "reasoning", directory);
            Assert.Empty(await store.ListAsync(path, "*"));
            Assert.Empty(await store.ListDirectoriesAsync(path));
        }
    }

    private static ReasoningServices CreateServices(Repository repository, IArtifactStore store)
    {
        IReasoningRepository reasoningRepository = new FileSystemReasoningRepository(
            store,
            new ReasoningArtifactProjectionService());
        IReasoningGraphService graphService = new ReasoningGraphService(
            new StubRepositoryService(repository),
            reasoningRepository,
            store);
        IReasoningReconstructionService reconstructionService = new ReasoningReconstructionService(
            new StubRepositoryService(repository),
            reasoningRepository,
            graphService);
        return new ReasoningServices(
            reasoningRepository,
            graphService,
            new ReasoningQueryService(reconstructionService));
    }

    private static ReasoningProvenance Provenance()
    {
        return new ReasoningProvenance("ManualCapture", "agent", ".agents/plan.md", "Milestone 7", "long-horizon validation", "m7-fixture");
    }

    private static Repository CreateRepository()
    {
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = "Repo",
            Path = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"))
        };
    }

    private sealed record ReasoningServices(
        IReasoningRepository Repository,
        IReasoningGraphService Graph,
        IReasoningQueryService Query);

    private sealed record LongHorizonFixture(
        string RejectedAlternativeId,
        string SelectedAlternativeId,
        string InvalidatedAssumptionId,
        string RecurringContradictionId,
        string DirectionShiftId);

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
}
