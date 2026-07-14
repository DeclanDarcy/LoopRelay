using System.Security.Cryptography;
using System.Text;

namespace LoopRelay.Certification;

public static class FixtureComposer
{
    public static ComposedCaseIdentity ValidateAndIdentify(
        FixtureRepository repository,
        FixtureScenario scenario)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository.Identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository.Version);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario.Identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario.Version);

        string[] overlayIds = scenario.Overlays.Select(item => item.Identity).ToArray();
        if (overlayIds.Distinct(StringComparer.Ordinal).Count() != overlayIds.Length)
        {
            throw new InvalidOperationException("Scenario overlay identities must be unique.");
        }

        foreach (ScenarioOverlay overlay in scenario.Overlays)
        {
            foreach (string requirement in overlay.Requires ?? [])
            {
                if (!overlayIds.Contains(requirement, StringComparer.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Overlay '{overlay.Identity}' requires missing overlay '{requirement}'.");
                }
            }

            string? conflict = (overlay.IncompatibleWith ?? [])
                .FirstOrDefault(item => overlayIds.Contains(item, StringComparer.Ordinal));
            if (conflict is not null)
            {
                throw new InvalidOperationException(
                    $"Overlay '{overlay.Identity}' is incompatible with '{conflict}'.");
            }
        }

        foreach (CaseAuthority authority in Enum.GetValues<CaseAuthority>().Where(value => value != CaseAuthority.None))
        {
            ScenarioOverlay[] owners = scenario.Overlays
                .Where(overlay => overlay.Authorities.HasFlag(authority))
                .OrderByDescending(overlay => overlay.Precedence)
                .ToArray();
            if (owners.Length > 1 && owners[0].Precedence == owners[1].Precedence)
            {
                throw new InvalidOperationException(
                    $"Authority '{authority}' has ambiguous owners '{owners[0].Identity}' and '{owners[1].Identity}'.");
            }
        }

        foreach (FixtureFile file in repository.Files)
        {
            ValidateRelativePath(file.Path);
        }

        string digestMaterial = string.Join("\n", new[]
        {
            repository.Identity,
            repository.Version,
            scenario.Identity,
            scenario.Version,
            string.Join("\n", repository.Files.OrderBy(file => file.Path, StringComparer.Ordinal)
                .Select(file => $"{file.Path}\0{file.Content}")),
            string.Join("\n", scenario.Overlays.OrderBy(overlay => overlay.Identity, StringComparer.Ordinal)
                .Select(overlay => $"{overlay.Identity}\0{overlay.Version}\0{overlay.Authorities}\0{overlay.Precedence}")),
        });

        return new ComposedCaseIdentity(
            repository.Identity,
            repository.Version,
            scenario.Identity,
            scenario.Version,
            HashText(digestMaterial));
    }

    public static async Task MaterializeAsync(
        FixtureRepository repository,
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(repositoryPath);
        foreach (FixtureFile file in repository.Files)
        {
            ValidateRelativePath(file.Path);
            string destination = Path.GetFullPath(Path.Combine(repositoryPath, file.Path));
            EnsureWithin(repositoryPath, destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await File.WriteAllTextAsync(
                destination,
                file.Content.Replace("\r\n", "\n", StringComparison.Ordinal),
                new UTF8Encoding(false),
                cancellationToken);
        }
    }

    internal static void ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
        {
            throw new InvalidOperationException($"Fixture path must be relative: '{path}'.");
        }

        string normalized = path.Replace('\\', '/');
        if (normalized.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidOperationException($"Fixture path escapes or is not canonical: '{path}'.");
        }
    }

    internal static void EnsureWithin(string root, string candidate)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullCandidate = Path.GetFullPath(candidate);
        if (!fullCandidate.StartsWith(fullRoot, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Path '{candidate}' escapes declared authority '{root}'.");
        }
    }

    internal static string HashText(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public static class FileObserver
{
    public static IReadOnlyList<FileObservation> Observe(string root)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new FileInfo(path))
            .Select(file => new FileObservation(
                Path.GetRelativePath(root, file.FullName).Replace('\\', '/'),
                file.Length,
                HashFile(file.FullName)))
            .ToArray();
    }

    public static string Digest(IReadOnlyList<FileObservation> files) =>
        FixtureComposer.HashText(string.Join("\n", files.Select(file =>
            $"{file.Path}\0{file.Length}\0{file.Sha256}")));

    public static IReadOnlyList<FileObservation> Difference(
        IReadOnlyList<FileObservation> before,
        IReadOnlyList<FileObservation> after)
    {
        Dictionary<string, FileObservation> baseline = before.ToDictionary(item => item.Path, StringComparer.Ordinal);
        return after.Where(item => !baseline.TryGetValue(item.Path, out FileObservation? prior) || prior != item)
            .Concat(before.Where(item => after.All(candidate => candidate.Path != item.Path)))
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
