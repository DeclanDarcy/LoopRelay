using System.Text.RegularExpressions;
using CommandCenter.Execution.Services;
using CommandCenter.Execution.Primitives;

namespace CommandCenter.Execution.Models;

public sealed class ExecutionSessionTransparency
{
    private static readonly Regex ExitCodePattern = new(@"code\s+(-?\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Guid SessionId { get; init; }

    public ExecutionPromptMetadata? PromptMetadata { get; init; }

    public ExecutionRecoveryTransparency Recovery { get; init; } = new();

    public ExecutionMonitoringTransparency Monitoring { get; init; } = new();

    public ExecutionHandoffProcessingTransparency HandoffProcessing { get; init; } = new();

    public static ExecutionSessionTransparency FromSession(ExecutionSession session)
    {
        ExecutionEvent[] recoveryEvents = session.Events
            .Where(executionEvent => executionEvent.Type == ExecutionEventType.Recovery)
            .OrderBy(executionEvent => executionEvent.Sequence)
            .ToArray();
        ExecutionEvent? latestRecoveryEvent = recoveryEvents.LastOrDefault();
        ExecutionEvent? providerExitEvent = session.Events
            .Where(executionEvent => executionEvent.Type == ExecutionEventType.ProviderExited)
            .OrderBy(executionEvent => executionEvent.Sequence)
            .LastOrDefault();
        int? providerExitCode = providerExitEvent is null ? null : ExtractExitCode(providerExitEvent.Message);
        bool orphanedProviderState = string.Equals(
            session.FailureReason,
            ExecutionSessionService.OrphanedProviderFailureReason,
            StringComparison.Ordinal);
        bool reattachSucceeded = recoveryEvents.Any(executionEvent =>
            string.Equals(
                executionEvent.Message,
                ExecutionSessionService.ReattachedProviderRecoveryMessage,
                StringComparison.Ordinal));
        bool sessionMarkedFailedByRecovery = orphanedProviderState &&
            session.State == ExecutionSessionState.Failed &&
            session.RepositoryState == RepositoryExecutionState.Failed;
        string providerProcessState = DetermineProviderProcessState(session, providerExitEvent);
        string[] monitoringWarnings = BuildMonitoringWarnings(
            session,
            providerProcessState,
            providerExitCode,
            orphanedProviderState);
        long? firstRetainedSequence = session.Events.Count == 0 ? null : session.Events.Min(executionEvent => executionEvent.Sequence);
        long? lastRetainedSequence = session.Events.Count == 0 ? null : session.Events.Max(executionEvent => executionEvent.Sequence);

        return new ExecutionSessionTransparency
        {
            SessionId = session.Id,
            PromptMetadata = session.PromptMetadata,
            Recovery = new ExecutionRecoveryTransparency
            {
                RecoveryRan = recoveryEvents.Length > 0 || orphanedProviderState,
                RecoveryTrigger = recoveryEvents.Length > 0 || orphanedProviderState ? "StartupRecovery" : null,
                ReattachAttempted = reattachSucceeded ? true : null,
                ReattachSucceeded = reattachSucceeded ? true : null,
                OrphanedProviderState = orphanedProviderState,
                SessionMarkedFailedByRecovery = sessionMarkedFailedByRecovery,
                RecoveryEventTimestamp = latestRecoveryEvent?.Timestamp,
                RecoveryMessage = latestRecoveryEvent?.Message ?? (orphanedProviderState ? session.FailureReason : null)
            },
            Monitoring = new ExecutionMonitoringTransparency
            {
                ProviderProcessState = providerProcessState,
                ExitCode = providerExitCode,
                LastActivityAt = session.LastActivityAt,
                StaleActivity = IsStaleActivity(session),
                RetainedEventCount = session.Events.Count,
                FirstRetainedEventSequence = firstRetainedSequence,
                LastRetainedEventSequence = lastRetainedSequence,
                EventRetentionTrimmingDetected = firstRetainedSequence > 1,
                MonitoringWarnings = monitoringWarnings
            },
            HandoffProcessing = ExecutionHandoffProcessingTransparency.FromSession(session, providerExitCode)
        };
    }

    private static string DetermineProviderProcessState(ExecutionSession session, ExecutionEvent? providerExitEvent)
    {
        if (providerExitEvent is not null)
        {
            return "Exited";
        }

        if (session.State == ExecutionSessionState.Executing && session.ProviderProcessId is not null)
        {
            return "Running";
        }

        if (session.ProviderStartedAt is null)
        {
            return "NotStarted";
        }

        return "Unknown";
    }

    private static bool IsStaleActivity(ExecutionSession session)
    {
        return session.State == ExecutionSessionState.Executing &&
            session.LastActivityAt is not null &&
            DateTimeOffset.UtcNow - session.LastActivityAt.Value > TimeSpan.FromMinutes(10);
    }

    private static int? ExtractExitCode(string message)
    {
        Match match = ExitCodePattern.Match(message);
        return match.Success && int.TryParse(match.Groups[1].Value, out int exitCode)
            ? exitCode
            : null;
    }

    private static string[] BuildMonitoringWarnings(
        ExecutionSession session,
        string providerProcessState,
        int? exitCode,
        bool orphanedProviderState)
    {
        List<string> warnings = [];

        if (IsStaleActivity(session))
        {
            warnings.Add("Executing session has stale provider activity.");
        }

        if (session.State == ExecutionSessionState.Failed && !string.IsNullOrWhiteSpace(session.FailureReason))
        {
            warnings.Add(session.FailureReason);
        }

        if (orphanedProviderState)
        {
            warnings.Add("Provider process could not be reattached during startup recovery.");
        }

        if (string.Equals(providerProcessState, "Exited", StringComparison.Ordinal) && exitCode is not null and not 0)
        {
            warnings.Add($"Provider exited with non-zero code {exitCode}.");
        }

        return warnings.Distinct(StringComparer.Ordinal).ToArray();
    }
}

public sealed class ExecutionRecoveryTransparency
{
    public bool RecoveryRan { get; init; }

    public string? RecoveryTrigger { get; init; }

    public bool? ReattachAttempted { get; init; }

    public bool? ReattachSucceeded { get; init; }

    public bool OrphanedProviderState { get; init; }

    public bool SessionMarkedFailedByRecovery { get; init; }

    public DateTimeOffset? RecoveryEventTimestamp { get; init; }

    public string? RecoveryMessage { get; init; }
}

public sealed class ExecutionMonitoringTransparency
{
    public string ProviderProcessState { get; init; } = "Unknown";

    public int? ExitCode { get; init; }

    public DateTimeOffset? LastActivityAt { get; init; }

    public bool StaleActivity { get; init; }

    public int RetainedEventCount { get; init; }

    public long? FirstRetainedEventSequence { get; init; }

    public long? LastRetainedEventSequence { get; init; }

    public bool EventRetentionTrimmingDetected { get; init; }

    public IReadOnlyList<string> MonitoringWarnings { get; init; } = [];
}

public sealed class ExecutionHandoffProcessingTransparency
{
    public bool HandoffProduced { get; init; }

    public bool HandoffMissing { get; init; }

    public bool HandoffArchived { get; init; }

    public string? ArchivePath { get; init; }

    public int? ArchiveSequence { get; init; }

    public bool ArchiveFailed { get; init; }

    public bool HandoffValidated { get; init; }

    public string? ValidationFailure { get; init; }

    public ExecutionSessionState ResultingSessionState { get; init; } = ExecutionSessionState.Created;

    public RepositoryExecutionState ResultingRepositoryState { get; init; } = RepositoryExecutionState.Ready;

    public DateTimeOffset? ProcessedAt { get; init; }

    public bool ProviderFailureDistinctFromHandoffFailure { get; init; }

    public string? ProviderFailureReason { get; init; }

    public string? HandoffFailureReason { get; init; }

    public IReadOnlyList<string> Diagnostics { get; init; } = [];

    public static ExecutionHandoffProcessingTransparency FromSession(ExecutionSession session, int? providerExitCode)
    {
        if (session.HandoffProcessing is not ExecutionHandoffProcessing processing)
        {
            bool providerFailure = providerExitCode is not null and not 0;
            return new ExecutionHandoffProcessingTransparency
            {
                HandoffProduced = !string.IsNullOrWhiteSpace(session.HandoffPath),
                HandoffMissing = string.IsNullOrWhiteSpace(session.HandoffPath),
                HandoffArchived = false,
                ArchiveFailed = false,
                HandoffValidated = session.RepositoryState == RepositoryExecutionState.AwaitingAcceptance,
                ValidationFailure = session.State == ExecutionSessionState.Failed ? session.FailureReason : null,
                ResultingSessionState = session.State,
                ResultingRepositoryState = session.RepositoryState,
                ProviderFailureDistinctFromHandoffFailure = providerFailure &&
                    !IsKnownHandoffFailure(session.FailureReason),
                ProviderFailureReason = providerFailure ? session.FailureReason : null,
                HandoffFailureReason = IsKnownHandoffFailure(session.FailureReason) ? session.FailureReason : null,
                Diagnostics = ["NoHandoffProcessingRecord"]
            };
        }

        return new ExecutionHandoffProcessingTransparency
        {
            HandoffProduced = processing.HandoffProduced,
            HandoffMissing = processing.HandoffMissing,
            HandoffArchived = processing.HandoffArchived,
            ArchivePath = processing.ArchivePath,
            ArchiveSequence = processing.ArchiveSequence,
            ArchiveFailed = processing.ArchiveFailed,
            HandoffValidated = processing.HandoffValidated,
            ValidationFailure = processing.ValidationFailure,
            ResultingSessionState = processing.ResultingSessionState,
            ResultingRepositoryState = processing.ResultingRepositoryState,
            ProcessedAt = processing.ProcessedAt,
            ProviderFailureDistinctFromHandoffFailure = processing.ProviderFailureDistinctFromHandoffFailure,
            ProviderFailureReason = processing.ProviderFailureReason,
            HandoffFailureReason = processing.HandoffFailureReason,
            Diagnostics = BuildDiagnostics(processing)
        };
    }

    private static bool IsKnownHandoffFailure(string? failureReason)
    {
        return string.Equals(
                failureReason,
                HandoffService.MissingCurrentHandoffFailureReason,
                StringComparison.Ordinal) ||
            string.Equals(
                failureReason,
                HandoffService.ArchivePreviousHandoffFailureReason,
                StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> BuildDiagnostics(ExecutionHandoffProcessing processing)
    {
        List<string> diagnostics = [];

        if (processing.HandoffMissing)
        {
            diagnostics.Add("HandoffMissing");
        }

        if (processing.ArchiveFailed)
        {
            diagnostics.Add("HandoffArchiveFailed");
        }

        if (processing.HandoffArchived && processing.ArchivePath is not null)
        {
            diagnostics.Add($"PreviousHandoffArchived:{processing.ArchivePath}");
        }

        if (!processing.HandoffValidated && !string.IsNullOrWhiteSpace(processing.ValidationFailure))
        {
            diagnostics.Add($"HandoffValidationFailed:{processing.ValidationFailure}");
        }

        if (processing.ProviderFailureDistinctFromHandoffFailure)
        {
            diagnostics.Add("ProviderFailureDistinctFromHandoffFailure");
        }

        return diagnostics;
    }
}
