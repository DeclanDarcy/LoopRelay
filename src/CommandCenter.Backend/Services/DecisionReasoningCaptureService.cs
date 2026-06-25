using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Continuity.Models;
using CommandCenter.Continuity.Primitives;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Execution.Models;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Services;

namespace CommandCenter.Backend.Services;

public sealed class DecisionReasoningCaptureService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository,
    IArtifactStore artifactStore,
    IReasoningRepository reasoningRepository)
    : IDecisionReasoningCaptureService
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<IReadOnlyList<ReasoningCaptureAttemptResult>> CaptureProposalResolvedAsync(
        Guid repositoryId,
        Decision decision,
        ResolveDecisionCommand command)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionResolution resolution = decision.Resolution
            ?? throw new InvalidOperationException($"Decision {decision.Id.Value} does not contain resolution metadata.");
        DecisionResolvedProposalSnapshot proposal = resolution.SourceProposalSnapshot
            ?? throw new InvalidOperationException($"Decision {decision.Id.Value} does not contain source proposal metadata.");
        string rationale = RequireText(command.Rationale, "Resolution rationale is required.");
        string resolver = RequireText(command.Resolver, "Resolver metadata is required.");
        string selectedOptionId = RequireText(command.SelectedOptionId, "Selected option id is required.");

        string transitionFingerprint = Fingerprint(new
        {
            Transition = "ProposalResolved",
            RepositoryId = repository.Id,
            ProposalId = proposal.ProposalId,
            CandidateId = proposal.CandidateId,
            SourceProposalFingerprint = proposal.ProposalFingerprint,
            SourceProposalState = proposal.ProposalState,
            DecisionId = decision.Id.Value,
            DecisionState = decision.State,
            ResolutionOutcome = resolution.Outcome,
            SelectedOptionId = selectedOptionId,
            ResolvedAt = resolution.ResolvedAt
        });

        ReasoningCaptureAttemptResult eventAttempt = await GetOrCreateProposalResolvedEventAsync(
            repository,
            decision,
            proposal,
            resolution,
            rationale,
            resolver,
            transitionFingerprint);
        string reasoningEventId = RequireCapturedOrExistingEventId(eventAttempt);

        await CreateDecisionDerivesFromProposalRelationshipIfMissingAsync(
            repository,
            decision,
            proposal,
            rationale,
            resolver,
            transitionFingerprint,
            reasoningEventId);

        return [eventAttempt];
    }

    public async Task<IReadOnlyList<ReasoningCaptureAttemptResult>> CaptureDecisionSupersededAsync(
        Guid repositoryId,
        Decision supersededDecision,
        SupersedeDecisionCommand command)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        string replacementDecisionId = RequireText(command.ReplacementDecisionId, "Replacement decision id is required.");
        string rationale = RequireText(command.Rationale, "Supersede rationale is required.");
        string resolver = RequireText(command.Resolver, "Resolver metadata is required.");
        DecisionId replacementId = DecisionId.Parse(replacementDecisionId);
        Decision replacementDecision = await decisionRepository.GetDecisionAsync(repository, replacementId)
            ?? throw new KeyNotFoundException($"Replacement decision was not found: {replacementId.Value}");

        string transitionFingerprint = Fingerprint(new
        {
            Transition = "DecisionSuperseded",
            RepositoryId = repository.Id,
            SupersededDecisionId = supersededDecision.Id.Value,
            ReplacementDecisionId = replacementDecision.Id.Value,
            SupersededState = supersededDecision.State,
            ReplacementState = replacementDecision.State,
            Rationale = rationale,
            Resolver = resolver
        });

        ReasoningCaptureAttemptResult eventAttempt = await GetOrCreateDecisionSupersededEventAsync(
            repository,
            supersededDecision,
            replacementDecision,
            rationale,
            resolver,
            transitionFingerprint);
        string reasoningEventId = RequireCapturedOrExistingEventId(eventAttempt);

        await CreateSupersedesRelationshipIfMissingAsync(
            repository,
            supersededDecision,
            replacementDecision,
            rationale,
            resolver,
            transitionFingerprint,
            reasoningEventId);

        return [eventAttempt];
    }

    public async Task<IReadOnlyList<ReasoningCaptureAttemptResult>> CaptureDecisionArchivedAsync(
        Guid repositoryId,
        Decision archivedDecision,
        ArchiveDecisionCommand command)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        string rationale = RequireText(command.Rationale, "Archive rationale is required.");
        string resolver = RequireText(command.Resolver, "Resolver metadata is required.");
        DecisionHistoryEntry archiveEntry = archivedDecision.History
            .LastOrDefault(entry =>
                string.Equals(entry.Event, "Archived", StringComparison.Ordinal) &&
                string.Equals(entry.ToState, DecisionState.Archived.ToString(), StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Decision {archivedDecision.Id.Value} does not contain archive history metadata.");

        string transitionFingerprint = Fingerprint(new
        {
            Transition = "DecisionArchived",
            RepositoryId = repository.Id,
            DecisionId = archivedDecision.Id.Value,
            FromState = archiveEntry.FromState,
            ToState = archiveEntry.ToState,
            ArchivedAt = archiveEntry.Timestamp,
            Rationale = rationale,
            Resolver = resolver
        });

        ReasoningCaptureAttemptResult eventAttempt = await GetOrCreateDecisionArchivedEventAsync(
            repository,
            archivedDecision,
            archiveEntry,
            rationale,
            resolver,
            transitionFingerprint);

        return [eventAttempt];
    }

    public async Task<IReadOnlyList<ReasoningCaptureAttemptResult>> CaptureGovernanceContradictionsAsync(
        Guid repositoryId,
        DecisionGovernanceReport report)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        if (report.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Governance report belongs to a different repository.");
        }

        List<ReasoningCaptureAttemptResult> attempts = [];
        foreach (DecisionGovernanceFinding finding in report.Findings)
        {
            if (!IsContradictionFinding(finding))
            {
                attempts.Add(SkippedAttempt(
                    "GovernanceContradictionObserved",
                    ReasoningReferenceFactory.GovernanceReportPath(report.Id),
                    report.GeneratedAt,
                    "Governance finding did not represent a reasoning contradiction.",
                    $"Finding {finding.Id} category {finding.Category} is not captured as inferred contradiction evidence.",
                    ["Only consistency, supersession lineage, authority-boundary, execution projection readiness, and fingerprint integrity findings are captured."]));
                continue;
            }

            string transitionFingerprint = Fingerprint(new
            {
                Transition = "GovernanceContradictionObserved",
                RepositoryId = repository.Id,
                ReportId = report.Id,
                report.InputFingerprint,
                report.GeneratedAt,
                Finding = finding
            });

            attempts.Add(await GetOrCreateGovernanceContradictionEventAsync(
                repository,
                report,
                finding,
                transitionFingerprint));
        }

        return attempts;
    }

    public async Task<IReadOnlyList<ReasoningCaptureAttemptResult>> CaptureOperationalContextPromotionAsync(
        Guid repositoryId,
        OperationalContextProposal proposal)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        if (proposal.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Operational-context proposal belongs to a different repository.");
        }

        if (proposal.Promotion.PromotedAt is null ||
            string.IsNullOrWhiteSpace(proposal.Promotion.PromotedContentHash))
        {
            throw new InvalidOperationException("Operational-context proposal does not contain successful promotion metadata.");
        }

        List<ReasoningCaptureAttemptResult> attempts = [];
        foreach (OperationalContextSemanticChange change in proposal.SemanticChanges)
        {
            if (!TryClassifyOperationalContextPromotionChange(change, out ReasoningEventFamily family, out ReasoningEventType type, out string tag))
            {
                attempts.Add(SkippedAttempt(
                    "OperationalContextPromotionReasoningObserved",
                    ReasoningReferenceFactory.OperationalContextProposalPath(proposal.ProposalId),
                    proposal.Promotion.PromotedAt,
                    "Promoted operational-context change did not map to a reasoning event family.",
                    $"Semantic change {change.Type} in {change.Section} is not captured as inferred reasoning.",
                    [$"Change description: {change.Description}"]));
                continue;
            }

            string transitionFingerprint = Fingerprint(new
            {
                Transition = "OperationalContextPromotionReasoningObserved",
                RepositoryId = repository.Id,
                ProposalId = proposal.ProposalId,
                proposal.Promotion.PromotedAt,
                proposal.Promotion.PromotedContentHash,
                proposal.Promotion.PromotedContentSourceRelativePath,
                SemanticChange = change
            });

            attempts.Add(await GetOrCreateOperationalContextPromotionEventAsync(
                repository,
                proposal,
                change,
                family,
                type,
                tag,
                transitionFingerprint));
        }

        return attempts;
    }

    public async Task<ReasoningCaptureAttemptResult> CaptureExecutionHandoffDecisionAsync(
        ExecutionSession session,
        bool accepted)
    {
        Repository repository = await GetRepositoryAsync(session.RepositoryId);
        string? handoffPath = NormalizeRelativePath(session.HandoffPath);
        if (handoffPath is null)
        {
            return SkippedAttempt(
                accepted ? "ExecutionHandoffAcceptedReasoningObserved" : "ExecutionHandoffRejectedReasoningObserved",
                null,
                accepted ? session.AcceptedAt : session.RejectedAt,
                "Execution session did not provide a handoff artifact.",
                "No handoff path was available for inferred reasoning capture.",
                [$"Execution session: {session.Id:D}"]);
        }

        string? handoffContent = await artifactStore.ReadAsync(ArtifactPath.ResolveRepositoryPath(repository, handoffPath));
        if (string.IsNullOrWhiteSpace(handoffContent) &&
            string.IsNullOrWhiteSpace(session.DecisionNote))
        {
            return SkippedAttempt(
                accepted ? "ExecutionHandoffAcceptedReasoningObserved" : "ExecutionHandoffRejectedReasoningObserved",
                handoffPath,
                accepted ? session.AcceptedAt : session.RejectedAt,
                "Execution handoff did not contain semantic content.",
                "Neither the handoff artifact nor the decision note contained text to classify.",
                [$"Execution session: {session.Id:D}"]);
        }

        string semanticText = $"{session.DecisionNote}\n{handoffContent}";
        if (!TryClassifyExecutionHandoffSignal(
                semanticText,
                accepted,
                out ReasoningEventFamily family,
                out ReasoningEventType type,
                out string tag,
                out string signal))
        {
            return SkippedAttempt(
                accepted ? "ExecutionHandoffAcceptedReasoningObserved" : "ExecutionHandoffRejectedReasoningObserved",
                handoffPath,
                accepted ? session.AcceptedAt : session.RejectedAt,
                "Execution handoff content did not match a reasoning capture signal.",
                "The handoff transition was workflow-only from the reasoning perspective.",
                [$"Execution session: {session.Id:D}"]);
        }

        DateTimeOffset? decidedAt = accepted ? session.AcceptedAt : session.RejectedAt;
        if (decidedAt is null)
        {
            throw new InvalidOperationException("Execution session does not contain successful handoff decision metadata.");
        }

        string handoffFingerprint = Fingerprint(new
        {
            Path = handoffPath,
            Content = handoffContent ?? string.Empty
        });
        string transitionFingerprint = Fingerprint(new
        {
            Transition = accepted ? "ExecutionHandoffAcceptedReasoningObserved" : "ExecutionHandoffRejectedReasoningObserved",
            RepositoryId = repository.Id,
            SessionId = session.Id,
            session.MilestonePath,
            DecidedAt = decidedAt,
            session.DecisionNote,
            HandoffPath = handoffPath,
            HandoffFingerprint = handoffFingerprint,
            Signal = signal
        });

        return await GetOrCreateExecutionHandoffDecisionEventAsync(
            repository,
            session,
            accepted,
            handoffPath,
            handoffContent ?? string.Empty,
            handoffFingerprint,
            family,
            type,
            tag,
            signal,
            transitionFingerprint);
    }

    private async Task<ReasoningCaptureAttemptResult> GetOrCreateProposalResolvedEventAsync(
        Repository repository,
        Decision decision,
        DecisionResolvedProposalSnapshot proposal,
        DecisionResolution resolution,
        string rationale,
        string resolver,
        string transitionFingerprint)
    {
        IReadOnlyList<ReasoningEvent> existingEvents = await reasoningRepository.ListEventsAsync(repository);
        ReasoningEvent? existing = existingEvents.FirstOrDefault(reasoningEvent =>
            reasoningEvent.Type == ReasoningEventType.EvidenceAdded &&
            string.Equals(reasoningEvent.Provenance.Fingerprint, transitionFingerprint, StringComparison.Ordinal));
        if (existing is not null)
        {
            return DuplicateAttempt(
                "ProposalResolved",
                ReasoningReferenceFactory.ProposalPath(proposal.ProposalId),
                resolution.ResolvedAt,
                "Proposal resolution already has inferred reasoning evidence.",
                existing);
        }

        ReasoningEvent created = await reasoningRepository.CreateEventAsync(
            repository,
            new CreateReasoningEventCommand(
                ReasoningEventFamily.Evidence,
                ReasoningEventType.EvidenceAdded,
                $"Proposal {proposal.ProposalId} informed decision {decision.Id.Value}",
                new ReasoningNarrative(
                    $"Decision {decision.Id.Value} was created from proposal {proposal.ProposalId}.",
                    $"The authoritative proposal resolution already occurred with outcome {resolution.Outcome} and selected option {resolution.SelectedOptionId}. Rationale: {rationale}"),
                [
                    ProposalReference(proposal),
                    CandidateReference(proposal),
                    DecisionReference(decision)
                ],
                Provenance(proposal, rationale, resolver, transitionFingerprint),
                [],
                ["decision-evolution", "inferred-capture", "proposal-resolution"]));
        return CapturedAttempt(
            "ProposalResolved",
            ReasoningReferenceFactory.ProposalPath(proposal.ProposalId),
            resolution.ResolvedAt,
            "Captured proposal resolution as inferred reasoning evidence.",
            created);
    }

    private async Task CreateDecisionDerivesFromProposalRelationshipIfMissingAsync(
        Repository repository,
        Decision decision,
        DecisionResolvedProposalSnapshot proposal,
        string rationale,
        string resolver,
        string transitionFingerprint,
        string reasoningEventId)
    {
        IReadOnlyList<ReasoningRelationship> existingRelationships = await reasoningRepository.ListRelationshipsAsync(repository);
        if (existingRelationships.Any(relationship =>
            relationship.Type == ReasoningRelationshipType.DerivesFrom &&
            relationship.Source.Kind == ReasoningReferenceKind.Decision &&
            relationship.Target.Kind == ReasoningReferenceKind.Proposal &&
            string.Equals(relationship.Source.Id, decision.Id.Value, StringComparison.Ordinal) &&
            string.Equals(relationship.Target.Id, proposal.ProposalId, StringComparison.Ordinal)))
        {
            return;
        }

        try
        {
            await reasoningRepository.CreateRelationshipAsync(
                repository,
                new CreateReasoningRelationshipCommand(
                    ReasoningRelationshipType.DerivesFrom,
                    DecisionReference(decision),
                    ProposalReference(proposal),
                    new ReasoningNarrative(
                        $"Decision {decision.Id.Value} derives from proposal {proposal.ProposalId}.",
                        $"Captured from reasoning event {reasoningEventId}. Rationale: {rationale}"),
                    Provenance(proposal, rationale, resolver, transitionFingerprint)));
        }
        catch (ReasoningConflictException)
        {
            // Another capture path recorded the same explanatory relationship first.
        }
    }

    private async Task<ReasoningCaptureAttemptResult> GetOrCreateDecisionSupersededEventAsync(
        Repository repository,
        Decision supersededDecision,
        Decision replacementDecision,
        string rationale,
        string resolver,
        string transitionFingerprint)
    {
        IReadOnlyList<ReasoningEvent> existingEvents = await reasoningRepository.ListEventsAsync(repository);
        ReasoningEvent? existing = existingEvents.FirstOrDefault(reasoningEvent =>
            reasoningEvent.Type == ReasoningEventType.DecisionSuperseded &&
            string.Equals(reasoningEvent.Provenance.Fingerprint, transitionFingerprint, StringComparison.Ordinal));
        if (existing is not null)
        {
            return DuplicateAttempt(
                "DecisionSuperseded",
                ReasoningReferenceFactory.DecisionPath(supersededDecision.Id.Value),
                supersededDecision.Metadata.UpdatedAt,
                "Decision supersession already has inferred reasoning evidence.",
                existing);
        }

        ReasoningEvent created = await reasoningRepository.CreateEventAsync(
            repository,
            new CreateReasoningEventCommand(
                ReasoningEventFamily.DecisionEvolution,
                ReasoningEventType.DecisionSuperseded,
                $"Decision {supersededDecision.Id.Value} superseded by {replacementDecision.Id.Value}",
                new ReasoningNarrative(
                    $"Decision {replacementDecision.Id.Value} replaced decision {supersededDecision.Id.Value}.",
                    $"The authoritative decision lifecycle transition already occurred. Rationale: {rationale}"),
                [
                    DecisionReference(supersededDecision),
                    DecisionReference(replacementDecision)
                ],
                DecisionProvenance(
                    supersededDecision,
                    "InferredDecisionSupersession",
                    "History: Superseded",
                    rationale,
                    resolver,
                    transitionFingerprint),
                [],
                ["decision-evolution", "inferred-capture", "supersession"]));
        return CapturedAttempt(
            "DecisionSuperseded",
            ReasoningReferenceFactory.DecisionPath(supersededDecision.Id.Value),
            supersededDecision.Metadata.UpdatedAt,
            "Captured decision supersession as inferred reasoning evidence.",
            created);
    }

    private async Task CreateSupersedesRelationshipIfMissingAsync(
        Repository repository,
        Decision supersededDecision,
        Decision replacementDecision,
        string rationale,
        string resolver,
        string transitionFingerprint,
        string reasoningEventId)
    {
        IReadOnlyList<ReasoningRelationship> existingRelationships = await reasoningRepository.ListRelationshipsAsync(repository);
        if (existingRelationships.Any(relationship =>
            relationship.Type == ReasoningRelationshipType.Supersedes &&
            relationship.Source.Kind == ReasoningReferenceKind.Decision &&
            relationship.Target.Kind == ReasoningReferenceKind.Decision &&
            string.Equals(relationship.Source.Id, replacementDecision.Id.Value, StringComparison.Ordinal) &&
            string.Equals(relationship.Target.Id, supersededDecision.Id.Value, StringComparison.Ordinal)))
        {
            return;
        }

        try
        {
            await reasoningRepository.CreateRelationshipAsync(
                repository,
                new CreateReasoningRelationshipCommand(
                    ReasoningRelationshipType.Supersedes,
                    DecisionReference(replacementDecision),
                    DecisionReference(supersededDecision),
                    new ReasoningNarrative(
                        $"Decision {replacementDecision.Id.Value} supersedes decision {supersededDecision.Id.Value}.",
                        $"Captured from reasoning event {reasoningEventId}. Rationale: {rationale}"),
                    DecisionProvenance(
                        supersededDecision,
                        "InferredDecisionSupersession",
                        "History: Superseded",
                        rationale,
                        resolver,
                        transitionFingerprint)));
        }
        catch (ReasoningConflictException)
        {
            // Another capture path recorded the same explanatory relationship first.
        }
    }

    private async Task<ReasoningCaptureAttemptResult> GetOrCreateDecisionArchivedEventAsync(
        Repository repository,
        Decision archivedDecision,
        DecisionHistoryEntry archiveEntry,
        string rationale,
        string resolver,
        string transitionFingerprint)
    {
        IReadOnlyList<ReasoningEvent> existingEvents = await reasoningRepository.ListEventsAsync(repository);
        ReasoningEvent? existing = existingEvents.FirstOrDefault(reasoningEvent =>
            reasoningEvent.Family == ReasoningEventFamily.DecisionEvolution &&
            reasoningEvent.Type == ReasoningEventType.EvidenceAdded &&
            string.Equals(reasoningEvent.Provenance.Fingerprint, transitionFingerprint, StringComparison.Ordinal));
        if (existing is not null)
        {
            return DuplicateAttempt(
                "DecisionArchived",
                ReasoningReferenceFactory.DecisionPath(archivedDecision.Id.Value),
                archiveEntry.Timestamp,
                "Decision archival already has inferred reasoning evidence.",
                existing);
        }

        ReasoningEvent created = await reasoningRepository.CreateEventAsync(
            repository,
            new CreateReasoningEventCommand(
                ReasoningEventFamily.DecisionEvolution,
                ReasoningEventType.EvidenceAdded,
                $"Decision {archivedDecision.Id.Value} archived",
                new ReasoningNarrative(
                    $"Decision {archivedDecision.Id.Value} was archived after reaching terminal authority state.",
                    $"The authoritative decision lifecycle transition already occurred from {archiveEntry.FromState} to {archiveEntry.ToState}. Rationale: {rationale}"),
                [DecisionReference(archivedDecision)],
                DecisionProvenance(
                    archivedDecision,
                    "InferredDecisionArchival",
                    "History: Archived",
                    rationale,
                    resolver,
                    transitionFingerprint),
                [],
                ["decision-evolution", "inferred-capture", "archival"]));
        return CapturedAttempt(
            "DecisionArchived",
            ReasoningReferenceFactory.DecisionPath(archivedDecision.Id.Value),
            archiveEntry.Timestamp,
            "Captured decision archival as inferred reasoning evidence.",
            created);
    }

    private async Task<ReasoningCaptureAttemptResult> GetOrCreateGovernanceContradictionEventAsync(
        Repository repository,
        DecisionGovernanceReport report,
        DecisionGovernanceFinding finding,
        string transitionFingerprint)
    {
        IReadOnlyList<ReasoningEvent> existingEvents = await reasoningRepository.ListEventsAsync(repository);
        ReasoningEvent? existing = existingEvents.FirstOrDefault(reasoningEvent =>
            reasoningEvent.Family == ReasoningEventFamily.Contradiction &&
            reasoningEvent.Type == ReasoningEventType.ContradictionIdentified &&
            string.Equals(reasoningEvent.Provenance.Fingerprint, transitionFingerprint, StringComparison.Ordinal));
        if (existing is not null)
        {
            return DuplicateAttempt(
                "GovernanceContradictionObserved",
                ReasoningReferenceFactory.GovernanceReportPath(report.Id),
                report.GeneratedAt,
                "Governance contradiction already has inferred reasoning evidence.",
                existing);
        }

        ReasoningEvent created = await reasoningRepository.CreateEventAsync(
            repository,
            new CreateReasoningEventCommand(
                ReasoningEventFamily.Contradiction,
                ReasoningEventType.ContradictionIdentified,
                $"Governance contradiction observed: {finding.Title}",
                new ReasoningNarrative(
                    $"Governance report {report.Id} identified {finding.Title}.",
                    $"Governance remains advisory; reasoning records the contradiction as explanatory evidence. {finding.Detail}"),
                GovernanceContradictionReferences(report, finding),
                GovernanceProvenance(report, finding, transitionFingerprint),
                [],
                ["contradiction", "governance", "inferred-capture"]));
        return CapturedAttempt(
            "GovernanceContradictionObserved",
            ReasoningReferenceFactory.GovernanceReportPath(report.Id),
            report.GeneratedAt,
            "Captured governance contradiction as inferred reasoning evidence.",
            created);
    }

    private async Task<ReasoningCaptureAttemptResult> GetOrCreateOperationalContextPromotionEventAsync(
        Repository repository,
        OperationalContextProposal proposal,
        OperationalContextSemanticChange change,
        ReasoningEventFamily family,
        ReasoningEventType type,
        string tag,
        string transitionFingerprint)
    {
        IReadOnlyList<ReasoningEvent> existingEvents = await reasoningRepository.ListEventsAsync(repository);
        ReasoningEvent? existing = existingEvents.FirstOrDefault(reasoningEvent =>
            reasoningEvent.Family == family &&
            reasoningEvent.Type == type &&
            string.Equals(reasoningEvent.Provenance.Fingerprint, transitionFingerprint, StringComparison.Ordinal));
        if (existing is not null)
        {
            return DuplicateAttempt(
                "OperationalContextPromotionReasoningObserved",
                ReasoningReferenceFactory.OperationalContextProposalPath(proposal.ProposalId),
                proposal.Promotion.PromotedAt,
                "Operational-context promotion already has inferred reasoning evidence.",
                existing);
        }

        ReasoningEvent created = await reasoningRepository.CreateEventAsync(
            repository,
            new CreateReasoningEventCommand(
                family,
                type,
                OperationalContextPromotionEventTitle(proposal, change),
                new ReasoningNarrative(
                    OperationalContextPromotionSummary(proposal, change),
                    $"Operational context remains authoritative current understanding; reasoning records the promoted semantic change as explanatory evidence. {change.Description}"),
                OperationalContextPromotionReferences(proposal, change),
                OperationalContextPromotionProvenance(proposal, change, transitionFingerprint),
                [],
                ["operational-context", "promotion", "inferred-capture", tag]));
        return CapturedAttempt(
            "OperationalContextPromotionReasoningObserved",
            ReasoningReferenceFactory.OperationalContextProposalPath(proposal.ProposalId),
            proposal.Promotion.PromotedAt,
            "Captured promoted operational-context change as inferred reasoning evidence.",
            created);
    }

    private async Task<ReasoningCaptureAttemptResult> GetOrCreateExecutionHandoffDecisionEventAsync(
        Repository repository,
        ExecutionSession session,
        bool accepted,
        string handoffPath,
        string handoffContent,
        string handoffFingerprint,
        ReasoningEventFamily family,
        ReasoningEventType type,
        string tag,
        string signal,
        string transitionFingerprint)
    {
        IReadOnlyList<ReasoningEvent> existingEvents = await reasoningRepository.ListEventsAsync(repository);
        ReasoningEvent? existing = existingEvents.FirstOrDefault(reasoningEvent =>
            reasoningEvent.Family == family &&
            reasoningEvent.Type == type &&
            string.Equals(reasoningEvent.Provenance.Fingerprint, transitionFingerprint, StringComparison.Ordinal));
        if (existing is not null)
        {
            return DuplicateAttempt(
                accepted ? "ExecutionHandoffAcceptedReasoningObserved" : "ExecutionHandoffRejectedReasoningObserved",
                handoffPath,
                accepted ? session.AcceptedAt : session.RejectedAt,
                "Execution handoff decision already has inferred reasoning evidence.",
                existing);
        }

        string sourceAction = accepted ? "accepted" : "rejected";
        ReasoningEvent created = await reasoningRepository.CreateEventAsync(
            repository,
            new CreateReasoningEventCommand(
                family,
                type,
                ExecutionHandoffDecisionEventTitle(session, signal),
                new ReasoningNarrative(
                    ExecutionHandoffDecisionSummary(session, signal),
                    $"Execution remains workflow authority; reasoning records the semantic meaning observed when execution output was {sourceAction}. {Excerpt(semanticText: session.DecisionNote ?? handoffContent, maxLength: 320)}"),
                ExecutionHandoffDecisionReferences(session, handoffPath, handoffContent, handoffFingerprint),
                ExecutionHandoffDecisionProvenance(session, sourceAction, handoffPath, transitionFingerprint),
                [],
                ["execution", "handoff", "inferred-capture", tag]));
        return CapturedAttempt(
            accepted ? "ExecutionHandoffAcceptedReasoningObserved" : "ExecutionHandoffRejectedReasoningObserved",
            handoffPath,
            accepted ? session.AcceptedAt : session.RejectedAt,
            "Captured execution handoff decision as inferred reasoning evidence.",
            created);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static ReasoningCaptureAttemptResult CapturedAttempt(
        string sourceTransition,
        string? sourceArtifact,
        DateTimeOffset? sourceTimestamp,
        string captureReason,
        ReasoningEvent reasoningEvent)
    {
        return new ReasoningCaptureAttemptResult(
            ReasoningCaptureMode.Inferred,
            ReasoningCaptureAttemptOutcome.Captured,
            sourceTransition,
            sourceArtifact,
            sourceTimestamp,
            captureReason,
            null,
            DuplicateSignal(reasoningEvent),
            null,
            ReasoningEventReference(reasoningEvent),
            []);
    }

    private static ReasoningCaptureAttemptResult DuplicateAttempt(
        string sourceTransition,
        string? sourceArtifact,
        DateTimeOffset? sourceTimestamp,
        string captureReason,
        ReasoningEvent existingEvent)
    {
        return new ReasoningCaptureAttemptResult(
            ReasoningCaptureMode.Inferred,
            ReasoningCaptureAttemptOutcome.Duplicate,
            sourceTransition,
            sourceArtifact,
            sourceTimestamp,
            captureReason,
            "Equivalent inferred reasoning evidence already exists.",
            DuplicateSignal(existingEvent),
            ReasoningEventReference(existingEvent),
            null,
            []);
    }

    private static ReasoningCaptureAttemptResult SkippedAttempt(
        string sourceTransition,
        string? sourceArtifact,
        DateTimeOffset? sourceTimestamp,
        string captureReason,
        string skipReason,
        IReadOnlyList<string> diagnostics)
    {
        return new ReasoningCaptureAttemptResult(
            ReasoningCaptureMode.Inferred,
            ReasoningCaptureAttemptOutcome.Skipped,
            sourceTransition,
            sourceArtifact,
            sourceTimestamp,
            captureReason,
            skipReason,
            null,
            null,
            null,
            diagnostics);
    }

    private static string RequireCapturedOrExistingEventId(ReasoningCaptureAttemptResult attempt)
    {
        return attempt.CapturedEventReference?.Id ??
            attempt.ExistingEventReference?.Id ??
            throw new InvalidOperationException("Reasoning capture attempt did not include a captured or existing event reference.");
    }

    private static ReasoningReference ReasoningEventReference(ReasoningEvent reasoningEvent)
    {
        return new ReasoningReference(
            ReasoningReferenceKind.ReasoningEvent,
            reasoningEvent.Id,
            null,
            reasoningEvent.Title,
            reasoningEvent.Narrative.Summary,
            reasoningEvent.Provenance.Fingerprint);
    }

    private static string? DuplicateSignal(ReasoningEvent reasoningEvent)
    {
        return string.IsNullOrWhiteSpace(reasoningEvent.Provenance.Fingerprint)
            ? null
            : $"Fingerprint {reasoningEvent.Provenance.Fingerprint}";
    }

    private static ReasoningReference DecisionReference(Decision decision)
    {
        return ReasoningReferenceFactory.Decision(
            decision.Id.Value,
            decision.Title,
            Fingerprint(decision));
    }

    private static ReasoningReference ProposalReference(DecisionResolvedProposalSnapshot proposal)
    {
        return ReasoningReferenceFactory.Proposal(
            proposal.ProposalId,
            proposal.Title,
            proposal.ProposalFingerprint,
            "Resolved Proposal");
    }

    private static ReasoningReference CandidateReference(DecisionResolvedProposalSnapshot proposal)
    {
        return ReasoningReferenceFactory.Candidate(
            proposal.CandidateId,
            proposal.Title,
            Fingerprint(new
            {
                proposal.CandidateId,
                proposal.ProposalId,
                proposal.ProposalFingerprint
            }),
            "Source Candidate");
    }

    private static IReadOnlyList<ReasoningReference> GovernanceContradictionReferences(
        DecisionGovernanceReport report,
        DecisionGovernanceFinding finding)
    {
        List<ReasoningReference> references =
        [
            ReasoningReferenceFactory.GovernanceFinding(
                finding.Id,
                report.Id,
                finding.Title,
                finding.Detail,
                Fingerprint(finding))
        ];

        references.AddRange(finding.RelatedDecisionIds.Select(decisionId =>
            ReasoningReferenceFactory.Decision(
                decisionId,
                section: "Related Governance Decision")));

        references.AddRange(finding.RelatedCandidateIds.Select(candidateId =>
            ReasoningReferenceFactory.Candidate(
                candidateId,
                section: "Related Governance Candidate")));

        references.AddRange(finding.RelatedProposalIds.Select(proposalId =>
            ReasoningReferenceFactory.Proposal(
                proposalId,
                section: "Related Governance Proposal")));

        return references;
    }

    private static IReadOnlyList<ReasoningReference> OperationalContextPromotionReferences(
        OperationalContextProposal proposal,
        OperationalContextSemanticChange change)
    {
        List<ReasoningReference> references =
        [
            ReasoningReferenceFactory.OperationalContextProposal(
                proposal.ProposalId,
                $"Semantic change: {change.Type}",
                change.Description,
                Fingerprint(new
                {
                    proposal.ProposalId,
                    proposal.Promotion.PromotedContentHash,
                    SemanticChange = change
                })),
            ReasoningReferenceFactory.Artifact(
                ".agents/operational_context.md",
                "Current Operational Context",
                fingerprint: proposal.Promotion.PromotedContentHash)
        ];

        if (!string.IsNullOrWhiteSpace(proposal.Promotion.PromotedContentSourceRelativePath))
        {
            references.Add(ReasoningReferenceFactory.Artifact(
                proposal.Promotion.PromotedContentSourceRelativePath,
                "Promoted Proposal Content"));
        }

        if (!string.IsNullOrWhiteSpace(proposal.Promotion.ArchivedRelativePath))
        {
            references.Add(ReasoningReferenceFactory.OperationalContextRevision(
                proposal.Promotion.ArchivedRelativePath,
                proposal.Promotion.ArchivedRelativePath,
                "Archived Previous Operational Context"));
        }

        return references;
    }

    private static IReadOnlyList<ReasoningReference> ExecutionHandoffDecisionReferences(
        ExecutionSession session,
        string handoffPath,
        string handoffContent,
        string handoffFingerprint)
    {
        return
        [
            ReasoningReferenceFactory.Handoff(
                handoffPath,
                Excerpt(handoffContent),
                handoffFingerprint,
                "Current Handoff"),
            ReasoningReferenceFactory.ExecutionOutput(
                session.Id.ToString("D"),
                $"Milestone: {session.MilestonePath}",
                Fingerprint(new
                {
                    session.Id,
                    session.MilestonePath,
                    session.StartedAt,
                    session.CompletedAt,
                    session.AcceptedAt,
                    session.RejectedAt,
                    session.DecisionNote
                }),
                "Execution Session")
        ];
    }

    private static ReasoningProvenance Provenance(
        DecisionResolvedProposalSnapshot proposal,
        string rationale,
        string resolver,
        string transitionFingerprint)
    {
        return new ReasoningProvenance(
            "InferredProposalResolution",
            resolver,
            ReasoningReferenceFactory.ProposalPath(proposal.ProposalId),
            "History: Resolved",
            rationale,
            transitionFingerprint);
    }

    private static ReasoningProvenance ExecutionHandoffDecisionProvenance(
        ExecutionSession session,
        string sourceAction,
        string handoffPath,
        string transitionFingerprint)
    {
        return new ReasoningProvenance(
            sourceAction == "accepted"
                ? "InferredExecutionHandoffAcceptance"
                : "InferredExecutionHandoffRejection",
            "execution-session-service",
            handoffPath,
            $"Execution output {sourceAction}",
            string.IsNullOrWhiteSpace(session.DecisionNote)
                ? $"Execution output for milestone {session.MilestonePath} was {sourceAction}."
                : session.DecisionNote.Trim(),
            transitionFingerprint);
    }

    private static ReasoningProvenance DecisionProvenance(
        Decision decision,
        string sourceKind,
        string section,
        string rationale,
        string resolver,
        string transitionFingerprint)
    {
        return new ReasoningProvenance(
            sourceKind,
            resolver,
            ReasoningReferenceFactory.DecisionPath(decision.Id.Value),
            section,
            rationale,
            transitionFingerprint);
    }

    private static ReasoningProvenance GovernanceProvenance(
        DecisionGovernanceReport report,
        DecisionGovernanceFinding finding,
        string transitionFingerprint)
    {
        return new ReasoningProvenance(
            "InferredGovernanceContradiction",
            "decision-governance-service",
            ReasoningReferenceFactory.GovernanceReportPath(report.Id),
            $"Finding: {finding.Id}",
            finding.Detail,
            transitionFingerprint);
    }

    private static ReasoningProvenance OperationalContextPromotionProvenance(
        OperationalContextProposal proposal,
        OperationalContextSemanticChange change,
        string transitionFingerprint)
    {
        return new ReasoningProvenance(
            "InferredOperationalContextPromotion",
            "operational-context-lifecycle-service",
            ReasoningReferenceFactory.OperationalContextProposalPath(proposal.ProposalId),
            $"Promotion semantic change: {change.Type}",
            string.IsNullOrWhiteSpace(proposal.Review.ReviewNote)
                ? change.Description
                : $"{change.Description} Review note: {proposal.Review.ReviewNote}",
            transitionFingerprint);
    }

    private static bool IsContradictionFinding(DecisionGovernanceFinding finding)
    {
        return finding.Category is
            DecisionGovernanceCategory.Consistency or
            DecisionGovernanceCategory.SupersessionLineage or
            DecisionGovernanceCategory.AuthorityBoundary or
            DecisionGovernanceCategory.ExecutionProjectionReadiness or
            DecisionGovernanceCategory.FingerprintIntegrity;
    }

    private static bool TryClassifyOperationalContextPromotionChange(
        OperationalContextSemanticChange change,
        out ReasoningEventFamily family,
        out ReasoningEventType type,
        out string tag)
    {
        switch (change.Type)
        {
            case OperationalContextSemanticChangeType.ConstraintAdded:
                family = ReasoningEventFamily.ConstraintEvolution;
                type = ReasoningEventType.ConstraintIntroduced;
                tag = "constraint-evolution";
                return true;
            case OperationalContextSemanticChangeType.ConstraintRemoved:
                family = ReasoningEventFamily.ConstraintEvolution;
                type = ReasoningEventType.ConstraintRetired;
                tag = "constraint-evolution";
                return true;
            case OperationalContextSemanticChangeType.ItemChanged
                when change.Section.Contains("constraint", StringComparison.OrdinalIgnoreCase):
            case OperationalContextSemanticChangeType.SectionChanged
                when change.Section.Contains("constraint", StringComparison.OrdinalIgnoreCase):
                family = ReasoningEventFamily.ConstraintEvolution;
                type = ReasoningEventType.ConstraintModified;
                tag = "constraint-evolution";
                return true;
            case OperationalContextSemanticChangeType.ImportantDecisionIntroduced:
            case OperationalContextSemanticChangeType.DecisionRetired:
            case OperationalContextSemanticChangeType.RationaleChanged:
            case OperationalContextSemanticChangeType.RationaleLostWarning:
            case OperationalContextSemanticChangeType.OpenDecisionResolved:
                family = ReasoningEventFamily.DecisionEvolution;
                type = ReasoningEventType.EvidenceAdded;
                tag = "decision-evolution";
                return true;
            default:
                family = default;
                type = default;
                tag = string.Empty;
                return false;
        }
    }

    private static bool TryClassifyExecutionHandoffSignal(
        string semanticText,
        bool accepted,
        out ReasoningEventFamily family,
        out ReasoningEventType type,
        out string tag,
        out string signal)
    {
        string text = semanticText.ToLowerInvariant();
        if (ContainsAny(text, "direction", "strategy", "strategic", "pivot", "shifted direction", "direction shifted"))
        {
            family = ReasoningEventFamily.Direction;
            type = accepted ? ReasoningEventType.DirectionShifted : ReasoningEventType.DirectionAbandoned;
            tag = "direction";
            signal = accepted ? "direction shifted" : "direction abandoned";
            return true;
        }

        if (ContainsAny(text, "assumption", "assume", "assumed"))
        {
            family = ReasoningEventFamily.AssumptionEvolution;
            type = ContainsAny(text, "invalid", "failed", "false", "wrong")
                ? ReasoningEventType.AssumptionInvalidated
                : ReasoningEventType.AssumptionReplaced;
            tag = "assumption-evolution";
            signal = type == ReasoningEventType.AssumptionInvalidated
                ? "assumption invalidated"
                : "assumption replaced";
            return true;
        }

        if (ContainsAny(text, "constraint", "required", "requirement", "must", "cannot"))
        {
            family = ReasoningEventFamily.ConstraintEvolution;
            type = ReasoningEventType.ConstraintModified;
            tag = "constraint-evolution";
            signal = "constraint modified";
            return true;
        }

        if (ContainsAny(text, "contradiction", "conflict", "conflicts", "inconsistent"))
        {
            family = ReasoningEventFamily.Contradiction;
            type = ContainsAny(text, "resolved", "fixed")
                ? ReasoningEventType.ContradictionResolved
                : ReasoningEventType.ContradictionIdentified;
            tag = "contradiction";
            signal = type == ReasoningEventType.ContradictionResolved
                ? "contradiction resolved"
                : "contradiction identified";
            return true;
        }

        if (ContainsAny(text, "decision", "superseded", "reframed", "reconsidered"))
        {
            family = ReasoningEventFamily.DecisionEvolution;
            type = ReasoningEventType.EvidenceAdded;
            tag = "decision-evolution";
            signal = "decision evidence added";
            return true;
        }

        if (ContainsAny(text, "evidence", "proved", "proven", "finding", "findings"))
        {
            family = ReasoningEventFamily.Evidence;
            type = ReasoningEventType.EvidenceAdded;
            tag = "evidence";
            signal = "evidence added";
            return true;
        }

        family = default;
        type = default;
        tag = string.Empty;
        signal = string.Empty;
        return false;
    }

    private static string OperationalContextPromotionEventTitle(
        OperationalContextProposal proposal,
        OperationalContextSemanticChange change)
    {
        return $"Operational context promotion {proposal.ProposalId} changed {change.Section}";
    }

    private static string OperationalContextPromotionSummary(
        OperationalContextProposal proposal,
        OperationalContextSemanticChange change)
    {
        return $"Promotion {proposal.ProposalId} changed project understanding: {change.Description}";
    }

    private static string ExecutionHandoffDecisionEventTitle(
        ExecutionSession session,
        string signal)
    {
        return $"Execution output {signal} for {Path.GetFileNameWithoutExtension(session.MilestonePath)}";
    }

    private static string ExecutionHandoffDecisionSummary(
        ExecutionSession session,
        string signal)
    {
        return $"Execution output for milestone {session.MilestonePath} showed that {signal}.";
    }

    private static string? NormalizeRelativePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        return relativePath.Replace('\\', '/').Trim();
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.Ordinal));
    }

    private static string Excerpt(string semanticText, int maxLength = 240)
    {
        string normalized = string.Join(
            " ",
            semanticText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= maxLength ? normalized : $"{normalized[..maxLength]}...";
    }

    private static string Fingerprint<T>(T value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string RequireText(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message);
        }

        return value.Trim();
    }
}
