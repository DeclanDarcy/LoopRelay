using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionRefinementServiceTests
{
    [Fact]
    public async Task RefinementRejectsStaleBaseProposalFingerprint()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        DecisionRefinementService refinementService = CreateRefinementService(repository, store, decisionRepository);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        await generationService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        DecisionProposal needsRefinement = await generationService.MarkProposalNeedsRefinementAsync(repository.Id, proposal.Id, "Needs sharper rationale.");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            refinementService.RefineProposalAsync(
                repository.Id,
                proposal.Id,
                new DecisionRefinementRequest(
                    "Apply stale edit.",
                    Context: "Refined context.",
                    BaseProposalFingerprint: Fingerprint(proposal))));

        Assert.Equal("Refinement base proposal fingerprint is stale.", exception.Message);
        DecisionProposal? reloaded = await decisionRepository.GetProposalAsync(repository, proposal.Id);
        Assert.Equal(DecisionProposalState.NeedsRefinement, reloaded?.State);
        Assert.Equal(Fingerprint(needsRefinement), Fingerprint(reloaded!));
        Assert.Empty(await refinementService.ListProposalRevisionsAsync(repository.Id, proposal.Id));
    }

    [Fact]
    public async Task RefinementPreservesRetiredOptionsAssumptionsAttributionAndDiagnostics()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            signalKind: "Conflict",
            summary: "Conflict between backend API approaches.");
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        DecisionRefinementService refinementService = CreateRefinementService(repository, store, decisionRepository);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        await generationService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        DecisionProposal needsRefinement = await generationService.MarkProposalNeedsRefinementAsync(repository.Id, proposal.Id, "Needs narrower alternatives.");
        DecisionOption retainedOption = needsRefinement.Options.Single(option => option.Id == "option-1") with
        {
            Description = "Adopt the narrower repository-backed direction."
        };
        DecisionRecommendation recommendation = needsRefinement.Recommendation! with
        {
            Rationale = "Narrower rationale after reviewer challenge."
        };

        DecisionProposal refined = await refinementService.RefineProposalAsync(
            repository.Id,
            proposal.Id,
            new DecisionRefinementRequest(
                "Remove weak alternative and document constraint.",
                Options: [retainedOption],
                Assumptions: [],
                Recommendation: recommendation,
                RequestedBy: "reviewer",
                BaseProposalFingerprint: Fingerprint(needsRefinement),
                Constraints:
                [
                    new DecisionConstraint(
                        "constraint-1",
                        "Refinement must preserve repository artifact authority.",
                        needsRefinement.Evidence)
                ],
                OptionRevisions:
                [
                    new DecisionOptionRevision(
                        "option-2",
                        "Removed",
                        "Alternative was not supported strongly enough.",
                        needsRefinement.Options.Single(option => option.Id == "option-2"),
                        null)
                ],
                AssumptionRevisions:
                [
                    new DecisionAssumptionRevision(
                        "assumption-1",
                        "Retired",
                        "Reviewer challenged the source freshness assumption.",
                        PreviousStatement: needsRefinement.Assumptions.Single().Statement)
                ],
                RejectedChanges: ["Do not resolve the proposal during refinement."]));

        DecisionProposalRevision revision = Assert.Single(await refinementService.ListProposalRevisionsAsync(repository.Id, proposal.Id));
        Assert.Equal(DecisionProposalState.Refined, refined.State);
        Assert.Equal("reviewer", revision.RequestedBy);
        Assert.Contains("Options", revision.ChangedFields);
        Assert.Contains("Assumptions", revision.ChangedFields);
        Assert.Contains("Recommendation", revision.ChangedFields);
        Assert.Contains("Constraints", revision.ChangedFields);
        Assert.Contains("OptionRevisions", revision.ChangedFields);
        Assert.Contains("AssumptionRevisions", revision.ChangedFields);
        Assert.Contains("RejectedChanges", revision.ChangedFields);
        Assert.Contains(revision.RetiredOptions ?? [], option => option.Id == "option-2");
        Assert.Contains(revision.RetiredAssumptions ?? [], assumption => assumption.Id == "assumption-1");
        Assert.Contains(revision.Constraints ?? [], constraint => constraint.Id == "constraint-1");
        Assert.Contains(revision.RejectedChanges ?? [], change => change == "Do not resolve the proposal during refinement.");
        Assert.Contains(revision.Diagnostics ?? [], diagnostic => diagnostic.Contains("Retired 1 option", StringComparison.Ordinal));
        Assert.Equal("Narrower rationale after reviewer challenge.", revision.RevisedRecommendationRationale);

        string markdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/revisions/REV-0001.md");
        Assert.Contains("Requested by: reviewer", markdown);
        Assert.Contains("## Retired Options", markdown);
        Assert.Contains("option-2", markdown);
        Assert.Contains("constraint-1: Refinement must preserve repository artifact authority.", markdown);
        Assert.Contains("Do not resolve the proposal during refinement.", markdown);
    }

    [Fact]
    public async Task RevisionComparisonCapturesTradeoffExpansionAndChainIntegrity()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            signalKind: "Conflict",
            summary: "Conflict between provider bridge approaches.");
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        DecisionRefinementService refinementService = CreateRefinementService(repository, store, decisionRepository);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        await generationService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        DecisionProposal needsRefinement = await generationService.MarkProposalNeedsRefinementAsync(repository.Id, proposal.Id, "Needs expanded tradeoffs.");
        DecisionTradeoff previousTradeoff = needsRefinement.Tradeoffs.Single(tradeoff => tradeoff.OptionId == "option-1");
        DecisionTradeoff revisedTradeoff = previousTradeoff with
        {
            Benefit = "Creates explicit bridge direction and preserves repository authority.",
            Cost = "Requires one more certification pass before UI controls are exposed."
        };
        DecisionTradeoff addedTradeoff = new(
            "option-1",
            "Keeps refinement review evidence adjacent to the recommendation.",
            "Adds comparison projection maintenance.",
            needsRefinement.Evidence);

        DecisionProposal refined = await refinementService.RefineProposalAsync(
            repository.Id,
            proposal.Id,
            new DecisionRefinementRequest(
                "Expand tradeoffs before resolution.",
                Context: "Refined context with expanded tradeoff review.",
                Tradeoffs: [revisedTradeoff, addedTradeoff, .. needsRefinement.Tradeoffs.Where(tradeoff => tradeoff.OptionId != "option-1")],
                BaseProposalFingerprint: Fingerprint(needsRefinement),
                TradeoffRevisions:
                [
                    new DecisionTradeoffRevision(
                        "option-1",
                        "Expanded",
                        "Reviewer requested fuller benefit and cost traceability.",
                        previousTradeoff,
                        revisedTradeoff),
                    new DecisionTradeoffRevision(
                        "option-1",
                        "Added",
                        "Reviewer requested an additional comparison-maintenance cost.",
                        null,
                        addedTradeoff)
                ]));

        DecisionProposalRevision revision = Assert.Single(await refinementService.ListProposalRevisionsAsync(repository.Id, proposal.Id));
        DecisionProposalRevisionComparison comparison = await refinementService.GetProposalRevisionComparisonAsync(
            repository.Id,
            proposal.Id,
            revision.Id);

        Assert.Equal(DecisionProposalState.Refined, refined.State);
        Assert.Equal(Fingerprint(needsRefinement), comparison.SourceProposalFingerprint);
        Assert.False(comparison.SourceMatchesCurrentProposal);
        Assert.Equal(Fingerprint(refined), comparison.CurrentProposalFingerprint);
        Assert.Contains("Tradeoffs", comparison.ChangedFields);
        Assert.Contains("Context", comparison.ChangedFields);
        Assert.Contains(comparison.FieldComparisons, field =>
            field.Field == "Tradeoffs" &&
            field.ChangeType == "Changed" &&
            field.PreviousValue!.Contains(previousTradeoff.Benefit, StringComparison.Ordinal) &&
            field.RevisedValue!.Contains(addedTradeoff.Benefit, StringComparison.Ordinal));
        Assert.Contains(comparison.TradeoffRevisions, tradeoffRevision => tradeoffRevision.ChangeType == "Expanded");
        Assert.Contains(comparison.TradeoffRevisions, tradeoffRevision => tradeoffRevision.ChangeType == "Added");
        Assert.Contains(comparison.PreviousTradeoffs, tradeoff => tradeoff.Benefit == previousTradeoff.Benefit);
        Assert.Contains(comparison.RevisedTradeoffs, tradeoff => tradeoff.Benefit == addedTradeoff.Benefit);

        string comparisonMarkdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/revisions/REV-0001.comparison.md");
        Assert.Contains("## Changed Fields", comparisonMarkdown);
        Assert.Contains("Tradeoffs: Changed", comparisonMarkdown);
        Assert.Contains("Keeps refinement review evidence adjacent to the recommendation.", comparisonMarkdown);
    }

    private static DecisionGenerationService CreateGenerationService(
        Repository repository,
        FileSystemArtifactStore store,
        FileSystemDecisionRepository decisionRepository)
    {
        var repositoryService = new StubRepositoryService(repository);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        return new DecisionGenerationService(repositoryService, decisionRepository, projectionService);
    }

    private static DecisionRefinementService CreateRefinementService(
        Repository repository,
        FileSystemArtifactStore store,
        FileSystemDecisionRepository decisionRepository)
    {
        var repositoryService = new StubRepositoryService(repository);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        return new DecisionRefinementService(repositoryService, decisionRepository, projectionService);
    }

    private static DecisionCandidate CreateCandidate(
        Guid repositoryId,
        DecisionCandidateState state,
        string signalKind = "MissingDirection",
        string summary = "Need to decide repository-backed persistence schema.")
    {
        return new DecisionCandidate(
            "CAND-0001",
            repositoryId,
            state,
            DecisionCandidatePriority.High,
            DecisionClassification.Architectural,
            "Decide persistence schema",
            summary,
            "source-fingerprint",
            [new DecisionSignal(
                signalKind,
                summary,
                DecisionClassification.Architectural,
                DecisionCandidatePriority.High,
                [new DecisionEvidence(
                    "Plan requires a persistence decision.",
                    [new DecisionSourceReference(
                        "Plan",
                        ".agents/plan.md",
                        Section: "Plan",
                        ItemId: "plan",
                        Excerpt: summary)])])],
            [new DecisionEvidence(
                "Plan requires a persistence decision.",
                [new DecisionSourceReference(
                    "Plan",
                    ".agents/plan.md",
                    Section: "Plan",
                    ItemId: "plan",
                    Excerpt: summary)])],
            [new DecisionSourceReference(
                "Plan",
                ".agents/plan.md",
                Section: "Plan",
                ItemId: "plan",
                Excerpt: summary)],
            ["Created by refinement test."],
            [new DecisionHistoryEntry(
                DateTimeOffset.UtcNow,
                state == DecisionCandidateState.Promoted ? "Promoted" : "Discovered",
                null,
                state.ToString(),
                "Seeded by refinement test.",
                [])]);
    }

    private static string Fingerprint(DecisionProposal proposal)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(proposal, CreateJsonOptions()));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static async Task<string> ReadAsync(Repository repository, string relativePath)
    {
        return await File.ReadAllTextAsync(Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar)));
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
