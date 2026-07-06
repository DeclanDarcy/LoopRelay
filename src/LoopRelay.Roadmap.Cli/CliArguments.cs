using LoopRelay.Core.Repositories;

namespace LoopRelay.Roadmap.Cli;

internal static class CliArguments
{
    public static bool TryParse(string[] args, out RoadmapCliInvocation invocation, out string error)
    {
        invocation = new RoadmapCliInvocation(RoadmapCliCommand.Run, new Repository());

        if (args.Length < 1)
        {
            error = Usage();
            return false;
        }

        RoadmapCliCommand command = RoadmapCliCommand.Run;
        string? repoPath = null;

        if (TryParseCommand(args[0], out RoadmapCliCommand leadingCommand))
        {
            command = leadingCommand;
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                error = Usage();
                return false;
            }

            repoPath = args[1];
            if (args.Length > 2)
            {
                error = $"Unexpected argument: {args[2]}";
                return false;
            }
        }
        else
        {
            repoPath = args[0];
            if (args.Length > 1)
            {
                if (!TryParseCommand(args[1], out command))
                {
                    error = $"Unsupported roadmap command: {args[1]}";
                    return false;
                }

                if (args.Length > 2)
                {
                    error = $"Unexpected argument: {args[2]}";
                    return false;
                }
            }
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
            });
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

    private static string Usage() =>
        "Usage: LoopRelay.Roadmap.Cli [status|run|unblock] <REPO_DIR>  (REPO_DIR is required)";
}

internal sealed record RoadmapCliInvocation(RoadmapCliCommand Command, Repository Repository);

internal enum RoadmapCliCommand
{
    Status,
    Run,
    Unblock,
}
