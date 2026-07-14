using LoopRelay.Application.Contracts;
using LoopRelay.Application.ReadModel;

namespace LoopRelay.Cli.Surface;

public sealed record ParsedCliRequest(string RepositoryPath, LoopRelayRequest Request);

public static class CliRequestParser
{
    public static bool TryParse(
        IReadOnlyList<string> args,
        out ParsedCliRequest parsed,
        out string error,
        string? workingDirectory = null)
    {
        string root = Path.GetFullPath(workingDirectory ?? Directory.GetCurrentDirectory());
        string? repository = null;
        bool forceEval = false;
        bool forceTraditional = false;
        bool interactive = false;
        var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
        var positional = new List<string>();
        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            if (argument == "--repo")
            {
                if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                    return Failure("Usage: --repo <path>", out parsed, out error);
                repository = args[index];
            }
            else if (argument == "--policy")
            {
                if (++index >= args.Count || !TryAssignment(args[index], out string key, out string value))
                    return Failure("Usage: --policy <key>=<value>", out parsed, out error);
                overrides[key] = value;
            }
            else if (argument == "--eval") forceEval = true;
            else if (argument == "--traditional") forceTraditional = true;
            else if (argument == "--interactive") interactive = true;
            else if (argument.StartsWith("--", StringComparison.Ordinal))
                return Failure($"Unknown option: {argument}", out parsed, out error);
            else positional.Add(argument);
        }
        if (forceEval && forceTraditional)
            return Failure("--eval and --traditional cannot be used together.", out parsed, out error);
        string repositoryPath = Path.GetFullPath(repository ?? root, root);
        if (!Directory.Exists(repositoryPath))
            return Failure($"Repository directory does not exist: {repositoryPath}", out parsed, out error);
        var context = new ApplicationRequestContext(
            ApplicationCorrelationId.New(),
            $"workspace-path:{repositoryPath}",
            repositoryPath,
            overrides,
            Interactive: interactive);
        if (!TryRequest(positional, context, forceEval, forceTraditional, out LoopRelayRequest request, out error))
        {
            parsed = null!;
            return false;
        }
        parsed = new ParsedCliRequest(repositoryPath, request);
        error = string.Empty;
        return true;
    }

    private static bool TryRequest(
        IReadOnlyList<string> values,
        ApplicationRequestContext context,
        bool forceEval,
        bool forceTraditional,
        out LoopRelayRequest request,
        out string error)
    {
        string verb = values.FirstOrDefault() ?? "run";
        string[] rest = values.Skip(1).ToArray();
        RunInvocationMode forced = forceEval ? RunInvocationMode.ForcedEval
            : forceTraditional ? RunInvocationMode.ForcedTraditional : RunInvocationMode.Default;
        LoopRelayRequest? candidate = verb switch
        {
            "run" when rest.Length == 0 => new RunWorkflowRequest(context, forced),
            "eval" when rest.Length == 0 => new RunWorkflowRequest(context, RunInvocationMode.BoundedWorkflow, "EvalRoadmap"),
            "traditional" when rest.Length == 0 => new RunWorkflowRequest(context, RunInvocationMode.BoundedWorkflow, "TraditionalRoadmap"),
            "plan" when rest.Length == 0 => new RunWorkflowRequest(context, RunInvocationMode.BoundedWorkflow, "Plan"),
            "execute" when rest.Length == 0 => new RunWorkflowRequest(context, RunInvocationMode.BoundedWorkflow, "Execute"),
            "status" when rest.Length == 0 => new CanonicalStatusRequest(context, forced),
            "storage" => Storage(context, rest),
            "import" => Import(context, rest),
            "recovery" => Recovery(context, rest),
            "interactions" => Interaction(context, rest),
            "completion" => Completion(context, rest),
            "capabilities" when rest.Length == 0 => new CapabilityDiagnosticsRequest(context),
            _ => null!,
        };
        if (candidate is not null)
        {
            request = candidate;
            error = string.Empty;
            return true;
        }
        request = null!;
        error = $"Unknown or invalid command: {string.Join(' ', values)}";
        return false;
    }

    private static LoopRelayRequest? Storage(ApplicationRequestContext context, IReadOnlyList<string> args) =>
        args.FirstOrDefault() switch
        {
            "verify" when args.Count == 1 => new StorageOperationRequest(context, StorageOperationKind.Verify),
            "init" when args.Count == 1 => new StorageOperationRequest(context, StorageOperationKind.Initialize),
            "migrate" when args.Count == 1 => new StorageOperationRequest(context, StorageOperationKind.Migrate),
            "export" when args.Count <= 2 => new StorageOperationRequest(context, StorageOperationKind.Export, args.Skip(1).FirstOrDefault()),
            "sync" when args.Count == 1 => new StorageOperationRequest(context, StorageOperationKind.Sync),
            _ => null,
        };

    private static LoopRelayRequest? Import(ApplicationRequestContext context, IReadOnlyList<string> args) =>
        Enum.TryParse(args.FirstOrDefault(), true, out ImportOperationKind operation) && args.Count <= 2
            ? new ImportOperationRequest(context, operation, args.Skip(1).FirstOrDefault()) : null;

    private static LoopRelayRequest? Recovery(ApplicationRequestContext context, IReadOnlyList<string> args) =>
        Enum.TryParse(args.FirstOrDefault(), true, out RecoveryOperationKind operation) && args.Count <= 2
            ? new RecoveryOperationRequest(context, operation, args.Skip(1).FirstOrDefault()) : null;

    private static LoopRelayRequest? Completion(ApplicationRequestContext context, IReadOnlyList<string> args) =>
        Enum.TryParse(args.FirstOrDefault(), true, out CompletionOperationKind operation) && args.Count <= 2
            ? new CompletionOperationRequest(context, operation, args.Skip(1).FirstOrDefault()) : null;

    private static LoopRelayRequest? Interaction(ApplicationRequestContext context, IReadOnlyList<string> args)
    {
        if (!Enum.TryParse(args.FirstOrDefault(), true, out InteractionOperationKind operation)) return null;
        bool valid = operation switch
        {
            InteractionOperationKind.List => args.Count == 1,
            InteractionOperationKind.Show => args.Count == 2,
            InteractionOperationKind.Respond => args.Count == 3,
            InteractionOperationKind.Cancel => args.Count is 2 or 3,
            _ => false,
        };
        return valid
            ? new InteractionOperationRequest(context, operation, args.Skip(1).FirstOrDefault(),
                args.Skip(2).FirstOrDefault())
            : null;
    }

    private static bool TryAssignment(string value, out string key, out string assigned)
    {
        int separator = value.IndexOf('=', StringComparison.Ordinal);
        key = separator > 0 ? value[..separator].Trim() : string.Empty;
        assigned = separator > 0 && separator < value.Length - 1 ? value[(separator + 1)..].Trim() : string.Empty;
        return key.Length > 0 && assigned.Length > 0;
    }

    private static bool Failure(string message, out ParsedCliRequest parsed, out string error)
    {
        parsed = null!;
        error = message;
        return false;
    }
}

public sealed record RenderedCliResult(IReadOnlyList<string> Output, IReadOnlyList<string> Errors);

public static class CliResultRenderer
{
    public static RenderedCliResult Render(LoopRelayResult result)
    {
        var output = new List<string>(result.Messages);
        output.AddRange(result.Warnings.Select(item => $"Warning: {item}"));
        output.AddRange(result.PendingEffects.Select(item => $"Pending effect: {item}"));
        output.AddRange(result.RequiredActions.Select(item => $"Required action: {item}"));
        if (result.Payload is CanonicalWorkspaceSnapshot snapshot)
            output.Add(CanonicalWorkspaceSnapshotRenderer.RenderText(snapshot));
        return new(output, result.Errors);
    }
}
