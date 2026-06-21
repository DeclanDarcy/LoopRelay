using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;

namespace CommandCenter.Execution.Services;

public sealed class FileSystemExecutionSessionStore : IExecutionSessionStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string storePath;

    public FileSystemExecutionSessionStore()
        : this(Environment.GetEnvironmentVariable("COMMAND_CENTER_EXECUTION_SESSIONS_PATH") ??
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CommandCenter",
                "execution-sessions.json"))
    {
    }

    public FileSystemExecutionSessionStore(string storePath)
    {
        this.storePath = storePath;
    }

    public async Task<IReadOnlyList<ExecutionSession>> LoadAsync()
    {
        if (!File.Exists(storePath))
        {
            return [];
        }

        await using FileStream stream = File.OpenRead(storePath);
        return await JsonSerializer.DeserializeAsync<ExecutionSession[]>(stream, SerializerOptions)
            ?? [];
    }

    public async Task SaveAsync(IReadOnlyList<ExecutionSession> sessions)
    {
        string? directory = Path.GetDirectoryName(storePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(storePath);
        await JsonSerializer.SerializeAsync(stream, sessions, SerializerOptions);
    }
}
