using System.Text.Json;
using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Agents.Services.Codex.Compatibility;

public sealed record CodexCompatibilityManifestEntry(
    string Id,
    string ServerVersion,
    string SchemaDigest,
    string FixtureIdentity,
    SessionOperationSupport ResumeSupport,
    SessionOperationSupport ExcludeTurnsSupport,
    SessionOperationSupport ForkSupport,
    SessionOperationSupport ReadSupport,
    SessionOperationSupport WriteSupport,
    int? MaximumRecoverableContext,
    string EvidenceDigest);

public sealed class CodexCompatibilityManifest
{
    private const string EmbeddedResourceName = "LoopRelay.Agents.Services.Codex.Compatibility.codex-compatibility-manifest.json";

    public CodexCompatibilityManifest(IEnumerable<CodexCompatibilityManifestEntry> entries)
    {
        Entries = entries.OrderBy(entry => entry.Id, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyList<CodexCompatibilityManifestEntry> Entries { get; }

    public CodexCompatibilityManifestEntry? FindExact(string? serverVersion, string? schemaDigest) =>
        serverVersion is null || schemaDigest is null
            ? null
            : Entries.SingleOrDefault(entry =>
                string.Equals(entry.ServerVersion, serverVersion, StringComparison.Ordinal)
                && string.Equals(entry.SchemaDigest, schemaDigest, StringComparison.OrdinalIgnoreCase));

    public static CodexCompatibilityManifest LoadEmbedded()
    {
        using Stream stream = typeof(CodexCompatibilityManifest).Assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException($"Embedded Codex compatibility manifest '{EmbeddedResourceName}' was not found.");
        using JsonDocument document = JsonDocument.Parse(stream);
        var entries = new List<CodexCompatibilityManifestEntry>();
        foreach (JsonElement item in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            entries.Add(new CodexCompatibilityManifestEntry(
                item.GetProperty("id").GetString()!,
                item.GetProperty("serverVersion").GetString()!,
                item.GetProperty("schemaDigest").GetString()!,
                item.GetProperty("fixtureIdentity").GetString()!,
                Enum.Parse<SessionOperationSupport>(item.GetProperty("resumeSupport").GetString()!, ignoreCase: true),
                Enum.Parse<SessionOperationSupport>(item.GetProperty("excludeTurnsSupport").GetString()!, ignoreCase: true),
                Enum.Parse<SessionOperationSupport>(item.GetProperty("forkSupport").GetString()!, ignoreCase: true),
                Enum.Parse<SessionOperationSupport>(item.GetProperty("readSupport").GetString()!, ignoreCase: true),
                Enum.Parse<SessionOperationSupport>(item.GetProperty("writeSupport").GetString()!, ignoreCase: true),
                item.TryGetProperty("maximumRecoverableContext", out JsonElement maximum) && maximum.ValueKind == JsonValueKind.Number
                    ? maximum.GetInt32()
                    : null,
                item.GetProperty("evidenceDigest").GetString()!));
        }

        return new CodexCompatibilityManifest(entries);
    }
}
