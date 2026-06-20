using System.Security.Cryptography;
using System.Text;
using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Execution;
using CommandCenter.Backend.Planning;
using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Continuity;

public sealed class OperationalContextGenerationService(
    IRepositoryService repositoryService,
    IArtifactService artifactService,
    IPlanningService planningService,
    IExecutionSessionService executionSessionService,
    IOperationalContextParser parser,
    IUnderstandingDiffService diffService,
    IOperationalContextProposalStore proposalStore) : IOperationalContextGenerationService
{
    private const string CurrentOperationalContextPath = ".agents/operational_context.md";
    private const string CurrentHandoffPath = ".agents/handoffs/handoff.md";
    private const string CurrentDecisionsPath = ".agents/decisions/decisions.md";

    public async Task<OperationalContextProposal> GenerateAsync(Guid repositoryId)
    {
        var repository = await GetRepositoryAsync(repositoryId);
        var inputSet = await BuildInputSetAsync(repository);
        var currentDocument = parser.Parse(inputSet.CurrentOperationalContext ?? string.Empty);
        var proposedDocument = BuildProposedDocument(inputSet, currentDocument);
        var generatedContent = parser.Render(proposedDocument);
        var generatedDocument = parser.Parse(generatedContent);
        var generatedContentHash = HashContent(generatedContent);

        await proposalStore.SupersedePendingAsync(repository);

        var proposal = new OperationalContextProposal
        {
            ProposalId = CreateProposalId(),
            RepositoryId = repository.Id,
            GeneratedAt = DateTimeOffset.UtcNow,
            Status = OperationalContextProposalStatus.Pending,
            InputFingerprints = BuildFingerprints(inputSet, generatedContent),
            BaselineCurrentContextHash = HashOptionalContent(inputSet.CurrentOperationalContext),
            GeneratedContentHash = generatedContentHash,
            SemanticChanges = diffService.Compare(currentDocument, generatedDocument),
            CompressionSummary = new OperationalContextCompressionSummary
            {
                PreservedItemCount = CountItems(currentDocument),
                AddedItemCount = Math.Max(0, CountItems(generatedDocument) - CountItems(currentDocument)),
                WarningCount = currentDocument.AdditionalSections.Count,
                Warnings = currentDocument.AdditionalSections.Count == 0
                    ? []
                    : ["Unknown operational-context sections were preserved for reviewer inspection."]
            }
        };

        return await proposalStore.SaveAsync(repository, proposal, generatedContent);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        var repository = (await repositoryService.GetAllAsync()).FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<OperationalContextInputSet> BuildInputSetAsync(Repository repository)
    {
        var artifacts = await artifactService.DiscoverAsync(repository);
        return new OperationalContextInputSet
        {
            Repository = repository,
            CurrentOperationalContext = await ReadOptionalAsync(repository, CurrentOperationalContextPath),
            CurrentHandoff = await ReadOptionalAsync(repository, CurrentHandoffPath),
            CurrentDecisions = await ReadOptionalAsync(repository, CurrentDecisionsPath),
            ExecutionHistory = await executionSessionService.GetRepositorySessionHistoryAsync(repository.Id, 5),
            MilestonePaths = artifacts
                .Where(artifact => artifact.Type == ArtifactType.Milestone)
                .Select(artifact => artifact.RelativePath)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            HasPlan = artifacts.Any(artifact => artifact.Type == ArtifactType.Plan),
            PlanningReadiness = (await planningService.DetermineReadinessAsync(repository)).ToString()
        };
    }

    private async Task<string?> ReadOptionalAsync(Repository repository, string relativePath)
    {
        return await artifactService.ExistsAsync(repository, relativePath)
            ? await artifactService.LoadAsync(repository, relativePath)
            : null;
    }

    private static OperationalContextDocument BuildProposedDocument(
        OperationalContextInputSet inputSet,
        OperationalContextDocument current)
    {
        var currentMentalModel = current.CurrentMentalModel.ToList();
        AddUnique(
            currentMentalModel,
            OperationalContextItemKind.MentalModel,
            $"Repository `{inputSet.Repository.Name}` uses repository-owned `.agents` artifacts as continuity inputs.",
            ".agents");
        AddUnique(
            currentMentalModel,
            OperationalContextItemKind.MentalModel,
            inputSet.HasPlan
                ? "Planning state is available from `.agents/plan.md` and milestone artifacts."
                : "Planning state is partial because `.agents/plan.md` is not present.",
            ".agents/plan.md");

        var stableDecisions = current.StableDecisions.ToList();
        foreach (var decision in ExtractDecisionBullets(inputSet.CurrentDecisions).Take(8))
        {
            AddUnique(stableDecisions, OperationalContextItemKind.StableDecision, decision, CurrentDecisionsPath);
        }

        var recentChanges = current.RecentUnderstandingChanges.ToList();
        foreach (var change in ExtractHandoffSignals(inputSet.CurrentHandoff).Take(8))
        {
            AddUnique(recentChanges, OperationalContextItemKind.RecentChange, change, CurrentHandoffPath);
        }

        foreach (var session in inputSet.ExecutionHistory.Take(5))
        {
            var status = session.State.ToString();
            var milestone = string.IsNullOrWhiteSpace(session.MilestonePath) ? "unknown milestone" : session.MilestonePath;
            AddUnique(
                recentChanges,
                OperationalContextItemKind.RecentChange,
                $"Recent execution for `{milestone}` is recorded with state `{status}`.",
                null);
        }

        return new OperationalContextDocument
        {
            Title = string.IsNullOrWhiteSpace(current.Title) ? "Operational Context" : current.Title,
            CurrentMentalModel = currentMentalModel,
            Architecture = current.Architecture,
            AuthorityBoundaries = current.AuthorityBoundaries,
            Constraints = current.Constraints,
            StableDecisions = stableDecisions,
            DecisionRationale = current.DecisionRationale,
            OpenQuestions = current.OpenQuestions,
            ActiveRisks = current.ActiveRisks,
            RecentUnderstandingChanges = recentChanges,
            AdditionalSections = current.AdditionalSections
        };
    }

    private static IEnumerable<string> ExtractDecisionBullets(string? decisionsMarkdown)
    {
        return ExtractBullets(decisionsMarkdown)
            .Where(line => !line.Contains("next-slice", StringComparison.OrdinalIgnoreCase))
            .Select(line => $"Decision signal: {line}");
    }

    private static IEnumerable<string> ExtractHandoffSignals(string? handoffMarkdown)
    {
        return ExtractBullets(handoffMarkdown)
            .Where(line => !line.StartsWith("`", StringComparison.Ordinal))
            .Select(line => $"Latest handoff signal: {line}");
    }

    private static IEnumerable<string> ExtractBullets(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            yield break;
        }

        foreach (var rawLine in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length > 2 &&
                (line[0] == '-' || line[0] == '*' || line[0] == '+') &&
                char.IsWhiteSpace(line[1]))
            {
                var text = line[2..].Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return text;
                }
            }
        }
    }

    private static void AddUnique(
        List<OperationalContextItem> items,
        OperationalContextItemKind kind,
        string text,
        string? sourceRelativePath)
    {
        if (items.Any(item => string.Equals(Normalize(item.Text), Normalize(text), StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        items.Add(new OperationalContextItem
        {
            Id = CreateItemId(kind.ToString(), text),
            Kind = kind,
            Text = text,
            SourceRelativePath = sourceRelativePath
        });
    }

    private static IReadOnlyList<OperationalContextInputFingerprint> BuildFingerprints(
        OperationalContextInputSet inputSet,
        string generatedContent)
    {
        var executionHistoryContent = string.Join(
            Environment.NewLine,
            inputSet.ExecutionHistory.Select(session => $"{session.SessionId}|{session.State}|{session.MilestonePath}|{session.CompletedAt:O}"));
        var planningContent = string.Join(
            Environment.NewLine,
            inputSet.MilestonePaths.Prepend($"PlanningReadiness={inputSet.PlanningReadiness}").Prepend($"HasPlan={inputSet.HasPlan}"));

        return
        [
            Fingerprint("CurrentOperationalContext", CurrentOperationalContextPath, inputSet.CurrentOperationalContext),
            Fingerprint("CurrentHandoff", CurrentHandoffPath, inputSet.CurrentHandoff),
            Fingerprint("CurrentDecisions", CurrentDecisionsPath, inputSet.CurrentDecisions),
            Fingerprint("ExecutionHistory", ".agents/execution-sessions", executionHistoryContent),
            Fingerprint("PlanningState", ".agents", planningContent),
            Fingerprint("GeneratedProposal", ".agents/operational_context/proposals", generatedContent)
        ];
    }

    private static OperationalContextInputFingerprint Fingerprint(string name, string relativePath, string? content)
    {
        var present = content is not null;
        var normalizedContent = content ?? "<absent>";
        var bytes = Encoding.UTF8.GetByteCount(normalizedContent);
        return new OperationalContextInputFingerprint
        {
            Name = name,
            RelativePath = relativePath,
            Present = present,
            Hash = HashContent(normalizedContent),
            CharacterCount = normalizedContent.Length,
            ByteCount = bytes
        };
    }

    private static string HashOptionalContent(string? content)
    {
        return HashContent(content ?? "<absent>");
    }

    private static string HashContent(string content)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    private static string CreateProposalId()
    {
        return $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
    }

    private static string CreateItemId(string section, string text)
    {
        var normalized = Normalize($"{section}:{text}");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"{Normalize(section).Replace(' ', '-')}-{Convert.ToHexString(bytes)[..12].ToLowerInvariant()}";
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static int CountItems(OperationalContextDocument document)
    {
        return document.CurrentMentalModel.Count +
            document.Architecture.Count +
            document.AuthorityBoundaries.Count +
            document.Constraints.Count +
            document.StableDecisions.Count +
            document.DecisionRationale.Count +
            document.OpenQuestions.Count +
            document.ActiveRisks.Count +
            document.RecentUnderstandingChanges.Count;
    }
}
