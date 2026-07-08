using System.Text.Json;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Models;

namespace LoopRelay.Orchestration.Services;

/// <summary>
/// Persists the CLI loop's decision-session resume state at <c>{repo}/.LoopRelay/decision-session.json</c>.
/// Fail-open in the telemetry sense: no read/write/clear failure may ever break a turn or the loop — failures
/// surface only through <paramref name="onWarning"/> (each CLI passes its console's Warn; ILoopConsole is
/// internal and duplicated per CLI, so this shared type takes a callback instead). Creating the directory also
/// drops a self-ignoring <c>.LoopRelay/.gitignore</c> (<c>*</c>): CommitGate and WorkingTreeChangeDetector
/// exclude only <c>.agents</c>, so an un-ignored state file would read as a real working-tree change
/// (corrupting the no-changes/stall gates) and be committed into the target repo.
/// </summary>
public sealed class FileDecisionSessionResumeStore(Repository repository, Action<string>? onWarning = null)
    : IDecisionSessionResumeStore
{
    public const string DirectoryName = ".LoopRelay";
    public const string FileName = "decision-session.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private string DirectoryPath => Path.Combine(repository.Path, DirectoryName);

    private string FilePath => Path.Combine(DirectoryPath, FileName);

    public async Task<DecisionSessionResumeState?> ReadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            string json = await File.ReadAllTextAsync(FilePath, cancellationToken);
            DecisionSessionResumeState? state = JsonSerializer.Deserialize<DecisionSessionResumeState>(json, Json);
            if (state is null
                || state.SchemaVersion != DecisionSessionResumeState.CurrentSchemaVersion
                || string.IsNullOrWhiteSpace(state.ThreadId))
            {
                onWarning?.Invoke(
                    $"Ignoring unusable decision-session resume state at {FilePath} (schema/content mismatch) — deleting it.");
                File.Delete(FilePath);
                return null;
            }

            return state;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            onWarning?.Invoke($"Could not read decision-session resume state at {FilePath}: {ex.Message} — deleting it.");
            TryDelete();
            return null;
        }
    }

    public async Task WriteAsync(DecisionSessionResumeState state, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            EnsureSelfIgnore();
            string json = JsonSerializer.Serialize(state with { SavedAtUtc = DateTimeOffset.UtcNow }, Json);
            await File.WriteAllTextAsync(FilePath, json, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            onWarning?.Invoke($"Could not persist decision-session resume state at {FilePath}: {ex.Message}");
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        TryDelete();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ensures <c>{repo}/.LoopRelay</c> exists and is self-ignoring WITHOUT writing any state. Called
    /// once at loop-CLI startup: the telemetry sink writes <c>.LoopRelay/telemetry</c> on the first
    /// execution turn, which can precede the first decision persist — without this, an un-gitignored target
    /// repo would commit the ledger and corrupt the no-changes/stall gates before WriteAsync ever runs.
    /// Fail-open like every other store operation.
    /// </summary>
    public void EnsureDirectoryProtection()
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            EnsureSelfIgnore();
        }
        catch (Exception ex)
        {
            onWarning?.Invoke($"Could not protect {DirectoryPath}: {ex.Message}");
        }
    }

    private void TryDelete()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch (Exception ex)
        {
            onWarning?.Invoke($"Could not delete decision-session resume state at {FilePath}: {ex.Message}");
        }
    }

    // `*` inside .LoopRelay/.gitignore ignores the whole directory (including the .gitignore itself),
    // making it self-ignoring regardless of the target repo's root .gitignore. Never overwrite an existing
    // file — an operator may have customized it.
    private void EnsureSelfIgnore()
    {
        string gitignore = Path.Combine(DirectoryPath, ".gitignore");
        if (!File.Exists(gitignore))
        {
            File.WriteAllText(gitignore, "*\n");
        }
    }
}
