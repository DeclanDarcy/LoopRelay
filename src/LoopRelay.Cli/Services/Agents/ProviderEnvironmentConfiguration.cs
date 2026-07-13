namespace LoopRelay.Cli.Services.Agents;

internal sealed record ProviderEnvironmentConfiguration(
    string CodexHome,
    string CodexHomeProvenance)
{
    public static ProviderEnvironmentConfiguration Resolve(
        Func<string, string?>? getEnvironmentVariable = null,
        Func<Environment.SpecialFolder, string>? getFolderPath = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        getFolderPath ??= Environment.GetFolderPath;
        string? configured = getEnvironmentVariable("CODEX_HOME");
        string home = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(getFolderPath(Environment.SpecialFolder.UserProfile), ".codex")
            : configured;
        if (!Path.IsPathFullyQualified(home))
            throw new InvalidOperationException("Resolved CODEX_HOME must be an absolute path.");
        return new(Path.GetFullPath(home), string.IsNullOrWhiteSpace(configured)
            ? "platform:user-profile/.codex" : "environment:CODEX_HOME");
    }
}
