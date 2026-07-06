using System.Text.Json;
using LoopRelay.Core.Repositories;
using LoopRelay.Agents.Abstractions;

namespace LoopRelay.Cli;

/// <summary>Reads a Codex quota snapshot, or null when it cannot be determined.</summary>
internal interface ICodexUsageProbe
{
    Task<CodexUsageStatus?> QueryAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Queries Codex quota over the app-server JSON-RPC protocol: it starts <c>codex app-server</c> on stdio,
/// sends <c>initialize</c> then the on-demand <c>account/rateLimits/read</c> request, and parses the
/// <c>RateLimitSnapshot</c> in the response with <see cref="CodexRateLimitsParser"/>. This reads quota
/// WITHOUT running a model turn, so it costs no tokens/usage, and — unlike the interactive TUI — the
/// app-server is built for programmatic stdio, so a redirected (non-TTY) pipe works. The raw-JSON
/// acquisition is a seam (<c>readRateLimitsJson</c>) so the parse path is unit-testable; the default seam
/// does the live read.
///
/// Everything about this probe FAILS OPEN: any failure — a missing/bad codex binary, a child that dies at
/// launch, a hung session (bounded by <c>readTimeout</c>), or a response the parser cannot use — is
/// swallowed and reported as null ("usage unknown"). It exists to gate the loop, never to crash it. Only a
/// caller-requested cancellation is propagated.
/// </summary>
internal sealed class CodexUsageProbe : ICodexUsageProbe
{
    private static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromSeconds(30);

    private static readonly string[] AppServerArguments = { "app-server", "--listen", "stdio://" };

    private const string InitializeRequest =
        "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"," +
        "\"params\":{\"clientInfo\":{\"name\":\"LoopRelay-cli\",\"version\":\"1.0\"}}}\n";

    private const string RateLimitsReadRequest =
        "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"account/rateLimits/read\",\"params\":null}\n";

    private const int RateLimitsRequestId = 2;

    private readonly Func<CancellationToken, Task<string?>> readRateLimitsJson;

    public CodexUsageProbe(
        IProcessRunner processRunner, IAgentExecutableResolver executableResolver, Repository repository)
        : this(processRunner, executableResolver, repository, DefaultReadTimeout)
    {
    }

    /// <summary>Test seam: same live read, but with a short timeout so the timeout/fail-open path is testable.</summary>
    internal CodexUsageProbe(
        IProcessRunner processRunner, IAgentExecutableResolver executableResolver, Repository repository,
        TimeSpan scrapeTimeout)
        => readRateLimitsJson = ct => ReadRateLimitsAsync(processRunner, executableResolver, repository, scrapeTimeout, ct);

    /// <summary>Test seam: supply the raw rate-limits response JSON directly, bypassing the live codex session.</summary>
    internal CodexUsageProbe(Func<CancellationToken, Task<string?>> readRateLimitsJson)
        => this.readRateLimitsJson = readRateLimitsJson;

    public async Task<CodexUsageStatus?> QueryAsync(CancellationToken cancellationToken)
    {
        string? json;
        try
        {
            json = await readRateLimitsJson(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // The caller asked to stop — never swallow that.
        }
        catch (Exception)
        {
            // Bad/missing binary (Win32Exception), a child that died at launch (IOException on a closed stdin
            // pipe), a read timeout, etc. The gate must not crash the loop, so report "unknown".
            return null;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return CodexRateLimitsParser.Parse(json!);
        }
        catch (Exception)
        {
            // Defensive: the parser is written to return null rather than throw, but the gate must never
            // crash on an unexpected response shape.
            return null;
        }
    }

    private static async Task<string?> ReadRateLimitsAsync(
        IProcessRunner processRunner,
        IAgentExecutableResolver executableResolver,
        Repository repository,
        TimeSpan readTimeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(readTimeout);

        try
        {
            string executable = executableResolver.Resolve();
            await using IAgentProcess process = await processRunner.StartInteractiveAsync(
                executable, AppServerArguments, repository.Path, timeoutCts.Token);

            // JSON-RPC handshake, then the on-demand rate-limits read. No model turn is run, so no quota is spent.
            await process.WritePromptAsync(InitializeRequest, timeoutCts.Token);
            await process.WritePromptAsync(RateLimitsReadRequest, timeoutCts.Token);

            await foreach (string line in process.ReadOutputLinesAsync(timeoutCts.Token))
            {
                // Skip the initialize response (id:1) and every server notification (no id); return the
                // rate-limits response (id:2) as soon as it arrives.
                if (IsResponseWithId(line, RateLimitsRequestId))
                {
                    return line;
                }
            }

            return null; // the stream ended before the id:2 response arrived
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own read timeout fired (not a caller-requested cancellation) — fail open.
            return null;
        }
    }

    private static bool IsResponseWithId(string line, int id)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("id", out JsonElement idElement)
                && idElement.ValueKind == JsonValueKind.Number
                && idElement.TryGetInt32(out int value)
                && value == id;
        }
        catch (JsonException)
        {
            return false; // banners / non-JSON noise / notifications without an id
        }
    }
}
