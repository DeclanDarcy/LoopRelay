namespace LoopRelay.Roadmap.Cli;

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
