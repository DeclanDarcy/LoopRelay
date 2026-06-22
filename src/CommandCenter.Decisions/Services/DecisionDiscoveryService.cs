using System.Security.Cryptography;
using System.Text;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class DecisionDiscoveryService(
    IRepositoryService repositoryService,
    IDecisionContextService contextService,
    IDecisionRepository decisionRepository,
    IDecisionArtifactProjectionService projectionService) : IDecisionDiscoveryService
{
    private static readonly string[] ArchitecturalTerms =
    [
        "architecture",
        "architectural",
        "backend",
        "frontend",
        "api",
        "persistence",
        "repository",
        "storage",
        "schema",
        "projection"
    ];

    private static readonly string[] StrategicTerms = ["strategy", "strategic", "policy", "governance", "adoption"];
    private static readonly string[] OperationalTerms = ["handoff", "execution", "workflow", "process", "runbook"];

    public async Task<IReadOnlyList<DecisionCandidate>> ListCandidatesAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await decisionRepository.ListCandidatesAsync(repository);
    }

    public async Task<DecisionDiscoveryResult> DiscoverAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionContext context = await contextService.BuildContextAsync(repositoryId);
        IReadOnlyList<DecisionCandidate> existing = await decisionRepository.ListCandidatesAsync(repository);
        var activeFingerprints = existing
            .Where(candidate => candidate.State == DecisionCandidateState.Discovered ||
                candidate.State == DecisionCandidateState.Promoted)
            .Select(candidate => candidate.SourceFingerprint)
            .ToHashSet(StringComparer.Ordinal);
        var terminalFingerprints = existing
            .Where(candidate => candidate.State is DecisionCandidateState.Dismissed or
                DecisionCandidateState.Expired or
                DecisionCandidateState.Duplicate)
            .Select(candidate => candidate.SourceFingerprint)
            .ToHashSet(StringComparer.Ordinal);

        var discovered = new List<DecisionCandidate>();
        int signalCount = 0;
        int suppressed = 0;
        foreach (DiscoveredSignal signal in ExtractSignals(context))
        {
            signalCount++;
            if (activeFingerprints.Contains(signal.SourceFingerprint) ||
                terminalFingerprints.Contains(signal.SourceFingerprint))
            {
                suppressed++;
                continue;
            }

            string candidateId = await decisionRepository.AllocateCandidateIdAsync(repository);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var candidate = new DecisionCandidate(
                candidateId,
                repository.Id,
                DecisionCandidateState.Discovered,
                signal.Priority,
                signal.Classification,
                signal.Title,
                signal.Summary,
                signal.SourceFingerprint,
                [signal.Signal],
                signal.Signal.Evidence,
                signal.Sources,
                signal.Diagnostics,
                [new DecisionHistoryEntry(
                    now,
                    "Discovered",
                    null,
                    DecisionCandidateState.Discovered.ToString(),
                    "Discovered from decision context.",
                    signal.Sources)]);

            await decisionRepository.SaveCandidateAsync(repository, candidate);
            await projectionService.ProjectCandidateAsync(repository, candidate);
            discovered.Add(candidate);
            activeFingerprints.Add(signal.SourceFingerprint);
        }

        if (discovered.Count > 0)
        {
            await projectionService.RefreshDecisionIndexAsync(repository);
        }

        return new DecisionDiscoveryResult(
            discovered,
            new DecisionDiscoveryDiagnostics(
                context.Fingerprint,
                context.Items.Count,
                signalCount,
                discovered.Count,
                suppressed,
                context.Validation.Warnings));
    }

    public Task<DecisionCandidate> PromoteCandidateAsync(Guid repositoryId, string candidateId, string? reason)
    {
        return TransitionCandidateAsync(
            repositoryId,
            candidateId,
            DecisionCandidateState.Promoted,
            "Promoted",
            reason ?? "Candidate promoted to proposal boundary.");
    }

    public Task<DecisionCandidate> DismissCandidateAsync(Guid repositoryId, string candidateId, string? reason)
    {
        return TransitionCandidateAsync(
            repositoryId,
            candidateId,
            DecisionCandidateState.Dismissed,
            "Dismissed",
            reason ?? "Candidate dismissed by reviewer.");
    }

    public Task<DecisionCandidate> ExpireCandidateAsync(Guid repositoryId, string candidateId, string? reason)
    {
        return TransitionCandidateAsync(
            repositoryId,
            candidateId,
            DecisionCandidateState.Expired,
            "Expired",
            reason ?? "Candidate expired by explicit candidate-management operation.");
    }

    public async Task<DecisionCandidate> MarkCandidateDuplicateAsync(
        Guid repositoryId,
        string candidateId,
        string duplicateOfCandidateId,
        string? reason)
    {
        if (string.IsNullOrWhiteSpace(duplicateOfCandidateId))
        {
            throw new ArgumentException("Duplicate candidate id is required.", nameof(duplicateOfCandidateId));
        }

        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionCandidate? duplicateOf = await decisionRepository.GetCandidateAsync(repository, duplicateOfCandidateId);
        if (duplicateOf is null)
        {
            throw new KeyNotFoundException($"Decision candidate was not found: {duplicateOfCandidateId}");
        }

        return await TransitionCandidateAsync(
            repository,
            candidateId,
            DecisionCandidateState.Duplicate,
            "MarkedDuplicate",
            reason ?? $"Candidate duplicates {duplicateOfCandidateId}.",
            [new DecisionSourceReference("DecisionCandidate", CandidatePath(duplicateOfCandidateId), CandidateId: duplicateOfCandidateId)]);
    }

    private async Task<DecisionCandidate> TransitionCandidateAsync(
        Guid repositoryId,
        string candidateId,
        DecisionCandidateState toState,
        string eventName,
        string reason)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await TransitionCandidateAsync(repository, candidateId, toState, eventName, reason, []);
    }

    private async Task<DecisionCandidate> TransitionCandidateAsync(
        Repository repository,
        string candidateId,
        DecisionCandidateState toState,
        string eventName,
        string reason,
        IReadOnlyList<DecisionSourceReference> additionalSources)
    {
        DecisionCandidate? candidate = await decisionRepository.GetCandidateAsync(repository, candidateId);
        if (candidate is null)
        {
            throw new KeyNotFoundException($"Decision candidate was not found: {candidateId}");
        }

        DecisionTransitionResult transition = DecisionLifecycleRules.ValidateCandidateTransition(candidate.State, toState);
        if (!transition.IsValid)
        {
            throw new InvalidOperationException(transition.Error);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSourceReference[] sources = candidate.Sources
            .Concat(additionalSources)
            .OrderBy(source => source.SourceKind, StringComparer.Ordinal)
            .ThenBy(source => source.RelativePath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(source => source.CandidateId ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
        DecisionCandidate updated = candidate with
        {
            State = toState,
            History = candidate.History
                .Concat([
                    new DecisionHistoryEntry(
                        now,
                        eventName,
                        candidate.State.ToString(),
                        toState.ToString(),
                        reason,
                        sources)
                ])
                .ToArray()
        };

        await decisionRepository.SaveCandidateAsync(repository, updated);
        await projectionService.ProjectCandidateAsync(repository, updated);
        await projectionService.RefreshDecisionIndexAsync(repository);
        return updated;
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static IEnumerable<DiscoveredSignal> ExtractSignals(DecisionContext context)
    {
        foreach (DecisionContextItem item in context.Items)
        {
            if (item.Kind is "DecisionCandidate" or "DecisionProposal")
            {
                continue;
            }

            foreach (string excerpt in ExtractCandidateExcerpts(item.Content))
            {
                DecisionSignal? signal = BuildSignal(item, excerpt);
                if (signal is null)
                {
                    continue;
                }

                string title = Title(signal.Kind, item.Title);
                string fingerprint = Fingerprint($"{item.Kind}\n{item.Id}\n{item.Fingerprint}\n{signal.Kind}\n{excerpt}");
                DecisionSourceReference[] sources = item.Sources
                    .Select(source => source with
                    {
                        Section = source.Section ?? item.Title,
                        ItemId = source.ItemId ?? item.Id,
                        Excerpt = excerpt
                    })
                    .ToArray();

                yield return new DiscoveredSignal(
                    title,
                    signal.Summary,
                    fingerprint,
                    signal.Classification,
                    signal.Priority,
                    signal,
                    sources,
                    [$"Signal kind '{signal.Kind}' extracted from context item '{item.Id}'."]);
            }
        }
    }

    private static DecisionSignal? BuildSignal(DecisionContextItem item, string excerpt)
    {
        string normalized = excerpt.ToLowerInvariant();
        if (item.Kind == "Milestone" &&
            (normalized.StartsWith("detect ", StringComparison.Ordinal) ||
                normalized.StartsWith("add ", StringComparison.Ordinal) ||
                normalized.StartsWith("implement ", StringComparison.Ordinal) ||
                normalized.StartsWith("support ", StringComparison.Ordinal) ||
                normalized.StartsWith("classify ", StringComparison.Ordinal) ||
                normalized.StartsWith("prioritize ", StringComparison.Ordinal) ||
                normalized.StartsWith("persist ", StringComparison.Ordinal)))
        {
            return null;
        }

        if (ContainsAny(normalized, ["blocked", "blocker", "cannot proceed", "stalled", "stop executing"]))
        {
            return CreateSignal("BlockedExecution", item, excerpt, DecisionCandidatePriority.Blocking);
        }

        if (ContainsAny(normalized, ["conflict", "conflicts", "contradict", "contradiction", "inconsistent"]))
        {
            return CreateSignal("Conflict", item, excerpt, DecisionCandidatePriority.High);
        }

        if (ContainsAny(normalized, ["ambiguous", "ambiguity", "unclear", "open question", "question remains", "uncertainty"]))
        {
            return CreateSignal("Ambiguity", item, excerpt, DecisionCandidatePriority.Medium);
        }

        if (ContainsAny(normalized, ["must decide", "need to decide", "needs direction", "missing direction", "requires decision"]))
        {
            return CreateSignal("MissingDirection", item, excerpt, DecisionCandidatePriority.High);
        }

        if (ContainsAny(normalized, ["fork", "alternative", "option", "tradeoff", "approach"]))
        {
            return CreateSignal("ArchitecturalFork", item, excerpt, DecisionCandidatePriority.Medium);
        }

        if (ContainsAny(normalized, ["drift", "out of sync", "stale", "no longer matches"]))
        {
            return CreateSignal("MilestoneContextDrift", item, excerpt, DecisionCandidatePriority.Medium);
        }

        if (item.Kind == "ContinuityDiagnostics" &&
            ContainsAny(normalized, ["repeated", "uncertainty", "question", "rework"]) &&
            !normalized.EndsWith(": [],", StringComparison.Ordinal) &&
            !normalized.EndsWith(": []", StringComparison.Ordinal) &&
            !normalized.EndsWith(": {", StringComparison.Ordinal) &&
            !normalized.EndsWith(": 0,", StringComparison.Ordinal) &&
            !normalized.EndsWith(": 0", StringComparison.Ordinal))
        {
            return CreateSignal("RepeatedContinuityUncertainty", item, excerpt, DecisionCandidatePriority.Medium);
        }

        if (item.Kind == "Decision" &&
            ContainsAny(normalized, ["\"state\": \"open\"", "\"state\":\"open\""]))
        {
            return CreateSignal("StaleOpenDecision", item, excerpt, DecisionCandidatePriority.Low);
        }

        return null;
    }

    private static DecisionSignal CreateSignal(
        string kind,
        DecisionContextItem item,
        string excerpt,
        DecisionCandidatePriority priority)
    {
        DecisionClassification classification = Classify($"{item.Title}\n{excerpt}");
        DecisionEvidence evidence = new(
            $"Context item '{item.Title}' contains {kind} signal.",
            item.Sources.Select(source => source with
            {
                Section = source.Section ?? item.Title,
                ItemId = source.ItemId ?? item.Id,
                Excerpt = excerpt
            }).ToArray());
        return new DecisionSignal(
            kind,
            $"{kind} signal found in {item.Kind} context: {TrimExcerpt(excerpt, 180)}",
            classification,
            priority,
            [evidence]);
    }

    private static DecisionClassification Classify(string content)
    {
        string normalized = content.ToLowerInvariant();
        if (ContainsAny(normalized, ArchitecturalTerms))
        {
            return DecisionClassification.Architectural;
        }

        if (ContainsAny(normalized, StrategicTerms))
        {
            return DecisionClassification.Strategic;
        }

        if (ContainsAny(normalized, OperationalTerms))
        {
            return DecisionClassification.Operational;
        }

        return DecisionClassification.Tactical;
    }

    private static IEnumerable<string> ExtractCandidateExcerpts(string content)
    {
        return content
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim().TrimStart('-', '*').Trim())
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(200);
    }

    private static string Title(string signalKind, string sourceTitle)
    {
        return $"{Humanize(signalKind)} in {sourceTitle}";
    }

    private static string Humanize(string text)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            if (i > 0 && char.IsUpper(text[i]))
            {
                builder.Append(' ');
            }

            builder.Append(text[i]);
        }

        return builder.ToString();
    }

    private static bool ContainsAny(string content, IEnumerable<string> terms)
    {
        return terms.Any(term => content.Contains(term, StringComparison.Ordinal));
    }

    private static string Fingerprint(string content)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content.Replace("\r\n", "\n").Replace('\r', '\n').Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string TrimExcerpt(string excerpt, int maxLength)
    {
        return excerpt.Length <= maxLength ? excerpt : $"{excerpt[..maxLength].Trim()}...";
    }

    private static string CandidatePath(string candidateId)
    {
        return $".agents/decisions/candidates/{candidateId}/candidate.json";
    }

    private sealed record DiscoveredSignal(
        string Title,
        string Summary,
        string SourceFingerprint,
        DecisionClassification Classification,
        DecisionCandidatePriority Priority,
        DecisionSignal Signal,
        IReadOnlyList<DecisionSourceReference> Sources,
        IReadOnlyList<string> Diagnostics);
}
