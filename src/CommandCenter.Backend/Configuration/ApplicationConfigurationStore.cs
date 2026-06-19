using System.Text.Json;

namespace CommandCenter.Backend.Configuration;

public sealed class ApplicationConfigurationStore : IApplicationConfigurationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string configurationPath;

    public ApplicationConfigurationStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CommandCenter",
            "configuration.json"))
    {
    }

    public ApplicationConfigurationStore(string configurationPath)
    {
        this.configurationPath = configurationPath;
    }

    public async Task<ApplicationConfiguration> LoadAsync()
    {
        if (!File.Exists(configurationPath))
        {
            return new ApplicationConfiguration();
        }

        await using var stream = File.OpenRead(configurationPath);
        return await JsonSerializer.DeserializeAsync<ApplicationConfiguration>(stream, SerializerOptions)
            ?? new ApplicationConfiguration();
    }

    public async Task SaveAsync(ApplicationConfiguration configuration)
    {
        var directory = Path.GetDirectoryName(configurationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(configurationPath);
        await JsonSerializer.SerializeAsync(stream, configuration, SerializerOptions);
    }
}
