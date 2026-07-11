using System.Text.RegularExpressions;

namespace LoopRelay.Certification;

public static partial class EvidenceNormalizer
{
    public const string Version = "1";

    public static string Normalize(string value, string casePath)
    {
        string normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\\', '/');
        string normalizedCasePath = Path.GetFullPath(casePath).Replace('\\', '/').TrimEnd('/');
        normalized = normalized.Replace(normalizedCasePath, "<CASE>",
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        normalized = GuidPattern().Replace(normalized, "<GUID>");
        normalized = TimestampPattern().Replace(normalized, "<TIMESTAMP>");
        normalized = DurationPattern().Replace(normalized, "<DURATION>");
        return normalized.TrimEnd();
    }

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b")]
    private static partial Regex GuidPattern();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})\b")]
    private static partial Regex TimestampPattern();

    [GeneratedRegex(@"\b\d+(?:\.\d+)?\s?(?:ms|milliseconds?|s|seconds?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DurationPattern();
}

public static partial class PrivacyScanner
{
    public const string Version = "1";

    public static IReadOnlyList<string> Scan(string value, string caseAuthorityRoot)
    {
        var findings = new SortedSet<string>(StringComparer.Ordinal);
        if (SecretPattern().IsMatch(value))
        {
            findings.Add("credential-or-secret-pattern");
        }

        if (EnvironmentDumpPattern().IsMatch(value))
        {
            findings.Add("environment-dump-pattern");
        }

        if (HiddenReasoningPattern().IsMatch(value))
        {
            findings.Add("hidden-reasoning-pattern");
        }

        if (Base64Pattern().IsMatch(value))
        {
            findings.Add("large-base64-payload");
        }

        string allowedRoot = Path.GetFullPath(caseAuthorityRoot).Replace('\\', '/');
        foreach (Match match in AbsolutePathPattern().Matches(value.Replace('\\', '/')))
        {
            string candidate = match.Value.TrimEnd(':');
            if (!candidate.StartsWith(allowedRoot, OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal) &&
                !candidate.StartsWith("<CASE>", StringComparison.Ordinal))
            {
                findings.Add("external-absolute-path");
                break;
            }
        }

        return findings.ToArray();
    }

    [GeneratedRegex(@"(?i)(?:api[_-]?key|access[_-]?token|client[_-]?secret|password)\s*[:=]\s*[^\s,;]{6,}|\bsk-[A-Za-z0-9_-]{12,}")]
    private static partial Regex SecretPattern();

    [GeneratedRegex(@"(?m)^(?:PATH|HOME|USERPROFILE|CODEX_HOME|AWS_[A-Z_]+|AZURE_[A-Z_]+)=")]
    private static partial Regex EnvironmentDumpPattern();

    [GeneratedRegex(@"(?i)chain[- ]of[- ]thought|hidden reasoning|private reasoning|internal monologue")]
    private static partial Regex HiddenReasoningPattern();

    [GeneratedRegex(@"(?<![A-Za-z0-9+/=])[A-Za-z0-9+/]{160,}={0,2}(?![A-Za-z0-9+/=])")]
    private static partial Regex Base64Pattern();

    [GeneratedRegex(@"(?<![A-Za-z0-9])(?:[A-Za-z]:/[^\r\n\t\""<>|]+|/(?:home|Users|tmp|var|etc)/[^\r\n\t\""<>|]+)")]
    private static partial Regex AbsolutePathPattern();
}
