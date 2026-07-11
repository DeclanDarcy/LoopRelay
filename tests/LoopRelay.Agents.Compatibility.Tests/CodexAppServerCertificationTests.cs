using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Codex.Compatibility;

namespace LoopRelay.Agents.Compatibility.Tests;

public sealed class CodexAppServerCertificationTests
{
    [Fact]
    public async Task ExplicitBinaryMatchesCheckedInProtocolFixtureInDisposableHome()
    {
        string? binary = Environment.GetEnvironmentVariable("LOOPRELAY_CODEX_CERT_BINARY");
        if (string.IsNullOrWhiteSpace(binary))
        {
            return; // ordinary hermetic runs validate the checked-in fixture; release runs supply a binary.
        }

        string root = Directory.CreateTempSubdirectory("looprelay-codex-cert-").FullName;
        string codexHome = Directory.CreateDirectory(Path.Combine(root, "codex-home")).FullName;
        string repository = Directory.CreateDirectory(Path.Combine(root, "repository")).FullName;
        string schemas = Directory.CreateDirectory(Path.Combine(root, "schemas")).FullName;
        try
        {
            string versionOutput = await RunAsync(binary, ["--version"], repository, codexHome);
            CertificationFixture expected = CertificationFixture.LoadAll()
                .Single(fixture => versionOutput.Contains(fixture.ServerVersion, StringComparison.Ordinal));
            await RunAsync(
                binary,
                ["app-server", "generate-json-schema", "--experimental", "--out", schemas],
                repository,
                codexHome);
            string schemaDigest = CanonicalJsonDigest(
                Path.Combine(schemas, "codex_app_server_protocol.v2.schemas.json"));

            await using var server = await AppServer.StartAsync(binary, repository, codexHome);
            JsonElement initialize = await server.RequestAsync(1, "initialize", new
            {
                clientInfo = new { name = "LoopRelay-certification", version = "1" },
                capabilities = new { experimentalApi = true },
            });
            await server.NotifyAsync("initialized", new { });
            JsonElement started = await server.RequestAsync(2, "thread/start", new
            {
                cwd = repository,
                sandbox = "read-only",
                approvalPolicy = "never",
            });
            string originalId = started.GetProperty("thread").GetProperty("id").GetString()!;
            await server.RequestAsync(3, "thread/inject_items", new
            {
                threadId = originalId,
                items = new object[]
                {
                    new
                    {
                        type = "message",
                        role = "user",
                        content = new[] { new { type = "input_text", text = "harmless certification fixture" } },
                    },
                },
            });
            JsonElement read = await server.RequestAsync(4, "thread/read", new
            {
                threadId = originalId,
                includeTurns = true,
            });
            JsonElement resumed = await server.RequestAsync(5, "thread/resume", new
            {
                threadId = originalId,
                cwd = repository,
                sandbox = "read-only",
                approvalPolicy = "never",
                excludeTurns = true,
            });
            JsonElement forked = await server.RequestAsync(6, "thread/fork", new
            {
                threadId = originalId,
                cwd = repository,
                sandbox = "read-only",
                approvalPolicy = "never",
                excludeTurns = true,
            });

            string childId = forked.GetProperty("thread").GetProperty("id").GetString()!;
            Assert.Equal(originalId, read.GetProperty("thread").GetProperty("id").GetString());
            Assert.Equal(originalId, resumed.GetProperty("thread").GetProperty("id").GetString());
            Assert.NotEqual(originalId, childId);

            Assert.True(string.Equals(expected.SchemaDigest, schemaDigest, StringComparison.Ordinal),
                $"Canonical schema digest mismatch. Actual: {schemaDigest}");
            Assert.Equal(expected.InitializeReportsExperimentalApi, HasExperimentalApi(initialize));
            Assert.True(expected.InjectMaterializesThread);
            Assert.True(expected.ReadPreservesIdentity);
            Assert.True(expected.ResumePreservesIdentity);
            Assert.True(expected.ForkProducesDistinctChild);
            Assert.Empty(Directory.EnumerateFiles(codexHome, "auth.json", SearchOption.AllDirectories));
        }
        finally
        {
            await DeleteEventuallyAsync(root);
        }
    }

    [Fact]
    public void CheckedInEvidenceDigestIsCanonicalAndContainsNoSessionContent()
    {
        foreach (CertificationFixture fixture in CertificationFixture.LoadAll())
        {
            string json = File.ReadAllText(fixture.Path);
            Assert.Equal(fixture.EvidenceDigest, Sha256(Encoding.UTF8.GetBytes(fixture.CanonicalEvidence())));
            Assert.DoesNotContain("thread-", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("auth", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void EmbeddedManifestPromotesOnlyTheOperationsCertifiedByTheFixture()
    {
        IReadOnlyList<CertificationFixture> fixtures = CertificationFixture.LoadAll();
        IReadOnlyList<CodexCompatibilityManifestEntry> entries = CodexCompatibilityManifest.LoadEmbedded().Entries;
        Assert.Equal(fixtures.Count, entries.Count);
        foreach (CertificationFixture fixture in fixtures)
        {
            CodexCompatibilityManifestEntry entry = Assert.Single(entries, item => item.ServerVersion == fixture.ServerVersion);
            Assert.Equal(fixture.SchemaDigest, entry.SchemaDigest);
            Assert.Equal(fixture.EvidenceDigest, entry.EvidenceDigest);
            Assert.Equal(SessionOperationSupport.Supported, entry.ResumeSupport);
            Assert.Equal(SessionOperationSupport.Supported, entry.ExcludeTurnsSupport);
            Assert.Equal(SessionOperationSupport.Supported, entry.ReadSupport);
            Assert.Equal(SessionOperationSupport.Unknown, entry.WriteSupport);
            Assert.Equal(SessionOperationSupport.Unknown, entry.ForkSupport);
            Assert.Null(entry.MaximumRecoverableContext);
        }
    }

    [Fact]
    public void ProductionIdentityProbeMatchesTheCertifiedCanonicalIdentityWhenEnabled()
    {
        if (Environment.GetEnvironmentVariable("LOOPRELAY_CODEX_CERT_IDENTITY_PROBE") != "1")
        {
            return;
        }

        CodexInstalledCompatibilityIdentity identity = CodexCompatibilityIdentityProbe.Resolve();
        CertificationFixture fixture = CertificationFixture.LoadAll()
            .Single(item => item.ServerVersion == identity.ServerVersion);
        Assert.Equal(fixture.ServerVersion, identity.ServerVersion);
        Assert.Equal(fixture.SchemaDigest, identity.SchemaDigest);
        Assert.DoesNotContain("session", identity.Evidence, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasExperimentalApi(JsonElement initialize) =>
        initialize.TryGetProperty("capabilities", out JsonElement capabilities)
        && capabilities.TryGetProperty("experimentalApi", out JsonElement value)
        && value.ValueKind == JsonValueKind.True;

    private static async Task<string> RunAsync(
        string binary,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        string codexHome)
    {
        using Process process = Start(binary, arguments, workingDirectory, codexHome, redirectInput: false);
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(process.ExitCode == 0, $"Codex exited {process.ExitCode}: {error}");
        return output + error;
    }

    private static Process Start(
        string binary,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        string codexHome,
        bool redirectInput)
    {
        var info = new ProcessStartInfo
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = redirectInput,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        if (OperatingSystem.IsWindows() && binary.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            info.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            string command = string.Join(' ', new[] { Quote(binary) }.Concat(arguments.Select(Quote)));
            info.Arguments = $"/d /s /c \"{command}\"";
        }
        else
        {
            info.FileName = binary;
            foreach (string argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }
        }
        info.Environment["CODEX_HOME"] = codexHome;
        info.Environment["CODEX_ANALYTICS_ENABLED"] = "false";
        return Process.Start(info) ?? throw new InvalidOperationException("Codex certification process did not start.");
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string CanonicalJsonDigest(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(writer, document.RootElement);
        }
        return Sha256(stream.ToArray());
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

    private static async Task DeleteEventuallyAsync(string path)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 9)
            {
                await Task.Delay(100);
            }
            catch (UnauthorizedAccessException) when (attempt < 9)
            {
                await Task.Delay(100);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }

    private sealed class AppServer(Process process) : IAsyncDisposable
    {
        private readonly Process _process = process;
        private readonly CancellationTokenSource _timeout = new(TimeSpan.FromSeconds(30));

        public static Task<AppServer> StartAsync(string binary, string repository, string codexHome) =>
            Task.FromResult(new AppServer(Start(
                binary, ["app-server", "--listen", "stdio://"], repository, codexHome, redirectInput: true)));

        public async Task<JsonElement> RequestAsync(long id, string method, object parameters)
        {
            await WriteAsync(new { jsonrpc = "2.0", id, method, @params = parameters });
            while (true)
            {
                string? line = await _process.StandardOutput.ReadLineAsync(_timeout.Token);
                if (line is null)
                {
                    throw new IOException("Codex app-server ended before the certification response arrived: "
                        + await _process.StandardError.ReadToEndAsync());
                }
                JsonDocument document;
                try { document = JsonDocument.Parse(line); }
                catch (JsonException) { continue; }
                using (document)
                {
                    JsonElement root = document.RootElement;
                    if (!root.TryGetProperty("id", out JsonElement responseId)
                        || responseId.ValueKind != JsonValueKind.Number
                        || responseId.GetInt64() != id)
                    {
                        continue;
                    }
                    if (root.TryGetProperty("error", out JsonElement error))
                    {
                        throw new InvalidOperationException($"{method} returned {error.GetRawText()}");
                    }
                    return root.GetProperty("result").Clone();
                }
            }
        }

        public Task NotifyAsync(string method, object parameters) =>
            WriteAsync(new { jsonrpc = "2.0", method, @params = parameters });

        private async Task WriteAsync(object frame)
        {
            await _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(frame));
            await _process.StandardInput.FlushAsync(_timeout.Token);
        }

        public async ValueTask DisposeAsync()
        {
            _timeout.Cancel();
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
            _process.Dispose();
            _timeout.Dispose();
        }
    }

    private sealed record CertificationFixture(
        string ServerVersion,
        string SchemaDigest,
        bool InitializeReportsExperimentalApi,
        bool InjectMaterializesThread,
        bool ReadPreservesIdentity,
        bool ResumePreservesIdentity,
        bool ForkProducesDistinctChild,
        string EvidenceDigest)
    {
        public string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", $"codex-{ServerVersion}-certification.json");
        public static IReadOnlyList<CertificationFixture> LoadAll() => Directory
            .EnumerateFiles(System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures"), "codex-*-certification.json")
            .Order(StringComparer.Ordinal)
            .Select(path => JsonSerializer.Deserialize<CertificationFixture>(
                File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .ToArray();
        public string CanonicalEvidence() => string.Join('|',
            ServerVersion, SchemaDigest, InitializeReportsExperimentalApi, InjectMaterializesThread, ReadPreservesIdentity,
            ResumePreservesIdentity, ForkProducesDistinctChild);
    }
}
