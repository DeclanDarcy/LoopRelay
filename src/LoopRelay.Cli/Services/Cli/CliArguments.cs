using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Resolution;

namespace LoopRelay.Cli.Services.Cli;

internal enum UnifiedCliCommandKind
{
    Run,
    Status,
    StorageInit,
    StorageImport,
    StorageExport,
    StorageSync,
    StorageVerify,
}

internal sealed record UnifiedCliCommand(
    UnifiedCliCommandKind Kind,
    IReadOnlyList<string> Arguments,
    InvocationModeKind? BoundedWorkflowMode = null)
{
    public bool RequiresStorageVerification =>
        Kind is UnifiedCliCommandKind.Run
            or UnifiedCliCommandKind.StorageInit
            or UnifiedCliCommandKind.StorageImport
            or UnifiedCliCommandKind.StorageExport
            or UnifiedCliCommandKind.StorageSync;
}

internal sealed record UnifiedCliInvocation(
    Repository Repository,
    WorkflowInvocation WorkflowInvocation,
    UnifiedCliCommand Command);

/// <summary>Parses the public LoopRelay CLI contract into repository, workflow mode, and command.</summary>
internal static class CliArguments
{
    public static bool TryParse(string[] args, out Repository repository, out string error)
    {
        if (!TryParse(args, out UnifiedCliInvocation invocation, out error))
        {
            repository = new Repository();
            return false;
        }

        repository = invocation.Repository;
        return true;
    }

    public static bool TryParse(
        string[] args,
        out UnifiedCliInvocation invocation,
        out string error,
        string? workingDirectory = null)
    {
        Repository repository = new();
        invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        string resolvedWorkingDirectory = workingDirectory is null
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(workingDirectory);

        string? repoArgument = null;
        bool forceEval = false;
        bool forceTraditional = false;
        var positional = new List<string>();
        for (int index = 0; index < args.Length; index++)
        {
            string arg = args[index];
            if (arg.Equals("--repo", StringComparison.Ordinal))
            {
                if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
                {
                    error = "Usage: looprelay [--repo <path>] [--eval|--traditional] [status|storage <init|import|export|sync|verify>|eval|traditional|plan|execute]";
                    return false;
                }

                repoArgument = args[++index];
                continue;
            }

            if (arg.Equals("--eval", StringComparison.Ordinal))
            {
                forceEval = true;
                continue;
            }

            if (arg.Equals("--traditional", StringComparison.Ordinal))
            {
                forceTraditional = true;
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unknown option: {arg}";
                return false;
            }

            positional.Add(arg);
        }

        if (forceEval && forceTraditional)
        {
            error = "--eval and --traditional cannot be used together.";
            return false;
        }

        string repositoryPath = repoArgument ?? resolvedWorkingDirectory;
        if (repoArgument is null &&
            positional.Count == 1 &&
            !IsCommand(positional[0]) &&
            (Directory.Exists(Path.GetFullPath(positional[0])) || IsPathLike(positional[0])))
        {
            repositoryPath = positional[0];
            positional.Clear();
        }

        repositoryPath = Path.GetFullPath(repositoryPath);
        if (!Directory.Exists(repositoryPath))
        {
            error = $"Repository directory does not exist: {repositoryPath}";
            return false;
        }

        if (!TryParseCommand(positional, out UnifiedCliCommand command, out error))
        {
            return false;
        }

        if (!TryResolveWorkflowInvocation(command, forceEval, forceTraditional, out WorkflowInvocation workflowInvocation, out error))
        {
            return false;
        }

        Repository parsedRepository = new()
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repositoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Path = repositoryPath,
        };

        invocation = new UnifiedCliInvocation(parsedRepository, workflowInvocation, command);
        error = string.Empty;
        return true;
    }

    private static bool TryParseCommand(
        IReadOnlyList<string> positional,
        out UnifiedCliCommand command,
        out string error)
    {
        command = new UnifiedCliCommand(UnifiedCliCommandKind.Run, []);
        error = string.Empty;
        if (positional.Count == 0)
        {
            return true;
        }

        string verb = positional[0];
        IReadOnlyList<string> rest = positional.Skip(1).ToArray();
        command = verb switch
        {
            "eval" => NoExtra(UnifiedCliCommandKind.Run, rest, InvocationModeKind.BoundedEval, out error),
            "traditional" => NoExtra(UnifiedCliCommandKind.Run, rest, InvocationModeKind.BoundedTraditional, out error),
            "plan" => NoExtra(UnifiedCliCommandKind.Run, rest, InvocationModeKind.BoundedPlan, out error),
            "execute" => NoExtra(UnifiedCliCommandKind.Run, rest, InvocationModeKind.BoundedExecute, out error),
            "status" => new UnifiedCliCommand(UnifiedCliCommandKind.Status, rest),
            "storage" => ParseStorage(rest, out error),
            _ => Unknown(verb, out error),
        };

        return string.IsNullOrEmpty(error);

        static UnifiedCliCommand NoExtra(
            UnifiedCliCommandKind kind,
            IReadOnlyList<string> rest,
            InvocationModeKind? boundedWorkflowMode,
            out string extraError)
        {
            if (rest.Count > 0)
            {
                extraError = $"Unexpected argument: {rest[0]}";
                return new UnifiedCliCommand(kind, [], boundedWorkflowMode);
            }

            extraError = string.Empty;
            return new UnifiedCliCommand(kind, [], boundedWorkflowMode);
        }

        static UnifiedCliCommand Unknown(string verb, out string unknownError)
        {
            unknownError = $"Unknown command: {verb}";
            return new UnifiedCliCommand(UnifiedCliCommandKind.Run, []);
        }
    }

    private static UnifiedCliCommand ParseStorage(
        IReadOnlyList<string> rest,
        out string error)
    {
        if (rest.Count == 0)
        {
            error = "Usage: looprelay storage <init|import|export|sync|verify>";
            return new UnifiedCliCommand(UnifiedCliCommandKind.Run, []);
        }

        UnifiedCliCommandKind? kind = rest[0] switch
        {
            "init" => UnifiedCliCommandKind.StorageInit,
            "import" => UnifiedCliCommandKind.StorageImport,
            "export" => UnifiedCliCommandKind.StorageExport,
            "sync" => UnifiedCliCommandKind.StorageSync,
            "verify" => UnifiedCliCommandKind.StorageVerify,
            _ => null,
        };
        if (kind is null)
        {
            error = $"Unknown storage command: {rest[0]}";
            return new UnifiedCliCommand(UnifiedCliCommandKind.Run, []);
        }

        error = string.Empty;
        return new UnifiedCliCommand(kind.Value, rest.Skip(1).ToArray());
    }

    private static bool TryResolveWorkflowInvocation(
        UnifiedCliCommand command,
        bool forceEval,
        bool forceTraditional,
        out WorkflowInvocation invocation,
        out string error)
    {
        invocation = new WorkflowInvocation(InvocationModeKind.DefaultChained);
        error = string.Empty;

        if (command.Kind != UnifiedCliCommandKind.Run)
        {
            invocation = new WorkflowInvocation(forceEval
                ? InvocationModeKind.ForcedEvalChain
                : forceTraditional
                    ? InvocationModeKind.ForcedTraditionalChain
                    : InvocationModeKind.DefaultChained);
            return true;
        }

        if (command.BoundedWorkflowMode is { } boundedMode)
        {
            if (forceEval && boundedMode != InvocationModeKind.BoundedEval)
            {
                error = "--eval can only be combined with the eval bounded workflow command.";
                return false;
            }

            if (forceTraditional && boundedMode != InvocationModeKind.BoundedTraditional)
            {
                error = "--traditional can only be combined with the traditional bounded workflow command.";
                return false;
            }

            invocation = new WorkflowInvocation(boundedMode);
            return true;
        }

        invocation = new WorkflowInvocation(forceEval
            ? InvocationModeKind.ForcedEvalChain
            : forceTraditional
                ? InvocationModeKind.ForcedTraditionalChain
                : InvocationModeKind.DefaultChained);
        return true;
    }

    private static bool IsCommand(string value) =>
        value is "eval"
            or "traditional"
            or "plan"
            or "execute"
            or "status"
            or "storage";

    private static bool IsPathLike(string value) =>
        Path.IsPathRooted(value) ||
        value.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
        value.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal) ||
        value.StartsWith(".", StringComparison.Ordinal);
}
