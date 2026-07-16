using System.Text.Json;
using System.Text.Json.Nodes;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Certification;

public static class CertificationFixtureSettings
{
    private static string _brainModel = CertifiedBrainModel;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public const string CertifiedBrainModel = "gpt-5.6-luna";
    public const string BrainEffort = "medium";
    public static readonly TimeSpan ProviderTurnTimeout = TimeSpan.FromMinutes(60);

    public static IReadOnlyList<string> CertifiedBrainModels { get; } =
        Array.AsReadOnly([CertifiedBrainModel]);

    public static string BrainModel => _brainModel;

    public static AgentModel BrainAgentModel => AgentConfigurationCatalog.ParseModel(BrainModel);

    public static string CertifiedProfileIdentity => $"{CertifiedBrainModel}/{BrainEffort}";

    public static string ResolveBrainModel(string? configuredModel)
    {
        string model = string.IsNullOrWhiteSpace(configuredModel)
            ? CertifiedBrainModel
            : configuredModel;
        if (!IsCertifiedBrainModel(model))
        {
            throw new ArgumentException(
                $"Certification model must be one of: {string.Join(", ", CertifiedBrainModels)}.",
                nameof(configuredModel));
        }

        return model;
    }

    public static bool IsCertifiedBrainModel(string? model) =>
        CertifiedBrainModels.Contains(model, StringComparer.Ordinal);

    public static void SelectBrainModel(string model) =>
        _brainModel = ResolveBrainModel(model);

    public static async Task<string> WriteAsync(
        string root,
        string cliPath,
        CancellationToken cancellationToken = default)
        => await WriteAsync(root, cliPath, BrainModel, cancellationToken);

    internal static async Task<string> WriteAsync(
        string root,
        string cliPath,
        string brainModel,
        CancellationToken cancellationToken = default)
    {
        brainModel = ResolveBrainModel(brainModel);
        string source = Path.Combine(Path.GetDirectoryName(cliPath)!, "settings.default.json");
        JsonNode settings = JsonNode.Parse(await File.ReadAllTextAsync(source, cancellationToken))
            ?? throw new InvalidOperationException("CLI settings template was empty.");
        JsonObject runtime = settings["runtime"]?.AsObject()
            ?? throw new InvalidOperationException("CLI settings template has no canonical runtime section.");
        JsonObject brain = runtime["brain"]?.AsObject()
            ?? throw new InvalidOperationException("CLI settings template has no canonical runtime brain section.");
        brain["model"] = brainModel;
        brain["effort"] = BrainEffort;
        string path = Path.Combine(root, "settings.json");
        await File.WriteAllTextAsync(path, settings.ToJsonString(JsonOptions), cancellationToken);
        return path;
    }
}
