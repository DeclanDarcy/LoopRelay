using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;
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
        Assert.Single(proposal.Options);
        Assert.NotNull(proposal.Recommendation);
        Assert.NotEmpty(proposal.Tradeoffs);
        Assert.NotEmpty(proposal.Assumptions);
        Assert.Contains(proposal.Assumptions, assumption =>
            assumption.Statement == "Only one viable option is currently represented in repository evidence; no unsupported alternatives were generated.");
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "proposals", "PROP-0001", "proposal.json")));
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "proposals", "PROP-0001", "proposal.md")));
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "proposals", "PROP-0001", "history.json")));

        string markdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/proposal.md");
        string index = await ReadAsync(repository, ".agents/decisions/decisions.md");
        Assert.Contains("# PROP-0001: Decide persistence schema", markdown);
        Assert.Contains("## Recommendation", markdown);
        Assert.Contains("Candidate CAND-0001 was promoted for proposal generation.", markdown);
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

        Assert.Equal(2, proposal.Options.Count);
        Assert.Contains(proposal.Options, option => option.Id == "option-2" && option.Title == "Preserve current direction until stronger evidence exists");
        Assert.DoesNotContain(proposal.Assumptions, assumption =>
            assumption.Statement.Contains("Only one viable option", StringComparison.Ordinal));
        Assert.Contains(proposal.Tradeoffs, tradeoff => tradeoff.OptionId == "option-2");
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
        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);
        await service.MarkProposalReadyForResolutionAsync(repository.Id, proposal.Id, "Ready for human resolution.");
        string operationalContextBefore = await ReadAsync(repository, ".agents/operational_context.md");

        Decision decision = await service.ResolveProposalAsync(
            repository.Id,
            proposal.Id,
            new ResolveDecisionCommand(
                "Accept the proposed persistence direction.",
                "human-reviewer",
                "option-1"));

        Assert.Equal("DEC-0001", decision.Id.Value);
        Assert.Equal(DecisionState.Resolved, decision.State);
        Assert.Equal(DecisionOutcome.Accepted, decision.Resolution?.Outcome);
        Assert.Equal("option-1", decision.Resolution?.SelectedOptionId);
        Assert.Equal("human-reviewer", decision.Resolution?.ResolvedBy);
        Assert.False(decision.Resolution?.RecommendationDiverged);
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "records", "DEC-0001", "decision.json")));
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "records", "DEC-0001", "decision.md")));
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "records", "DEC-0001", "history.json")));

        DecisionProposal? resolvedProposal = await decisionRepository.GetProposalAsync(repository, proposal.Id);
        string decisionMarkdown = await ReadAsync(repository, ".agents/decisions/records/DEC-0001/decision.md");
        string proposalMarkdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/proposal.md");
        string index = await ReadAsync(repository, ".agents/decisions/decisions.md");
        string operationalContextAfter = await ReadAsync(repository, ".agents/operational_context.md");
        Assert.Equal(DecisionProposalState.Resolved, resolvedProposal?.State);
        Assert.Contains("- Selected option: option-1", decisionMarkdown);
        Assert.Contains("- Resolved by: human-reviewer", decisionMarkdown);
        Assert.Contains("- Recommendation diverged: False", decisionMarkdown);
        Assert.Contains("- State: Resolved", proposalMarkdown);
        Assert.Contains("- DEC-0001 | Resolved | Architectural | Accepted | Decide persistence schema", index);
        Assert.Contains("- PROP-0001 | Resolved | CAND-0001 | Decide persistence schema", index);
        Assert.Equal(operationalContextBefore, operationalContextAfter);
        Assert.False(Directory.Exists(Path.Combine(repository.Path, ".agents", "decisions", "assimilation")));
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
        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);

        InvalidOperationException stateException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ResolveProposalAsync(
                repository.Id,
                proposal.Id,
                new ResolveDecisionCommand("Resolve too early.", "human-reviewer", "option-1")));

        await service.MarkProposalReadyForResolutionAsync(repository.Id, proposal.Id, "Ready now.");
        ArgumentException rationaleException = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ResolveProposalAsync(
                repository.Id,
                proposal.Id,
                new ResolveDecisionCommand("", "human-reviewer", "option-1")));
        ArgumentException resolverException = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ResolveProposalAsync(
                repository.Id,
                proposal.Id,
                new ResolveDecisionCommand("Resolve.", "", "option-1")));
        ArgumentException optionException = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ResolveProposalAsync(
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
        DecisionProposal proposal = await service.GenerateProposalAsync(repository.Id, candidate.Id);
        await service.MarkProposalReadyForResolutionAsync(repository.Id, proposal.Id, "Ready for alternate option.");

        Decision decision = await service.ResolveProposalAsync(
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
        Assert.Equal(DecisionProposalState.Viewed, (await viewedResponse.Content.ReadFromJsonAsync<DecisionProposal>(jsonOptions))!.State);
        Assert.Equal(DecisionProposalState.ReadyForResolution, (await readyResponse.Content.ReadFromJsonAsync<DecisionProposal>(jsonOptions))!.State);
        Assert.Equal(DecisionProposalState.Expired, (await expireResponse.Content.ReadFromJsonAsync<DecisionProposal>(jsonOptions))!.State);
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
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);
        Assert.Equal("DEC-0001", decision.Id.Value);
        Assert.Equal(DecisionState.Resolved, decision.State);
        Assert.Equal("option-1", decision.Resolution?.SelectedOptionId);
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

    private static DecisionGenerationService CreateGenerationService(
        Repository repository,
        FileSystemArtifactStore store,
        FileSystemDecisionRepository decisionRepository)
    {
        var repositoryService = new StubRepositoryService(repository);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        return new DecisionGenerationService(repositoryService, decisionRepository, projectionService);
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
