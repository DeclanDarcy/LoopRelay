using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public sealed record ExplicitHitlNonImplementationRequestCaptureResult(
    int CapturedCount,
    IReadOnlyList<NonImplementationHitlRequestEntry> CapturedRequests);

public sealed class ExplicitHitlNonImplementationRequestCaptureService(
    NonImplementationReviewLedgerStore ledgerStore)
{
    public const string SectionHeading = "## HITL-Requested Non-Implementation Deliverables";

    public async Task<ExplicitHitlNonImplementationRequestCaptureResult> CaptureFromSourceAsync(
        string sourceArtifactPath,
        string sourceContent,
        DateTimeOffset? capturedAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceArtifactPath);
        ArgumentNullException.ThrowIfNull(sourceContent);

        IReadOnlyList<NonImplementationHitlRequestEntry> parsed =
            ParseStructuredRequests(sourceArtifactPath, sourceContent, capturedAtUtc ?? DateTimeOffset.UtcNow);
        if (parsed.Count == 0)
        {
            return new ExplicitHitlNonImplementationRequestCaptureResult(0, Array.Empty<NonImplementationHitlRequestEntry>());
        }

        NonImplementationReviewLedgerDocument document = await ledgerStore.LoadOrCreateAsync();
        var requests = document.HitlRequests.ToList();
        var captured = new List<NonImplementationHitlRequestEntry>();
        foreach (NonImplementationHitlRequestEntry request in parsed)
        {
            if (requests.Any(existing => SameRequestIdentity(existing, request)))
            {
                continue;
            }

            requests.Add(request);
            captured.Add(request);
        }

        if (captured.Count > 0)
        {
            await ledgerStore.SaveAsync(document with { HitlRequests = requests });
        }

        return new ExplicitHitlNonImplementationRequestCaptureResult(captured.Count, captured);
    }

    public async Task<int> AttachRequestEvidenceAsync()
    {
        NonImplementationReviewLedgerDocument document = await ledgerStore.LoadOrCreateAsync();
        var entries = new List<NonImplementationReviewLedgerEntry>(document.Entries.Count);
        int changed = 0;
        foreach (NonImplementationReviewLedgerEntry entry in document.Entries)
        {
            NonImplementationReviewLedgerEntry updated = AttachRequestEvidence(entry, document.HitlRequests);
            if (!EqualityComparer<NonImplementationReviewLedgerEntry>.Default.Equals(entry, updated))
            {
                changed++;
            }

            entries.Add(updated);
        }

        if (changed > 0)
        {
            await ledgerStore.SaveAsync(document with { Entries = entries });
        }

        return changed;
    }

    public static NonImplementationReviewLedgerEntry AttachRequestEvidence(
        NonImplementationReviewLedgerEntry entry,
        IReadOnlyList<NonImplementationHitlRequestEntry> requests)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(requests);

        NonImplementationHitlRequestEntry? match =
            requests.FirstOrDefault(request => MatchesPath(request.DeliverablePathOrPattern, entry.Path));
        if (match is null)
        {
            return entry;
        }

        NonImplementationHitlProvenanceKind provenance =
            entry.HitlProvenanceKind == NonImplementationHitlProvenanceKind.None
                ? NonImplementationHitlProvenanceKind.HitlRequested
                : entry.HitlProvenanceKind;

        return entry with
        {
            HitlProvenanceKind = provenance,
            HitlProvenanceEvidencePath = match.SourceArtifactPath,
            HitlProvenanceEvidenceExcerpt = match.EvidenceExcerpt,
            HitlProvenanceSourceHash = match.SourceHash,
            HitlProvenanceRationale = match.Rationale,
        };
    }

    public static IReadOnlyList<NonImplementationHitlRequestEntry> ParseStructuredRequests(
        string sourceArtifactPath,
        string sourceContent,
        DateTimeOffset capturedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceArtifactPath);
        ArgumentNullException.ThrowIfNull(sourceContent);

        string[] lines = sourceContent.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int sectionStart = Array.FindIndex(
            lines,
            line => string.Equals(line.Trim(), SectionHeading, StringComparison.Ordinal));
        if (sectionStart < 0)
        {
            return Array.Empty<NonImplementationHitlRequestEntry>();
        }

        string sourceHash = Sha256(sourceContent);
        var entries = new List<NonImplementationHitlRequestEntry>();
        int[]? columnMap = null;
        for (int index = sectionStart + 1; index < lines.Length; index++)
        {
            string line = lines[index].Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            if (!line.StartsWith("|", StringComparison.Ordinal))
            {
                continue;
            }

            string[] cells = SplitMarkdownRow(line);
            if (cells.Length == 0 || IsSeparator(cells))
            {
                continue;
            }

            if (columnMap is null)
            {
                columnMap = BuildColumnMap(cells);
                continue;
            }

            if (cells.Length <= columnMap[0] || cells.Length <= columnMap[1])
            {
                continue;
            }

            string pathOrPattern = NormalizeCell(cells[columnMap[0]]);
            string rationale = NormalizeCell(cells[columnMap[1]]);
            if (string.IsNullOrWhiteSpace(pathOrPattern))
            {
                continue;
            }

            entries.Add(new NonImplementationHitlRequestEntry(
                pathOrPattern,
                sourceArtifactPath.Trim(),
                sourceHash,
                NonImplementationHitlProvenanceKind.HitlRequested,
                string.IsNullOrWhiteSpace(rationale) ? "Explicit HITL-request marker." : rationale,
                capturedAtUtc.ToUniversalTime(),
                line));
        }

        return entries;
    }

    private static int[] BuildColumnMap(string[] header)
    {
        int pathIndex = FindColumn(header, "Path Or Pattern");
        int rationaleIndex = FindColumn(header, "Rationale");
        return pathIndex < 0 || rationaleIndex < 0
            ? throw new NonImplementationReviewLedgerException(
                $"{SectionHeading} table must include Path Or Pattern and Rationale columns.")
            : [pathIndex, rationaleIndex];
    }

    private static int FindColumn(string[] header, string columnName)
    {
        for (int index = 0; index < header.Length; index++)
        {
            if (string.Equals(NormalizeHeader(header[index]), NormalizeHeader(columnName), StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool SameRequestIdentity(
        NonImplementationHitlRequestEntry left,
        NonImplementationHitlRequestEntry right) =>
        string.Equals(left.DeliverablePathOrPattern, right.DeliverablePathOrPattern, StringComparison.Ordinal) &&
        string.Equals(left.SourceArtifactPath, right.SourceArtifactPath, StringComparison.Ordinal) &&
        string.Equals(left.SourceHash, right.SourceHash, StringComparison.Ordinal) &&
        string.Equals(left.Rationale, right.Rationale, StringComparison.Ordinal);

    private static bool MatchesPath(string pathOrPattern, string path)
    {
        string normalizedPattern = NormalizePath(pathOrPattern);
        string normalizedPath = NormalizePath(path);
        if (!HasWildcard(normalizedPattern))
        {
            return string.Equals(normalizedPattern, normalizedPath, StringComparison.Ordinal);
        }

        string regex = "^" +
            Regex.Escape(normalizedPattern)
                .Replace("\\*", ".*", StringComparison.Ordinal)
                .Replace("\\?", ".", StringComparison.Ordinal) +
            "$";
        return Regex.IsMatch(normalizedPath, regex, RegexOptions.CultureInvariant);
    }

    private static bool HasWildcard(string value) =>
        value.Contains('*', StringComparison.Ordinal) || value.Contains('?', StringComparison.Ordinal);

    private static string NormalizePath(string path) =>
        NormalizeCell(path).Replace('\\', '/');

    private static string NormalizeHeader(string value) =>
        NormalizeCell(value).Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static string NormalizeCell(string value)
    {
        string trimmed = value.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '`' && trimmed[^1] == '`'
            ? trimmed[1..^1].Trim()
            : trimmed;
    }

    private static string[] SplitMarkdownRow(string line)
    {
        string trimmed = line.Trim().Trim('|');
        return trimmed.Split('|', StringSplitOptions.None).Select(cell => cell.Trim()).ToArray();
    }

    private static bool IsSeparator(string[] cells) =>
        cells.All(cell => cell.All(ch => ch is '-' or ':' or ' '));

    private static string Sha256(string content)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
