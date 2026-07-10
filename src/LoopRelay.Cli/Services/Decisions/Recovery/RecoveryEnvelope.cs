using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Orchestration.Recovery;

namespace LoopRelay.Cli.Services.Decisions.Recovery;

internal sealed record RecoveryEnvelopeItem(
    int Order,
    string SourceKind,
    string Kind,
    string Role,
    string Text,
    string Digest);

internal sealed record RecoveryEnvelope(
    string SchemaVersion,
    string Marker,
    string ScopeId,
    string OriginalProviderThreadId,
    RecoveryCompleteness Completeness,
    IReadOnlyList<RecoverySourceDescriptor> Sources,
    IReadOnlyList<RecoveryEnvelopeItem> Items,
    IReadOnlyList<string> Omissions,
    int EstimatedTokens,
    int OutputReserveTokens,
    string NormalizerVersion,
    string SanitizerVersion,
    string Digest,
    string CanonicalJson);

internal sealed class RecoveryEnvelopeException(string message) : InvalidOperationException(message);

internal sealed partial class RecoveryEnvelopeBuilder
{
    public const string SchemaVersion = "recovery-envelope.v1";
    public const string NormalizerVersion = "recovery-normalizer.v1";
    public const string SanitizerVersion = "recovery-sanitizer.v1";

    public RecoveryEnvelope Build(
        string marker,
        string scopeId,
        string originalProviderThreadId,
        IEnumerable<RecoverySourceObservation> observations,
        int? maximumRecoverableContext,
        int outputReserveTokens,
        int mandatoryOverheadTokens = 2_000)
    {
        if (maximumRecoverableContext is null)
        {
            throw new RecoveryEnvelopeException("MaximumRecoverableContext is Unknown; textual recovery is ineligible.");
        }

        int available = maximumRecoverableContext.Value - outputReserveTokens - mandatoryOverheadTokens;
        if (available <= 0)
        {
            throw new RecoveryEnvelopeException("No verified context budget remains after the output reserve.");
        }

        RecoverySourceObservation[] orderedSources = observations
            .OrderBy(observation => observation.Descriptor.Order)
            .ThenBy(observation => observation.Descriptor.Kind, StringComparer.Ordinal)
            .ThenBy(observation => observation.Descriptor.Digest, StringComparer.Ordinal)
            .ToArray();
        var omissions = new SortedSet<string>(StringComparer.Ordinal);
        foreach (RecoverySourceObservation source in orderedSources
                     .OrderBy(item => item.Descriptor.Kind == "Repository" ? 0 : 1)
                     .ThenBy(item => item.Descriptor.Order))
        {
            foreach (string omission in source.Descriptor.Omissions)
            {
                omissions.Add(omission);
            }
        }

        var candidates = new List<RecoveryEnvelopeItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (RecoverySourceObservation source in orderedSources)
        {
            foreach (SessionContentRecord record in source.Records.OrderBy(record => record.Order))
            {
                string normalized = Normalize(record.Text);
                if (!TrySanitize(normalized, out string sanitized, out string? omission))
                {
                    omissions.Add($"{source.Descriptor.Kind}:{omission}");
                    continue;
                }

                string digest = Sha256($"{record.Role}\n{sanitized}");
                if (!seen.Add(digest))
                {
                    omissions.Add($"{source.Descriptor.Kind}:duplicate-item");
                    continue;
                }

                candidates.Add(new RecoveryEnvelopeItem(
                    candidates.Count,
                    source.Descriptor.Kind,
                    record.Kind,
                    record.Role,
                    sanitized,
                    digest));
            }
        }

        var selected = new List<RecoveryEnvelopeItem>();
        int estimated = 0;
        foreach (RecoveryEnvelopeItem item in candidates)
        {
            int tokens = EstimateTokens(item.Text);
            if (estimated + tokens > available)
            {
                if (item.SourceKind == "Repository")
                {
                    throw new RecoveryEnvelopeException(
                        "Mandatory repository recovery content does not fit within the verified context budget.");
                }
                omissions.Add($"{item.SourceKind}:budget-overflow:{item.Digest}");
                continue;
            }

            selected.Add(item with { Order = selected.Count });
            estimated += tokens;
        }

        if (selected.Count == 0)
        {
            throw new RecoveryEnvelopeException("No sanitized recovery content fit within the verified context budget.");
        }
        if (orderedSources.Any(source => source.Descriptor.Kind == "Repository")
            && selected.All(item => item.SourceKind != "Repository"))
        {
            throw new RecoveryEnvelopeException("No sanitized mandatory repository recovery content was available.");
        }

        RecoveryCompleteness completeness = DetermineCompleteness(orderedSources, omissions);
        object canonical = new
        {
            schemaVersion = SchemaVersion,
            marker,
            scopeId,
            originalProviderThreadId,
            completeness,
            sources = orderedSources.Select(source => new
            {
                source.Descriptor.Order,
                source.Descriptor.Kind,
                source.Descriptor.Location,
                source.Descriptor.Digest,
                source.Descriptor.VerifiedBoundary,
                source.Descriptor.NormalizerVersion,
                source.Descriptor.Completeness,
                omissions = source.Descriptor.Omissions.Order(StringComparer.Ordinal),
            }),
            items = selected,
            omissions = omissions.ToArray(),
            estimatedTokens = estimated,
            outputReserveTokens,
            normalizerVersion = NormalizerVersion,
            sanitizerVersion = SanitizerVersion,
        };
        string json = JsonSerializer.Serialize(canonical, JsonOptions);
        return new RecoveryEnvelope(
            SchemaVersion, marker, scopeId, originalProviderThreadId, completeness,
            orderedSources.Select(source => source.Descriptor).ToArray(), selected, omissions.ToArray(),
            estimated, outputReserveTokens, NormalizerVersion, SanitizerVersion, Sha256(json), json);
    }

    private static RecoveryCompleteness DetermineCompleteness(
        IReadOnlyList<RecoverySourceObservation> sources,
        IReadOnlyCollection<string> omissions)
    {
        RecoveryCompleteness best = sources.Select(source => source.Descriptor.Completeness)
            .DefaultIfEmpty(RecoveryCompleteness.Unknown)
            .Min();
        if (omissions.Count > 0)
        {
            return best == RecoveryCompleteness.RepositoryOnly ? RecoveryCompleteness.RepositoryOnly : RecoveryCompleteness.Selective;
        }

        return best;
    }

    private static string Normalize(string value) =>
        string.Join('\n', value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.TrimEnd()))
            .Trim();

    private static bool TrySanitize(string value, out string sanitized, out string? omission)
    {
        sanitized = value;
        omission = null;
        if (value.Length == 0)
        {
            omission = "empty";
            return false;
        }

        if (SecretPattern().IsMatch(value)
            || EnvironmentDumpPattern().IsMatch(value)
            || Base64Pattern().IsMatch(value)
            || HiddenReasoningPattern().IsMatch(value))
        {
            omission = "sensitive-or-unsupported-content";
            sanitized = string.Empty;
            return false;
        }

        sanitized = ExternalWindowsPathPattern().Replace(sanitized, "<external-path>");
        sanitized = ExternalUnixPathPattern().Replace(sanitized, "<external-path>");
        return true;
    }

    private static int EstimateTokens(string value) => Math.Max(1, (Encoding.UTF8.GetByteCount(value) + 3) / 4);
    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [GeneratedRegex(@"(?im)(authorization\s*:\s*bearer|api[_-]?key|access[_-]?token|secret\s*=|password\s*=|sk-[a-z0-9]{16,})")]
    private static partial Regex SecretPattern();

    [GeneratedRegex(@"(?m)^[A-Z_][A-Z0-9_]{2,}=.+$")]
    private static partial Regex EnvironmentDumpPattern();

    [GeneratedRegex(@"[A-Za-z0-9+/]{256,}={0,2}")]
    private static partial Regex Base64Pattern();

    [GeneratedRegex(@"(?i)(encrypted_content|chain[- ]of[- ]thought|hidden reasoning)")]
    private static partial Regex HiddenReasoningPattern();

    [GeneratedRegex(@"(?i)\b[A-Z]:\\(?:[^\s<>:""|?*]+\\)*[^\s<>:""|?*]*")]
    private static partial Regex ExternalWindowsPathPattern();

    [GeneratedRegex(@"(?<![\w.])/(?:home|users|var|etc|opt|tmp)/[^\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalUnixPathPattern();
}
