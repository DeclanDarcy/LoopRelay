using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend;
using CommandCenter.Backend.Services;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;
using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Projections;
using CommandCenter.Reasoning.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionGenerationServiceTests
{
    [Fact]
    public async Task GenerateProposalRequiresPromotedCandidate()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Discovered);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateProposalAsync(repository.Id, candidate.Id));

        Assert.Equal("Only promoted candidates can generate decision proposals.", exception.Message);
        Assert.Empty(await decisionRepository.ListProposalsAsync(repository));
    }

    [Fact]
    public async Task GenerateProposalPersistsStructuredArtifactMarkdownProjectionAndIndex()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);

        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        Assert.Equal("PROP-0001", proposal.Id);
        Assert.Equal(candidate.Id, proposal.CandidateId);
        Assert.Equal(DecisionProposalState.Generated, proposal.State);
        Assert.True(proposal.Options.Count >= 2);
        Assert.NotNull(proposal.Recommendation);
        Assert.NotEmpty(proposal.Tradeoffs);
        Assert.NotEmpty(proposal.Assumptions);
        Assert.All(proposal.Options, option =>
        {
            Assert.NotEmpty(option.Assumptions);
            Assert.NotEmpty(option.Dependencies);
        });
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "proposals", "PROP-0001", "proposal.json")));
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "proposals", "PROP-0001", "proposal.md")));
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "proposals", "PROP-0001", "history.json")));
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "proposals", "PROP-0001", "versions", "PKG-0001.json")));
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "proposals", "PROP-0001", "versions", "PKG-0001.md")));
        DecisionPackageVersion packageVersion = Assert.Single(await decisionRepository.ListPackageVersionsAsync(repository, proposal.Id));
        Assert.Equal("PKG-0001", packageVersion.Id);
        Assert.Equal(proposal.Id, packageVersion.Package.ProposalId);
        Assert.Equal(candidate.Id, packageVersion.Package.CandidateId);
        Assert.Equal(proposal.Options.Count, packageVersion.Package.Options.Count);
        Assert.Equal(proposal.AnalyzedOptions.Count, packageVersion.Package.AnalyzedOptions.Count);
        Assert.Equal(proposal.Recommendation?.OptionId, packageVersion.Package.Recommendation?.OptionId);
        Assert.Equal(".agents/milestones/m6-decision-packages.md", packageVersion.Package.Metadata.MilestonePath);
        Assert.False(string.IsNullOrWhiteSpace(packageVersion.PackageFingerprint));
        Assert.False(string.IsNullOrWhiteSpace(packageVersion.Package.Metadata.SourceProposalFingerprint));

        string markdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/proposal.md");
        string packageMarkdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/versions/PKG-0001.md");
        string index = await ReadAsync(repository, ".agents/decisions/decisions.md");
        Assert.Contains("# PROP-0001: Decide persistence schema", markdown);
        Assert.Contains("## Recommendation", markdown);
        Assert.Contains("Candidate CAND-0001 was promoted for proposal generation.", markdown);
        Assert.Contains("# PKG-0001: Decide persistence schema", packageMarkdown);
        Assert.Contains("## Decision Summary", packageMarkdown);
        Assert.Contains("## Tradeoff Analysis", packageMarkdown);
        Assert.Contains("## Recommendation", packageMarkdown);
        Assert.Contains("- PROP-0001 | Generated | CAND-0001 | Decide persistence schema", index);
    }

    [Fact]
    public async Task GenerateProposalBindsRecommendationToCandidateEvidence()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);

        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        Assert.NotNull(proposal.Recommendation);
        Assert.NotEmpty(proposal.Recommendation.Evidence);
        Assert.Contains(proposal.Recommendation.Evidence, evidence =>
            evidence.Sources.Any(source =>
                source.RelativePath == ".agents/plan.md" &&
                source.Excerpt == "Need to decide repository-backed persistence schema."));
        Assert.All(proposal.Tradeoffs, tradeoff => Assert.NotEmpty(tradeoff.Evidence));
        Assert.All(proposal.Assumptions, assumption => Assert.NotEmpty(assumption.Evidence));
    }

    [Fact]
    public async Task ConflictCandidateGeneratesRealAlternativeOption()
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
        var service = CreateGenerationService(repository, store, decisionRepository);

        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        Assert.True(proposal.Options.Count >= 2);
        Assert.Contains(proposal.Options, option => option.Type == DecisionOptionType.Investigate);
        Assert.DoesNotContain(proposal.Assumptions, assumption =>
            assumption.Statement.Contains("Only one viable option", StringComparison.Ordinal));
        Assert.Contains(proposal.Tradeoffs, tradeoff => tradeoff.OptionId == "option-2");
    }

    [Theory]
    [InlineData(DecisionClassification.Architectural, "ArchitecturalFork", DecisionOptionType.Preserve, DecisionOptionType.Refactor, DecisionOptionType.Replace)]
    [InlineData(DecisionClassification.Operational, "OperationalBlocker", DecisionOptionType.Adopt, DecisionOptionType.Refactor, DecisionOptionType.Delay)]
    [InlineData(DecisionClassification.Strategic, "MissingDirection", DecisionOptionType.Expand, DecisionOptionType.Preserve, DecisionOptionType.Constrain)]
    [InlineData(DecisionClassification.Tactical, "MissingDirection", DecisionOptionType.Adopt, DecisionOptionType.Delay, DecisionOptionType.Refactor)]
    public async Task GenerateProposalCreatesCandidateSpecificTypedOptions(
        DecisionClassification classification,
        string signalKind,
        DecisionOptionType first,
        DecisionOptionType second,
        DecisionOptionType third)
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            signalKind: signalKind,
            classification: classification);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);

        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        Assert.True(proposal.Options.Count >= 3);
        Assert.Contains(proposal.Options, option => option.Type == first);
        Assert.Contains(proposal.Options, option => option.Type == second);
        Assert.Contains(proposal.Options, option => option.Type == third);
        Assert.All(proposal.Options, option => Assert.NotEmpty(option.Evidence));
        Assert.NotNull(proposal.Recommendation);
        Assert.Contains(proposal.Options, option => option.Id == proposal.Recommendation.OptionId);
        Assert.NotEmpty(proposal.Recommendation.OptionEvaluations);
    }

    [Fact]
    public async Task GenerateProposalPersistsOptionValidationDiagnosticsAndRelationships()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            signalKind: "ConstraintConflict",
            summary: "Constraint conflict between package versioning and near-term validation.");
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);

        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);
        DecisionProposal reloaded = (await decisionRepository.GetProposalAsync(repository, proposal.Id))!;

        Assert.NotNull(proposal.GenerationDiagnostics);
        Assert.Equal(proposal.Options.Count, proposal.GenerationDiagnostics.AcceptedOptionCount);
        Assert.Equal(0, proposal.GenerationDiagnostics.RejectedOptionCount);
        Assert.All(proposal.GenerationDiagnostics.OptionValidationResults, result => Assert.True(result.IsValid));
        Assert.NotEmpty(proposal.OptionRelationships);
        Assert.Contains(proposal.OptionRelationships, relationship =>
            relationship.Type == DecisionOptionRelationshipType.ConflictsWith &&
            relationship.Evidence.Count > 0);
        Assert.Equal(proposal.OptionRelationships.Count, reloaded.OptionRelationships.Count);
        Assert.All(proposal.OptionRelationships, relationship =>
            Assert.Contains(reloaded.OptionRelationships, reloadedRelationship =>
                reloadedRelationship.SourceOptionId == relationship.SourceOptionId &&
                reloadedRelationship.TargetOptionId == relationship.TargetOptionId &&
                reloadedRelationship.Type == relationship.Type &&
                reloadedRelationship.Rationale == relationship.Rationale &&
                reloadedRelationship.Evidence.Count == relationship.Evidence.Count));
        Assert.NotNull(reloaded.GenerationDiagnostics);
        Assert.Equal(proposal.GenerationDiagnostics.GeneratedOptionCount, reloaded.GenerationDiagnostics.GeneratedOptionCount);
        Assert.Equal(proposal.GenerationDiagnostics.AcceptedOptionCount, reloaded.GenerationDiagnostics.AcceptedOptionCount);
        Assert.Equal(proposal.GenerationDiagnostics.RejectedOptionCount, reloaded.GenerationDiagnostics.RejectedOptionCount);
        Assert.Equal(
            proposal.GenerationDiagnostics.OptionValidationResults.Select(result => result.OptionId),
            reloaded.GenerationDiagnostics.OptionValidationResults.Select(result => result.OptionId));

        string markdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/proposal.md");
        Assert.Contains("## Option Relationships", markdown);
        Assert.Contains("## Generation Diagnostics", markdown);
        Assert.Contains("- Accepted options:", markdown);
        Assert.Contains("option-1: Valid", markdown);
    }

    [Fact]
    public async Task GenerateProposalPersistsStructuredTradeoffAnalysisForEveryOption()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            signalKind: "ArchitecturalFork",
            classification: DecisionClassification.Architectural);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);

        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);
        DecisionProposal reloaded = (await decisionRepository.GetProposalAsync(repository, proposal.Id))!;

        Assert.Equal(proposal.Options.Count, proposal.AnalyzedOptions.Count);
        Assert.Equal(proposal.Options.Count, proposal.Tradeoffs.Count);
        Assert.All(proposal.Options, option =>
        {
            AnalyzedDecisionOption analyzed = Assert.Single(proposal.AnalyzedOptions, item => item.OptionId == option.Id);
            Assert.NotEmpty(analyzed.Benefits);
            Assert.NotEmpty(analyzed.Costs);
            Assert.NotEmpty(analyzed.Risks);
            Assert.NotEmpty(analyzed.Dependencies);
            Assert.NotEmpty(analyzed.Consequences);
            Assert.Contains(analyzed.Diagnostics, diagnostic =>
                diagnostic.Contains(candidate.Classification.ToString(), StringComparison.Ordinal));
            DecisionTradeoff legacyTradeoff = Assert.Single(proposal.Tradeoffs, tradeoff => tradeoff.OptionId == option.Id);
            Assert.Equal(analyzed.Benefits[0].Statement, legacyTradeoff.Benefit);
            Assert.Equal(analyzed.Costs[0].Statement, legacyTradeoff.Cost);
        });
        Assert.Equal(proposal.AnalyzedOptions.Count, reloaded.AnalyzedOptions.Count);
        Assert.NotNull(reloaded.TradeoffAnalysisDiagnostics);
        Assert.Equal(proposal.Options.Count, reloaded.TradeoffAnalysisDiagnostics.AnalyzedOptionCount);
        Assert.NotEmpty(reloaded.TradeoffAnalysisDiagnostics.ContextFingerprint);

        string markdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/proposal.md");
        Assert.Contains("## Structured Tradeoff Analysis", markdown);
        Assert.Contains("#### Benefits", markdown);
        Assert.Contains("#### Risks", markdown);
        Assert.Contains("## Tradeoff Analysis Diagnostics", markdown);
    }

    [Fact]
    public async Task GenerateProposalRepresentsUnknownRisksExplicitly()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            signalKind: "OperationalBlocker",
            classification: DecisionClassification.Operational);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);

        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        AnalyzedDecisionOption delayedOption = Assert.Single(proposal.AnalyzedOptions, option =>
            proposal.Options.Any(source => source.Id == option.OptionId && source.Type == DecisionOptionType.Delay));
        Assert.Contains(delayedOption.Risks, risk => risk.IsUnknown);
        Assert.NotNull(proposal.TradeoffAnalysisDiagnostics);
        Assert.Contains(proposal.TradeoffAnalysisDiagnostics.Unknowns, unknown =>
            unknown.Contains(delayedOption.OptionId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GenerateProposalComparesOptionsWithoutRecommending()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            signalKind: "MissingDirection",
            classification: DecisionClassification.Strategic);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);

        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        Assert.Equal(proposal.Options.Count, proposal.TradeoffComparisons.Count);
        Assert.All(proposal.TradeoffComparisons, comparison =>
        {
            Assert.NotEmpty(comparison.RelativeStrengths);
            Assert.NotEmpty(comparison.RelativeWeaknesses);
            Assert.NotEmpty(comparison.UniqueAdvantages);
            Assert.NotEmpty(comparison.UniqueRisks);
            Assert.DoesNotContain(comparison.RelativeStrengths, strength =>
                strength.Contains("recommend", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(comparison.RelativeWeaknesses, weakness =>
                weakness.Contains("recommend", StringComparison.OrdinalIgnoreCase));
        });
        Assert.Contains(proposal.TradeoffComparisons, comparison =>
            comparison.UniqueRisks.Any(risk => risk.Contains("Unknown downstream impact", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task GenerateProposalDerivesRecommendationFromStructuredEvaluations()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        DecisionEvidence evidence = CandidateEvidence(candidate);
        DecisionOption weak = TestOption("option-weak", "Defer persistence decision", DecisionOptionType.Delay, evidence);
        DecisionOption strong = TestOption("option-strong", "Refactor persistence boundary", DecisionOptionType.Refactor, evidence);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(
            repository,
            store,
            decisionRepository,
            new RejectedDiagnosticOptionGenerationService(OptionGenerationResult([weak, strong])));

        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        Assert.NotNull(proposal.Recommendation);
        Assert.Equal("option-strong", proposal.Recommendation.OptionId);
        Assert.Equal(RecommendationMode.PreferredPlusAlternative, proposal.Recommendation.Mode);
        Assert.NotEmpty(proposal.Recommendation.SupportingFactors);
        Assert.NotEmpty(proposal.Recommendation.Concerns);
        Assert.NotEmpty(proposal.Recommendation.Assumptions);
        Assert.Contains(proposal.Recommendation.AlternativeExplanations, explanation =>
            explanation.Contains("option-weak", StringComparison.Ordinal));
        Assert.Contains(proposal.Recommendation.RecommendationEvidence, item =>
            item.Type == RecommendationEvidenceType.Benefit &&
            item.OptionId == "option-strong");
        OptionEvaluation strongEvaluation = Assert.Single(
            proposal.Recommendation.OptionEvaluations,
            evaluation => evaluation.OptionId == "option-strong");
        Assert.Equal(1, strongEvaluation.Rank);
        Assert.Contains("Score", strongEvaluation.ScoreExplanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecommendationDoesNotDependOnGeneratedOptionOrdering()
    {
        DecisionProposal first = await GenerateWithOptionsAsync([
            TestOption("option-weak", "Defer persistence decision", DecisionOptionType.Delay),
            TestOption("option-strong", "Refactor persistence boundary", DecisionOptionType.Refactor)
        ]);
        DecisionProposal second = await GenerateWithOptionsAsync([
            TestOption("option-strong", "Refactor persistence boundary", DecisionOptionType.Refactor),
            TestOption("option-weak", "Defer persistence decision", DecisionOptionType.Delay)
        ]);

        Assert.Equal("option-strong", first.Recommendation?.OptionId);
        Assert.Equal(first.Recommendation?.OptionId, second.Recommendation?.OptionId);
        Assert.Equal(
            first.Recommendation?.OptionEvaluations.Select(evaluation => evaluation.OptionId),
            second.Recommendation?.OptionEvaluations.Select(evaluation => evaluation.OptionId));
    }

    [Fact]
    public async Task ExcessiveUnknownRiskProducesNoRecommendation()
    {
        DecisionProposal proposal = await GenerateWithOptionsAsync([
            TestOption("option-delay", "Defer persistence decision", DecisionOptionType.Delay),
            TestOption("option-investigate", "Investigate persistence decision", DecisionOptionType.Investigate)
        ]);

        Assert.NotNull(proposal.Recommendation);
        Assert.Equal(RecommendationMode.NoRecommendation, proposal.Recommendation.Mode);
        Assert.Equal(string.Empty, proposal.Recommendation.OptionId);
        Assert.Contains(proposal.Recommendation.Concerns, concern =>
            concern.Contains("uncertainty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DisqualifyingConstraintsPreventConstraintViolatingRecommendation()
    {
        Repository repository = CreateRepository();
        await WriteAsync(
            repository,
            ".agents/plan.md",
            """
            # Plan

            - Constraint: preserve compatibility for persistence schema consumers; must not replace the persistence architecture.
            """);
        DecisionCandidate candidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            signalKind: "ConstraintConflict",
            classification: DecisionClassification.Architectural);
        DecisionEvidence evidence = CandidateEvidence(candidate);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(
            repository,
            store,
            decisionRepository,
            new RejectedDiagnosticOptionGenerationService(OptionGenerationResult([
                TestOption("option-replace-a", "Replace persistence architecture", DecisionOptionType.Replace, evidence),
                TestOption("option-replace-b", "Replace persistence storage", DecisionOptionType.Replace, evidence)
            ])));

        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        Assert.NotNull(proposal.Recommendation);
        Assert.Equal(RecommendationMode.NoRecommendation, proposal.Recommendation.Mode);
        Assert.Equal(string.Empty, proposal.Recommendation.OptionId);
        Assert.All(proposal.Recommendation.OptionEvaluations, evaluation =>
            Assert.NotEmpty(evaluation.Constraints));
    }

    [Fact]
    public async Task InsufficientEvidenceProducesNoRecommendation()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            includeEvidence: false);
        DecisionEvidence syntheticEvidence = new(
            "Candidate was promoted without repository source evidence.",
            [new DecisionSourceReference("DecisionCandidate", ".agents/decisions/candidates/CAND-0001/candidate.json", CandidateId: candidate.Id)]);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(
            repository,
            store,
            decisionRepository,
            new RejectedDiagnosticOptionGenerationService(OptionGenerationResult([
                TestOption("option-adopt", "Adopt persistence schema", DecisionOptionType.Adopt, syntheticEvidence),
                TestOption("option-refactor", "Refactor persistence schema", DecisionOptionType.Refactor, syntheticEvidence)
            ])));

        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        Assert.NotNull(proposal.Recommendation);
        Assert.Equal(RecommendationMode.NoRecommendation, proposal.Recommendation.Mode);
        Assert.Equal(string.Empty, proposal.Recommendation.OptionId);
        Assert.Contains(proposal.Recommendation.Concerns, concern =>
            concern.Contains("evidence is insufficient", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UnresolvedContradictionProducesNoRecommendation()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            signalKind: "Contradiction",
            summary: "Contradiction between persistence schema directions.");
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);

        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        Assert.NotNull(proposal.Recommendation);
        Assert.Equal(RecommendationMode.NoRecommendation, proposal.Recommendation.Mode);
        Assert.Equal(string.Empty, proposal.Recommendation.OptionId);
        Assert.Contains(proposal.Recommendation.Concerns, concern =>
            concern.Contains("unresolved contradiction", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecommendationCarriesPriorDecisionAndRepositoryStateEvidence()
    {
        Repository repository = CreateRepository();
        await WriteAsync(
            repository,
            ".agents/plan.md",
            """
            # Plan

            - Goal: decide persistence schema with automated decision generation.
            """);
        await WriteAsync(
            repository,
            ".agents/milestones/m5-recommendation-generation.md",
            """
            # Milestone 5

            - Recommendation generation must derive from repository evidence.
            """);
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveDecisionAsync(repository, CreatePriorDecision(repository.Id));
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);

        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        Assert.NotNull(proposal.Recommendation);
        Assert.Contains(proposal.Recommendation.RecommendationEvidence, item =>
            item.Type == RecommendationEvidenceType.PriorDecision);
        Assert.Contains(proposal.Recommendation.RecommendationEvidence, item =>
            item.Type == RecommendationEvidenceType.RepositoryState &&
            item.Evidence.SelectMany(evidence => evidence.Sources).Any(source =>
                source.RelativePath == ".agents/plan.md"));
    }

    [Fact]
    public async Task ConstraintConflictsSurfaceAsTradeoffDisqualifiers()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            signalKind: "ConstraintConflict",
            summary: "Constraint conflict between package versioning and near-term validation.");
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);

        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        Assert.Contains(proposal.TradeoffComparisons, comparison =>
            comparison.DisqualifyingConstraints.Any(constraint =>
                constraint.Contains("constraint conflict", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(proposal.AnalyzedOptions.SelectMany(option => option.Risks), risk =>
            risk.Statement.Contains("Constraint conflict", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GenerateProposalUsesDecisionGenerationContextInTradeoffAnalysis()
    {
        Repository repository = CreateRepository();
        await WriteAsync(
            repository,
            ".agents/plan.md",
            """
            # Plan

            - Goal: decide persistence schema with automated decision generation.
            - Constraint: preserve compatibility for persistence schema consumers.
            - Dependency: requires migration plan before execution guidance.
            - Open question: Which storage engine migration path is least disruptive?
            """);
        await WriteAsync(
            repository,
            ".agents/handoffs/handoff.md",
            """
            # Handoff

            - Current slice is enriching Milestone 4 tradeoff analysis.
            """);
        DecisionCandidate candidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            signalKind: "ArchitecturalFork",
            classification: DecisionClassification.Architectural);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);

        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        Assert.Contains(proposal.AnalyzedOptions.SelectMany(option => option.Benefits), benefit =>
            benefit.Statement.Contains("generation-context goal", StringComparison.OrdinalIgnoreCase) &&
            benefit.Statement.Contains("persistence schema", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(proposal.AnalyzedOptions.SelectMany(option => option.Costs), cost =>
            cost.Statement.Contains("active constraint", StringComparison.OrdinalIgnoreCase) &&
            cost.Statement.Contains("preserve compatibility", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(proposal.AnalyzedOptions.SelectMany(option => option.Dependencies), dependency =>
            dependency.Statement.Contains("Generation context dependency", StringComparison.OrdinalIgnoreCase) &&
            dependency.Statement.Contains("migration plan", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(proposal.AnalyzedOptions.SelectMany(option => option.Risks), risk =>
            risk.IsUnknown &&
            risk.Statement.Contains("storage engine migration path", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(proposal.AnalyzedOptions.Where(option =>
                proposal.Options.Any(source => source.Id == option.OptionId && source.Type == DecisionOptionType.Replace))
            .SelectMany(option => option.Risks), risk =>
                risk.Statement.Contains("Constraint may be violated", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(proposal.TradeoffComparisons.Where(comparison =>
                proposal.Options.Any(source => source.Id == comparison.OptionId && source.Type == DecisionOptionType.Replace))
            .SelectMany(comparison => comparison.DisqualifyingConstraints), constraint =>
                constraint.Contains("Constraint may be violated", StringComparison.OrdinalIgnoreCase) &&
                constraint.Contains("preserve compatibility", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(proposal.TradeoffComparisons.SelectMany(comparison => comparison.UniqueAdvantages), advantage =>
            advantage.Contains("Distinct", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(proposal.TradeoffComparisons.SelectMany(comparison => comparison.RelativeWeaknesses), weakness =>
            weakness.Contains("Dependency load", StringComparison.OrdinalIgnoreCase) ||
            weakness.Contains("cost is at least as significant", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(proposal.AnalyzedOptions.SelectMany(option => option.Consequences), consequence =>
            consequence.Statement.Contains("handoff continuity", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(proposal.AnalyzedOptions.SelectMany(option => option.Diagnostics), diagnostic =>
            diagnostic.Contains("Generation context inputs", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("duplicate", DecisionOptionValidationIssueType.Duplicate)]
    [InlineData("non-actionable", DecisionOptionValidationIssueType.NonActionable)]
    [InlineData("unrelated-evidence", DecisionOptionValidationIssueType.EvidenceUnrelated)]
    public void OptionValidationRejectsInvalidGeneratedOptions(
        string invalidCase,
        DecisionOptionValidationIssueType expectedIssue)
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        DecisionEvidence relatedEvidence = new(
            "Plan requires a persistence decision.",
            [new DecisionSourceReference("Plan", ".agents/plan.md", CandidateId: candidate.Id)]);
        DecisionEvidence unrelatedEvidence = new(
            "Unrelated candidate evidence.",
            [new DecisionSourceReference("DecisionCandidate", CandidateId: "CAND-9999")]);
        var accepted = new DecisionOption(
            "option-1",
            "Implement now",
            "Resolve the candidate using repository evidence.",
            [relatedEvidence])
        {
            Type = DecisionOptionType.Adopt
        };
        DecisionOption option = invalidCase switch
        {
            "duplicate" => accepted with { Id = "option-2" },
            "non-actionable" => accepted with
            {
                Id = "option-2",
                Title = "TBD",
                Description = "Unknown resolution path."
            },
            _ => accepted with
            {
                Id = "option-2",
                Evidence = [unrelatedEvidence]
            }
        };
        DecisionOption[] acceptedOptions = invalidCase == "duplicate" ? [accepted] : [];
        var validationService = new OptionValidationService();

        DecisionOptionValidationResult result = validationService.ValidateOption(option, candidate, acceptedOptions);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Type == expectedIssue);
    }

    [Fact]
    public async Task GenerateProposalPersistsRejectedOptionDiagnostics()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var relatedEvidence = new DecisionEvidence(
            "Plan requires a persistence decision.",
            [new DecisionSourceReference("Plan", ".agents/plan.md", CandidateId: candidate.Id)]);
        var optionGeneration = new RejectedDiagnosticOptionGenerationService(
            new DecisionOptionGenerationResult(
                [
                    new DecisionOption(
                        "option-1",
                        "Implement now",
                        "Resolve the candidate using repository evidence.",
                        [relatedEvidence]),
                    new DecisionOption(
                        "option-2",
                        "Implement later",
                        "Defer the candidate until sequencing is clearer.",
                        [relatedEvidence])
                    {
                        Type = DecisionOptionType.Delay
                    }
                ],
                [],
                new DecisionGenerationDiagnostics(
                    3,
                    2,
                    1,
                    1,
                    0,
                    [
                        new DecisionOptionValidationResult("option-1", true, []),
                        new DecisionOptionValidationResult("option-2", true, []),
                        new DecisionOptionValidationResult(
                            "option-3",
                            false,
                            [new DecisionOptionValidationIssue(
                                DecisionOptionValidationIssueType.Duplicate,
                                "Option duplicates option-1 by normalized title, type, or overlapping evidence.")])
                    ],
                    ["Rejected option-3: Option duplicates option-1 by normalized title, type, or overlapping evidence."])));
        var service = CreateGenerationService(repository, store, decisionRepository, optionGeneration);

        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);
        DecisionProposal reloaded = (await decisionRepository.GetProposalAsync(repository, proposal.Id))!;

        Assert.NotNull(reloaded.GenerationDiagnostics);
        Assert.Equal(1, reloaded.GenerationDiagnostics.RejectedOptionCount);
        Assert.Equal(1, reloaded.GenerationDiagnostics.DeduplicatedOptionCount);
        Assert.Contains(reloaded.GenerationDiagnostics.OptionValidationResults, result =>
            result.OptionId == "option-3" &&
            !result.IsValid &&
            result.Issues.Any(issue => issue.Type == DecisionOptionValidationIssueType.Duplicate));

        string markdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/proposal.md");
        Assert.Contains("- Rejected options: 1", markdown);
        Assert.Contains("- Deduplicated options: 1", markdown);
        Assert.Contains("option-3: Invalid", markdown);
        Assert.Contains("Duplicate: Option duplicates option-1", markdown);
    }

    [Fact]
    public async Task GenerateProposalDoesNotMutateCandidateDecisionOrContextArtifacts()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/operational_context.md", "# Operational Context\n\nStable understanding.");
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        string candidateBefore = await ReadAsync(repository, ".agents/decisions/candidates/CAND-0001/candidate.json");
        string operationalContextBefore = await ReadAsync(repository, ".agents/operational_context.md");
        var service = CreateGenerationService(repository, store, decisionRepository);

        await service.GenerateProposalAsync(repository.Id, candidate.Id);

        string candidateAfter = await ReadAsync(repository, ".agents/decisions/candidates/CAND-0001/candidate.json");
        string operationalContextAfter = await ReadAsync(repository, ".agents/operational_context.md");
        Assert.Equal(candidateBefore, candidateAfter);
        Assert.Equal(operationalContextBefore, operationalContextAfter);
        Assert.Empty(await decisionRepository.ListDecisionsAsync(repository));
    }

    [Fact]
    public async Task ActiveProposalSuppressesDuplicateGenerationUntilExpired()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);

        DecisionProposal first = await service.GenerateProposalAsync(repository.Id, candidate.Id);
        InvalidOperationException activeException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateProposalAsync(repository.Id, candidate.Id));
        DecisionProposal expired = await service.ExpireProposalAsync(repository.Id, first.Id, "Source candidate changed.");
        DecisionProposal second = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        Assert.Equal($"An active proposal already exists for candidate {candidate.Id}.", activeException.Message);
        Assert.Equal(DecisionProposalState.Expired, expired.State);
        Assert.Equal("PROP-0002", second.Id);
    }

    [Fact]
    public async Task ReviewTransitionsPersistStateHistoryMarkdownAndIndex()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);
        DecisionProposal generated = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        DecisionProposal viewed = await service.MarkProposalViewedAsync(repository.Id, generated.Id, "Reviewer opened the proposal.");
        DecisionProposal needsRefinement = await service.MarkProposalNeedsRefinementAsync(repository.Id, generated.Id, "Recommendation needs clearer evidence.");

        Assert.Equal(DecisionProposalState.Viewed, viewed.State);
        Assert.Equal(DecisionProposalState.NeedsRefinement, needsRefinement.State);
        Assert.Contains(needsRefinement.History, entry =>
            entry.Event == "Viewed" &&
            entry.FromState == DecisionProposalState.Generated.ToString() &&
            entry.ToState == DecisionProposalState.Viewed.ToString() &&
            entry.Reason == "Reviewer opened the proposal.");
        Assert.Contains(needsRefinement.History, entry =>
            entry.Event == "NeedsRefinement" &&
            entry.FromState == DecisionProposalState.Viewed.ToString() &&
            entry.ToState == DecisionProposalState.NeedsRefinement.ToString() &&
            entry.Reason == "Recommendation needs clearer evidence.");

        DecisionProposal? reloaded = await decisionRepository.GetProposalAsync(repository, generated.Id);
        string markdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/proposal.md");
        string index = await ReadAsync(repository, ".agents/decisions/decisions.md");
        Assert.Equal(DecisionProposalState.NeedsRefinement, reloaded?.State);
        Assert.Contains("- State: NeedsRefinement", markdown);
        Assert.Contains("- PROP-0001 | NeedsRefinement | CAND-0001 | Decide persistence schema", index);
    }

    [Fact]
    public async Task ReadyForResolutionRequiresAllowedProposalState()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);
        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);
        await service.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        await service.MarkProposalNeedsRefinementAsync(repository.Id, proposal.Id, null);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MarkProposalReadyForResolutionAsync(repository.Id, proposal.Id, "Skip refinement."));

        Assert.Equal("Proposal transition from NeedsRefinement to ReadyForResolution is not allowed.", exception.Message);
        DecisionProposal? reloaded = await decisionRepository.GetProposalAsync(repository, proposal.Id);
        Assert.Equal(DecisionProposalState.NeedsRefinement, reloaded?.State);
    }

    [Fact]
    public async Task RefinementCreatesRevisionArtifactBeforeProposalCanBecomeRefined()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);
        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);
        await service.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        await service.MarkProposalNeedsRefinementAsync(repository.Id, proposal.Id, "Needs clearer scope.");

        DecisionProposal refined = await service.RefineProposalAsync(
            repository.Id,
            proposal.Id,
            new DecisionRefinementRequest(
                "Clarify context for reviewer.",
                Context: "Refined context with clearer decision scope."));

        Assert.Equal(DecisionProposalState.Refined, refined.State);
        Assert.Equal("Refined context with clearer decision scope.", refined.Context);
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "proposals", "PROP-0001", "revisions", "REV-0001.json")));
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "proposals", "PROP-0001", "revisions", "REV-0001.md")));

        DecisionProposalRevision revision = Assert.Single(await service.ListProposalRevisionsAsync(repository.Id, proposal.Id));
        Assert.Equal("REV-0001", revision.Id);
        Assert.Equal(proposal.Id, revision.ProposalId);
        Assert.Contains("Context", revision.ChangedFields);
        Assert.False(string.IsNullOrWhiteSpace(revision.SourceProposalFingerprint));
        Assert.Contains(refined.History, entry =>
            entry.Event == "Refined" &&
            entry.FromState == DecisionProposalState.NeedsRefinement.ToString() &&
            entry.ToState == DecisionProposalState.Refined.ToString() &&
            entry.Sources.Any(source => source.RelativePath == ".agents/decisions/proposals/PROP-0001/revisions/REV-0001.json"));

        string proposalMarkdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/proposal.md");
        string revisionMarkdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/revisions/REV-0001.md");
        string index = await ReadAsync(repository, ".agents/decisions/decisions.md");
        Assert.Contains("- State: Refined", proposalMarkdown);
        Assert.Contains("Clarify context for reviewer.", revisionMarkdown);
        Assert.Contains("- PROP-0001 | Refined | CAND-0001 | Decide persistence schema", index);
    }

    [Fact]
    public async Task RefinementRequiresNeedsRefinementStateAndChangedContent()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);
        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        InvalidOperationException stateException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RefineProposalAsync(
                repository.Id,
                proposal.Id,
                new DecisionRefinementRequest("Try refining too early.", Context: "Changed context.")));

        await service.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        await service.MarkProposalNeedsRefinementAsync(repository.Id, proposal.Id, null);
        ArgumentException unchangedException = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RefineProposalAsync(
                repository.Id,
                proposal.Id,
                new DecisionRefinementRequest("No content changed.")));

        Assert.Equal("Proposal transition from Generated to Refined is not allowed.", stateException.Message);
        Assert.Equal("Refinement must change proposal content. (Parameter 'request')", unchangedException.Message);
        Assert.Empty(await service.ListProposalRevisionsAsync(repository.Id, proposal.Id));
    }

    [Fact]
    public async Task ReadyForResolutionCanBeMarkedFromGeneratedOrViewedProposal()
    {
        Repository repository = CreateRepository();
        DecisionCandidate firstCandidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        DecisionCandidate secondCandidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            summary: "Need to decide review state projection.") with
        {
            Id = "CAND-0002",
            Title = "Decide review projection"
        };
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, firstCandidate);
        await decisionRepository.SaveCandidateAsync(repository, secondCandidate);
        var service = CreateGenerationService(repository, store, decisionRepository);

        DecisionProposal generated = await service.GenerateProposalAsync(repository.Id, firstCandidate.Id);
        DecisionProposal generatedReady = await service.MarkProposalReadyForResolutionAsync(repository.Id, generated.Id, "Enough evidence.");
        DecisionProposal viewed = await service.GenerateProposalAsync(repository.Id, secondCandidate.Id);
        await service.MarkProposalViewedAsync(repository.Id, viewed.Id, null);
        DecisionProposal viewedReady = await service.MarkProposalReadyForResolutionAsync(repository.Id, viewed.Id, "Reviewer agrees.");

        Assert.Equal(DecisionProposalState.ReadyForResolution, generatedReady.State);
        Assert.Equal(DecisionProposalState.ReadyForResolution, viewedReady.State);
    }

    [Fact]
    public async Task ResolveProposalCreatesDecisionRecordAndMarksProposalResolved()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/operational_context.md", "# Operational Context\n\nStable understanding.");
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);
        var resolutionService = CreateResolutionService(repository, store, decisionRepository);
        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);
        await service.MarkProposalReadyForResolutionAsync(repository.Id, proposal.Id, "Ready for human resolution.");
        string operationalContextBefore = await ReadAsync(repository, ".agents/operational_context.md");
        string selectedOptionId = proposal.Recommendation?.OptionId ?? "option-1";

        Decision decision = await resolutionService.ResolveProposalAsync(
            repository.Id,
            proposal.Id,
            new ResolveDecisionCommand(
                "Accept the proposed persistence direction.",
                "human-reviewer",
                selectedOptionId));

        Assert.Equal("DEC-0001", decision.Id.Value);
        Assert.Equal(DecisionState.Resolved, decision.State);
        Assert.Equal(DecisionOutcome.Accepted, decision.Resolution?.Outcome);
        Assert.Equal(selectedOptionId, decision.Resolution?.SelectedOptionId);
        Assert.Equal("human-reviewer", decision.Resolution?.ResolvedBy);
        Assert.False(decision.Resolution?.RecommendationDiverged);
        Assert.NotNull(decision.Resolution?.SourceProposalSnapshot);
        Assert.Equal(proposal.Id, decision.Resolution?.SourceProposalSnapshot?.ProposalId);
        Assert.Equal(DecisionProposalState.ReadyForResolution, decision.Resolution?.SourceProposalSnapshot?.ProposalState);
        Assert.False(string.IsNullOrWhiteSpace(decision.Resolution?.SourceProposalSnapshot?.ProposalFingerprint));
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "records", "DEC-0001", "decision.json")));
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "records", "DEC-0001", "decision.md")));
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "records", "DEC-0001", "history.json")));

        DecisionProposal? resolvedProposal = await decisionRepository.GetProposalAsync(repository, proposal.Id);
        string decisionMarkdown = await ReadAsync(repository, ".agents/decisions/records/DEC-0001/decision.md");
        string proposalMarkdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/proposal.md");
        string index = await ReadAsync(repository, ".agents/decisions/decisions.md");
        string operationalContextAfter = await ReadAsync(repository, ".agents/operational_context.md");
        Assert.Equal(DecisionProposalState.Resolved, resolvedProposal?.State);
        Assert.Contains($"- Selected option: {selectedOptionId}", decisionMarkdown);
        Assert.Contains("- Resolved by: human-reviewer", decisionMarkdown);
        Assert.Contains("- Recommendation diverged: False", decisionMarkdown);
        Assert.Contains("- Source proposal: PROP-0001", decisionMarkdown);
        Assert.Contains("- Source proposal state: ReadyForResolution", decisionMarkdown);
        Assert.Contains("- Captured revisions: 0", decisionMarkdown);
        Assert.Contains("- State: Resolved", proposalMarkdown);
        Assert.Contains("- DEC-0001 | Resolved | Architectural | Accepted | Decide persistence schema", index);
        Assert.Contains("- PROP-0001 | Resolved | CAND-0001 | Decide persistence schema", index);
        Assert.Equal(operationalContextBefore, operationalContextAfter);
        Assert.False(Directory.Exists(Path.Combine(repository.Path, ".agents", "decisions", "assimilation")));
    }

    [Fact]
    public async Task ProposalResolutionReasoningCaptureIsIdempotentAndNonAuthoritative()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/operational_context.md", "# Operational Context\n\nStable understanding.");
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var generationService = CreateGenerationService(repository, store, decisionRepository);
        var resolutionService = CreateResolutionService(repository, store, decisionRepository);
        var reasoningRepository = new FileSystemReasoningRepository(
            store,
            new ReasoningArtifactProjectionService());
        var captureService = new DecisionReasoningCaptureService(
            new StubRepositoryService(repository),
            decisionRepository,
            store,
            reasoningRepository);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        await generationService.MarkProposalReadyForResolutionAsync(repository.Id, proposal.Id, "Ready for human resolution.");
        var command = new ResolveDecisionCommand(
            "Accept the proposed persistence direction.",
            "human-reviewer",
            "option-1");
        Decision decision = await resolutionService.ResolveProposalAsync(repository.Id, proposal.Id, command);
        DecisionProposal? proposalAfterResolution = await decisionRepository.GetProposalAsync(repository, proposal.Id);
        Decision? decisionAfterResolution = await decisionRepository.GetDecisionAsync(repository, decision.Id);

        await captureService.CaptureProposalResolvedAsync(repository.Id, decision, command);
        await captureService.CaptureProposalResolvedAsync(repository.Id, decision, command);

        IReadOnlyList<ReasoningEvent> events = await reasoningRepository.ListEventsAsync(repository);
        IReadOnlyList<ReasoningRelationship> relationships = await reasoningRepository.ListRelationshipsAsync(repository);
        ReasoningEvent reasoningEvent = Assert.Single(events);
        ReasoningRelationship relationship = Assert.Single(relationships);
        DecisionProposal? proposalAfterCapture = await decisionRepository.GetProposalAsync(repository, proposal.Id);
        Decision? decisionAfterCapture = await decisionRepository.GetDecisionAsync(repository, decision.Id);

        Assert.Equal(ReasoningEventFamily.Evidence, reasoningEvent.Family);
        Assert.Equal(ReasoningEventType.EvidenceAdded, reasoningEvent.Type);
        Assert.Equal("InferredProposalResolution", reasoningEvent.Provenance.SourceKind);
        Assert.Equal("human-reviewer", reasoningEvent.Provenance.CapturedBy);
        Assert.NotNull(reasoningEvent.Provenance.Fingerprint);
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.Proposal &&
            reference.Id == proposal.Id);
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.Decision &&
            reference.Id == decision.Id.Value);
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.Candidate &&
            reference.Id == candidate.Id);
        Assert.Equal(ReasoningRelationshipType.DerivesFrom, relationship.Type);
        Assert.Equal(decision.Id.Value, relationship.Source.Id);
        Assert.Equal(proposal.Id, relationship.Target.Id);
        Assert.Equal(proposalAfterResolution?.State, proposalAfterCapture?.State);
        Assert.Equal(proposalAfterResolution?.History.Count, proposalAfterCapture?.History.Count);
        Assert.Equal(decisionAfterResolution?.State, decisionAfterCapture?.State);
        Assert.Equal(decisionAfterResolution?.Resolution?.Outcome, decisionAfterCapture?.Resolution?.Outcome);
        Assert.Equal(decisionAfterResolution?.Resolution?.SelectedOptionId, decisionAfterCapture?.Resolution?.SelectedOptionId);
        Assert.Equal(
            decisionAfterResolution?.Resolution?.SourceProposalSnapshot?.ProposalFingerprint,
            decisionAfterCapture?.Resolution?.SourceProposalSnapshot?.ProposalFingerprint);
    }

    [Fact]
    public async Task AssimilationRecommendationPersistsAdvisoryPackageWithoutMutatingOperationalContext()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "# Plan\n\n- Need to decide repository-backed persistence schema.");
        await WriteAsync(repository, ".agents/operational_context.md", "# Operational Context\n\nStable understanding.");
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var generationService = CreateGenerationService(repository, store, decisionRepository);
        var resolutionService = CreateResolutionService(repository, store, decisionRepository);
        var assimilationService = CreateAssimilationService(repository, store, decisionRepository);
        Decision decision = await ResolveAcceptedDecisionAsync(repository, generationService, resolutionService, candidate.Id);
        string operationalContextBefore = await ReadAsync(repository, ".agents/operational_context.md");

        DecisionAssimilationRecommendation recommendation =
            await assimilationService.ProposeOperationalContextAssimilationAsync(
                repository.Id,
                decision.Id.Value,
                new CreateDecisionAssimilationRecommendationCommand("human-reviewer", "Prepare for continuity review."));

        string operationalContextAfter = await ReadAsync(repository, ".agents/operational_context.md");
        var restartedRepository = new FileSystemDecisionRepository(store);
        DecisionAssimilationRecommendation? reloaded =
            await restartedRepository.GetAssimilationRecommendationAsync(repository, decision.Id);
        string markdown = await ReadAsync(repository, ".agents/decisions/assimilation/DEC-0001/recommendation.md");

        Assert.Equal(decision.Id.Value, recommendation.DecisionId);
        Assert.Equal(decision.Id, recommendation.SourceDecision.Id);
        Assert.Equal(repository.Id, recommendation.ContextSnapshot.RepositoryId);
        Assert.Equal(recommendation.ContextSnapshotId, recommendation.ContextSnapshot.SnapshotId);
        Assert.Equal(recommendation.ContextFingerprint, recommendation.ContextSnapshot.Fingerprint);
        Assert.False(string.IsNullOrWhiteSpace(recommendation.DecisionFingerprint));
        Assert.Contains(recommendation.Diagnostics, diagnostic =>
            diagnostic.Contains("advisory", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(recommendation.Sources, source => source.SourceKind == "DecisionRecord" && source.DecisionId == decision.Id);
        Assert.Contains(recommendation.Sources, source => source.SourceKind == "DecisionContextSnapshot");
        Assert.Equal(operationalContextBefore, operationalContextAfter);
        Assert.NotNull(reloaded);
        Assert.Equal(recommendation.DecisionFingerprint, reloaded.DecisionFingerprint);
        Assert.Equal(recommendation.ContextFingerprint, reloaded.ContextFingerprint);
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "assimilation", "DEC-0001", "recommendation.json")));
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "assimilation", "DEC-0001", "recommendation.md")));
        Assert.Contains("# DEC-0001: Operational Context Assimilation Recommendation", markdown);
        Assert.Contains("Continuity remains responsible", markdown);
    }

    [Fact]
    public async Task AssimilationRecommendationRequiresResolvedDecision()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var generationService = CreateGenerationService(repository, store, decisionRepository);
        var resolutionService = CreateResolutionService(repository, store, decisionRepository);
        var assimilationService = CreateAssimilationService(repository, store, decisionRepository);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        await generationService.MarkProposalReadyForResolutionAsync(repository.Id, proposal.Id, "Ready to defer.");
        Decision underReviewDecision = await resolutionService.ResolveProposalAsync(
            repository.Id,
            proposal.Id,
            new ResolveDecisionCommand("Defer until more evidence exists.", "human-reviewer", "option-1", DecisionOutcome.Deferred));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            assimilationService.ProposeOperationalContextAssimilationAsync(
                repository.Id,
                underReviewDecision.Id.Value,
                null));

        Assert.Equal("Only resolved decisions can produce operational-context assimilation recommendations.", exception.Message);
        Assert.False(Directory.Exists(Path.Combine(repository.Path, ".agents", "decisions", "assimilation")));
    }

    [Fact]
    public async Task AssimilationRecommendationEndpointCreatesAndReturnsPackage()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "# Plan\n\n- Need to decide repository-backed persistence schema.");
        await WriteAsync(repository, ".agents/operational_context.md", "# Operational Context\n\nStable understanding.");
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();
        Decision decision = await ResolveViaEndpointAsync(root, client, jsonOptions, repository.Id, candidate.Id);
        string operationalContextBefore = await ReadAsync(repository, ".agents/operational_context.md");

        HttpResponseMessage proposeResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/{decision.Id.Value}/assimilation/propose-operational-context",
            new CreateDecisionAssimilationRecommendationCommand("human-reviewer", "Endpoint package."),
            jsonOptions);
        HttpResponseMessage getResponse = await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/{decision.Id.Value}/assimilation");

        DecisionAssimilationRecommendation proposed =
            (await proposeResponse.Content.ReadFromJsonAsync<DecisionAssimilationRecommendation>(jsonOptions))!;
        DecisionAssimilationRecommendation fetched =
            (await getResponse.Content.ReadFromJsonAsync<DecisionAssimilationRecommendation>(jsonOptions))!;
        string operationalContextAfter = await ReadAsync(repository, ".agents/operational_context.md");

        Assert.Equal(HttpStatusCode.OK, proposeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(decision.Id.Value, proposed.DecisionId);
        Assert.Equal(proposed.DecisionFingerprint, fetched.DecisionFingerprint);
        Assert.Equal(proposed.ContextSnapshotId, fetched.ContextSnapshotId);
        Assert.Equal(operationalContextBefore, operationalContextAfter);
    }

    [Theory]
    [InlineData(DecisionOutcome.Accepted, DecisionState.Resolved)]
    [InlineData(DecisionOutcome.Rejected, DecisionState.Archived)]
    [InlineData(DecisionOutcome.Deferred, DecisionState.UnderReview)]
    public async Task ResolveProposalOutcomeDrivesDecisionStateAndProjection(
        DecisionOutcome outcome,
        DecisionState expectedState)
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);
        var resolutionService = CreateResolutionService(repository, store, decisionRepository);
        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);
        await service.MarkProposalReadyForResolutionAsync(repository.Id, proposal.Id, "Ready for explicit outcome.");

        Decision decision = await resolutionService.ResolveProposalAsync(
            repository.Id,
            proposal.Id,
            new ResolveDecisionCommand(
                $"Apply {outcome} outcome.",
                "human-reviewer",
                "option-1",
                outcome));

        Decision? reloadedDecision = await decisionRepository.GetDecisionAsync(repository, decision.Id);
        DecisionProposal? reloadedProposal = await decisionRepository.GetProposalAsync(repository, proposal.Id);
        string decisionMarkdown = await ReadAsync(repository, ".agents/decisions/records/DEC-0001/decision.md");
        string index = await ReadAsync(repository, ".agents/decisions/decisions.md");

        Assert.Equal(expectedState, decision.State);
        Assert.Equal(expectedState, reloadedDecision?.State);
        Assert.Equal(outcome, decision.Resolution?.Outcome);
        Assert.Equal(outcome, reloadedDecision?.Resolution?.Outcome);
        Assert.Equal(DecisionProposalState.Resolved, reloadedProposal?.State);
        Assert.Equal(proposal.Id, decision.Resolution?.SourceProposalSnapshot?.ProposalId);
        Assert.Equal(DecisionProposalState.ReadyForResolution, decision.Resolution?.SourceProposalSnapshot?.ProposalState);
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "records", "DEC-0001", "decision.json")));
        Assert.Contains($"- State: {expectedState}", decisionMarkdown);
        Assert.Contains($"- Outcome: {outcome}", decisionMarkdown);
        Assert.Contains("- Source proposal state: ReadyForResolution", decisionMarkdown);
        Assert.Contains($"- DEC-0001 | {expectedState} | Architectural | {outcome} | Decide persistence schema", index);
        Assert.Contains("- PROP-0001 | Resolved | CAND-0001 | Decide persistence schema", index);
    }

    [Fact]
    public async Task SupersedeDecisionPersistsReplacementLineageMarkdownAndIndex()
    {
        Repository repository = CreateRepository();
        DecisionCandidate firstCandidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        DecisionCandidate secondCandidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            summary: "Need to decide replacement authority.") with
        {
            Id = "CAND-0002",
            Title = "Decide replacement authority"
        };
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, firstCandidate);
        await decisionRepository.SaveCandidateAsync(repository, secondCandidate);
        var generationService = CreateGenerationService(repository, store, decisionRepository);
        var resolutionService = CreateResolutionService(repository, store, decisionRepository);
        Decision firstDecision = await ResolveAcceptedDecisionAsync(repository, generationService, resolutionService, firstCandidate.Id);
        Decision replacementDecision = await ResolveAcceptedDecisionAsync(repository, generationService, resolutionService, secondCandidate.Id);

        Decision superseded = await resolutionService.SupersedeDecisionAsync(
            repository.Id,
            firstDecision.Id.Value,
            new SupersedeDecisionCommand(
                replacementDecision.Id.Value,
                "Replacement captures the current authority.",
                "human-reviewer"));

        Decision? reloadedSuperseded = await decisionRepository.GetDecisionAsync(repository, firstDecision.Id);
        Decision? reloadedReplacement = await decisionRepository.GetDecisionAsync(repository, replacementDecision.Id);
        DecisionRelationship relationship = Assert.Single(reloadedReplacement!.Relationships);
        string supersededMarkdown = await ReadAsync(repository, ".agents/decisions/records/DEC-0001/decision.md");
        string replacementMarkdown = await ReadAsync(repository, ".agents/decisions/records/DEC-0002/decision.md");
        string index = await ReadAsync(repository, ".agents/decisions/decisions.md");

        Assert.Equal(DecisionState.Superseded, superseded.State);
        Assert.Equal(DecisionState.Superseded, reloadedSuperseded?.State);
        Assert.Equal(DecisionState.Resolved, reloadedReplacement.State);
        Assert.Equal(replacementDecision.Id, relationship.SourceDecisionId);
        Assert.Equal(firstDecision.Id, relationship.TargetDecisionId);
        Assert.Equal(DecisionRelationshipType.Supersedes, relationship.Type);
        Assert.Contains(reloadedSuperseded!.History, entry =>
            entry.Event == "Superseded" &&
            entry.FromState == DecisionState.Resolved.ToString() &&
            entry.ToState == DecisionState.Superseded.ToString() &&
            entry.Sources.Any(source => source.DecisionId == replacementDecision.Id));
        Assert.Contains(reloadedReplacement.History, entry =>
            entry.Event == "Supersedes" &&
            entry.Sources.Any(source => source.DecisionId == firstDecision.Id));
        Assert.Contains("- State: Superseded", supersededMarkdown);
        Assert.Contains("Superseded | Resolved -> Superseded", supersededMarkdown);
        Assert.Contains("DEC-0002 Supersedes DEC-0001", replacementMarkdown);
        Assert.Contains("- DEC-0001 | Superseded | Architectural | Accepted | Decide persistence schema", index);
        Assert.Contains("- DEC-0002 | Resolved | Architectural | Accepted | Decide replacement authority", index);
    }

    [Fact]
    public async Task DecisionSupersessionReasoningCaptureIsIdempotent()
    {
        Repository repository = CreateRepository();
        DecisionCandidate firstCandidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        DecisionCandidate secondCandidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            summary: "Need to decide replacement authority.") with
        {
            Id = "CAND-0002",
            Title = "Decide replacement authority"
        };
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, firstCandidate);
        await decisionRepository.SaveCandidateAsync(repository, secondCandidate);
        var generationService = CreateGenerationService(repository, store, decisionRepository);
        var resolutionService = CreateResolutionService(repository, store, decisionRepository);
        var reasoningRepository = new FileSystemReasoningRepository(
            store,
            new ReasoningArtifactProjectionService());
        var captureService = new DecisionReasoningCaptureService(
            new StubRepositoryService(repository),
            decisionRepository,
            store,
            reasoningRepository);
        Decision firstDecision = await ResolveAcceptedDecisionAsync(repository, generationService, resolutionService, firstCandidate.Id);
        Decision replacementDecision = await ResolveAcceptedDecisionAsync(repository, generationService, resolutionService, secondCandidate.Id);
        var command = new SupersedeDecisionCommand(
            replacementDecision.Id.Value,
            "Replacement captures the current authority.",
            "human-reviewer");
        Decision superseded = await resolutionService.SupersedeDecisionAsync(repository.Id, firstDecision.Id.Value, command);

        await captureService.CaptureDecisionSupersededAsync(repository.Id, superseded, command);
        await captureService.CaptureDecisionSupersededAsync(repository.Id, superseded, command);

        IReadOnlyList<ReasoningEvent> events = await reasoningRepository.ListEventsAsync(repository);
        IReadOnlyList<ReasoningRelationship> relationships = await reasoningRepository.ListRelationshipsAsync(repository);
        ReasoningEvent reasoningEvent = Assert.Single(events);
        ReasoningRelationship relationship = Assert.Single(relationships);

        Assert.Equal(ReasoningEventFamily.DecisionEvolution, reasoningEvent.Family);
        Assert.Equal(ReasoningEventType.DecisionSuperseded, reasoningEvent.Type);
        Assert.Equal("InferredDecisionSupersession", reasoningEvent.Provenance.SourceKind);
        Assert.Equal("human-reviewer", reasoningEvent.Provenance.CapturedBy);
        Assert.NotNull(reasoningEvent.Provenance.Fingerprint);
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.Decision &&
            reference.Id == firstDecision.Id.Value);
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.Decision &&
            reference.Id == replacementDecision.Id.Value);
        Assert.Equal(ReasoningRelationshipType.Supersedes, relationship.Type);
        Assert.Equal(replacementDecision.Id.Value, relationship.Source.Id);
        Assert.Equal(firstDecision.Id.Value, relationship.Target.Id);
    }

    [Fact]
    public async Task DecisionArchivalReasoningCaptureIsIdempotent()
    {
        Repository repository = CreateRepository();
        DecisionCandidate firstCandidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        DecisionCandidate secondCandidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            summary: "Need to decide replacement authority.") with
        {
            Id = "CAND-0002",
            Title = "Decide replacement authority"
        };
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, firstCandidate);
        await decisionRepository.SaveCandidateAsync(repository, secondCandidate);
        var generationService = CreateGenerationService(repository, store, decisionRepository);
        var resolutionService = CreateResolutionService(repository, store, decisionRepository);
        var reasoningRepository = new FileSystemReasoningRepository(
            store,
            new ReasoningArtifactProjectionService());
        var captureService = new DecisionReasoningCaptureService(
            new StubRepositoryService(repository),
            decisionRepository,
            store,
            reasoningRepository);
        Decision firstDecision = await ResolveAcceptedDecisionAsync(repository, generationService, resolutionService, firstCandidate.Id);
        Decision replacementDecision = await ResolveAcceptedDecisionAsync(repository, generationService, resolutionService, secondCandidate.Id);
        Decision superseded = await resolutionService.SupersedeDecisionAsync(
            repository.Id,
            firstDecision.Id.Value,
            new SupersedeDecisionCommand(
                replacementDecision.Id.Value,
                "Replacement captures the current authority.",
                "human-reviewer"));
        var command = new ArchiveDecisionCommand(
            "Superseded authority is no longer active.",
            "human-reviewer");
        Decision archived = await resolutionService.ArchiveDecisionAsync(repository.Id, superseded.Id.Value, command);

        await captureService.CaptureDecisionArchivedAsync(repository.Id, archived, command);
        await captureService.CaptureDecisionArchivedAsync(repository.Id, archived, command);

        IReadOnlyList<ReasoningEvent> events = await reasoningRepository.ListEventsAsync(repository);
        IReadOnlyList<ReasoningRelationship> relationships = await reasoningRepository.ListRelationshipsAsync(repository);
        ReasoningEvent reasoningEvent = Assert.Single(events);
        Decision? decisionAfterCapture = await decisionRepository.GetDecisionAsync(repository, archived.Id);

        Assert.Equal(ReasoningEventFamily.DecisionEvolution, reasoningEvent.Family);
        Assert.Equal(ReasoningEventType.EvidenceAdded, reasoningEvent.Type);
        Assert.Equal("InferredDecisionArchival", reasoningEvent.Provenance.SourceKind);
        Assert.Equal("human-reviewer", reasoningEvent.Provenance.CapturedBy);
        Assert.NotNull(reasoningEvent.Provenance.Fingerprint);
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.Decision &&
            reference.Id == firstDecision.Id.Value);
        Assert.Empty(relationships);
        Assert.Equal(DecisionState.Archived, decisionAfterCapture?.State);
        Assert.Equal(archived.History.Count, decisionAfterCapture?.History.Count);
    }

    [Fact]
    public async Task ArchiveDecisionPersistsTerminalStateAfterSupersession()
    {
        Repository repository = CreateRepository();
        DecisionCandidate firstCandidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        DecisionCandidate secondCandidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            summary: "Need to decide replacement authority.") with
        {
            Id = "CAND-0002",
            Title = "Decide replacement authority"
        };
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, firstCandidate);
        await decisionRepository.SaveCandidateAsync(repository, secondCandidate);
        var generationService = CreateGenerationService(repository, store, decisionRepository);
        var resolutionService = CreateResolutionService(repository, store, decisionRepository);
        Decision firstDecision = await ResolveAcceptedDecisionAsync(repository, generationService, resolutionService, firstCandidate.Id);
        Decision replacementDecision = await ResolveAcceptedDecisionAsync(repository, generationService, resolutionService, secondCandidate.Id);
        await resolutionService.SupersedeDecisionAsync(
            repository.Id,
            firstDecision.Id.Value,
            new SupersedeDecisionCommand(replacementDecision.Id.Value, "Replacement is authoritative.", "human-reviewer"));

        Decision archived = await resolutionService.ArchiveDecisionAsync(
            repository.Id,
            firstDecision.Id.Value,
            new ArchiveDecisionCommand("Superseded authority is no longer active.", "human-reviewer"));

        Decision? reloaded = await decisionRepository.GetDecisionAsync(repository, firstDecision.Id);
        string markdown = await ReadAsync(repository, ".agents/decisions/records/DEC-0001/decision.md");
        string index = await ReadAsync(repository, ".agents/decisions/decisions.md");
        Assert.Equal(DecisionState.Archived, archived.State);
        Assert.Equal(DecisionState.Archived, reloaded?.State);
        Assert.Contains(reloaded!.History, entry =>
            entry.Event == "Archived" &&
            entry.FromState == DecisionState.Superseded.ToString() &&
            entry.ToState == DecisionState.Archived.ToString());
        Assert.Contains("- State: Archived", markdown);
        Assert.Contains("Archived | Superseded -> Archived", markdown);
        Assert.Contains("- DEC-0001 | Archived | Architectural | Accepted | Decide persistence schema", index);
    }

    [Fact]
    public async Task SupersedeAndArchiveRejectInvalidAuthorityTransitions()
    {
        Repository repository = CreateRepository();
        DecisionCandidate firstCandidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        DecisionCandidate secondCandidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            summary: "Need to decide replacement authority.") with
        {
            Id = "CAND-0002",
            Title = "Decide replacement authority"
        };
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, firstCandidate);
        await decisionRepository.SaveCandidateAsync(repository, secondCandidate);
        var generationService = CreateGenerationService(repository, store, decisionRepository);
        var resolutionService = CreateResolutionService(repository, store, decisionRepository);
        Decision firstDecision = await ResolveAcceptedDecisionAsync(repository, generationService, resolutionService, firstCandidate.Id);
        DecisionProposal replacementProposal = await generationService.GenerateProposalAsync(repository.Id, secondCandidate.Id);
        await generationService.MarkProposalReadyForResolutionAsync(repository.Id, replacementProposal.Id, "Ready for explicit deferral.");
        Decision underReviewReplacement = await resolutionService.ResolveProposalAsync(
            repository.Id,
            replacementProposal.Id,
            new ResolveDecisionCommand("Defer replacement.", "human-reviewer", "option-1", DecisionOutcome.Deferred));

        InvalidOperationException replacementException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resolutionService.SupersedeDecisionAsync(
                repository.Id,
                firstDecision.Id.Value,
                new SupersedeDecisionCommand(
                    underReviewReplacement.Id.Value,
                    "Try to replace with non-resolved authority.",
                    "human-reviewer")));
        InvalidOperationException archiveException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resolutionService.ArchiveDecisionAsync(
                repository.Id,
                firstDecision.Id.Value,
                new ArchiveDecisionCommand("Archive before supersession.", "human-reviewer")));
        ArgumentException selfException = await Assert.ThrowsAsync<ArgumentException>(() =>
            resolutionService.SupersedeDecisionAsync(
                repository.Id,
                firstDecision.Id.Value,
                new SupersedeDecisionCommand(
                    firstDecision.Id.Value,
                    "Self replacement is invalid.",
                    "human-reviewer")));

        Assert.Equal("Replacement decision must be Resolved before it can supersede another decision.", replacementException.Message);
        Assert.Equal("Decision transition from Resolved to Archived is not allowed.", archiveException.Message);
        Assert.Equal("A decision cannot supersede itself. (Parameter 'command')", selfException.Message);
        Decision? reloaded = await decisionRepository.GetDecisionAsync(repository, firstDecision.Id);
        Assert.Equal(DecisionState.Resolved, reloaded?.State);
    }

    [Fact]
    public async Task ResolveProposalCapturesResolvedProposalContentAndRevisionContext()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);
        var resolutionService = CreateResolutionService(repository, store, decisionRepository);
        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);
        await service.MarkProposalViewedAsync(repository.Id, proposal.Id, "Reviewer opened the proposal.");
        await service.MarkProposalNeedsRefinementAsync(repository.Id, proposal.Id, "Needs clearer context.");
        DecisionProposal refined = await service.RefineProposalAsync(
            repository.Id,
            proposal.Id,
            new DecisionRefinementRequest(
                "Clarify context for resolution.",
                Context: "Refined context captured by resolution."));
        DecisionProposal ready = await service.MarkProposalReadyForResolutionAsync(
            repository.Id,
            refined.Id,
            "Ready after refinement.");

        Decision decision = await resolutionService.ResolveProposalAsync(
            repository.Id,
            ready.Id,
            new ResolveDecisionCommand(
                "Resolve using refined proposal context.",
                "human-reviewer",
                "option-1"));

        DecisionResolvedProposalSnapshot snapshot = Assert.IsType<DecisionResolvedProposalSnapshot>(
            decision.Resolution?.SourceProposalSnapshot);
        DecisionProposalRevision revision = Assert.Single(snapshot.Revisions);
        Assert.Equal(ready.Id, snapshot.ProposalId);
        Assert.Equal(candidate.Id, snapshot.CandidateId);
        Assert.Equal(DecisionProposalState.ReadyForResolution, snapshot.ProposalState);
        Assert.Equal("Refined context captured by resolution.", snapshot.Context);
        Assert.Equal("REV-0001", revision.Id);
        Assert.Contains("Context", revision.ChangedFields);
        Assert.Contains(snapshot.History, entry => entry.Event == "Refined");
        Assert.Contains(snapshot.History, entry => entry.Event == "ReadyForResolution");
        Assert.False(string.IsNullOrWhiteSpace(snapshot.ProposalFingerprint));

        Decision? reloaded = await decisionRepository.GetDecisionAsync(repository, decision.Id);
        Assert.Equal(snapshot.ProposalFingerprint, reloaded?.Resolution?.SourceProposalSnapshot?.ProposalFingerprint);
        Assert.Equal("REV-0001", Assert.Single(reloaded!.Resolution!.SourceProposalSnapshot!.Revisions).Id);
    }

    [Fact]
    public async Task ResolveProposalRequiresReadyStateRationaleResolverAndSelectedOption()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);
        var resolutionService = CreateResolutionService(repository, store, decisionRepository);
        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        InvalidOperationException stateException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resolutionService.ResolveProposalAsync(
                repository.Id,
                proposal.Id,
                new ResolveDecisionCommand("Resolve too early.", "human-reviewer", "option-1")));

        await service.MarkProposalReadyForResolutionAsync(repository.Id, proposal.Id, "Ready now.");
        ArgumentException rationaleException = await Assert.ThrowsAsync<ArgumentException>(() =>
            resolutionService.ResolveProposalAsync(
                repository.Id,
                proposal.Id,
                new ResolveDecisionCommand("", "human-reviewer", "option-1")));
        ArgumentException resolverException = await Assert.ThrowsAsync<ArgumentException>(() =>
            resolutionService.ResolveProposalAsync(
                repository.Id,
                proposal.Id,
                new ResolveDecisionCommand("Resolve.", "", "option-1")));
        ArgumentException optionException = await Assert.ThrowsAsync<ArgumentException>(() =>
            resolutionService.ResolveProposalAsync(
                repository.Id,
                proposal.Id,
                new ResolveDecisionCommand("Resolve.", "human-reviewer", "missing-option")));

        Assert.Equal("Proposal transition from Generated to Resolved is not allowed.", stateException.Message);
        Assert.Equal("Resolution rationale is required. (Parameter 'command')", rationaleException.Message);
        Assert.Equal("Resolver metadata is required. (Parameter 'command')", resolverException.Message);
        Assert.Equal("Selected option was not found: missing-option (Parameter 'command')", optionException.Message);
        Assert.Empty(await decisionRepository.ListDecisionsAsync(repository));
    }

    [Fact]
    public async Task ResolveProposalRecordsRecommendationDivergence()
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
        var service = CreateGenerationService(repository, store, decisionRepository);
        var resolutionService = CreateResolutionService(repository, store, decisionRepository);
        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);
        await service.MarkProposalReadyForResolutionAsync(repository.Id, proposal.Id, "Ready for alternate option.");

        Decision decision = await resolutionService.ResolveProposalAsync(
            repository.Id,
            proposal.Id,
            new ResolveDecisionCommand(
                "Choose the safer deferral option despite the advisory recommendation.",
                "human-reviewer",
                "option-2",
                DecisionOutcome.Deferred));

        Assert.Equal(DecisionOutcome.Deferred, decision.Resolution?.Outcome);
        Assert.Equal("option-2", decision.Resolution?.SelectedOptionId);
        Assert.True(decision.Resolution?.RecommendationDiverged);
    }

    [Fact]
    public async Task DiscardProposalPersistsTerminalStateHistoryMarkdownAndIndexWithoutDecisionMutation()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/operational_context.md", "# Operational Context\n\nStable understanding.");
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        string candidateBefore = await ReadAsync(repository, ".agents/decisions/candidates/CAND-0001/candidate.json");
        string operationalContextBefore = await ReadAsync(repository, ".agents/operational_context.md");
        var service = CreateGenerationService(repository, store, decisionRepository);
        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);
        await service.MarkProposalViewedAsync(repository.Id, proposal.Id, "Reviewer inspected the proposal.");

        DecisionProposal discarded = await service.DiscardProposalAsync(repository.Id, proposal.Id, "Proposal no longer matches direction.");

        Assert.Equal(DecisionProposalState.Discarded, discarded.State);
        Assert.Contains(discarded.History, entry =>
            entry.Event == "Discarded" &&
            entry.FromState == DecisionProposalState.Viewed.ToString() &&
            entry.ToState == DecisionProposalState.Discarded.ToString() &&
            entry.Reason == "Proposal no longer matches direction.");

        DecisionProposal? reloaded = await decisionRepository.GetProposalAsync(repository, proposal.Id);
        string proposalMarkdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/proposal.md");
        string index = await ReadAsync(repository, ".agents/decisions/decisions.md");
        string candidateAfter = await ReadAsync(repository, ".agents/decisions/candidates/CAND-0001/candidate.json");
        string operationalContextAfter = await ReadAsync(repository, ".agents/operational_context.md");
        Assert.Equal(DecisionProposalState.Discarded, reloaded?.State);
        Assert.Contains("- State: Discarded", proposalMarkdown);
        Assert.Contains("- PROP-0001 | Discarded | CAND-0001 | Decide persistence schema", index);
        Assert.Equal(candidateBefore, candidateAfter);
        Assert.Equal(operationalContextBefore, operationalContextAfter);
        Assert.Empty(await decisionRepository.ListDecisionsAsync(repository));
        Assert.False(Directory.Exists(Path.Combine(repository.Path, ".agents", "decisions", "records")));
        Assert.False(Directory.Exists(Path.Combine(repository.Path, ".agents", "decisions", "assimilation")));
    }

    [Fact]
    public async Task DiscardProposalRejectsResolvedProposalsAndLeavesDecisionRecordsUntouched()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(repository, store, decisionRepository);
        var resolutionService = CreateResolutionService(repository, store, decisionRepository);
        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);
        await service.MarkProposalReadyForResolutionAsync(repository.Id, proposal.Id, "Ready for human resolution.");
        Decision decision = await resolutionService.ResolveProposalAsync(
            repository.Id,
            proposal.Id,
            new ResolveDecisionCommand("Resolve before discard attempt.", "human-reviewer", "option-1"));
        string decisionBefore = await ReadAsync(repository, ".agents/decisions/records/DEC-0001/decision.json");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DiscardProposalAsync(repository.Id, proposal.Id, "Try to discard after resolution."));

        string decisionAfter = await ReadAsync(repository, ".agents/decisions/records/DEC-0001/decision.json");
        DecisionProposal? reloaded = await decisionRepository.GetProposalAsync(repository, proposal.Id);
        Assert.Equal("Proposal transition from Resolved to Discarded is not allowed.", exception.Message);
        Assert.Equal(decision.Id, Assert.Single(await decisionRepository.ListDecisionsAsync(repository)).Id);
        Assert.Equal(decisionBefore, decisionAfter);
        Assert.Equal(DecisionProposalState.Resolved, reloaded?.State);
        Assert.False(Directory.Exists(Path.Combine(repository.Path, ".agents", "decisions", "assimilation")));
    }

    [Fact]
    public async Task ProposalEndpointsReturnSuccessForGenerationListingGetReviewTransitionsAndExpiration()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "# Plan\n\n- Need to decide repository-backed persistence schema.");
        await WriteAsync(repository, ".agents/milestones/m3-proposal-generation.md", "# M3\n\n- Generate proposals.");

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        DecisionDiscoveryResult discovery = (await (await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/discover",
            null)).Content.ReadFromJsonAsync<DecisionDiscoveryResult>(jsonOptions))!;
        DecisionCandidate candidate = Assert.Single(discovery.Candidates);
        await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/candidates/{candidate.Id}/promote",
            new DecisionCandidateTransitionRequest("Ready for proposal."));

        HttpResponseMessage generateResponse = await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/candidates/{candidate.Id}/proposals",
            null);
        DecisionProposal generated = (await generateResponse.Content.ReadFromJsonAsync<DecisionProposal>(jsonOptions))!;
        HttpResponseMessage listResponse = await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals");
        DecisionProposal[] listed = (await listResponse.Content.ReadFromJsonAsync<DecisionProposal[]>(jsonOptions))!;
        HttpResponseMessage getResponse = await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{generated.Id}");
        HttpResponseMessage expireResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{generated.Id}/expire",
            new DecisionProposalTransitionRequest("No longer current."));
        HttpResponseMessage regenerateResponse = await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/candidates/{candidate.Id}/proposals",
            null);
        DecisionProposal regenerated = (await regenerateResponse.Content.ReadFromJsonAsync<DecisionProposal>(jsonOptions))!;
        HttpResponseMessage viewedResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{regenerated.Id}/review/viewed",
            new DecisionProposalTransitionRequest("Viewed in review workspace."));
        HttpResponseMessage readyResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{regenerated.Id}/review/ready-for-resolution",
            new DecisionProposalTransitionRequest("Ready for human resolution."));
        HttpResponseMessage refineTooLateResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{regenerated.Id}/refinements",
            new DecisionRefinementRequest("Attempt after ready.", Context: "Changed context."));

        Assert.Equal(HttpStatusCode.OK, generateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, expireResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, regenerateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, viewedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, refineTooLateResponse.StatusCode);
        Assert.Single(listed);
        Assert.Equal(generated.Id, listed[0].Id);
        Assert.Equal(DecisionProposalState.Viewed, (await viewedResponse.Content.ReadFromJsonAsync<DecisionReviewWorkspace>(jsonOptions))!.Proposal.State);
        Assert.Equal(DecisionProposalState.ReadyForResolution, (await readyResponse.Content.ReadFromJsonAsync<DecisionReviewWorkspace>(jsonOptions))!.Proposal.State);
        Assert.Equal(DecisionProposalState.Expired, (await expireResponse.Content.ReadFromJsonAsync<DecisionProposal>(jsonOptions))!.State);
    }

    [Fact]
    public async Task ProposalDiscardEndpointReturnsDiscardedProposalAndConflictForResolvedProposal()
    {
        Repository repository = CreateRepository();
        DecisionCandidate firstCandidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        DecisionCandidate secondCandidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            summary: "Need to decide review state projection.") with
        {
            Id = "CAND-0002",
            Title = "Decide review projection"
        };
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, firstCandidate);
        await decisionRepository.SaveCandidateAsync(repository, secondCandidate);

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        DecisionProposal firstProposal = (await (await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/candidates/{firstCandidate.Id}/proposals",
            null)).Content.ReadFromJsonAsync<DecisionProposal>(jsonOptions))!;
        HttpResponseMessage discardResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{firstProposal.Id}/discard",
            new DecisionProposalTransitionRequest("Discard via endpoint."));

        DecisionProposal secondProposal = (await (await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/candidates/{secondCandidate.Id}/proposals",
            null)).Content.ReadFromJsonAsync<DecisionProposal>(jsonOptions))!;
        await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{secondProposal.Id}/review/ready-for-resolution",
            null);
        await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{secondProposal.Id}/resolve",
            new ResolveDecisionCommand("Resolve via endpoint.", "human-reviewer", "option-1"));
        HttpResponseMessage discardResolvedResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{secondProposal.Id}/discard",
            new DecisionProposalTransitionRequest("Try to discard resolved proposal."));

        Assert.Equal(HttpStatusCode.OK, discardResponse.StatusCode);
        Assert.Equal(DecisionProposalState.Discarded, (await discardResponse.Content.ReadFromJsonAsync<DecisionProposal>(jsonOptions))!.State);
        Assert.Equal(HttpStatusCode.Conflict, discardResolvedResponse.StatusCode);
    }

    [Fact]
    public async Task ProposalResolveEndpointReturnsDecisionRecord()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        DecisionProposal proposal = (await (await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/candidates/{candidate.Id}/proposals",
            null)).Content.ReadFromJsonAsync<DecisionProposal>(jsonOptions))!;
        await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/review/ready-for-resolution",
            null);

        HttpResponseMessage resolveResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/resolve",
            new ResolveDecisionCommand("Resolve via endpoint.", "human-reviewer", "option-1"));

        Decision decision = (await resolveResponse.Content.ReadFromJsonAsync<Decision>(jsonOptions))!;
        ReasoningEvent[] reasoningEvents = (await (await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/events"))
            .Content.ReadFromJsonAsync<ReasoningEvent[]>(jsonOptions))!;
        ReasoningRelationship[] reasoningRelationships = (await (await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/relationships"))
            .Content.ReadFromJsonAsync<ReasoningRelationship[]>(jsonOptions))!;
        ReasoningEvent reasoningEvent = Assert.Single(reasoningEvents);
        ReasoningRelationship relationship = Assert.Single(reasoningRelationships);

        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);
        Assert.Equal("DEC-0001", decision.Id.Value);
        Assert.Equal(DecisionState.Resolved, decision.State);
        Assert.Equal("option-1", decision.Resolution?.SelectedOptionId);
        Assert.Equal(ReasoningEventFamily.Evidence, reasoningEvent.Family);
        Assert.Equal(ReasoningEventType.EvidenceAdded, reasoningEvent.Type);
        Assert.Equal("InferredProposalResolution", reasoningEvent.Provenance.SourceKind);
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.Proposal &&
            reference.Id == proposal.Id);
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.Decision &&
            reference.Id == decision.Id.Value);
        Assert.Equal(ReasoningRelationshipType.DerivesFrom, relationship.Type);
        Assert.Equal(decision.Id.Value, relationship.Source.Id);
        Assert.Equal(proposal.Id, relationship.Target.Id);
    }

    [Fact]
    public async Task ProposalResolveEndpointDoesNotCaptureReasoningWhenResolutionFails()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        DecisionProposal proposal = (await (await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/candidates/{candidate.Id}/proposals",
            null)).Content.ReadFromJsonAsync<DecisionProposal>(jsonOptions))!;

        HttpResponseMessage resolveResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/resolve",
            new ResolveDecisionCommand("Resolve before ready state.", "human-reviewer", "option-1"),
            jsonOptions);

        ReasoningEvent[] reasoningEvents = (await (await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/events"))
            .Content.ReadFromJsonAsync<ReasoningEvent[]>(jsonOptions))!;
        ReasoningRelationship[] reasoningRelationships = (await (await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/relationships"))
            .Content.ReadFromJsonAsync<ReasoningRelationship[]>(jsonOptions))!;

        Assert.Equal(HttpStatusCode.Conflict, resolveResponse.StatusCode);
        Assert.Empty(reasoningEvents);
        Assert.Empty(reasoningRelationships);
    }

    [Fact]
    public async Task DecisionSupersedeAndArchiveEndpointsReturnMutatedDecisionAndConflicts()
    {
        Repository repository = CreateRepository();
        DecisionCandidate firstCandidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        DecisionCandidate secondCandidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            summary: "Need to decide replacement authority.") with
        {
            Id = "CAND-0002",
            Title = "Decide replacement authority"
        };
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, firstCandidate);
        await decisionRepository.SaveCandidateAsync(repository, secondCandidate);

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        Decision firstDecision = await ResolveViaEndpointAsync(root, client, jsonOptions, repository.Id, firstCandidate.Id);
        Decision replacementDecision = await ResolveViaEndpointAsync(root, client, jsonOptions, repository.Id, secondCandidate.Id);

        HttpResponseMessage supersedeResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/{firstDecision.Id.Value}/supersede",
            new SupersedeDecisionCommand(
                replacementDecision.Id.Value,
                "Replacement captures the current authority.",
                "human-reviewer"),
            jsonOptions);
        HttpResponseMessage archiveResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/{firstDecision.Id.Value}/archive",
            new ArchiveDecisionCommand("Superseded authority is no longer active.", "human-reviewer"),
            jsonOptions);
        HttpResponseMessage archiveResolvedResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/{replacementDecision.Id.Value}/archive",
            new ArchiveDecisionCommand("Archive before supersession.", "human-reviewer"),
            jsonOptions);

        Decision superseded = (await supersedeResponse.Content.ReadFromJsonAsync<Decision>(jsonOptions))!;
        Decision archived = (await archiveResponse.Content.ReadFromJsonAsync<Decision>(jsonOptions))!;
        ReasoningEvent[] reasoningEvents = (await (await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/events"))
            .Content.ReadFromJsonAsync<ReasoningEvent[]>(jsonOptions))!;
        ReasoningRelationship[] reasoningRelationships = (await (await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/relationships"))
            .Content.ReadFromJsonAsync<ReasoningRelationship[]>(jsonOptions))!;

        Assert.Equal(HttpStatusCode.OK, supersedeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, archiveResolvedResponse.StatusCode);
        Assert.Equal(DecisionState.Superseded, superseded.State);
        Assert.Equal(DecisionState.Archived, archived.State);
        Assert.Equal(4, reasoningEvents.Length);
        Assert.Equal(3, reasoningRelationships.Length);
        ReasoningEvent supersededEvent = Assert.Single(reasoningEvents, reasoningEvent =>
            reasoningEvent.Type == ReasoningEventType.DecisionSuperseded);
        ReasoningEvent archivedEvent = Assert.Single(reasoningEvents, reasoningEvent =>
            reasoningEvent.Type == ReasoningEventType.EvidenceAdded &&
            reasoningEvent.Family == ReasoningEventFamily.DecisionEvolution);
        ReasoningRelationship relationship = Assert.Single(reasoningRelationships, relationship =>
            relationship.Type == ReasoningRelationshipType.Supersedes);
        Assert.Equal(ReasoningEventType.DecisionSuperseded, supersededEvent.Type);
        Assert.Equal(ReasoningEventFamily.DecisionEvolution, supersededEvent.Family);
        Assert.Equal("InferredDecisionArchival", archivedEvent.Provenance.SourceKind);
        Assert.Contains(archivedEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.Decision &&
            reference.Id == firstDecision.Id.Value);
        Assert.Equal(ReasoningRelationshipType.Supersedes, relationship.Type);
        Assert.Equal(replacementDecision.Id.Value, relationship.Source.Id);
        Assert.Equal(firstDecision.Id.Value, relationship.Target.Id);
    }

    [Fact]
    public async Task ProposalReviewEndpointReturnsConflictForInvalidTransition()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();

        DecisionProposal proposal = (await (await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/candidates/{candidate.Id}/proposals",
            null)).Content.ReadFromJsonAsync<DecisionProposal>(CreateJsonOptions()))!;
        await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/review/viewed",
            null);
        await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/review/needs-refinement",
            null);

        HttpResponseMessage readyResponse = await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/review/ready-for-resolution",
            null);

        Assert.Equal(HttpStatusCode.Conflict, readyResponse.StatusCode);
    }

    [Fact]
    public async Task ProposalRefinementEndpointPersistsRevisionAndListsIt()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        DecisionProposal proposal = (await (await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/candidates/{candidate.Id}/proposals",
            null)).Content.ReadFromJsonAsync<DecisionProposal>(jsonOptions))!;
        await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/review/viewed",
            null);
        await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/review/needs-refinement",
            null);

        HttpResponseMessage refineResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/refinements",
            new DecisionRefinementRequest("Refine endpoint context.", Context: "Endpoint-refined context."));
        HttpResponseMessage revisionsResponse = await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/revisions");

        DecisionProposal refined = (await refineResponse.Content.ReadFromJsonAsync<DecisionProposal>(jsonOptions))!;
        DecisionProposalRevision[] revisions = (await revisionsResponse.Content.ReadFromJsonAsync<DecisionProposalRevision[]>(jsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, refineResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revisionsResponse.StatusCode);
        Assert.Equal(DecisionProposalState.Refined, refined.State);
        DecisionProposalRevision revision = Assert.Single(revisions);
        Assert.Equal("REV-0001", revision.Id);
        Assert.Contains("Context", revision.ChangedFields);
    }

    [Fact]
    public async Task ProposalLineageEndpointReturnsReadOnlyRevisionProjection()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        var decisionRepository = new FileSystemDecisionRepository(new FileSystemArtifactStore());
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionProposal proposal = (await (await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/candidates/{candidate.Id}/proposals",
            null)).Content.ReadFromJsonAsync<DecisionProposal>(jsonOptions))!;
        await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/review/viewed",
            null);
        await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/review/needs-refinement",
            null);
        await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/notes",
            new DecisionReviewNoteRequest("Keep lineage read-only.", "reviewer"),
            jsonOptions);
        await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/refinements",
            new DecisionRefinementRequest("Refine endpoint lineage.", Context: "Endpoint lineage context."),
            jsonOptions);

        HttpResponseMessage lineageResponse = await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/lineage");

        DecisionProposalLineage lineage =
            (await lineageResponse.Content.ReadFromJsonAsync<DecisionProposalLineage>(jsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, lineageResponse.StatusCode);
        Assert.Equal(DecisionProposalState.Refined, lineage.CurrentState);
        Assert.Equal("Endpoint lineage context.", lineage.CurrentProposal.Context);
        DecisionProposalRevisionSnapshot snapshot = Assert.Single(lineage.Revisions);
        Assert.False(snapshot.IsCurrentProposal);
        Assert.Equal("REV-0001", snapshot.Revision.Id);
        Assert.Contains(lineage.ReviewNotes, note => note.Body == "Keep lineage read-only.");
        Assert.Contains(lineage.Diagnostics, diagnostic =>
            diagnostic.Contains("Current proposal is authoritative", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProposalEndpointReturnsConflictForUnpromotedCandidate()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "# Plan\n\n- Need to decide repository-backed persistence schema.");

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        DecisionDiscoveryResult discovery = (await (await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/discover",
            null)).Content.ReadFromJsonAsync<DecisionDiscoveryResult>(jsonOptions))!;
        DecisionCandidate candidate = Assert.Single(discovery.Candidates);

        HttpResponseMessage generateResponse = await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/candidates/{candidate.Id}/proposals",
            null);

        Assert.Equal(HttpStatusCode.Conflict, generateResponse.StatusCode);
    }

    private static async Task<DecisionProposal> GenerateWithOptionsAsync(IReadOnlyList<DecisionOption> options)
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        DecisionEvidence evidence = CandidateEvidence(candidate);
        DecisionOption[] generatedOptions = options
            .Select(option => option with
            {
                Evidence = option.Evidence.Count == 0
                    ? [evidence]
                    : option.Evidence
            })
            .ToArray();
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var service = CreateGenerationService(
            repository,
            store,
            decisionRepository,
            new RejectedDiagnosticOptionGenerationService(OptionGenerationResult(generatedOptions)));

        return await service.GenerateProposalAsync(repository.Id, candidate.Id);
    }

    private static DecisionOption TestOption(
        string id,
        string title,
        DecisionOptionType type,
        DecisionEvidence? evidence = null)
    {
        return new DecisionOption(
            id,
            title,
            $"Generated test option for {title}.",
            evidence is null ? [] : [evidence])
        {
            Type = type,
            Assumptions = ["Generated test assumption."],
            Dependencies = ["Generated test dependency."]
        };
    }

    private static DecisionEvidence CandidateEvidence(DecisionCandidate candidate)
    {
        return new DecisionEvidence(
            "Plan requires a persistence decision.",
            [new DecisionSourceReference(
                "Plan",
                ".agents/plan.md",
                Section: "Plan",
                ItemId: "plan",
                CandidateId: candidate.Id,
                Excerpt: candidate.Summary)]);
    }

    private static DecisionOptionGenerationResult OptionGenerationResult(IReadOnlyList<DecisionOption> options)
    {
        return new DecisionOptionGenerationResult(
            options,
            [],
            new DecisionGenerationDiagnostics(
                options.Count,
                options.Count,
                0,
                0,
                0,
                [],
                ["Generated by recommendation test."]));
    }

    private static Decision CreatePriorDecision(Guid repositoryId)
    {
        DateTimeOffset now = new(2026, 06, 22, 12, 00, 00, TimeSpan.Zero);
        return new Decision(
            new DecisionId("DEC-0001"),
            DecisionState.Resolved,
            DecisionClassification.Architectural,
            "Repository files remain authoritative",
            "Prior decision context for recommendation evidence.",
            new DecisionMetadata(repositoryId, now, now),
            null,
            [],
            [new DecisionEvidence("Prior decision requires repository files as authority.", [new DecisionSourceReference("Decision", ".agents/decisions/records/DEC-0001/decision.json", DecisionId: new DecisionId("DEC-0001"))])],
            [new DecisionHistoryEntry(now, "Resolved", null, DecisionState.Resolved.ToString(), "Seeded by recommendation test.", [])]);
    }

    private static DecisionGenerationService CreateGenerationService(
        Repository repository,
        FileSystemArtifactStore store,
        FileSystemDecisionRepository decisionRepository,
        IOptionGenerationService? optionGenerationService = null)
    {
        var repositoryService = new StubRepositoryService(repository);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        var contextService = new DecisionContextService(repositoryService, store, decisionRepository);
        return new DecisionGenerationService(
            repositoryService,
            decisionRepository,
            projectionService,
            optionGenerationService ?? new OptionGenerationService(),
            contextService);
    }

    private static DecisionResolutionService CreateResolutionService(
        Repository repository,
        FileSystemArtifactStore store,
        FileSystemDecisionRepository decisionRepository)
    {
        var repositoryService = new StubRepositoryService(repository);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        return new DecisionResolutionService(repositoryService, decisionRepository, projectionService);
    }

    private static DecisionOperationalContextAssimilationService CreateAssimilationService(
        Repository repository,
        FileSystemArtifactStore store,
        FileSystemDecisionRepository decisionRepository)
    {
        var repositoryService = new StubRepositoryService(repository);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        var contextService = new DecisionContextService(repositoryService, store, decisionRepository);
        return new DecisionOperationalContextAssimilationService(
            repositoryService,
            decisionRepository,
            contextService,
            projectionService);
    }

    private static async Task<Decision> ResolveAcceptedDecisionAsync(
        Repository repository,
        DecisionGenerationService generationService,
        DecisionResolutionService resolutionService,
        string candidateId)
    {
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidateId);
        await generationService.MarkProposalReadyForResolutionAsync(repository.Id, proposal.Id, "Ready for human resolution.");
        return await resolutionService.ResolveProposalAsync(
            repository.Id,
            proposal.Id,
            new ResolveDecisionCommand("Accept the proposal.", "human-reviewer", "option-1"));
    }

    private static async Task<Decision> ResolveViaEndpointAsync(
        string root,
        HttpClient client,
        JsonSerializerOptions jsonOptions,
        Guid repositoryId,
        string candidateId)
    {
        DecisionProposal proposal = (await (await client.PostAsync(
            $"{root}/api/repositories/{repositoryId}/decisions/candidates/{candidateId}/proposals",
            null)).Content.ReadFromJsonAsync<DecisionProposal>(jsonOptions))!;
        await client.PostAsync(
            $"{root}/api/repositories/{repositoryId}/decisions/proposals/{proposal.Id}/review/ready-for-resolution",
            null);
        return (await (await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repositoryId}/decisions/proposals/{proposal.Id}/resolve",
            new ResolveDecisionCommand("Resolve via endpoint.", "human-reviewer", "option-1"),
            jsonOptions)).Content.ReadFromJsonAsync<Decision>(jsonOptions))!;
    }

    private static DecisionCandidate CreateCandidate(
        Guid repositoryId,
        DecisionCandidateState state,
        string signalKind = "MissingDirection",
        string summary = "Need to decide repository-backed persistence schema.",
        DecisionClassification classification = DecisionClassification.Architectural,
        bool includeEvidence = true)
    {
        DecisionEvidence[] evidence = includeEvidence
            ? [
                new DecisionEvidence(
                    "Plan requires a persistence decision.",
                    [new DecisionSourceReference(
                        "Plan",
                        ".agents/plan.md",
                        Section: "Plan",
                        ItemId: "plan",
                        Excerpt: summary)])
            ]
            : [];
        DecisionSourceReference[] sources = includeEvidence
            ? [
                new DecisionSourceReference(
                    "Plan",
                    ".agents/plan.md",
                    Section: "Plan",
                    ItemId: "plan",
                    Excerpt: summary)
            ]
            : [];
        return new DecisionCandidate(
            "CAND-0001",
            repositoryId,
            state,
            DecisionCandidatePriority.High,
            classification,
            "Decide persistence schema",
            summary,
            "source-fingerprint",
            [new DecisionSignal(
                signalKind,
                summary,
                classification,
                DecisionCandidatePriority.High,
                [new DecisionEvidence(
                    "Plan requires a persistence decision.",
                    [new DecisionSourceReference(
                        "Plan",
                        ".agents/plan.md",
                        Section: "Plan",
                        ItemId: "plan",
                        Excerpt: summary)])])],
            evidence,
            sources,
            ["Created by generation test."],
            [new DecisionHistoryEntry(
                DateTimeOffset.UtcNow,
                state == DecisionCandidateState.Promoted ? "Promoted" : "Discovered",
                null,
                state.ToString(),
                "Seeded by generation test.",
                [])]);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        return jsonOptions;
    }

    private static async Task WriteAsync(Repository repository, string relativePath, string content)
    {
        string path = Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
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

    private sealed class RejectedDiagnosticOptionGenerationService(
        DecisionOptionGenerationResult result) : IOptionGenerationService
    {
        public DecisionOptionGenerationResult GenerateOptions(
            DecisionCandidate candidate,
            IReadOnlyList<DecisionEvidence> evidence)
        {
            return result;
        }
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
