using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Cli.Services.Execution;

/// <summary>
/// LoopStart epic-complete gate. Aggregates GitHub task-list checkboxes (parsed with the canonical
/// RepositoryProjectionService.CountCheckboxes rule) across every .agents/milestones/m*.md. The epic
/// is complete only when at least one checkbox exists across them and every checkbox is checked. Files
/// with zero checkboxes contribute nothing and never block. plan.md is intentionally excluded: agents
/// never tick its boxes, so folding it into the aggregate would keep the epic permanently incomplete.
///
/// The CLI runs this gate once per loop iteration, so during the long middle of an epic the same
/// milestone file sits with an unchecked box across many calls. To avoid re-reading and re-parsing
/// every file each time, the gate remembers each parsed-incomplete file together with the last-write
/// time it observed (and the unticked items that parse produced). On the next call it FIRST checks
/// those remembered files: if any is unchanged since we parsed it, it still has an unchecked box, so
/// the aggregate is still incomplete and the gate returns false WITHOUT reading or listing anything.
/// This short-circuit only ever yields false — any single unchecked box keeps the aggregate false
/// regardless of files completed or appearing elsewhere — so it can never wrongly report completion.
/// When no remembered file is both present and unchanged (first call, any tracked file
/// modified/removed, or no timestamps available at all), the gate falls back to the full parse,
/// behaving exactly as it did before the optimization.
///
/// <see cref="GetUntickedItemsAsync"/> consumes the same cache from the other side: it always lists
/// (new files must stay discoverable, and the listing fixes the item order), but serves each file's
/// unticked items from the remembered parse while its last-write time is unchanged, re-reading only
/// files whose stamp moved or was never captured. Any write to a milestone file — e.g. the work turn
/// ticking a box — advances its stamp and forces the re-parse, so served items are current per stamp.
///
/// Known limitation: both paths key on last-write time, so a file that changes without its
/// last-write time advancing — due to coarse filesystem timestamp granularity or timestamp-preserving
/// tooling such as restore-from-backup — could be skipped on a re-parse. This is safe for the epic
/// gate because it ONLY ever short-circuits to false, so it can never wrongly report completion; the
/// worst case is one extra loop iteration (or one stamp-stale unticked listing) before the updated
/// content is detected.
/// </summary>
internal sealed class MilestoneGate
{
    private readonly IArtifactStore _store;
    private readonly Repository _repository;
    private readonly Func<string, DateTime?> _lastWriteTime;

    /// <summary>What the last parse of a still-incomplete file produced, keyed by the last-write time
    /// captured BEFORE that read (so a racing write forces a re-parse next call, never a stale skip).</summary>
    private readonly record struct ParsedIncomplete(DateTime Stamp, IReadOnlyList<string> Unticked);

    /// <summary>
    /// resolved-path -> stamp + unticked items from when we last parsed it AND found >=1 unchecked box.
    /// Membership invariant: an entry ALWAYS means "had an unchecked box at this stamp" — files that are
    /// fully checked or have an unknown stamp are never cached. IsEpicCompleteAsync's short-circuit
    /// soundness rests on this: any unchanged entry proves the aggregate is still incomplete.
    /// </summary>
    private readonly Dictionary<string, ParsedIncomplete> incomplete = new(StringComparer.OrdinalIgnoreCase);

    public MilestoneGate(IArtifactStore store, Repository repository, Func<string, DateTime?>? lastWriteTime = null)
    {
        _store = store;
        _repository = repository;
        // The CLI always runs FileSystemArtifactStore over real paths, so default to the filesystem.
        // File.GetLastWriteTimeUtc returns a 1601 sentinel (not an exception) for a missing file, so the
        // File.Exists guard is REQUIRED: surface null (meaning unknown) for an absent file.
        _lastWriteTime = lastWriteTime
            ?? (path => File.Exists(path) ? File.GetLastWriteTimeUtc(path) : (DateTime?)null);
    }

    public async Task<bool> IsEpicCompleteAsync()
    {
        // SHORT-CIRCUIT: a remembered incomplete file unchanged since we parsed it still has an unchecked
        // box, so the aggregate is still incomplete. Sound regardless of files completed/appearing elsewhere
        // — any single unchecked box keeps the aggregate false. We ONLY ever short-circuit to false.
        foreach ((string path, ParsedIncomplete parsed) in incomplete)
        {
            if (_lastWriteTime(path) is { } now && now == parsed.Stamp)
            {
                return false;
            }
        }

        // CACHE MISS: re-derive from scratch with the same full parse as before the optimization.
        incomplete.Clear();
        int total = 0;
        int completed = 0;

        string dir = ArtifactPath.ResolveRepositoryPath(_repository, OrchestrationArtifactPaths.MilestonesDirectory);
        IReadOnlyList<string> files = await _store.ListAsync(dir, OrchestrationArtifactPaths.MilestoneSearchPattern);
        foreach (string file in files)
        {
            // Capture the stamp BEFORE the read so a write racing the read records an older stamp and forces a
            // re-parse next call (never a stale skip). ?? string.Empty tolerates a listed-then-deleted file.
            DateTime? stamp = _lastWriteTime(file);
            string content = await _store.ReadAsync(file) ?? string.Empty;
            Accumulate(file, stamp, content, ref total, ref completed);
        }

        return total > 0 && completed == total;
    }

    /// <summary>
    /// Every unticked checkbox item — the trimmed full line, e.g. "- [ ] Implement X" — across
    /// .agents/milestones/m*.md, in file-listing order and document order within a file. Feeds the
    /// GenerateNoChangesHandoff prompt when an execution turn produced no real working-tree change, so
    /// the agent must account for exactly the items it left open. A box ticked during that turn is
    /// excluded by construction: the write advanced the file's stamp, which forces the re-parse here.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetUntickedItemsAsync()
    {
        var items = new List<string>();
        string dir = ArtifactPath.ResolveRepositoryPath(_repository, OrchestrationArtifactPaths.MilestonesDirectory);
        IReadOnlyList<string> files = await _store.ListAsync(dir, OrchestrationArtifactPaths.MilestoneSearchPattern);
        foreach (string file in files)
        {
            // Same stamp discipline as the full parse: capture BEFORE the read, never cache an unknown stamp.
            DateTime? stamp = _lastWriteTime(file);
            if (stamp is { } current
                && incomplete.TryGetValue(file, out ParsedIncomplete cached)
                && cached.Stamp == current)
            {
                items.AddRange(cached.Unticked);
                continue;
            }

            string content = await _store.ReadAsync(file) ?? string.Empty;
            (int t, int c, IReadOnlyList<string> unticked) = CountCheckboxes(content);
            // Re-establish the membership invariant for this file: drop any stale entry first, so a file
            // that became fully checked (or lost its stamp) can never keep short-circuiting the epic gate.
            incomplete.Remove(file);
            if (c < t && stamp is { } s)
            {
                incomplete[file] = new ParsedIncomplete(s, unticked);
            }

            items.AddRange(unticked);
        }

        return items;
    }

    private void Accumulate(string path, DateTime? stamp, string content, ref int total, ref int completed)
    {
        (int t, int c, IReadOnlyList<string> unticked) = CountCheckboxes(content);
        total += t;
        completed += c;
        // Remember a still-incomplete file (>=1 unchecked box) so the next call can short-circuit on it
        // and GetUntickedItemsAsync can serve its items without a re-read.
        if (c < t && stamp is { } s)
        {
            incomplete[path] = new ParsedIncomplete(s, unticked);
        }
    }

    // Ported from RepositoryProjectionService.CountCheckboxes (the canonical rule — legacy backend code
    // that must NOT be modified). The RECOGNITION rule (fence toggling, the "- [ ] "/"- [x] " shape) is
    // byte-identical to the backend's; the only extension is collecting each unticked item's trimmed line
    // so callers can render the open items, not just count them.
    internal static (int Total, int Completed, IReadOnlyList<string> Unticked) CountCheckboxes(string content)
    {
        int total = 0;
        int completed = 0;
        var unticked = new List<string>();
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
                unticked.Add(line.TrimEnd().ToString());
            }
            else if (mark is 'x' or 'X')
            {
                total++;
                completed++;
            }
        }

        return (total, completed, unticked);
    }
}
