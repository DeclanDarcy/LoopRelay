using System.Text;
using CommandCenter.Continuity.Abstractions;
using CommandCenter.Continuity.Models;
using CommandCenter.Continuity.Primitives;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Continuity.Services;

public sealed class ContinuityDiagnosticsService(
    IRepositoryService repositoryService,
    IArtifactService artifactService,
    IArtifactStore artifactStore,
    IOperationalContextParser parser,
    IUnderstandingDiffService diffService,
    IOperationalContextProposalStore proposalStore) : IContinuityDiagnosticsService
{
    public async Task<ContinuityDiagnostics> GetDiagnosticsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        IReadOnlyList<RevisionEntry> revisions = await LoadRevisionEntriesAsync(repository);
        var ledger = new UnderstandingEvolutionLedger
        {
            Revisions = revisions
                .Select(entry => entry.Snapshot)
                .ToArray()
        };
        UnderstandingRevisionSnapshot? first = ledger.Revisions.FirstOrDefault();
        UnderstandingRevisionSnapshot? current = ledger.CurrentRevision;
        IReadOnlyList<OperationalContextProposal> proposals = await proposalStore.ListAsync(repository);
        var warnings = new List<string>();
        warnings.AddRange(proposals.SelectMany(proposal => proposal.CompressionSummary.Warnings));
        warnings.AddRange(proposals.SelectMany(proposal => proposal.CompressionSummary.StableUnderstandingRetentionWarnings));
        OperationalEvolutionSummary operationalEvolution = BuildOperationalEvolution(revisions);
        IReadOnlyList<ContinuityDiagnosticGroup> diagnosticGroups = BuildDiagnosticGroups(operationalEvolution, proposals);

        return new ContinuityDiagnostics
        {
            RepositoryId = repository.Id,
            GeneratedAt = DateTimeOffset.UtcNow,
            RevisionCount = ledger.Revisions.Count,
            CurrentContextByteCount = current?.ByteCount ?? 0,
            CurrentContextCharacterCount = current?.CharacterCount ?? 0,
            ContextByteGrowth = current is null || first is null ? 0 : current.ByteCount - first.ByteCount,
            AverageBytesPerRevision = ledger.Revisions.Count == 0 ? 0 : ledger.Revisions.Average(revision => revision.ByteCount),
            RevisionFrequency = CalculateRevisionFrequency(ledger.Revisions),
            EvolutionLedger = ledger,
            OperationalEvolution = operationalEvolution,
            ArchitectureTrend = BuildTrend(operationalEvolution.SemanticChanges, "Architecture"),
            ConstraintTrend = BuildTrend(operationalEvolution.SemanticChanges, "Constraints"),
            DecisionTrend = BuildTrend(operationalEvolution.SemanticChanges, "Stable Decisions"),
            RationaleTrend = BuildTrend(operationalEvolution.SemanticChanges, "Decision Rationale"),
            OpenQuestionTrend = BuildActiveTrend(
                operationalEvolution.SemanticChanges,
                revisions,
                "Open Questions",
                document => document.OpenQuestions,
                "resolved question"),
            ActiveRiskTrend = BuildActiveTrend(
                operationalEvolution.SemanticChanges,
                revisions,
                "Active Risks",
                document => document.ActiveRisks,
                "retired risk"),
            CompressionTrend = BuildCompressionTrend(proposals),
            RepeatedInvestigationIndicators = FindRepeatedIndicators(
                revisions,
                document => document.RecentUnderstandingChanges,
                ["investigat"]),
            RepeatedQuestionIndicators = FindRepeatedQuestions(revisions),
            DecisionReworkIndicators = FindRepeatedIndicators(
                revisions,
                document => document.StableDecisions.Concat(document.RecentUnderstandingChanges),
                ["rework", "revisit", "replace decision", "changed decision"]),
            ContinuityWarnings = warnings
                .Where(warning => !string.IsNullOrWhiteSpace(warning))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            DiagnosticGroups = diagnosticGroups
        };
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync()).FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<IReadOnlyList<RevisionEntry>> LoadRevisionEntriesAsync(Repository repository)
    {
        var artifacts = (await artifactService.DiscoverAsync(repository))
            .Where(artifact => artifact.Family == ArtifactFamily.OperationalContext)
            .Select(artifact => new
            {
                Artifact = artifact,
                RevisionNumber = artifact.VersionKind == ArtifactVersionKind.Current
                    ? int.MaxValue
                    : TryParseHistoricalRevisionNumber(artifact.Name)
            })
            .Where(entry => entry.RevisionNumber > 0)
            .OrderBy(entry => entry.RevisionNumber)
            .ToArray();

        int highestHistoricalRevision = artifacts
            .Where(entry => entry.Artifact.VersionKind == ArtifactVersionKind.Historical)
            .Select(entry => entry.RevisionNumber)
            .DefaultIfEmpty(0)
            .Max();
        var entries = new List<RevisionEntry>();
        foreach (var entry in artifacts)
        {
            string content = await artifactStore.ReadAsync(ArtifactPath.ResolveRepositoryPath(repository, entry.Artifact.RelativePath)) ?? string.Empty;
            int bytes = Encoding.UTF8.GetByteCount(content);
            int revisionNumber = entry.Artifact.VersionKind == ArtifactVersionKind.Current
                ? highestHistoricalRevision + 1
                : entry.RevisionNumber;
            OperationalContextDocument document = parser.Parse(content);
            entries.Add(new RevisionEntry(
                document,
                new UnderstandingRevisionSnapshot
                {
                    RevisionNumber = revisionNumber,
                    RelativePath = entry.Artifact.RelativePath,
                    LastUpdatedAt = GetLastWriteTime(ArtifactPath.ResolveRepositoryPath(repository, entry.Artifact.RelativePath)),
                    ByteCount = bytes,
                    CharacterCount = content.Length,
                    ArchitectureItemCount = document.Architecture.Count,
                    ConstraintCount = document.Constraints.Count,
                    StableDecisionCount = document.StableDecisions.Count,
                    DecisionRationaleCount = document.DecisionRationale.Count,
                    OpenQuestionCount = document.OpenQuestions.Count,
                    ActiveRiskCount = document.ActiveRisks.Count
                }));
        }

        return entries;
    }

    private OperationalEvolutionSummary BuildOperationalEvolution(IReadOnlyList<RevisionEntry> revisions)
    {
        if (revisions.Count < 2)
        {
            return new OperationalEvolutionSummary
            {
                DiagnosticGroups =
                [
                    new ContinuityDiagnosticGroup
                    {
                        Category = "evolution",
                        Title = "Operational evolution",
                        Diagnostics = ["At least two operational-context revisions are required to compare evolution."]
                    }
                ]
            };
        }

        IReadOnlyList<OperationalContextSemanticChange> changes = diffService.Compare(revisions[^2].Document, revisions[^1].Document);
        int resolvedCount = CountResolved(changes) + CountResolutionEvidence(revisions, "resolved question") + CountResolutionEvidence(revisions, "retired risk");
        int removedCount = changes.Count(IsRemovedChange);
        IReadOnlyList<OperationalEvolutionTimelineEntry> timelineEntries = BuildTimelineEntries(revisions[^2], revisions[^1], changes);
        OperationalEvolutionSummary summary = new()
        {
            AddedCount = changes.Count(IsAddedChange),
            ModifiedCount = changes.Count(IsModifiedChange),
            RemovedCount = removedCount,
            PreservedCount = CountPreservedItems(revisions[^2].Document, revisions[^1].Document),
            LostCount = Math.Max(0, removedCount - resolvedCount),
            ResolvedCount = resolvedCount,
            SemanticChanges = changes,
            TimelineEntries = timelineEntries,
            DiagnosticGroups = BuildOperationalEvolutionDiagnosticGroups(changes, removedCount, resolvedCount)
        };
        return summary;
    }

    private static ContinuityTrend BuildTrend(
        IReadOnlyList<OperationalContextSemanticChange> changes,
        string section)
    {
        OperationalContextSemanticChange[] sectionChanges = changes
            .Where(change => string.Equals(change.Section, section, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        int removedCount = sectionChanges.Count(IsRemovedChange);
        return new ContinuityTrend
        {
            AddedCount = sectionChanges.Count(IsAddedChange),
            ModifiedCount = sectionChanges.Count(IsModifiedChange),
            RemovedCount = removedCount,
            LostCount = removedCount
        };
    }

    private static ContinuityTrend BuildActiveTrend(
        IReadOnlyList<OperationalContextSemanticChange> changes,
        IReadOnlyList<RevisionEntry> revisions,
        string section,
        Func<OperationalContextDocument, IEnumerable<OperationalContextItem>> getItems,
        string resolutionPrefix)
    {
        ContinuityTrend trend = BuildTrend(changes, section);
        if (revisions.Count < 2)
        {
            return trend;
        }

        HashSet<string> previous = ToNormalizedSet(getItems(revisions[^2].Document));
        HashSet<string> current = ToNormalizedSet(getItems(revisions[^1].Document));
        string[] removed = previous.Except(current, StringComparer.OrdinalIgnoreCase).ToArray();
        HashSet<string> resolutionEvidence = ToNormalizedSet(revisions[^1].Document.RecentUnderstandingChanges
            .Where(item => item.Text.StartsWith(resolutionPrefix, StringComparison.OrdinalIgnoreCase)));
        int resolved = removed.Count(item => resolutionEvidence.Any(evidence => evidence.Contains(item, StringComparison.OrdinalIgnoreCase)));
        return new ContinuityTrend
        {
            AddedCount = trend.AddedCount,
            ModifiedCount = trend.ModifiedCount,
            RemovedCount = trend.RemovedCount,
            ResolvedCount = resolved,
            LostCount = Math.Max(0, trend.RemovedCount - resolved)
        };
    }

    private static CompressionTrend BuildCompressionTrend(IReadOnlyList<OperationalContextProposal> proposals)
    {
        string[] warnings = proposals.SelectMany(proposal => proposal.CompressionSummary.Warnings)
            .Concat(proposals.SelectMany(proposal => proposal.CompressionSummary.StableUnderstandingRetentionWarnings))
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new CompressionTrend
        {
            ProposalCount = proposals.Count,
            CompressedItemCount = proposals.Sum(proposal => proposal.CompressionSummary.CompressedItemCount),
            RemovedItemCount = proposals.Sum(proposal => proposal.CompressionSummary.RemovedItemCount),
            ResolvedQuestionCount = proposals.Sum(proposal => proposal.CompressionSummary.ResolvedQuestionCount),
            RetiredRiskCount = proposals.Sum(proposal => proposal.CompressionSummary.RetiredRiskCount),
            WarningCount = warnings.Length,
            Warnings = warnings,
            NoiseRemovedIndicators = proposals
                .SelectMany(proposal => proposal.CompressionSummary.NoiseRemovedIndicators)
                .Where(indicator => !string.IsNullOrWhiteSpace(indicator))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    private static IReadOnlyList<string> FindRepeatedQuestions(IReadOnlyList<RevisionEntry> revisions)
    {
        return revisions
            .SelectMany(revision => revision.Document.OpenQuestions)
            .Select(item => item.Text)
            .GroupBy(Normalize, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.First())
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> FindRepeatedIndicators(
        IReadOnlyList<RevisionEntry> revisions,
        Func<OperationalContextDocument, IEnumerable<OperationalContextItem>> getItems,
        IReadOnlyList<string> markers)
    {
        return revisions
            .SelectMany(revision => getItems(revision.Document))
            .Select(item => item.Text)
            .Where(text => markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(Normalize, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.First())
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static HashSet<string> ToNormalizedSet(IEnumerable<OperationalContextItem> items)
    {
        return items
            .Select(item => Normalize(item.Text))
            .Where(text => text.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        string normalized = value.Trim().ToLowerInvariant();
        foreach (string prefix in new[] { "resolved question:", "retired risk:", "decision:", "rationale for" })
        {
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                normalized = normalized[prefix.Length..].Trim();
            }
        }

        return string.Join(' ', normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static IReadOnlyList<ContinuityDiagnosticGroup> BuildDiagnosticGroups(
        OperationalEvolutionSummary operationalEvolution,
        IReadOnlyList<OperationalContextProposal> proposals)
    {
        var groups = new List<ContinuityDiagnosticGroup>();
        groups.Add(BuildAssimilationDiagnosticGroup(proposals));
        groups.Add(BuildClassificationDiagnosticGroup(proposals));
        groups.Add(BuildContradictionDiagnosticGroup(proposals));
        groups.AddRange(operationalEvolution.DiagnosticGroups);
        groups.Add(BuildLostUnderstandingDiagnosticGroup(operationalEvolution.SemanticChanges, operationalEvolution.LostCount));
        groups.Add(BuildResolvedUnderstandingDiagnosticGroup(operationalEvolution.SemanticChanges, operationalEvolution.ResolvedCount));

        string[] compressionDiagnostics =
        [
            $"Proposal count: {proposals.Count}.",
            $"Compressed item count: {proposals.Sum(proposal => proposal.CompressionSummary.CompressedItemCount)}.",
            $"Removed item count: {proposals.Sum(proposal => proposal.CompressionSummary.RemovedItemCount)}.",
            $"Noise removed indicator count: {proposals.Sum(proposal => proposal.CompressionSummary.NoiseRemovedIndicators.Count)}."
        ];
        groups.Add(new ContinuityDiagnosticGroup
        {
            Category = "compression",
            Title = "Compression diagnostics",
            Diagnostics = compressionDiagnostics
        });
        groups.Add(BuildRecoveryDiagnosticGroup(proposals));

        return groups;
    }

    private static ContinuityDiagnosticGroup BuildAssimilationDiagnosticGroup(IReadOnlyList<OperationalContextProposal> proposals)
    {
        int totalAnalyzed = proposals.Sum(proposal => proposal.DecisionAssimilation.Limit.TotalAnalyzedItemCount);
        int totalQualifying = proposals.Sum(proposal => proposal.DecisionAssimilation.Limit.TotalQualifyingItemCount);
        int totalAssimilated = proposals.Sum(proposal => proposal.DecisionAssimilation.Limit.AssimilatedItemCount);
        int totalOmitted = proposals.Sum(proposal => proposal.DecisionAssimilation.Limit.OmittedItemCount);
        var diagnostics = new List<string>
        {
            $"Proposal count: {proposals.Count}.",
            $"Analyzed decision count: {totalAnalyzed}.",
            $"Qualifying decision count: {totalQualifying}.",
            $"Assimilated decision count: {totalAssimilated}.",
            $"Omitted decision count: {totalOmitted}."
        };
        diagnostics.AddRange(proposals
            .SelectMany(proposal => proposal.DecisionAssimilation.Decisions)
            .Select(record =>
            {
                string reason = record.IsOmittedByLimit
                    ? record.OmissionReason ?? "omitted by limit"
                    : record.ExclusionReason ?? record.Rationale ?? "no exclusion or omission reason recorded";
                return $"{record.DecisionId}: {record.Status}; assimilated={record.IsAssimilated}; durable={record.IsDurable}; reason={reason}.";
            }));

        return new ContinuityDiagnosticGroup
        {
            Category = "assimilation",
            Title = "Decision assimilation",
            Diagnostics = diagnostics
        };
    }

    private static ContinuityDiagnosticGroup BuildClassificationDiagnosticGroup(IReadOnlyList<OperationalContextProposal> proposals)
    {
        DecisionAssimilationRecord[] records = proposals
            .SelectMany(proposal => proposal.DecisionAssimilation.Decisions)
            .ToArray();
        string[] diagnostics = records.Length == 0
            ? ["No decision taxonomy classifications recorded."]
            : records.Select(record =>
                $"{record.DecisionId}: taxonomy={record.TaxonomyBasis.Taxonomy}; rules={record.TaxonomyBasis.MatchedRules.Count}; evidence={record.TaxonomyBasis.MatchedEvidence.Count}; heuristic={record.TaxonomyBasis.IsHeuristicFallback}; diagnostics={record.TaxonomyBasis.Diagnostics.Count}.")
                .ToArray();

        return new ContinuityDiagnosticGroup
        {
            Category = "classification",
            Title = "Taxonomy classification",
            Diagnostics = diagnostics
        };
    }

    private static ContinuityDiagnosticGroup BuildContradictionDiagnosticGroup(IReadOnlyList<OperationalContextProposal> proposals)
    {
        ContinuityDecisionContradiction[] contradictions = proposals
            .SelectMany(proposal => proposal.DecisionAssimilation.Contradictions)
            .ToArray();
        string[] diagnostics = contradictions.Length == 0
            ? ["No decision contradictions recorded."]
            : contradictions.Select(contradiction =>
                $"{contradiction.ContradictionId}: {contradiction.ConflictType}; severity={contradiction.Severity}; evidence={contradiction.ConflictEvidence.Count}; guidance={contradiction.ResolutionGuidance}.")
                .ToArray();

        return new ContinuityDiagnosticGroup
        {
            Category = "contradictions",
            Title = "Decision contradictions",
            Diagnostics = diagnostics
        };
    }

    private static ContinuityDiagnosticGroup BuildRecoveryDiagnosticGroup(IReadOnlyList<OperationalContextProposal> proposals)
    {
        string[] diagnostics = proposals
            .Where(proposal =>
                proposal.Status is OperationalContextProposalStatus.Rejected or OperationalContextProposalStatus.Superseded ||
                !string.IsNullOrWhiteSpace(proposal.Review.StaleReason) ||
                !string.IsNullOrWhiteSpace(proposal.Promotion.ArchiveFailureReason) ||
                !string.IsNullOrWhiteSpace(proposal.Promotion.WriteFailureReason))
            .Select(proposal =>
            {
                string?[] reasons =
                [
                    proposal.Review.StaleReason,
                    proposal.Promotion.ArchiveFailureReason,
                    proposal.Promotion.WriteFailureReason
                ];
                string reason = reasons.FirstOrDefault(reason => !string.IsNullOrWhiteSpace(reason)) ?? "proposal lifecycle status requires attention";
                return $"{proposal.ProposalId}: status={proposal.Status}; reason={reason}.";
            })
            .ToArray();

        return new ContinuityDiagnosticGroup
        {
            Category = "recovery",
            Title = "Continuity recovery",
            Diagnostics = diagnostics.Length == 0 ? ["No continuity recovery issues recorded."] : diagnostics
        };
    }

    private static ContinuityDiagnosticGroup BuildLostUnderstandingDiagnosticGroup(
        IReadOnlyList<OperationalContextSemanticChange> changes,
        int lostCount)
    {
        string[] diagnostics = changes
            .Where(change => change.Type is OperationalContextSemanticChangeType.LostUnderstanding or
                OperationalContextSemanticChangeType.RationaleLostWarning)
            .Select(change => $"{change.Type} in {change.Section}: {change.Description}")
            .ToArray();

        return new ContinuityDiagnosticGroup
        {
            Category = "lost understanding",
            Title = "Lost understanding",
            Diagnostics = diagnostics.Length == 0 ? [$"Lost item count: {lostCount}."] : diagnostics
        };
    }

    private static ContinuityDiagnosticGroup BuildResolvedUnderstandingDiagnosticGroup(
        IReadOnlyList<OperationalContextSemanticChange> changes,
        int resolvedCount)
    {
        string[] diagnostics = changes
            .Where(change => change.Type is OperationalContextSemanticChangeType.ResolvedUnderstanding or
                OperationalContextSemanticChangeType.OpenDecisionResolved)
            .Select(change => $"{change.Type} in {change.Section}: {change.Description}")
            .ToArray();

        return new ContinuityDiagnosticGroup
        {
            Category = "resolved understanding",
            Title = "Resolved understanding",
            Diagnostics = diagnostics.Length == 0 ? [$"Resolved item count: {resolvedCount}."] : diagnostics
        };
    }

    private static IReadOnlyList<ContinuityDiagnosticGroup> BuildOperationalEvolutionDiagnosticGroups(
        IReadOnlyList<OperationalContextSemanticChange> changes,
        int removedCount,
        int resolvedCount)
    {
        var groups = new List<ContinuityDiagnosticGroup>
        {
            new()
            {
                Category = "evolution",
                Title = "Operational evolution",
                Diagnostics =
                [
                    $"Added item count: {changes.Count(IsAddedChange)}.",
                    $"Modified item count: {changes.Count(IsModifiedChange)}.",
                    $"Removed item count: {removedCount}.",
                    $"Resolved item count: {resolvedCount}."
                ]
            },
            new()
            {
                Category = "diff",
                Title = "Semantic diff",
                Diagnostics = changes.Count == 0
                    ? ["No semantic changes detected between the latest operational-context revisions."]
                    : changes.Select(change => $"{change.Type} in {change.Section}: {change.Description}").ToArray()
            }
        };

        foreach (OperationalContextSemanticChange change in changes.Where(IsModifiedChange))
        {
            var diagnostics = new List<string>
            {
                $"Section: {change.Section}.",
                $"Item id: {change.ItemId}.",
                $"Identity basis: {change.IdentityBasis}.",
                $"Previous state: {change.PreviousState}.",
                $"Current state: {change.CurrentState}.",
                $"Modification reason: {change.ModificationReason}."
            };
            diagnostics.AddRange(change.SupportingEvidence.Select(evidence => $"Supporting evidence: {evidence}"));
            groups.Add(new ContinuityDiagnosticGroup
            {
                Category = "evolution",
                Title = "Modified operational-context item",
                Diagnostics = diagnostics
            });
        }

        return groups;
    }

    private static int CountResolutionEvidence(IReadOnlyList<RevisionEntry> revisions, string resolutionPrefix)
    {
        if (revisions.Count < 2)
        {
            return 0;
        }

        return revisions[^1].Document.RecentUnderstandingChanges.Count(item =>
            item.Text.StartsWith(resolutionPrefix, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountResolved(IReadOnlyList<OperationalContextSemanticChange> changes)
    {
        return changes.Count(change => change.Type == Primitives.OperationalContextSemanticChangeType.OpenDecisionResolved);
    }

    private static int CountPreservedItems(OperationalContextDocument previous, OperationalContextDocument current)
    {
        HashSet<string> previousItems = ToNormalizedSet(EnumerateKnownItems(previous));
        HashSet<string> currentItems = ToNormalizedSet(EnumerateKnownItems(current));
        return previousItems.Intersect(currentItems, StringComparer.OrdinalIgnoreCase).Count();
    }

    private static IReadOnlyList<OperationalEvolutionTimelineEntry> BuildTimelineEntries(
        RevisionEntry previous,
        RevisionEntry current,
        IReadOnlyList<OperationalContextSemanticChange> changes)
    {
        List<KnownItemEntry> previousItems = EnumerateKnownItemEntries(previous.Document).ToList();
        List<KnownItemEntry> currentItems = EnumerateKnownItemEntries(current.Document).ToList();
        var entries = new List<OperationalEvolutionTimelineEntry>();

        foreach (OperationalContextSemanticChange change in changes)
        {
            string outcome = OutcomeFor(change, current.Document);
            KnownItemEntry? previousItem = FindItem(previousItems, change.ItemId, change.PreviousState);
            KnownItemEntry? currentItem = FindItem(currentItems, change.ItemId, change.CurrentState);
            string? previousState = change.PreviousState ?? previousItem?.Item.Text ?? RemovedDescriptionState(change);
            string? currentState = change.CurrentState ?? currentItem?.Item.Text ?? AddedDescriptionState(change);
            string? resolutionEvidence = outcome == "Resolved"
                ? FindResolutionEvidence(current.Document, previousState)
                : null;

            entries.Add(new OperationalEvolutionTimelineEntry
            {
                Outcome = outcome,
                SemanticEventType = change.Type.ToString(),
                Section = change.Section,
                Description = change.Description,
                ItemId = change.ItemId,
                PreviousState = previousState,
                CurrentState = currentState ?? resolutionEvidence,
                Reason = TimelineReasonFor(change, outcome, resolutionEvidence),
                IdentityBasis = change.IdentityBasis,
                PreviousRevisionNumber = previous.Snapshot.RevisionNumber,
                CurrentRevisionNumber = current.Snapshot.RevisionNumber,
                SupportingEvidence = TimelineEvidenceFor(change, previous.Snapshot, current.Snapshot, resolutionEvidence)
            });
        }

        HashSet<string> changedPreviousStates = changes
            .Select(change => change.PreviousState ?? RemovedDescriptionState(change))
            .Where(state => !string.IsNullOrWhiteSpace(state))
            .Select(state => Normalize(state!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> changedCurrentStates = changes
            .Select(change => change.CurrentState ?? AddedDescriptionState(change))
            .Where(state => !string.IsNullOrWhiteSpace(state))
            .Select(state => Normalize(state!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (KnownItemEntry previousItem in previousItems.OrderBy(item => item.Section, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Item.Text, StringComparer.OrdinalIgnoreCase))
        {
            string normalized = Normalize(previousItem.Item.Text);
            if (changedPreviousStates.Contains(normalized) || changedCurrentStates.Contains(normalized))
            {
                continue;
            }

            KnownItemEntry? currentItem = currentItems.FirstOrDefault(item =>
                string.Equals(Normalize(item.Item.Text), normalized, StringComparison.OrdinalIgnoreCase));
            if (currentItem is null)
            {
                continue;
            }

            entries.Add(new OperationalEvolutionTimelineEntry
            {
                Outcome = "Preserved",
                SemanticEventType = "StableUnderstandingPreserved",
                Section = previousItem.Section,
                Description = $"Item preserved in {previousItem.Section}: {previousItem.Item.Text}",
                ItemId = string.IsNullOrWhiteSpace(currentItem.Item.Id) ? previousItem.Item.Id : currentItem.Item.Id,
                PreviousState = previousItem.Item.Text,
                CurrentState = currentItem.Item.Text,
                Reason = "The normalized operational-context item is present in both compared revisions.",
                IdentityBasis = "normalized-state",
                PreviousRevisionNumber = previous.Snapshot.RevisionNumber,
                CurrentRevisionNumber = current.Snapshot.RevisionNumber,
                SupportingEvidence =
                [
                    $"Previous revision: {previous.Snapshot.RevisionNumber} ({previous.Snapshot.RelativePath})",
                    $"Current revision: {current.Snapshot.RevisionNumber} ({current.Snapshot.RelativePath})",
                    $"Section: {previousItem.Section}"
                ]
            });
        }

        return entries
            .OrderBy(entry => OutcomeOrder(entry.Outcome))
            .ThenBy(entry => entry.Section, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Description, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<OperationalContextItem> EnumerateKnownItems(OperationalContextDocument document)
    {
        return document.CurrentMentalModel
            .Concat(document.Architecture)
            .Concat(document.AuthorityBoundaries)
            .Concat(document.Constraints)
            .Concat(document.StableDecisions)
            .Concat(document.DecisionRationale)
            .Concat(document.OpenQuestions)
            .Concat(document.ActiveRisks)
            .Concat(document.RecentUnderstandingChanges);
    }

    private static IEnumerable<KnownItemEntry> EnumerateKnownItemEntries(OperationalContextDocument document)
    {
        foreach (OperationalContextItem item in document.CurrentMentalModel)
        {
            yield return new KnownItemEntry("Current Mental Model", item);
        }

        foreach (OperationalContextItem item in document.Architecture)
        {
            yield return new KnownItemEntry("Architecture", item);
        }

        foreach (OperationalContextItem item in document.AuthorityBoundaries)
        {
            yield return new KnownItemEntry("Authority Boundaries", item);
        }

        foreach (OperationalContextItem item in document.Constraints)
        {
            yield return new KnownItemEntry("Constraints", item);
        }

        foreach (OperationalContextItem item in document.StableDecisions)
        {
            yield return new KnownItemEntry("Stable Decisions", item);
        }

        foreach (OperationalContextItem item in document.DecisionRationale)
        {
            yield return new KnownItemEntry("Decision Rationale", item);
        }

        foreach (OperationalContextItem item in document.OpenQuestions)
        {
            yield return new KnownItemEntry("Open Questions", item);
        }

        foreach (OperationalContextItem item in document.ActiveRisks)
        {
            yield return new KnownItemEntry("Active Risks", item);
        }

        foreach (OperationalContextItem item in document.RecentUnderstandingChanges)
        {
            yield return new KnownItemEntry("Recent Understanding Changes", item);
        }
    }

    private static KnownItemEntry? FindItem(
        IReadOnlyList<KnownItemEntry> items,
        string? itemId,
        string? state)
    {
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            KnownItemEntry? byId = items.FirstOrDefault(entry =>
                string.Equals(entry.Item.Id, itemId, StringComparison.OrdinalIgnoreCase));
            if (byId is not null)
            {
                return byId;
            }
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        string normalized = Normalize(state);
        return items.FirstOrDefault(entry =>
            string.Equals(Normalize(entry.Item.Text), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string OutcomeFor(
        OperationalContextSemanticChange change,
        OperationalContextDocument current)
    {
        if (IsAddedChange(change))
        {
            return "Added";
        }

        if (IsModifiedChange(change))
        {
            return "Modified";
        }

        if (change.Type is OperationalContextSemanticChangeType.OpenDecisionResolved or
            OperationalContextSemanticChangeType.ResolvedUnderstanding)
        {
            return "Resolved";
        }

        if (change.Type is OperationalContextSemanticChangeType.RationaleLostWarning or
            OperationalContextSemanticChangeType.LostUnderstanding)
        {
            return "Lost";
        }

        if (IsRemovedChange(change))
        {
            string? removedState = change.PreviousState ?? RemovedDescriptionState(change);
            return FindResolutionEvidence(current, removedState) is null ? "Lost" : "Resolved";
        }

        return "Other";
    }

    private static int OutcomeOrder(string outcome)
    {
        return outcome switch
        {
            "Added" => 0,
            "Modified" => 1,
            "Removed" => 2,
            "Preserved" => 3,
            "Lost" => 4,
            "Resolved" => 5,
            _ => 6
        };
    }

    private static string TimelineReasonFor(
        OperationalContextSemanticChange change,
        string outcome,
        string? resolutionEvidence)
    {
        if (!string.IsNullOrWhiteSpace(change.ModificationReason))
        {
            return change.ModificationReason!;
        }

        if (outcome == "Resolved" && !string.IsNullOrWhiteSpace(resolutionEvidence))
        {
            return "The current operational context records resolution evidence for the removed item.";
        }

        return outcome switch
        {
            "Added" => "The item appears in the current revision and was not present in the previous revision.",
            "Lost" => "The item was removed from the current revision without matching resolution evidence.",
            "Removed" => "The item was removed from the current revision.",
            _ => $"The semantic diff classified this item as {change.Type}."
        };
    }

    private static IReadOnlyList<string> TimelineEvidenceFor(
        OperationalContextSemanticChange change,
        UnderstandingRevisionSnapshot previous,
        UnderstandingRevisionSnapshot current,
        string? resolutionEvidence)
    {
        var evidence = new List<string>
        {
            $"Previous revision: {previous.RevisionNumber} ({previous.RelativePath})",
            $"Current revision: {current.RevisionNumber} ({current.RelativePath})"
        };
        evidence.AddRange(change.SupportingEvidence);
        if (!string.IsNullOrWhiteSpace(resolutionEvidence))
        {
            evidence.Add($"Resolution evidence: {resolutionEvidence}");
        }

        return evidence
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? FindResolutionEvidence(OperationalContextDocument current, string? previousState)
    {
        if (string.IsNullOrWhiteSpace(previousState))
        {
            return null;
        }

        string normalizedPrevious = Normalize(previousState);
        return current.RecentUnderstandingChanges
            .Select(item => item.Text)
            .FirstOrDefault(text =>
            {
                string normalized = NormalizeRaw(text);
                return (normalized.StartsWith("resolved question:", StringComparison.OrdinalIgnoreCase) ||
                        normalized.StartsWith("retired risk:", StringComparison.OrdinalIgnoreCase)) &&
                    normalized.Contains(normalizedPrevious, StringComparison.OrdinalIgnoreCase);
            });
    }

    private static string NormalizeRaw(string value)
    {
        return string.Join(' ', value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? AddedDescriptionState(OperationalContextSemanticChange change)
    {
        const string marker = ": ";
        if (!IsAddedChange(change))
        {
            return null;
        }

        int index = change.Description.IndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? null : change.Description[(index + marker.Length)..].Trim();
    }

    private static string? RemovedDescriptionState(OperationalContextSemanticChange change)
    {
        const string marker = ": ";
        if (!IsRemovedChange(change))
        {
            return null;
        }

        int index = change.Description.IndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? null : change.Description[(index + marker.Length)..].Trim();
    }

    private static bool IsAddedChange(OperationalContextSemanticChange change)
    {
        return change.Type is
            Primitives.OperationalContextSemanticChangeType.ItemAdded or
            Primitives.OperationalContextSemanticChangeType.ConstraintAdded or
            Primitives.OperationalContextSemanticChangeType.QuestionAdded or
            Primitives.OperationalContextSemanticChangeType.RiskAdded or
            Primitives.OperationalContextSemanticChangeType.DecisionAdded or
            Primitives.OperationalContextSemanticChangeType.ImportantDecisionIntroduced;
    }

    private static bool IsModifiedChange(OperationalContextSemanticChange change)
    {
        return change.Type is
            Primitives.OperationalContextSemanticChangeType.ItemChanged or
            Primitives.OperationalContextSemanticChangeType.ModifiedArchitecture or
            Primitives.OperationalContextSemanticChangeType.ModifiedConstraint or
            Primitives.OperationalContextSemanticChangeType.ModifiedWorkflow or
            Primitives.OperationalContextSemanticChangeType.ModifiedDecision or
            Primitives.OperationalContextSemanticChangeType.ModifiedUnderstanding or
            Primitives.OperationalContextSemanticChangeType.SectionChanged;
    }

    private static bool IsRemovedChange(OperationalContextSemanticChange change)
    {
        return change.Type is
            Primitives.OperationalContextSemanticChangeType.ItemRemoved or
            Primitives.OperationalContextSemanticChangeType.ConstraintRemoved or
            Primitives.OperationalContextSemanticChangeType.QuestionRemoved or
            Primitives.OperationalContextSemanticChangeType.RiskRemoved or
            Primitives.OperationalContextSemanticChangeType.DecisionRemoved or
            Primitives.OperationalContextSemanticChangeType.DecisionRetired or
            Primitives.OperationalContextSemanticChangeType.RationaleLostWarning or
            Primitives.OperationalContextSemanticChangeType.OpenDecisionResolved or
            Primitives.OperationalContextSemanticChangeType.LostUnderstanding or
            Primitives.OperationalContextSemanticChangeType.ResolvedUnderstanding or
            Primitives.OperationalContextSemanticChangeType.DuplicateRemoved or
            Primitives.OperationalContextSemanticChangeType.TransientRemoved;
    }

    private static TimeSpan? CalculateRevisionFrequency(IReadOnlyList<UnderstandingRevisionSnapshot> revisions)
    {
        DateTimeOffset[] timestamps = revisions
            .Select(revision => revision.LastUpdatedAt)
            .Where(timestamp => timestamp.HasValue)
            .Select(timestamp => timestamp!.Value)
            .Order()
            .ToArray();
        if (timestamps.Length < 2)
        {
            return null;
        }

        return TimeSpan.FromTicks((long)timestamps.Zip(timestamps.Skip(1), (left, right) => right - left).Average(span => span.Ticks));
    }

    private static DateTimeOffset? GetLastWriteTime(string path)
    {
        return File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) : null;
    }

    private static int TryParseHistoricalRevisionNumber(string fileName)
    {
        const string prefix = "operational_context.";
        const string suffix = ".md";
        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(fileName[prefix.Length..^suffix.Length], out int revision) ? revision : 0;
    }

    private sealed record RevisionEntry(
        OperationalContextDocument Document,
        UnderstandingRevisionSnapshot Snapshot);

    private sealed record KnownItemEntry(
        string Section,
        OperationalContextItem Item);
}
