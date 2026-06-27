using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Primitives;

namespace CommandCenter.Execution.Services;

public sealed class ExecutionSessionService(
    IExecutionContextService executionContextService,
    IExecutionSessionStore sessionStore,
    IExecutionProvider executionProvider,
    IExecutionPromptBuilder promptBuilder,
    IExecutionMonitoringService monitoringService,
    IGitService gitService,
    IDecisionInfluenceService? decisionInfluenceService = null) : IExecutionSessionService
{
    public const string OrphanedProviderFailureReason =
        "Active provider process could not be reattached after backend restart.";

    public const string ReattachedProviderRecoveryMessage =
        "Active provider process was reattached after backend restart.";

    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task RecoverAsync()
    {
        await gate.WaitAsync();
        try
        {
            List<ExecutionSession> sessions = (await sessionStore.LoadAsync()).ToList();
            DateTimeOffset recoveredAt = DateTimeOffset.UtcNow;
            bool changed = false;
            var reattachedSessionIds = new List<Guid>();

            for (int index = 0; index < sessions.Count; index++)
            {
                ExecutionSession session = sessions[index];
                if (session.RepositoryState != RepositoryExecutionState.Executing ||
                    session.State != ExecutionSessionState.Executing)
                {
                    continue;
                }

                if (executionProvider.SupportsReattach &&
                    await executionProvider.TryReattachAsync(
                        session,
                        monitoringService.CreateProviderObserver(session.Id)))
                {
                    sessions[index] = session.WithState(
                        ExecutionSessionState.Executing,
                        RepositoryExecutionState.Executing,
                        lastActivityAt: recoveredAt);
                    changed = true;
                    reattachedSessionIds.Add(session.Id);
                    continue;
                }

                sessions[index] = session.WithState(
                    ExecutionSessionState.Failed,
                    RepositoryExecutionState.Failed,
                    completedAt: recoveredAt,
                    lastActivityAt: recoveredAt,
                    failureReason: OrphanedProviderFailureReason);
                changed = true;
            }

            if (changed)
            {
                await sessionStore.SaveAsync(sessions);
                foreach (Guid sessionId in reattachedSessionIds)
                {
                    await monitoringService.RecordRecoveryAsync(sessionId, ReattachedProviderRecoveryMessage);
                }

                foreach (ExecutionSession session in sessions.Where(session => session.FailureReason == OrphanedProviderFailureReason))
                {
                    await monitoringService.RecordRecoveryAsync(session.Id, OrphanedProviderFailureReason);
                }
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<RepositoryExecutionState> GetRepositoryStateAsync(Guid repositoryId)
    {
        ExecutionSession? session = (await sessionStore.LoadAsync())
            .Where(session => session.RepositoryId == repositoryId)
            .OrderByDescending(session => session.StartedAt)
            .FirstOrDefault();

        return session?.RepositoryState ?? RepositoryExecutionState.Ready;
    }

    public async Task<ExecutionSessionSummary?> GetActiveSessionAsync(Guid repositoryId)
    {
        ExecutionSession? session = (await sessionStore.LoadAsync())
            .Where(session => session.RepositoryId == repositoryId && IsActiveRepositoryState(session.RepositoryState))
            .OrderByDescending(session => session.StartedAt)
            .FirstOrDefault();

        return session?.ToSummary();
    }

    public async Task<ExecutionSessionSummary?> GetRepositorySessionSummaryAsync(Guid repositoryId)
    {
        ExecutionSession? session = (await sessionStore.LoadAsync())
            .Where(session => session.RepositoryId == repositoryId)
            .OrderByDescending(session => session.StartedAt)
            .FirstOrDefault();

        return session?.ToSummary();
    }

    public async Task<IReadOnlyList<ExecutionSessionSummary>> GetRepositorySessionHistoryAsync(
        Guid repositoryId,
        int limit = 10)
    {
        if (limit <= 0)
        {
            return [];
        }

        return (await sessionStore.LoadAsync())
            .Where(session => session.RepositoryId == repositoryId)
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id)
            .Take(limit)
            .Select(session => session.ToSummary())
            .ToArray();
    }

    public async Task<ExecutionSessionSummary> StartAsync(Guid repositoryId, ExecutionStartRequest request)
    {
        await gate.WaitAsync();
        try
        {
            List<ExecutionSession> sessions = (await sessionStore.LoadAsync()).ToList();
            if (sessions.Any(session => session.RepositoryId == repositoryId && IsActiveRepositoryState(session.RepositoryState)))
            {
                throw new InvalidOperationException("Repository already has an active execution session.");
            }

            ExecutionContext context = await executionContextService.BuildContextAsync(repositoryId);
            if (context.Diagnostics.LaunchBlocked)
            {
                List<string> reasons = context.Diagnostics.ValidationErrors.ToList();
                if (context.Diagnostics.HardLimitExceeded)
                {
                    reasons.Add("Execution context size hard limit was exceeded.");
                }

                throw new InvalidOperationException(
                    $"Execution launch is blocked: {string.Join(" ", reasons)}");
            }

            DateTimeOffset startedAt = DateTimeOffset.UtcNow;
            var session = new ExecutionSession
            {
                Id = Guid.NewGuid(),
                RepositoryId = context.Id,
                RepositoryPath = context.Path,
                StartedAt = startedAt,
                LastActivityAt = startedAt,
                State = ExecutionSessionState.Created,
                RepositoryState = RepositoryExecutionState.Ready,
                ProviderName = executionProvider.Name,
                RepositorySnapshot = context.Snapshot,
                PreviousHandoffContent = context.Artifacts
                    .SingleOrDefault(artifact => artifact.Role == "CurrentHandoff")
                    ?.Content,
                PreviousHandoffCapturedAt = context.Artifacts.Any(artifact => artifact.Role == "CurrentHandoff")
                    ? startedAt
                    : null
            };

            sessions.Add(session);
            await sessionStore.SaveAsync(sessions);

            ExecutionPrompt prompt = promptBuilder.Build(context);
            ExecutionPromptManifest promptManifest = BuildPromptManifest(session.Id, context, prompt);
            if (context.DecisionProjection is not null && decisionInfluenceService is not null)
            {
                await decisionInfluenceService.RecordExecutionInfluenceAsync(
                    context.Id,
                    session.Id,
                    context.DecisionProjection);
            }

            ExecutionProviderStartResult startResult;
            try
            {
                startResult = await executionProvider.StartAsync(
                    prompt,
                    session,
                    monitoringService.CreateProviderObserver(session.Id));
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException)
            {
                ExecutionSession failedSession = session.WithState(
                    ExecutionSessionState.Failed,
                    RepositoryExecutionState.Ready,
                    completedAt: DateTimeOffset.UtcNow,
                    failureReason: exception.Message);
                await ReplaceSessionAsync(sessions, failedSession);
                await monitoringService.RecordFailureAsync(session.Id, exception.Message);
                return failedSession.ToSummary();
            }

            ExecutionSession latestSession = (await sessionStore.LoadAsync()).FirstOrDefault(storedSession => storedSession.Id == session.Id) ?? session;
            DateTimeOffset providerStartedAt = startResult.StartedAt == default ? DateTimeOffset.UtcNow : startResult.StartedAt;
            ExecutionSession executingSession = latestSession.WithState(
                ExecutionSessionState.Executing,
                RepositoryExecutionState.Executing,
                lastActivityAt: DateTimeOffset.UtcNow,
                providerExecutablePath: startResult.ExecutablePath,
                providerProcessId: startResult.ProcessId,
                providerStartedAt: providerStartedAt,
                promptMetadata: prompt.Metadata,
                promptManifest: promptManifest);
            await ReplaceSessionAsync(sessions, executingSession);
            await monitoringService.RecordProviderStartedAsync(session.Id, providerStartedAt);
            return executingSession.ToSummary();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ExecutionSession?> GetSessionAsync(Guid sessionId)
    {
        return (await sessionStore.LoadAsync()).FirstOrDefault(session => session.Id == sessionId);
    }

    public async Task<ExecutionSessionSummary> AcceptAsync(Guid sessionId, ExecutionAcceptanceRequest request)
    {
        await gate.WaitAsync();
        try
        {
            List<ExecutionSession> sessions = (await sessionStore.LoadAsync()).ToList();
            ExecutionSession session = sessions.FirstOrDefault(session => session.Id == sessionId)
                                       ?? throw new KeyNotFoundException($"Execution session was not found: {sessionId}");
            if (session.RepositoryState != RepositoryExecutionState.AwaitingAcceptance)
            {
                throw new InvalidOperationException("Execution can only be accepted while awaiting acceptance.");
            }

            DateTimeOffset acceptedAt = DateTimeOffset.UtcNow;
            var repository = new Repository
            {
                Id = session.RepositoryId,
                Name = Path.GetFileName(session.RepositoryPath),
                Path = session.RepositoryPath
            };
            RepositorySnapshot snapshot = await gitService.GetSnapshotAsync(repository);
            RepositoryExecutionState repositoryState = snapshot.DirtyState.IsClean
                ? RepositoryExecutionState.Ready
                : RepositoryExecutionState.AwaitingCommit;
            ExecutionSession acceptedSession = session.WithDecision(
                repositoryState,
                acceptedAt: acceptedAt,
                lastActivityAt: acceptedAt,
                decisionNote: NormalizeDecisionNote(request.DecisionNote),
                repositorySnapshot: snapshot);

            await ReplaceSessionAsync(sessions, acceptedSession);
            return acceptedSession.ToSummary();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ExecutionSessionSummary> RejectAsync(Guid sessionId, ExecutionAcceptanceRequest request)
    {
        await gate.WaitAsync();
        try
        {
            List<ExecutionSession> sessions = (await sessionStore.LoadAsync()).ToList();
            ExecutionSession session = sessions.FirstOrDefault(session => session.Id == sessionId)
                                       ?? throw new KeyNotFoundException($"Execution session was not found: {sessionId}");
            if (session.RepositoryState != RepositoryExecutionState.AwaitingAcceptance)
            {
                throw new InvalidOperationException("Execution can only be rejected while awaiting acceptance.");
            }

            DateTimeOffset rejectedAt = DateTimeOffset.UtcNow;
            ExecutionSession rejectedSession = session.WithDecision(
                RepositoryExecutionState.Ready,
                rejectedAt: rejectedAt,
                lastActivityAt: rejectedAt,
                decisionNote: NormalizeDecisionNote(request.DecisionNote));

            await ReplaceSessionAsync(sessions, rejectedSession);
            return rejectedSession.ToSummary();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<CommitPreparation> PrepareCommitAsync(Guid sessionId)
    {
        await gate.WaitAsync();
        try
        {
            List<ExecutionSession> sessions = (await sessionStore.LoadAsync()).ToList();
            ExecutionSession session = sessions.FirstOrDefault(session => session.Id == sessionId)
                                       ?? throw new KeyNotFoundException($"Execution session was not found: {sessionId}");
            if (session.RepositoryState != RepositoryExecutionState.AwaitingCommit)
            {
                throw new InvalidOperationException("Commit can only be prepared while awaiting commit.");
            }

            var repository = new Repository
            {
                Id = session.RepositoryId,
                Name = Path.GetFileName(session.RepositoryPath),
                Path = session.RepositoryPath
            };
            CommitPreparation preparation = await gitService.PrepareCommitAsync(repository, session);
            ExecutionSession preparedSession = session.WithCommitPreparation(preparation, DateTimeOffset.UtcNow);
            await ReplaceSessionAsync(sessions, preparedSession);
            await monitoringService.RecordCommitPreparationCreatedAsync(
                session.Id,
                preparation.ScopeItems.Count,
                preparation.HasPreExistingChanges);
            return preparation;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ExecutionSessionSummary> CommitAsync(Guid sessionId, CommitRequest request)
    {
        await gate.WaitAsync();
        try
        {
            List<ExecutionSession> sessions = (await sessionStore.LoadAsync()).ToList();
            ExecutionSession session = sessions.FirstOrDefault(session => session.Id == sessionId)
                                       ?? throw new KeyNotFoundException($"Execution session was not found: {sessionId}");
            if (session.RepositoryState != RepositoryExecutionState.AwaitingCommit)
            {
                throw new InvalidOperationException("Commit can only run while awaiting commit.");
            }

            CommitPreparation preparation = session.CommitPreparation
                                            ?? throw new InvalidOperationException("Commit preparation is required before commit.");
            if (!string.Equals(
                    request.StatusSnapshotId,
                    preparation.StatusSnapshot.Id,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Commit request uses a stale status snapshot.");
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                throw new InvalidOperationException("Commit message is required.");
            }

            IReadOnlyList<string> selectedPaths = NormalizeSelectedPaths(request.SelectedPaths, session.RepositoryPath);
            if (selectedPaths.Count == 0)
            {
                throw new InvalidOperationException("At least one path must be selected for commit.");
            }

            HashSet<string> preparedPaths = preparation.ScopeItems
                .Select(item => item.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            string? unknownPath = selectedPaths.FirstOrDefault(path => !preparedPaths.Contains(path));
            if (unknownPath is not null)
            {
                throw new InvalidOperationException($"Selected path was not in the prepared commit scope: {unknownPath}");
            }

            var repository = new Repository
            {
                Id = session.RepositoryId,
                Name = Path.GetFileName(session.RepositoryPath),
                Path = session.RepositoryPath
            };
            CommitStatusSnapshot currentSnapshot = await gitService.GetCommitStatusSnapshotAsync(repository);
            if (!string.Equals(currentSnapshot.Id, preparation.StatusSnapshot.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Repository status changed after commit preparation. Refresh commit review before committing.");
            }

            CommitResult result;
            try
            {
                result = await gitService.CommitAsync(
                    repository,
                    request.Message.Trim(),
                    selectedPaths,
                    preparation.StatusSnapshot.Id);
            }
            catch (InvalidOperationException)
            {
                throw;
            }

            ExecutionSession committedSession = session.WithCommitResult(result, DateTimeOffset.UtcNow);
            await ReplaceSessionAsync(sessions, committedSession);
            await monitoringService.RecordCommitSucceededAsync(session.Id, result.CommitSha);
            return committedSession.ToSummary();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ExecutionSessionSummary> PushAsync(Guid sessionId, PushRequest request)
    {
        await gate.WaitAsync();
        try
        {
            List<ExecutionSession> sessions = (await sessionStore.LoadAsync()).ToList();
            ExecutionSession session = sessions.FirstOrDefault(session => session.Id == sessionId)
                                       ?? throw new KeyNotFoundException($"Execution session was not found: {sessionId}");
            if (session.RepositoryState != RepositoryExecutionState.AwaitingPush)
            {
                throw new InvalidOperationException("Push can only run while awaiting push.");
            }

            var repository = new Repository
            {
                Id = session.RepositoryId,
                Name = Path.GetFileName(session.RepositoryPath),
                Path = session.RepositoryPath
            };

            PushResult result;
            try
            {
                result = await gitService.PushAsync(repository, session.CommitSha);
            }
            catch (InvalidOperationException exception)
            {
                DateTimeOffset failedAttemptAt = DateTimeOffset.UtcNow;
                ExecutionSession retryableSession = session.WithPushFailure(failedAttemptAt, exception.Message);
                await ReplaceSessionAsync(sessions, retryableSession);
                await monitoringService.RecordPushAttemptedAsync(session.Id);
                await monitoringService.RecordPushFailedAsync(session.Id, exception.Message);
                throw;
            }

            RepositorySnapshot snapshot = await gitService.GetSnapshotAsync(repository);
            ExecutionSession pushedSession = session.WithPushResult(result, DateTimeOffset.UtcNow, snapshot);
            await ReplaceSessionAsync(sessions, pushedSession);
            await monitoringService.RecordPushAttemptedAsync(session.Id);
            await monitoringService.RecordPushSucceededAsync(session.Id, result.PushedCommitSha);
            return pushedSession.ToSummary();
        }
        finally
        {
            gate.Release();
        }
    }

    private static bool IsActiveRepositoryState(RepositoryExecutionState state)
    {
        return state == RepositoryExecutionState.Executing;
    }

    private async Task ReplaceSessionAsync(List<ExecutionSession> sessions, ExecutionSession replacement)
    {
        int index = sessions.FindIndex(session => session.Id == replacement.Id);
        if (index < 0)
        {
            sessions.Add(replacement);
        }
        else
        {
            sessions[index] = replacement;
        }

        await sessionStore.SaveAsync(sessions);
    }

    private static string? NormalizeDecisionNote(string? note)
    {
        return string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    private static ExecutionPromptManifest BuildPromptManifest(
        Guid sessionId,
        ExecutionContext context,
        ExecutionPrompt prompt)
    {
        bool dirtyRepository = context.Snapshot?.DirtyState.IsClean == false;
        ExecutionPromptManifestArtifact[] deliveredArtifacts = context.Artifacts
            .Select(artifact => new ExecutionPromptManifestArtifact
            {
                Role = artifact.Role,
                RelativePath = artifact.RelativePath,
                ByteCount = artifact.ByteCount,
                CharacterCount = artifact.CharacterCount,
                Delivered = true
            })
            .OrderBy(artifact => artifact.Role, StringComparer.Ordinal)
            .ThenBy(artifact => artifact.RelativePath, StringComparer.Ordinal)
            .ToArray();
        ExecutionPromptManifestArtifact[] requestedArtifacts = BuildRequestedArtifacts(context, deliveredArtifacts);
        int governedDecisionCount = CountGovernedDecisions(context.DecisionProjection);

        return new ExecutionPromptManifest
        {
            SessionId = sessionId,
            GeneratedAt = prompt.Metadata.GeneratedAt,
            PromptText = prompt.Text,
            Provenance = prompt.Provenance,
            RequestedArtifacts = requestedArtifacts,
            RequestedContextBytes = context.Diagnostics.TotalBytes,
            RequestedContextCharacters = context.Diagnostics.TotalCharacters,
            DeliveredArtifacts = deliveredArtifacts,
            DeliveredContextBytes = context.Diagnostics.TotalBytes,
            DeliveredContextCharacters = context.Diagnostics.TotalCharacters,
            DirtyRepositoryAtRequestTime = dirtyRepository,
            DirtyRepositoryAtDeliveryTime = dirtyRepository,
            GovernedDecisionCountRequested = governedDecisionCount,
            GovernedDecisionCountDelivered = governedDecisionCount,
            OperationalContextSourceRequested = ".agents/operational_context.md",
            OperationalContextSourceDelivered = FindDeliveredPath(context, "OperationalContext"),
            HandoffSourceRequested = ".agents/handoffs/handoff.md",
            HandoffSourceDelivered = FindDeliveredPath(context, "CurrentHandoff"),
            ProviderDeliveryStatus = "DeliveredAsRequested",
            ProviderAdjustments = [],
            Diagnostics = [ExecutionPromptManifest.NoProviderDivergenceSignalDiagnostic]
        };
    }

    private static ExecutionPromptManifestArtifact[] BuildRequestedArtifacts(
        ExecutionContext context,
        IReadOnlyList<ExecutionPromptManifestArtifact> deliveredArtifacts)
    {
        Dictionary<string, ExecutionPromptManifestArtifact> deliveredByPath = deliveredArtifacts
            .ToDictionary(artifact => artifact.RelativePath, StringComparer.OrdinalIgnoreCase);

        return ExpectedArtifacts()
            .Select(expected =>
            {
                if (deliveredByPath.TryGetValue(expected.RelativePath, out ExecutionPromptManifestArtifact? delivered))
                {
                    return delivered;
                }

                return new ExecutionPromptManifestArtifact
                {
                    Role = expected.Role,
                    RelativePath = expected.RelativePath,
                    Delivered = false
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<(string Role, string RelativePath)> ExpectedArtifacts() =>
    [
        ("Plan", ".agents/plan.md"),
        ("OperationalContext", ".agents/operational_context.md"),
        ("CurrentHandoff", ".agents/handoffs/handoff.md")
    ];

    private static string? FindDeliveredPath(ExecutionContext context, string role)
    {
        return context.Artifacts.SingleOrDefault(artifact => artifact.Role == role)?.RelativePath;
    }

    private static int CountGovernedDecisions(ExecutionDecisionProjection? projection)
    {
        if (projection is null)
        {
            return 0;
        }

        return projection.IncludedDecisions
            .Concat(projection.ExcludedDecisions)
            .Concat(projection.SupersededDecisions)
            .Concat(projection.ConflictingDecisions)
            .Concat(projection.IgnoredDecisions)
            .Concat(projection.BlockedDecisions)
            .Select(diagnostic => diagnostic.DecisionId)
            .Where(decisionId => !string.IsNullOrWhiteSpace(decisionId))
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private static IReadOnlyList<string> NormalizeSelectedPaths(
        IEnumerable<string>? selectedPaths,
        string repositoryPath)
    {
        var normalizedPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        string repositoryRoot = Path.GetFullPath(repositoryPath)
                                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                Path.DirectorySeparatorChar;

        foreach (string selectedPath in selectedPaths ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                throw new InvalidOperationException("Selected paths must be repository-relative paths.");
            }

            string normalizedPath = selectedPath.Replace('\\', '/').Trim();
            if (Path.IsPathRooted(normalizedPath) ||
                normalizedPath.Split('/').Any(segment => segment is ".." or "."))
            {
                throw new InvalidOperationException($"Selected path is not a safe repository-relative path: {selectedPath}");
            }

            string fullPath = Path.GetFullPath(Path.Combine(repositoryPath, normalizedPath));
            if (!fullPath.StartsWith(repositoryRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Selected path escapes the repository: {selectedPath}");
            }

            normalizedPaths.Add(normalizedPath);
        }

        return normalizedPaths.ToArray();
    }
}

file static class ExecutionSessionMutation
{
    public static ExecutionSession WithState(
        this ExecutionSession session,
        ExecutionSessionState state,
        RepositoryExecutionState repositoryState,
        DateTimeOffset? completedAt = null,
        DateTimeOffset? lastActivityAt = null,
        string? failureReason = null,
        string? providerExecutablePath = null,
        int? providerProcessId = null,
        DateTimeOffset? providerStartedAt = null,
        ExecutionPromptMetadata? promptMetadata = null,
        ExecutionPromptManifest? promptManifest = null)
    {
        return new ExecutionSession
        {
            Id = session.Id,
            RepositoryId = session.RepositoryId,
            RepositoryPath = session.RepositoryPath,
            StartedAt = session.StartedAt,
            CompletedAt = completedAt ?? session.CompletedAt,
            AcceptedAt = session.AcceptedAt,
            RejectedAt = session.RejectedAt,
            DecisionNote = session.DecisionNote,
            LastActivityAt = lastActivityAt ?? session.LastActivityAt,
            State = state,
            RepositoryState = repositoryState,
            ProviderName = session.ProviderName,
            ProviderExecutablePath = providerExecutablePath ?? session.ProviderExecutablePath,
            ProviderProcessId = providerProcessId ?? session.ProviderProcessId,
            ProviderStartedAt = providerStartedAt ?? session.ProviderStartedAt,
            PromptMetadata = promptMetadata ?? session.PromptMetadata,
            PromptManifest = promptManifest ?? session.PromptManifest,
            RepositorySnapshot = session.RepositorySnapshot,
            CommitPreparation = session.CommitPreparation,
            CommitSha = session.CommitSha,
            CommittedAt = session.CommittedAt,
            CommitMessage = session.CommitMessage,
            PreparationSnapshotId = session.PreparationSnapshotId,
            PushAttemptedAt = session.PushAttemptedAt,
            PushedAt = session.PushedAt,
            PushedCommitSha = session.PushedCommitSha,
            PushRemoteName = session.PushRemoteName,
            PushBranchName = session.PushBranchName,
            PreviousHandoffContent = session.PreviousHandoffContent,
            PreviousHandoffCapturedAt = session.PreviousHandoffCapturedAt,
            HandoffPath = session.HandoffPath,
            HandoffProcessing = session.HandoffProcessing,
            FailureReason = failureReason ?? session.FailureReason,
            Events = session.Events
        };
    }

    public static ExecutionSession WithDecision(
        this ExecutionSession session,
        RepositoryExecutionState repositoryState,
        DateTimeOffset? acceptedAt = null,
        DateTimeOffset? rejectedAt = null,
        DateTimeOffset? lastActivityAt = null,
        string? decisionNote = null,
        RepositorySnapshot? repositorySnapshot = null)
    {
        return new ExecutionSession
        {
            Id = session.Id,
            RepositoryId = session.RepositoryId,
            RepositoryPath = session.RepositoryPath,
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt,
            AcceptedAt = acceptedAt ?? session.AcceptedAt,
            RejectedAt = rejectedAt ?? session.RejectedAt,
            DecisionNote = decisionNote ?? session.DecisionNote,
            LastActivityAt = lastActivityAt ?? session.LastActivityAt,
            State = session.State,
            RepositoryState = repositoryState,
            ProviderName = session.ProviderName,
            ProviderExecutablePath = session.ProviderExecutablePath,
            ProviderProcessId = session.ProviderProcessId,
            ProviderStartedAt = session.ProviderStartedAt,
            PromptMetadata = session.PromptMetadata,
            PromptManifest = session.PromptManifest,
            RepositorySnapshot = repositorySnapshot ?? session.RepositorySnapshot,
            CommitPreparation = session.CommitPreparation,
            CommitSha = session.CommitSha,
            CommittedAt = session.CommittedAt,
            CommitMessage = session.CommitMessage,
            PreparationSnapshotId = session.PreparationSnapshotId,
            PushAttemptedAt = session.PushAttemptedAt,
            PushedAt = session.PushedAt,
            PushedCommitSha = session.PushedCommitSha,
            PushRemoteName = session.PushRemoteName,
            PushBranchName = session.PushBranchName,
            PreviousHandoffContent = session.PreviousHandoffContent,
            PreviousHandoffCapturedAt = session.PreviousHandoffCapturedAt,
            HandoffPath = session.HandoffPath,
            HandoffProcessing = session.HandoffProcessing,
            FailureReason = session.FailureReason,
            Events = session.Events
        };
    }

    public static ExecutionSession WithCommitPreparation(
        this ExecutionSession session,
        CommitPreparation commitPreparation,
        DateTimeOffset lastActivityAt)
    {
        return new ExecutionSession
        {
            Id = session.Id,
            RepositoryId = session.RepositoryId,
            RepositoryPath = session.RepositoryPath,
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt,
            AcceptedAt = session.AcceptedAt,
            RejectedAt = session.RejectedAt,
            DecisionNote = session.DecisionNote,
            LastActivityAt = lastActivityAt,
            State = session.State,
            RepositoryState = session.RepositoryState,
            ProviderName = session.ProviderName,
            ProviderExecutablePath = session.ProviderExecutablePath,
            ProviderProcessId = session.ProviderProcessId,
            ProviderStartedAt = session.ProviderStartedAt,
            PromptMetadata = session.PromptMetadata,
            PromptManifest = session.PromptManifest,
            RepositorySnapshot = session.RepositorySnapshot,
            CommitPreparation = commitPreparation,
            CommitSha = session.CommitSha,
            CommittedAt = session.CommittedAt,
            CommitMessage = session.CommitMessage,
            PreparationSnapshotId = session.PreparationSnapshotId,
            PushAttemptedAt = session.PushAttemptedAt,
            PushedAt = session.PushedAt,
            PushedCommitSha = session.PushedCommitSha,
            PushRemoteName = session.PushRemoteName,
            PushBranchName = session.PushBranchName,
            PreviousHandoffContent = session.PreviousHandoffContent,
            PreviousHandoffCapturedAt = session.PreviousHandoffCapturedAt,
            HandoffPath = session.HandoffPath,
            HandoffProcessing = session.HandoffProcessing,
            FailureReason = session.FailureReason,
            Events = session.Events
        };
    }

    public static ExecutionSession WithCommitResult(
        this ExecutionSession session,
        CommitResult commitResult,
        DateTimeOffset lastActivityAt)
    {
        return new ExecutionSession
        {
            Id = session.Id,
            RepositoryId = session.RepositoryId,
            RepositoryPath = session.RepositoryPath,
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt,
            AcceptedAt = session.AcceptedAt,
            RejectedAt = session.RejectedAt,
            DecisionNote = session.DecisionNote,
            LastActivityAt = lastActivityAt,
            State = session.State,
            RepositoryState = RepositoryExecutionState.AwaitingPush,
            ProviderName = session.ProviderName,
            ProviderExecutablePath = session.ProviderExecutablePath,
            ProviderProcessId = session.ProviderProcessId,
            ProviderStartedAt = session.ProviderStartedAt,
            PromptMetadata = session.PromptMetadata,
            PromptManifest = session.PromptManifest,
            RepositorySnapshot = session.RepositorySnapshot,
            CommitPreparation = session.CommitPreparation,
            CommitSha = commitResult.CommitSha,
            CommittedAt = commitResult.CommittedAt,
            CommitMessage = commitResult.CommitMessage,
            PreparationSnapshotId = commitResult.PreparationSnapshotId,
            PushAttemptedAt = session.PushAttemptedAt,
            PushedAt = session.PushedAt,
            PushedCommitSha = session.PushedCommitSha,
            PushRemoteName = session.PushRemoteName,
            PushBranchName = session.PushBranchName,
            PreviousHandoffContent = session.PreviousHandoffContent,
            PreviousHandoffCapturedAt = session.PreviousHandoffCapturedAt,
            HandoffPath = session.HandoffPath,
            HandoffProcessing = session.HandoffProcessing,
            FailureReason = session.FailureReason,
            Events = session.Events
        };
    }

    public static ExecutionSession WithPushResult(
        this ExecutionSession session,
        PushResult pushResult,
        DateTimeOffset lastActivityAt,
        RepositorySnapshot repositorySnapshot)
    {
        return new ExecutionSession
        {
            Id = session.Id,
            RepositoryId = session.RepositoryId,
            RepositoryPath = session.RepositoryPath,
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt,
            AcceptedAt = session.AcceptedAt,
            RejectedAt = session.RejectedAt,
            DecisionNote = session.DecisionNote,
            LastActivityAt = lastActivityAt,
            State = session.State,
            RepositoryState = RepositoryExecutionState.Ready,
            ProviderName = session.ProviderName,
            ProviderExecutablePath = session.ProviderExecutablePath,
            ProviderProcessId = session.ProviderProcessId,
            ProviderStartedAt = session.ProviderStartedAt,
            PromptMetadata = session.PromptMetadata,
            PromptManifest = session.PromptManifest,
            RepositorySnapshot = repositorySnapshot,
            CommitPreparation = session.CommitPreparation,
            CommitSha = session.CommitSha,
            CommittedAt = session.CommittedAt,
            CommitMessage = session.CommitMessage,
            PreparationSnapshotId = session.PreparationSnapshotId,
            PushAttemptedAt = pushResult.PushAttemptedAt,
            PushedAt = pushResult.PushedAt,
            PushedCommitSha = pushResult.PushedCommitSha,
            PushRemoteName = pushResult.RemoteName,
            PushBranchName = pushResult.BranchName,
            PreviousHandoffContent = session.PreviousHandoffContent,
            PreviousHandoffCapturedAt = session.PreviousHandoffCapturedAt,
            HandoffPath = session.HandoffPath,
            HandoffProcessing = session.HandoffProcessing,
            FailureReason = null,
            Events = session.Events
        };
    }

    public static ExecutionSession WithPushFailure(
        this ExecutionSession session,
        DateTimeOffset attemptedAt,
        string failureReason)
    {
        return new ExecutionSession
        {
            Id = session.Id,
            RepositoryId = session.RepositoryId,
            RepositoryPath = session.RepositoryPath,
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt,
            AcceptedAt = session.AcceptedAt,
            RejectedAt = session.RejectedAt,
            DecisionNote = session.DecisionNote,
            LastActivityAt = attemptedAt,
            State = session.State,
            RepositoryState = RepositoryExecutionState.AwaitingPush,
            ProviderName = session.ProviderName,
            ProviderExecutablePath = session.ProviderExecutablePath,
            ProviderProcessId = session.ProviderProcessId,
            ProviderStartedAt = session.ProviderStartedAt,
            PromptMetadata = session.PromptMetadata,
            PromptManifest = session.PromptManifest,
            RepositorySnapshot = session.RepositorySnapshot,
            CommitPreparation = session.CommitPreparation,
            CommitSha = session.CommitSha,
            CommittedAt = session.CommittedAt,
            CommitMessage = session.CommitMessage,
            PreparationSnapshotId = session.PreparationSnapshotId,
            PushAttemptedAt = attemptedAt,
            PushedAt = session.PushedAt,
            PushedCommitSha = session.PushedCommitSha,
            PushRemoteName = session.PushRemoteName,
            PushBranchName = session.PushBranchName,
            PreviousHandoffContent = session.PreviousHandoffContent,
            PreviousHandoffCapturedAt = session.PreviousHandoffCapturedAt,
            HandoffPath = session.HandoffPath,
            HandoffProcessing = session.HandoffProcessing,
            FailureReason = failureReason,
            Events = session.Events
        };
    }
}
