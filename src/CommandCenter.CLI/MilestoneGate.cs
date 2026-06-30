using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;

namespace CommandCenter.Cli;

/// <summary>
/// LoopStart epic-complete gate. Aggregates GitHub task-list checkboxes (parsed with the canonical
/// RepositoryProjectionService.CountCheckboxes rule) across .agents/plan.md (if present) and every
/// .agents/milestones/m*.md. The epic is complete only when at least one checkbox exists across them
/// and every checkbox is checked. Files with zero checkboxes contribute nothing and never block.
///
/// The CLI runs this gate once per loop iteration, so during the long middle of an epic the same
/// milestone file sits with an unchecked box across many calls. To avoid re-reading and re-parsing
/// every file each time, the gate remembers each parsed-incomplete file together with the last-write
/// time it observed. On the next call it FIRST checks those remembered files: if any is unchanged
/// since we parsed it, it still has an unchecked box, so the aggregate is still incomplete and the
/// gate returns false WITHOUT reading or listing anything. This short-circuit only ever yields false
/// — any single unchecked box keeps the aggregate false regardless of files completed or appearing
/// elsewhere — so it can never wrongly report completion. When no remembered file is both present and
/// unchanged (first call, any tracked file modified/removed, or no timestamps available at all), the
/// gate falls back to the full parse, behaving exactly as it did before the optimization.
///
/// Known limitation: the short-circuit keys on last-write time, so a file that changes without its
/// last-write time advancing — due to coarse filesystem timestamp granularity or timestamp-preserving
/// tooling such as restore-from-backup — could be skipped on a re-parse. This is safe because the
/// gate ONLY ever short-circuits to false, so it can never wrongly report completion; the worst case
/// is one extra loop iteration before the updated content is detected.
/// </summary>
internal sealed class MilestoneGate
{
    private readonly IArtifactStore store;
    private readonly Repository repository;
    private readonly Func<string, DateTime?> lastWriteTime;

    /// <summary>resolved-path -> last-write time observed when we last parsed it AND found >=1 unchecked box.</summary>
    private readonly Dictionary<string, DateTime> incomplete = new(StringComparer.OrdinalIgnoreCase);

    public MilestoneGate(IArtifactStore store, Repository repository, Func<string, DateTime?>? lastWriteTime = null)
    {
        this.store = store;
        this.repository = repository;
        // The CLI always runs FileSystemArtifactStore over real paths, so default to the filesystem.
        // File.GetLastWriteTimeUtc returns a 1601 sentinel (not an exception) for a missing file, so the
        // File.Exists guard is REQUIRED: surface null (meaning unknown) for an absent file.
        this.lastWriteTime = lastWriteTime
            ?? (path => File.Exists(path) ? File.GetLastWriteTimeUtc(path) : (DateTime?)null);
    }

    public async Task<bool> IsEpicCompleteAsync()
    {
        // SHORT-CIRCUIT: a remembered incomplete file unchanged since we parsed it still has an unchecked
        // box, so the aggregate is still incomplete. Sound regardless of files completed/appearing elsewhere
        // — any single unchecked box keeps the aggregate false. We ONLY ever short-circuit to false.
        foreach ((string path, DateTime parsedAt) in incomplete)
        {
            if (lastWriteTime(path) is { } now && now == parsedAt)
            {
                return false;
            }
        }

        // CACHE MISS: re-derive from scratch with the same full parse as before the optimization.
        incomplete.Clear();
        int total = 0;
        int completed = 0;

        // .agents/plan.md (if present) contributes its checkboxes alongside the milestones.
        string planPath = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Plan);
        DateTime? planStamp = lastWriteTime(planPath);
        string? plan = await store.ReadAsync(planPath);
        if (plan is not null)
        {
            Accumulate(planPath, planStamp, plan, ref total, ref completed);
        }

        string dir = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.MilestonesDirectory);
        IReadOnlyList<string> files = await store.ListAsync(dir, OrchestrationArtifactPaths.MilestoneSearchPattern);
        foreach (string file in files)
        {
            // Capture the stamp BEFORE the read so a write racing the read records an older stamp and forces a
            // re-parse next call (never a stale skip). ?? string.Empty tolerates a listed-then-deleted file.
            DateTime? stamp = lastWriteTime(file);
            string content = await store.ReadAsync(file) ?? string.Empty;
            Accumulate(file, stamp, content, ref total, ref completed);
        }

        return total > 0 && completed == total;
    }

    private void Accumulate(string path, DateTime? stamp, string content, ref int total, ref int completed)
    {
        (int t, int c) = CountCheckboxes(content);
        total += t;
        completed += c;
        // Remember a still-incomplete file (>=1 unchecked box) so the next call can short-circuit on it.
        if (c < t && stamp is { } s)
        {
            incomplete[path] = s;
        }
    }

    // Ported verbatim from RepositoryProjectionService.CountCheckboxes (the canonical, authoritative rule).
    internal static (int total, int completed) CountCheckboxes(string content)
    {
        int total = 0;
        int completed = 0;
        bool insideFence = false;

        foreach (ReadOnlySpan<char> rawLine in content.AsSpan().EnumerateLines())
        {
            ReadOnlySpan<char> line = rawLine.TrimStart();
            if (line.StartsWith("```"))
            {
                insideFence = !insideFence;
                continue;
            }

            if (insideFence || line.Length < 6)
            {
                continue;
            }

            if (line[0] != '-' || line[1] != ' ' || line[2] != '[' || line[4] != ']' || line[5] != ' ')
            {
                continue;
            }

            char mark = line[3];
            if (mark == ' ')
            {
                total++;
            }
            else if (mark is 'x' or 'X')
            {
                total++;
                completed++;
            }
        }

        return (total, completed);
    }
}
