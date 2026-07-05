using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class ExecutionDispositionProtocolTests
{
    [Theory]
    [InlineData("EpicComplete", "EvaluateEpicCompletionAndDrift", "EpicComplete")]
    [InlineData("ContinueRequired", "ContinueExecution", "ContinueRequired")]
    [InlineData("ExecutionBlocked", "ResolveExecutionBlocker", "ExecutionBlocked")]
    public void Policy_validates_supported_execution_protocol_pairs(
        string statusName,
        string commandName,
        string expectedOutcomeName)
    {
        ExecutionDispositionStatus status = Enum.Parse<ExecutionDispositionStatus>(statusName);
        ExecutionDispositionCommand command = Enum.Parse<ExecutionDispositionCommand>(commandName);
        RoadmapExecutionOutcomeKind expectedOutcome = Enum.Parse<RoadmapExecutionOutcomeKind>(expectedOutcomeName);

        ExecutionDispositionValidationResult result = new ExecutionDispositionPolicy().Validate(
            Disposition(status, command));

        Assert.True(result.IsValid);
        Assert.NotNull(result.Route);
        Assert.Equal(expectedOutcome, result.Route!.OutcomeKind);
        Assert.Equal(command, result.Route.Command);
    }

    [Theory]
    [InlineData("EpicComplete", "ContinueExecution")]
    [InlineData("ContinueRequired", "EvaluateEpicCompletionAndDrift")]
    [InlineData("ExecutionBlocked", "ContinueExecution")]
    public void Policy_rejects_contradictory_execution_protocol_pairs(
        string statusName,
        string commandName)
    {
        ExecutionDispositionStatus status = Enum.Parse<ExecutionDispositionStatus>(statusName);
        ExecutionDispositionCommand command = Enum.Parse<ExecutionDispositionCommand>(commandName);

        ExecutionDispositionValidationResult result = new ExecutionDispositionPolicy().Validate(
            Disposition(status, command));

        Assert.False(result.IsValid);
        Assert.Null(result.Route);
        Assert.Contains("protocol violation", result.ViolationReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ExecutionDispositionProtocol.StatusText(status), result.ViolationReason, StringComparison.Ordinal);
        Assert.Contains(ExecutionDispositionProtocol.CommandText(command), result.ViolationReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Policy_rejects_any_non_routed_state_command_pair()
    {
        var policy = new ExecutionDispositionPolicy();
        HashSet<(ExecutionDispositionStatus Status, ExecutionDispositionCommand Command)> routedPairs = policy.AllRoutes
            .Select(route => (route.Status, route.Command))
            .ToHashSet();
        var contradictoryPairs = ExecutionDispositionProtocol.Statuses
            .SelectMany(
                status => ExecutionDispositionProtocol.Commands,
                (status, command) => (status, command))
            .Where(pair => !routedPairs.Contains(pair))
            .ToArray();

        Assert.NotEmpty(contradictoryPairs);
        foreach ((ExecutionDispositionStatus status, ExecutionDispositionCommand command) in contradictoryPairs)
        {
            ExecutionDispositionValidationResult result = policy.Validate(Disposition(status, command));

            Assert.False(result.IsValid);
        }
    }

    [Fact]
    public void Policy_rejects_unknown_typed_commands()
    {
        ExecutionDispositionValidationResult result = new ExecutionDispositionPolicy().Validate(
            Disposition(ExecutionDispositionStatus.ContinueRequired, (ExecutionDispositionCommand)999));

        Assert.False(result.IsValid);
        Assert.Contains("unsupported command", result.ViolationReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Policy_rejects_incomplete_route_tables()
    {
        ExecutionDispositionPolicy complete = new();
        IEnumerable<ExecutionDispositionRoute> incomplete = complete.AllRoutes
            .Where(route => route.Status != ExecutionDispositionStatus.ExecutionBlocked);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new ExecutionDispositionPolicy(incomplete));

        Assert.Contains("Execution Blocked", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ResolveExecutionBlocker", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Policy_rejects_future_routes_without_protocol_vocabulary()
    {
        ExecutionDispositionPolicy complete = new();
        IEnumerable<ExecutionDispositionRoute> extended = complete.AllRoutes.Append(new ExecutionDispositionRoute(
            ExecutionDispositionStatus.ContinueRequired,
            (ExecutionDispositionCommand)999,
            RoadmapExecutionOutcomeKind.ContinueRequired,
            RoadmapState.ExecutionLoop,
            "FutureExecutionCommand"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new ExecutionDispositionPolicy(extended));

        Assert.Contains("unknown command", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Protocol_vocabulary_and_policy_routes_are_synchronized()
    {
        ExecutionDispositionPolicy policy = new();

        Assert.Equal(
            ExecutionDispositionProtocol.Statuses.OrderBy(status => status),
            policy.AllRoutes.Select(route => route.Status).Distinct().OrderBy(status => status));
        Assert.Equal(
            ExecutionDispositionProtocol.Commands.OrderBy(command => command),
            policy.AllRoutes.Select(route => route.Command).Distinct().OrderBy(command => command));
        Assert.Equal(
            ExecutionDispositionProtocol.ValidPairDescriptions.Order(StringComparer.Ordinal),
            policy.AllRoutes
                .Select(route => $"{ExecutionDispositionProtocol.StatusText(route.Status)} -> {ExecutionDispositionProtocol.CommandText(route.Command)}")
                .Order(StringComparer.Ordinal));
    }

    private static ExecutionDisposition Disposition(
        ExecutionDispositionStatus status,
        ExecutionDispositionCommand command) =>
        new(status, "High", "Evidence.", command);
}
