using LoopRelay.Core.Models.Repositories;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.Invocation;
using LoopRelay.Roadmap.Cli.Primitives.State;

namespace LoopRelay.Roadmap.Cli.Services.Cli;

internal static class CliArguments
{
    public static bool TryParse(string[] args, out RoadmapCliInvocation invocation, out string error)
    {
        invocation = new RoadmapCliInvocation(RoadmapCliCommand.Run, new Repository(), RoadmapExecutionOptions.Default);

        if (args.Length < 1)
        {
            error = Usage();
            return false;
        }

        RoadmapCliCommand command = RoadmapCliCommand.Run;
        string? repoPath = null;
        int optionStart;

        if (TryParseCommand(args[0], out RoadmapCliCommand leadingCommand))
        {
            command = leadingCommand;
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                error = Usage();
                return false;
            }

            repoPath = args[1];
            optionStart = 2;
        }
        else
        {
            repoPath = args[0];
            optionStart = 1;

            if (args.Length > 1 && !args[1].StartsWith("--", StringComparison.Ordinal))
            {
                if (!TryParseCommand(args[1], out command))
                {
                    error = $"Unsupported roadmap command: {args[1]}";
                    return false;
                }

                optionStart = 2;
            }
        }

        if (!TryParseExecutionOptions(args[optionStart..], out RoadmapExecutionOptions executionOptions, out error))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(repoPath))
        {
            error = Usage();
            return false;
        }

        string path = Path.GetFullPath(repoPath);
        if (!Directory.Exists(path))
        {
            error = $"Repository directory does not exist: {path}";
            return false;
        }

        invocation = new RoadmapCliInvocation(
            command,
            new Repository
            {
                Id = Guid.NewGuid(),
                Name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                Path = path,
            },
            executionOptions);
        error = string.Empty;
        return true;
    }

    public static bool TryParse(string[] args, out Repository repository, out string error)
    {
        bool parsed = TryParse(args, out RoadmapCliInvocation invocation, out error);
        repository = invocation.Repository;
        return parsed;
    }

    private static bool TryParseCommand(string value, out RoadmapCliCommand command)
    {
        if (Enum.TryParse(value, ignoreCase: true, out command) &&
            Enum.IsDefined(command))
        {
            return true;
        }

        command = RoadmapCliCommand.Run;
        return false;
    }

    private static bool TryParseExecutionOptions(
        string[] args,
        out RoadmapExecutionOptions options,
        out string error)
    {
        options = RoadmapExecutionOptions.Default;
        error = string.Empty;

        if (args.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg is "--elevated" or "--execution-elevated")
            {
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                {
                    error = "Elevated roadmap execution requires a non-empty reason.";
                    return false;
                }

                options = RoadmapExecutionOptions.Elevated(args[++i]);
                continue;
            }

            error = $"Unexpected argument: {arg}";
            return false;
        }

        return true;
    }

    private static string Usage() =>
        "Usage: LoopRelay.Roadmap.Cli [status|run|unblock] <REPO_DIR> [--elevated REASON]  (REPO_DIR is required)";
}
