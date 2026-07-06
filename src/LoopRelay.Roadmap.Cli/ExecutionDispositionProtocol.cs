namespace LoopRelay.Roadmap.Cli;

internal static class ExecutionDispositionProtocol
{
    private static readonly IReadOnlyDictionary<string, ExecutionDispositionStatus> StatusesByText =
        new Dictionary<string, ExecutionDispositionStatus>(StringComparer.Ordinal)
        {
            ["Epic Complete"] = ExecutionDispositionStatus.EpicComplete,
            ["Continue Required"] = ExecutionDispositionStatus.ContinueRequired,
            ["Execution Blocked"] = ExecutionDispositionStatus.ExecutionBlocked,
        };

    private static readonly IReadOnlyDictionary<string, ExecutionDispositionCommand> CommandsByText =
        new Dictionary<string, ExecutionDispositionCommand>(StringComparer.Ordinal)
        {
            ["EvaluateEpicCompletionAndDrift"] = ExecutionDispositionCommand.EvaluateEpicCompletionAndDrift,
            ["ContinueExecution"] = ExecutionDispositionCommand.ContinueExecution,
            ["ResolveExecutionBlocker"] = ExecutionDispositionCommand.ResolveExecutionBlocker,
        };

    public static IReadOnlyList<ExecutionDispositionStatus> Statuses =>
    [
        ExecutionDispositionStatus.EpicComplete,
        ExecutionDispositionStatus.ContinueRequired,
        ExecutionDispositionStatus.ExecutionBlocked,
    ];

    public static IReadOnlyList<ExecutionDispositionCommand> Commands =>
    [
        ExecutionDispositionCommand.EvaluateEpicCompletionAndDrift,
        ExecutionDispositionCommand.ContinueExecution,
        ExecutionDispositionCommand.ResolveExecutionBlocker,
    ];

    public static IReadOnlyList<ExecutionDispositionRoute> Routes =>
    [
        new(
            ExecutionDispositionStatus.EpicComplete,
            ExecutionDispositionCommand.EvaluateEpicCompletionAndDrift,
            RoadmapExecutionOutcomeKind.EpicComplete,
            RoadmapState.EpicCompletionDetected,
            CommandText(ExecutionDispositionCommand.EvaluateEpicCompletionAndDrift)),
        new(
            ExecutionDispositionStatus.ContinueRequired,
            ExecutionDispositionCommand.ContinueExecution,
            RoadmapExecutionOutcomeKind.ContinueRequired,
            RoadmapState.ExecutionLoop,
            CommandText(ExecutionDispositionCommand.ContinueExecution)),
        new(
            ExecutionDispositionStatus.ExecutionBlocked,
            ExecutionDispositionCommand.ResolveExecutionBlocker,
            RoadmapExecutionOutcomeKind.ExecutionBlocked,
            RoadmapState.ExecutionBlocked,
            CommandText(ExecutionDispositionCommand.ResolveExecutionBlocker)),
    ];

    public static string StatusOptionsText => string.Join(" OR ", Statuses.Select(StatusText));

    public static string CommandOptionsText => string.Join(" OR ", Commands.Select(CommandText));

    public static IReadOnlyList<string> ValidPairDescriptions =>
        Routes.Select(route => $"{StatusText(route.Status)} -> {CommandText(route.Command)}").ToArray();

    public static bool TryParseStatus(string value, out ExecutionDispositionStatus status) =>
        StatusesByText.TryGetValue(value, out status);

    public static bool TryParseCommand(string value, out ExecutionDispositionCommand command) =>
        CommandsByText.TryGetValue(value, out command);

    public static string StatusText(ExecutionDispositionStatus status) =>
        StatusesByText.SingleOrDefault(pair => pair.Value == status).Key ?? status.ToString();

    public static string CommandText(ExecutionDispositionCommand command) =>
        CommandsByText.SingleOrDefault(pair => pair.Value == command).Key ?? command.ToString();
}

internal sealed class ExecutionDispositionPolicy
{
    private readonly IReadOnlyDictionary<(ExecutionDispositionStatus Status, ExecutionDispositionCommand Command), ExecutionDispositionRoute> routes;

    public ExecutionDispositionPolicy()
        : this(ExecutionDispositionProtocol.Routes)
    {
    }

    public ExecutionDispositionPolicy(IEnumerable<ExecutionDispositionRoute> routes)
    {
        ExecutionDispositionRoute[] materialized = routes.ToArray();
        if (materialized.Length == 0)
        {
            throw new InvalidOperationException("Execution disposition protocol policy must define at least one route.");
        }

        foreach (ExecutionDispositionRoute route in materialized)
        {
            if (!Enum.IsDefined(route.Status))
            {
                throw new InvalidOperationException($"Execution disposition protocol policy includes unknown status `{route.Status}`.");
            }

            if (!Enum.IsDefined(route.Command))
            {
                throw new InvalidOperationException($"Execution disposition protocol policy includes unknown command `{route.Command}`.");
            }

            if (!Enum.IsDefined(route.OutcomeKind))
            {
                throw new InvalidOperationException($"Execution disposition protocol policy includes unknown outcome `{route.OutcomeKind}`.");
            }
        }

        ExecutionDispositionRoute? duplicate = materialized
            .GroupBy(route => (route.Status, route.Command))
            .Where(group => group.Count() > 1)
            .Select(group => group.First())
            .FirstOrDefault();
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                "Execution disposition protocol policy contains a duplicate route for " +
                $"`{ExecutionDispositionProtocol.StatusText(duplicate.Status)}` and `{ExecutionDispositionProtocol.CommandText(duplicate.Command)}`.");
        }

        string[] missingStatuses = ExecutionDispositionProtocol.Statuses
            .Except(materialized.Select(route => route.Status))
            .Select(ExecutionDispositionProtocol.StatusText)
            .ToArray();
        string[] missingCommands = ExecutionDispositionProtocol.Commands
            .Except(materialized.Select(route => route.Command))
            .Select(ExecutionDispositionProtocol.CommandText)
            .ToArray();
        if (missingStatuses.Length > 0 || missingCommands.Length > 0)
        {
            string statusMessage = missingStatuses.Length == 0 ? "none" : string.Join(", ", missingStatuses);
            string commandMessage = missingCommands.Length == 0 ? "none" : string.Join(", ", missingCommands);
            throw new InvalidOperationException(
                "Execution disposition protocol policy is incomplete. " +
                $"Missing statuses: {statusMessage}. Missing commands: {commandMessage}.");
        }

        this.routes = materialized.ToDictionary(route => (route.Status, route.Command));
    }

    public IReadOnlyList<ExecutionDispositionRoute> AllRoutes => routes.Values.ToArray();

    public ExecutionDispositionValidationResult Validate(ExecutionDisposition disposition)
    {
        if (!Enum.IsDefined(disposition.Status))
        {
            return ExecutionDispositionValidationResult.Invalid(
                disposition,
                $"Execution disposition protocol includes unsupported status `{disposition.Status}`.");
        }

        if (!Enum.IsDefined(disposition.NextStep))
        {
            return ExecutionDispositionValidationResult.Invalid(
                disposition,
                $"Execution disposition protocol includes unsupported command `{disposition.NextStep}`.");
        }

        if (routes.TryGetValue((disposition.Status, disposition.NextStep), out ExecutionDispositionRoute? route))
        {
            return ExecutionDispositionValidationResult.Valid(disposition, route);
        }

        string expectedCommands = string.Join(
            " or ",
            routes.Values
                .Where(candidate => candidate.Status == disposition.Status)
                .Select(candidate => $"`{ExecutionDispositionProtocol.CommandText(candidate.Command)}`"));
        if (string.IsNullOrWhiteSpace(expectedCommands))
        {
            expectedCommands = "no command";
        }

        return ExecutionDispositionValidationResult.Invalid(
            disposition,
            "Execution disposition protocol violation: " +
            $"state `{disposition.StatusText}` is not valid with command `{disposition.NextStepText}`. " +
            $"Expected {expectedCommands}.");
    }
}

internal sealed record ExecutionDispositionRoute(
    ExecutionDispositionStatus Status,
    ExecutionDispositionCommand Command,
    RoadmapExecutionOutcomeKind OutcomeKind,
    RoadmapState TargetState,
    string WorkflowTransition);

internal sealed record ExecutionDispositionValidationResult(
    bool IsValid,
    ExecutionDisposition Disposition,
    ExecutionDispositionRoute? Route,
    string? ViolationReason,
    string RequiredRecoveryPath)
{
    public static ExecutionDispositionValidationResult Valid(
        ExecutionDisposition disposition,
        ExecutionDispositionRoute route) =>
        new(true, disposition, route, null, route.WorkflowTransition);

    public static ExecutionDispositionValidationResult Invalid(
        ExecutionDisposition disposition,
        string violationReason) =>
        new(
            false,
            disposition,
            null,
            violationReason,
            "Review the raw execution output, correct the Execution Disposition to a valid protocol pair, and rerun the roadmap CLI.");
}

internal sealed record ExecutionDisposition(
    ExecutionDispositionStatus Status,
    string Confidence,
    string EvidenceSummary,
    ExecutionDispositionCommand NextStep)
{
    public string StatusText => ExecutionDispositionProtocol.StatusText(Status);

    public string NextStepText => ExecutionDispositionProtocol.CommandText(NextStep);
}

internal enum ExecutionDispositionStatus
{
    EpicComplete,
    ContinueRequired,
    ExecutionBlocked,
}

internal enum ExecutionDispositionCommand
{
    EvaluateEpicCompletionAndDrift,
    ContinueExecution,
    ResolveExecutionBlocker,
}
