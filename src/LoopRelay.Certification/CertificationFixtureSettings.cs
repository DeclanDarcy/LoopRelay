using System.Text.Json;
using System.Text.Json.Nodes;

namespace LoopRelay.Certification;

public static class CertificationFixtureSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public const string BrainModel = "gpt-5.3-codex-spark";
    public const string BrainEffort = "medium";
    public static readonly TimeSpan ProviderTurnTimeout = TimeSpan.FromMinutes(60);

    public static async Task<string> WriteAsync(
        string root,
        string cliPath,
        CancellationToken cancellationToken = default)
    {
        string source = Path.Combine(Path.GetDirectoryName(cliPath)!, "settings.default.json");
        JsonNode settings = JsonNode.Parse(await File.ReadAllTextAsync(source, cancellationToken))
            ?? throw new InvalidOperationException("CLI settings template was empty.");
        JsonObject runtime = settings["runtime"]?.AsObject()
            ?? throw new InvalidOperationException("CLI settings template has no canonical runtime section.");
        JsonObject brain = runtime["brain"]?.AsObject()
            ?? throw new InvalidOperationException("CLI settings template has no canonical runtime brain section.");
        brain["model"] = BrainModel;
        brain["effort"] = BrainEffort;
        string path = Path.Combine(root, "settings.json");
        await File.WriteAllTextAsync(path, settings.ToJsonString(JsonOptions), cancellationToken);
        return path;
    }
}
