using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

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
        Cli.ExecutionDispositionStatus status = Enum.Parse<Cli.ExecutionDispositionStatus>(statusName);
        Cli.ExecutionDispositionCommand command = Enum.Parse<Cli.ExecutionDispositionCommand>(commandName);
        Cli.RoadmapExecutionOutcomeKind expectedOutcome = Enum.Parse<Cli.RoadmapExecutionOutcomeKind>(expectedOutcomeName);

        Cli.ExecutionDispositionValidationResult result = new Cli.ExecutionDispositionPolicy().Validate(
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
        Cli.ExecutionDispositionStatus status = Enum.Parse<Cli.ExecutionDispositionStatus>(statusName);
        Cli.ExecutionDispositionCommand command = Enum.Parse<Cli.ExecutionDispositionCommand>(commandName);

        Cli.ExecutionDispositionValidationResult result = new Cli.ExecutionDispositionPolicy().Validate(
            Disposition(status, command));

        Assert.False(result.IsValid);
        Assert.Null(result.Route);
        Assert.Contains("protocol violation", result.ViolationReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Cli.ExecutionDispositionProtocol.StatusText(status), result.ViolationReason, StringComparison.Ordinal);
        Assert.Contains(Cli.ExecutionDispositionProtocol.CommandText(command), result.ViolationReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Policy_rejects_any_non_routed_state_command_pair()
    {
        var policy = new Cli.ExecutionDispositionPolicy();
        HashSet<(Cli.ExecutionDispositionStatus Status, Cli.ExecutionDispositionCommand Command)> routedPairs = policy.AllRoutes
            .Select(route => (route.Status, route.Command))
            .ToHashSet();
        var contradictoryPairs = Cli.ExecutionDispositionProtocol.Statuses
            .SelectMany(
                status => Cli.ExecutionDispositionProtocol.Commands,
                (status, command) => (status, command))
            .Where(pair => !routedPairs.Contains(pair))
            .ToArray();

        Assert.NotEmpty(contradictoryPairs);
        foreach ((Cli.ExecutionDispositionStatus status, Cli.ExecutionDispositionCommand command) in contradictoryPairs)
        {
            Cli.ExecutionDispositionValidationResult result = policy.Validate(Disposition(status, command));

            Assert.False(result.IsValid);
        }
    }

    [Fact]
    public void Policy_rejects_unknown_typed_commands()
    {
        Cli.ExecutionDispositionValidationResult result = new Cli.ExecutionDispositionPolicy().Validate(
            Disposition(Cli.ExecutionDispositionStatus.ContinueRequired, (Cli.ExecutionDispositionCommand)999));

        Assert.False(result.IsValid);
        Assert.Contains("unsupported command", result.ViolationReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Policy_rejects_incomplete_route_tables()
    {
        Cli.ExecutionDispositionPolicy complete = new();
        IEnumerable<Cli.ExecutionDispositionRoute> incomplete = complete.AllRoutes
            .Where(route => route.Status != Cli.ExecutionDispositionStatus.ExecutionBlocked);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new Cli.ExecutionDispositionPolicy(incomplete));

        Assert.Contains("Execution Blocked", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ResolveExecutionBlocker", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Policy_rejects_future_routes_without_protocol_vocabulary()
    {
        Cli.ExecutionDispositionPolicy complete = new();
        IEnumerable<Cli.ExecutionDispositionRoute> extended = complete.AllRoutes.Append(new Cli.ExecutionDispositionRoute(
            Cli.ExecutionDispositionStatus.ContinueRequired,
            (Cli.ExecutionDispositionCommand)999,
            Cli.RoadmapExecutionOutcomeKind.ContinueRequired,
            Cli.RoadmapState.ExecutionLoop,
            "FutureExecutionCommand"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new Cli.ExecutionDispositionPolicy(extended));

        Assert.Contains("unknown command", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Protocol_vocabulary_and_policy_routes_are_synchronized()
    {
        Cli.ExecutionDispositionPolicy policy = new();

        Assert.Equal(
            Cli.ExecutionDispositionProtocol.Statuses.OrderBy(status => status),
            policy.AllRoutes.Select(route => route.Status).Distinct().OrderBy(status => status));
        Assert.Equal(
            Cli.ExecutionDispositionProtocol.Commands.OrderBy(command => command),
            policy.AllRoutes.Select(route => route.Command).Distinct().OrderBy(command => command));
        Assert.Equal(
            Cli.ExecutionDispositionProtocol.ValidPairDescriptions.Order(StringComparer.Ordinal),
            policy.AllRoutes
                .Select(route => $"{Cli.ExecutionDispositionProtocol.StatusText(route.Status)} -> {Cli.ExecutionDispositionProtocol.CommandText(route.Command)}")
                .Order(StringComparer.Ordinal));
    }

    private static Cli.ExecutionDisposition Disposition(
        Cli.ExecutionDispositionStatus status,
        Cli.ExecutionDispositionCommand command) =>
        new(status, "High", "Evidence.", command);
}
