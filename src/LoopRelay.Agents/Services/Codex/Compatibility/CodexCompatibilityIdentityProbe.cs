using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LoopRelay.Agents.Services.Codex.Compatibility;

public sealed record CodexInstalledCompatibilityIdentity(
    string? ServerVersion,
    string? SchemaDigest,
    string ExecutableIdentity,
    string Evidence);

/// <summary>Resolves exact local identity in a disposable CODEX_HOME; it never reads the user's session store.</summary>
public static partial class CodexCompatibilityIdentityProbe
{
    private static readonly Lazy<CodexInstalledCompatibilityIdentity> Cached = new(Probe);

    public static CodexInstalledCompatibilityIdentity Resolve() => Cached.Value;

    private static CodexInstalledCompatibilityIdentity Probe()
    {
        string executable = Environment.GetEnvironmentVariable("CODEX_EXECUTABLE") ?? "codex";
        string root = Directory.CreateTempSubdirectory("looprelay-codex-identity-").FullName;
        string home = Directory.CreateDirectory(Path.Combine(root, "codex-home")).FullName;
        string schemas = Directory.CreateDirectory(Path.Combine(root, "schemas")).FullName;
        try
        {
            string versionOutput = Run(executable, ["--version"], home, root);
            Match version = VersionPattern().Match(versionOutput);
            if (!version.Success)
            {
                return new CodexInstalledCompatibilityIdentity(null, null, executable, "version output was unrecognized");
            }

            Run(executable,
                ["app-server", "generate-json-schema", "--experimental", "--out", schemas],
                home, root);
            string schemaPath = Path.Combine(schemas, "codex_app_server_protocol.v2.schemas.json");
            return new CodexInstalledCompatibilityIdentity(
                version.Groups[1].Value,
                File.Exists(schemaPath) ? CanonicalJsonDigest(schemaPath) : null,
                executable,
                "disposable local version and canonical experimental schema probe");
        }
        catch (Exception exception)
        {
            return new CodexInstalledCompatibilityIdentity(
                null, null, executable, $"identity probe failed: {exception.GetType().Name}");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static string Run(string executable, IReadOnlyList<string> arguments, string home, string cwd)
    {
        var info = new ProcessStartInfo
        {
            WorkingDirectory = cwd,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        string extension = Path.GetExtension(executable);
        if (OperatingSystem.IsWindows() &&
            (string.IsNullOrEmpty(extension) ||
             extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
             extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)))
        {
            info.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            string command = string.Join(' ', new[] { Quote(executable) }.Concat(arguments.Select(Quote)));
            info.Arguments = $"/d /s /c \"{command}\"";
        }
        else
        {
            info.FileName = executable;
            foreach (string argument in arguments) info.ArgumentList.Add(argument);
        }
        info.Environment["CODEX_HOME"] = home;
        info.Environment["CODEX_ANALYTICS_ENABLED"] = "false";
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(info)
            ?? throw new InvalidOperationException("Codex identity probe did not start.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Codex identity probe timed out.");
        }
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Codex identity probe exited {process.ExitCode}: {error}");
        }
        return output + error;
    }

    private static string CanonicalJsonDigest(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, document.RootElement);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    [GeneratedRegex(@"(?:codex-cli\s+)?(\d+\.\d+\.\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionPattern();
}
