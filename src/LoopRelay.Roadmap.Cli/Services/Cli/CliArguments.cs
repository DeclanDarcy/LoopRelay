using LoopRelay.Core.Models.Repositories;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.Invocation;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Services.Persistence;

namespace LoopRelay.Roadmap.Cli.Services.Cli;

internal static class CliArguments
{
    public static bool TryParse(string[] args, out RoadmapCliInvocation invocation, out string error)
    {
        invocation = new RoadmapCliInvocation(
            RoadmapCliCommand.Run,
            new Repository(),
            RoadmapExecutionOptions.Default,
            RoadmapStorageOptions.Default);

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

        if (!TryParseOptions(
            args[optionStart..],
            command,
            out RoadmapExecutionOptions executionOptions,
            out RoadmapStorageOptions storageOptions,
            out error))
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
            executionOptions,
            storageOptions);
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
        command = value.ToLowerInvariant() switch
        {
            "storage-init" => RoadmapCliCommand.StorageInit,
            "storage-import" => RoadmapCliCommand.StorageImport,
            "storage-export" => RoadmapCliCommand.StorageExport,
            "storage-sync" => RoadmapCliCommand.StorageSync,
            "storage-verify" => RoadmapCliCommand.StorageVerify,
            _ => RoadmapCliCommand.Run,
        };
        if (command is RoadmapCliCommand.StorageInit
            or RoadmapCliCommand.StorageImport
            or RoadmapCliCommand.StorageExport
            or RoadmapCliCommand.StorageSync
            or RoadmapCliCommand.StorageVerify)
        {
            return true;
        }

        if (Enum.TryParse(value, ignoreCase: true, out command) &&
            Enum.IsDefined(command))
        {
            return true;
        }

        command = RoadmapCliCommand.Run;
        return false;
    }

    private static bool TryParseOptions(
        string[] args,
        RoadmapCliCommand command,
        out RoadmapExecutionOptions options,
        out RoadmapStorageOptions storageOptions,
        out string error)
    {
        options = RoadmapExecutionOptions.Default;
        storageOptions = RoadmapStorageOptions.Default;
        error = string.Empty;
        var domains = new SortedSet<WorkspaceSyncDomain>();
        bool forceImport = false;
        bool forceExport = false;
        bool fullRoundtrip = false;

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

            if (arg == "--domain")
            {
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                {
                    error = "Storage domain requires a non-empty value.";
                    return false;
                }

                foreach (string domainValue in args[++i].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!WorkspaceSyncDomains.TryParse(domainValue, out WorkspaceSyncDomain domain))
                    {
                        error = $"Unsupported storage domain: {domainValue}";
                        return false;
                    }

                    domains.Add(domain);
                }

                continue;
            }

            if (arg == "--force")
            {
                if (command == RoadmapCliCommand.StorageImport)
                {
                    forceImport = true;
                }
                else if (command == RoadmapCliCommand.StorageExport)
                {
                    forceExport = true;
                }
                else
                {
                    forceImport = true;
                    forceExport = true;
                }

                continue;
            }

            if (arg == "--force-import")
            {
                forceImport = true;
                continue;
            }

            if (arg == "--force-export")
            {
                forceExport = true;
                continue;
            }

            if (arg == "--full-roundtrip")
            {
                fullRoundtrip = true;
                continue;
            }

            error = $"Unexpected argument: {arg}";
            return false;
        }

        storageOptions = new RoadmapStorageOptions(
            domains.Count == 0 ? null : domains,
            forceImport,
            forceExport,
            fullRoundtrip);
        return true;
    }

    private static string Usage() =>
        "Usage: LoopRelay.Roadmap.Cli [status|run|unblock|storage-init|storage-import|storage-export|storage-sync|storage-verify] <REPO_DIR> [--elevated REASON] [--domain DOMAIN] [--force|--force-import|--force-export] [--full-roundtrip]  (REPO_DIR is required)";
}
