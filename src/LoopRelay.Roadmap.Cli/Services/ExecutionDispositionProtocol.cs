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
