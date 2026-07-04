using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Models;

namespace CommandCenter.Orchestration.Services;

/// <summary>
/// Persists the CLI loop's decision-session resume state at <c>{repo}/.commandcenter/decision-session.json</c>.
/// Fail-open in the telemetry sense: no read/write/clear failure may ever break a turn or the loop — failures
/// surface only through <paramref name="onWarning"/> (each CLI passes its console's Warn; ILoopConsole is
/// internal and duplicated per CLI, so this shared type takes a callback instead). Creating the directory also
/// drops a self-ignoring <c>.commandcenter/.gitignore</c> (<c>*</c>): CommitGate and WorkingTreeChangeDetector
/// exclude only <c>.agents</c>, so an un-ignored state file would read as a real working-tree change
/// (corrupting the no-changes/stall gates) and be committed into the target repo.
/// </summary>
public sealed class FileDecisionSessionResumeStore(Repository repository, Action<string>? onWarning = null)
    : IDecisionSessionResumeStore
{
    public const string DirectoryName = ".commandcenter";
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

    // `*` inside .commandcenter/.gitignore ignores the whole directory (including the .gitignore itself),
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

/// <summary>No-op store: nothing is persisted and nothing is ever found (default for tests/compositions
/// that do not opt into resume).</summary>
public sealed class NullDecisionSessionResumeStore : IDecisionSessionResumeStore
{
    public Task<DecisionSessionResumeState?> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<DecisionSessionResumeState?>(null);

    public Task WriteAsync(DecisionSessionResumeState state, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
