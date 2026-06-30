using CommandCenter.Core.Repositories;

namespace CommandCenter.Cli;

/// <summary>Parses and validates the single REPO_DIR positional argument into a <see cref="Repository"/>.</summary>
internal static class CliArguments
{
    public static bool TryParse(string[] args, out Repository repository, out string error)
    {
        repository = new Repository();

        if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            error = "Usage: CommandCenter.CLI <REPO_DIR>  (REPO_DIR is required)";
            return false;
        }

        string path = Path.GetFullPath(args[0]);
        if (!Directory.Exists(path))
        {
            error = $"Repository directory does not exist: {path}";
            return false;
        }

        repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Path = path,
        };
        error = string.Empty;
        return true;
    }
}
