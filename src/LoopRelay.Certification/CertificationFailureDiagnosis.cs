using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Codex;
using LoopRelay.Agents.Services.Process;
using LoopRelay.Agents.Services.Usage;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Certification;

public sealed record CertificationFailureContext(
    string InvocationId,
    bool LiveProviderInvoked,
    CertificationClassification Classification,
    bool QuotaExhaustionConfirmed,
    string ExistingExplanation,
    IReadOnlyList<string> DeterministicEvidence,
    string? ActionableNextStep,
    object FailureResult,
    string AuthorityRoot,
    string RetainedFixtureSource,
    string CodexHome,
    string CodexExecutable,
    IReadOnlyList<string> RelevantSourceFiles,
    string? FailedTransition = null,
    string? ProviderTurnIdHint = null,
    bool ExplicitRequest = false,
    string? ExistingAttemptRecord = null);

public interface ICertificationFailureDiagnoser
{
    Task<CertificationDiagnosisOutcome> DiagnoseIfNeededAsync(
        CertificationFailureContext context,
        CancellationToken cancellationToken);
}

public sealed record CertificationDiagnosticRequest(
    string InvocationId,
    string ScratchRoot,
    string Prompt);

public interface ICertificationDiagnosticAgent
{
    Task<string> AnalyzeAsync(
        CertificationDiagnosticRequest request,
        CancellationToken cancellationToken);
}

public sealed record CertificationPrivateSessionEvent(
    int EventOrdinal,
    int RolloutLine,
    string Kind,
    string Role,
    string? Name,
    string? CallId,
    int? PairedEventOrdinal,
    string Content,
    string SourceLocation);

internal sealed record CertificationPrivateSessionSegment(
    bool TurnFound,
    bool Partial,
    bool Truncated,
    int SourceEventCount,
    int PersistedByteCount,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<CertificationPrivateSessionEvent> Events);

internal sealed record CertificationTelemetryRecord(
    DateTimeOffset Timestamp,
    string SessionId,
    int TurnIndex,
    string? ProviderThreadId,
    string? ProviderTurnId,
    string? CodexLogPath,
    string? CertificationInvocationId,
    string? InvocationRole,
    string Source);

internal sealed record CertificationCorrelationResult(
    CertificationTelemetryResolution Resolution,
    string? RolloutPath,
    bool Partial);

internal sealed record CertificationRetainedPathObservation(
    string Path,
    bool Exists,
    long? Length,
    string? Sha256);

internal interface ICertificationRolloutResolver
{
    Task<CodexRolloutReadResult> ReadExactAsync(
        string codexHome,
        string threadId,
        CancellationToken cancellationToken);
}

internal sealed class CertificationRolloutResolver : ICertificationRolloutResolver
{
    public Task<CodexRolloutReadResult> ReadExactAsync(
        string codexHome,
        string threadId,
        CancellationToken cancellationToken) =>
        new CodexRolloutRepository().ReadExactAsync(codexHome, threadId, cancellationToken);
}

public sealed class CertificationFailureDiagnoser : ICertificationFailureDiagnoser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private static readonly JsonSerializerOptions JsonLineOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly TimeSpan CollectionBudget = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DiagnosisBudget = TimeSpan.FromMinutes(10);
    private const long MaxDiagnosticInputFileBytes = 524_288;
    private const long MaxDiagnosticInputsBytes = 2_097_152;

    private readonly ICertificationDiagnosticAgent? diagnosticAgent;
    private readonly CertificationPrivateRolloutReader privateReader;
    private readonly ICertificationRolloutResolver rolloutResolver;

    public CertificationFailureDiagnoser(ICertificationDiagnosticAgent? diagnosticAgent = null)
    {
        this.diagnosticAgent = diagnosticAgent;
        privateReader = new CertificationPrivateRolloutReader();
        rolloutResolver = new CertificationRolloutResolver();
    }

    internal CertificationFailureDiagnoser(
        ICertificationDiagnosticAgent? diagnosticAgent,
        CertificationPrivateRolloutReader privateReader,
        ICertificationRolloutResolver rolloutResolver)
    {
        this.diagnosticAgent = diagnosticAgent;
        this.privateReader = privateReader;
        this.rolloutResolver = rolloutResolver;
    }

    public async Task<CertificationDiagnosisOutcome> DiagnoseIfNeededAsync(
        CertificationFailureContext context,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context.InvocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.AuthorityRoot);
        if (context.Classification == CertificationClassification.Passed)
            throw new ArgumentException("Successful certification results must not enter failure diagnosis.", nameof(context));

        string attemptRoot = await RetainFailureAsync(context, cancellationToken);
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        if (!context.ExplicitRequest && !context.LiveProviderInvoked)
        {
            return await CompleteStatusAsync(
                attemptRoot,
                new CertificationDiagnosisStatus(
                    CertificationDiagnosisDisposition.NotNeeded,
                    context.InvocationId,
                    "no-live-provider-invoked",
                    createdAt),
                context.ExistingExplanation,
                cancellationToken,
                context.DeterministicEvidence,
                context.ActionableNextStep);
        }

        bool quotaBypass = CertificationDiagnosisPolicy.BypassReason(context) == "confirmed-quota-exhaustion";
        if (!context.ExplicitRequest && quotaBypass)
        {
            return await CompleteStatusAsync(
                attemptRoot,
                new CertificationDiagnosisStatus(
                    CertificationDiagnosisDisposition.NotNeeded,
                    context.InvocationId,
                    "confirmed-quota-exhaustion",
                    createdAt),
                context.ExistingExplanation,
                cancellationToken,
                context.DeterministicEvidence,
                context.ActionableNextStep);
        }

        try
        {
            CertificationCorrelationResult correlation;
            CertificationPrivateSessionSegment? retainedSegment = null;
            string retainedSegmentPath = Path.Combine(attemptRoot, "session-segment.private.jsonl");
            string retainedResolutionPath = Path.Combine(attemptRoot, "telemetry-reference.json");
            if (context.ExistingAttemptRecord is { Length: > 0 }
                && File.Exists(retainedSegmentPath)
                && File.Exists(retainedResolutionPath))
            {
                CertificationTelemetryResolution retainedResolution = await ReadResolutionOrDefaultAsync(
                    attemptRoot,
                    context.InvocationId,
                    cancellationToken);
                retainedSegment = await ReadPersistedSegmentAsync(retainedSegmentPath, cancellationToken);
                correlation = new CertificationCorrelationResult(
                    retainedResolution,
                    retainedSegmentPath,
                    retainedResolution.Status == CertificationTelemetryResolutionStatus.Partial);
            }
            else
            {
                using var collection = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                collection.CancelAfter(CollectionBudget);
                correlation = await CorrelateAsync(context, collection.Token);
                await WriteJsonAsync(
                    retainedResolutionPath,
                    correlation.Resolution,
                    cancellationToken);
            }

            if (correlation.RolloutPath is null)
            {
                return await CompleteUnavailableAsync(
                    context,
                    attemptRoot,
                    correlation.Resolution,
                    correlation.Resolution.Diagnostic ?? correlation.Resolution.Status.ToString(),
                    cancellationToken);
            }

            CertificationPrivateSessionSegment segment;
            if (retainedSegment is not null)
            {
                segment = retainedSegment;
            }
            else
            {
                using var collection = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                collection.CancelAfter(CollectionBudget);
                segment = await privateReader.ReadAsync(
                    correlation.RolloutPath,
                    correlation.Resolution.ProviderTurnId!,
                    correlation.Partial,
                    collection.Token);
            }

            if (!segment.TurnFound)
            {
                CertificationTelemetryResolution turnAbsent = correlation.Resolution with
                {
                    Status = CertificationTelemetryResolutionStatus.TurnAbsent,
                    Diagnostic = "The exact rollout did not contain the recorded provider turn.",
                };
                await WriteJsonAsync(
                    Path.Combine(attemptRoot, "telemetry-reference.json"),
                    turnAbsent,
                    cancellationToken);
                return await CompleteUnavailableAsync(
                    context,
                    attemptRoot,
                    turnAbsent,
                    "provider-turn-absent",
                    cancellationToken);
            }

            string[] segmentWarnings = correlation.Resolution.Warnings
                .Concat(segment.Warnings)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            CertificationTelemetryResolution evidenceResolution = correlation.Resolution with
            {
                Warnings = segmentWarnings,
            };
            correlation = correlation with { Resolution = evidenceResolution };
            if (context.ExistingAttemptRecord is null)
                await WriteJsonAsync(retainedResolutionPath, evidenceResolution, cancellationToken);

            if (retainedSegment is null)
                await PersistSegmentAsync(retainedSegmentPath, segment, cancellationToken);

            string scratch = Path.Combine(attemptRoot, ".diagnostic-scratch");
            ProtectFile(Path.Combine(attemptRoot, "telemetry-reference.json"));
            ProtectFile(retainedSegmentPath);
            Directory.CreateDirectory(scratch);
            try
            {
                string prompt;
                using (var preparation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    preparation.CancelAfter(CollectionBudget);
                    prompt = await BuildDiagnosticInputsAsync(
                        context,
                        attemptRoot,
                        scratch,
                        segment,
                        preparation.Token);
                }
                ProtectFile(Path.Combine(attemptRoot, "retained-case-observations.json"));
                ICertificationDiagnosticAgent agent = diagnosticAgent
                    ?? new CodexCertificationDiagnosticAgent(context.CodexExecutable);
                string raw;
                using (var diagnosis = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    diagnosis.CancelAfter(DiagnosisBudget);
                    raw = await agent.AnalyzeAsync(
                        new CertificationDiagnosticRequest(context.InvocationId, scratch, prompt),
                        diagnosis.Token);
                }

                await File.WriteAllTextAsync(
                    Path.Combine(scratch, "diagnosis-response.json"),
                    raw,
                    cancellationToken);
                CertificationFailureDiagnosis accepted = ValidateDiagnosis(
                    raw,
                    context.InvocationId,
                    correlation.Resolution,
                    attemptRoot,
                    scratch);
                await WriteJsonAsync(Path.Combine(attemptRoot, "diagnosis.json"), accepted, cancellationToken);
                await File.WriteAllTextAsync(
                    Path.Combine(attemptRoot, "diagnosis.md"),
                    RenderDiagnosis(accepted),
                    cancellationToken);
                var status = new CertificationDiagnosisStatus(
                    accepted.Disposition,
                    context.InvocationId,
                    accepted.Disposition == CertificationDiagnosisDisposition.Inconclusive
                        ? "available-evidence-inconclusive"
                        : null,
                    accepted.CreatedAt);
                await WriteJsonAsync(Path.Combine(attemptRoot, "diagnosis-status.json"), status, cancellationToken);
                return new CertificationDiagnosisOutcome(status, attemptRoot, accepted);
            }
            finally
            {
                DeleteScratch(scratch);
            }
        }
        catch (OperationCanceledException)
        {
            CertificationTelemetryResolution unavailable = Unresolved(
                context.InvocationId,
                CertificationTelemetryResolutionStatus.NotAttempted,
                "diagnostic-collection-or-agent-cancelled");
            return await CompleteUnavailableAsync(
                context,
                attemptRoot,
                unavailable,
                cancellationToken.IsCancellationRequested ? "operator-cancelled" : "diagnostic-timeout",
                CancellationToken.None);
        }
        catch (Exception exception) when (exception is not CertificationRetentionException)
        {
            CertificationTelemetryResolution resolution = await ReadResolutionOrDefaultAsync(
                attemptRoot,
                context.InvocationId,
                CancellationToken.None);
            return await CompleteUnavailableAsync(
                context,
                attemptRoot,
                resolution,
                $"diagnostic-mechanism-failed:{exception.GetType().Name}",
                CancellationToken.None);
        }
    }

    private static async Task<string> RetainFailureAsync(
        CertificationFailureContext context,
        CancellationToken cancellationToken)
    {
        if (context.ExistingAttemptRecord is { Length: > 0 } existing)
        {
            string fullExisting = Path.GetFullPath(existing);
            if (!Directory.Exists(fullExisting))
            {
                throw new CertificationRetentionException("The requested retained attempt record does not exist.");
            }

            return fullExisting;
        }

        string attempts = Path.Combine(context.AuthorityRoot, "evidence", "attempts");
        string attemptRoot = Path.Combine(attempts, $"attempt-{SafeName(context.InvocationId)}");
        try
        {
            Directory.CreateDirectory(attempts);
            if (Directory.Exists(attemptRoot))
            {
                throw new CertificationRetentionException(
                    $"Attempt record already exists and will not be overwritten: {attemptRoot}");
            }

            Directory.CreateDirectory(attemptRoot);
            string retained = Path.Combine(attemptRoot, "retained-case");
            CopyDirectory(context.RetainedFixtureSource, retained);
            await WriteJsonAsync(Path.Combine(attemptRoot, "failure.json"), context.FailureResult, cancellationToken);
            await WriteJsonAsync(
                Path.Combine(attemptRoot, "diagnosis-status.json"),
                new CertificationDiagnosisStatus(
                    CertificationDiagnosisDisposition.Unavailable,
                    context.InvocationId,
                    "diagnosis-pending",
                    DateTimeOffset.UtcNow),
                cancellationToken);
            ProtectDirectory(retained);
            ProtectFile(Path.Combine(attemptRoot, "failure.json"));
            return attemptRoot;
        }
        catch (CertificationRetentionException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new CertificationRetentionException(
                $"Failed to retain certification attempt {context.InvocationId}.",
                exception);
        }
    }

    private static async Task<CertificationDiagnosisOutcome> CompleteStatusAsync(
        string attemptRoot,
        CertificationDiagnosisStatus status,
        string explanation,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? deterministicEvidence = null,
        string? actionableNextStep = null)
    {
        var document = new
        {
            status.Disposition,
            status.InvocationId,
            status.BypassOrFailureReason,
            status.CreatedAt,
            ExistingExplanation = explanation,
            DeterministicEvidence = deterministicEvidence ?? [],
            ActionableNextStep = actionableNextStep,
        };
        await WriteJsonAsync(Path.Combine(attemptRoot, "diagnosis-status.json"), document, cancellationToken);
        return new CertificationDiagnosisOutcome(status, attemptRoot);
    }

    private static async Task<CertificationDiagnosisOutcome> CompleteUnavailableAsync(
        CertificationFailureContext context,
        string attemptRoot,
        CertificationTelemetryResolution resolution,
        string reason,
        CancellationToken cancellationToken)
    {
        string telemetryPath = Path.Combine(attemptRoot, "telemetry-reference.json");
        if (!File.Exists(telemetryPath))
            await WriteJsonAsync(telemetryPath, resolution, cancellationToken);
        var diagnosis = new CertificationFailureDiagnosis(
            CertificationDiagnosisDisposition.Unavailable,
            context.InvocationId,
            resolution,
            "The diagnostic mechanism could not produce an evidence-backed explanation.",
            [],
            [],
            [reason],
            null,
            DateTimeOffset.UtcNow);
        var status = new CertificationDiagnosisStatus(
            CertificationDiagnosisDisposition.Unavailable,
            context.InvocationId,
            reason,
            diagnosis.CreatedAt);
        await WriteJsonAsync(Path.Combine(attemptRoot, "diagnosis.json"), diagnosis, cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(attemptRoot, "diagnosis.md"),
            RenderDiagnosis(diagnosis),
            cancellationToken);
        await WriteJsonAsync(Path.Combine(attemptRoot, "diagnosis-status.json"), status, cancellationToken);
        return new CertificationDiagnosisOutcome(status, attemptRoot, diagnosis);
    }

    private async Task<CertificationCorrelationResult> CorrelateAsync(
        CertificationFailureContext context,
        CancellationToken cancellationToken)
    {
        CertificationTelemetryRecord[] records = await ReadTelemetryAsync(
            context.RetainedFixtureSource,
            context.InvocationId,
            cancellationToken);
        if (records.Length == 0)
        {
            return new CertificationCorrelationResult(
                Unresolved(context.InvocationId, CertificationTelemetryResolutionStatus.Absent,
                    "No telemetry row matched the certification invocation id."),
                null,
                false);
        }

        CertificationTelemetryRecord[] candidates = records
            .Where(record => record.InvocationRole is null or "product")
            .Where(record => !string.IsNullOrWhiteSpace(record.ProviderTurnId))
            .ToArray();
        if (context.ProviderTurnIdHint is { Length: > 0 } hint)
        {
            candidates = candidates.Where(record => record.ProviderTurnId == hint).ToArray();
        }

        string[] turns = candidates.Select(record => record.ProviderTurnId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (turns.Length == 0)
        {
            return new CertificationCorrelationResult(
                Unresolved(context.InvocationId, CertificationTelemetryResolutionStatus.TurnAbsent,
                    "Invocation telemetry did not record a provider turn id."),
                null,
                false);
        }

        if (turns.Length > 1)
        {
            return new CertificationCorrelationResult(
                new CertificationTelemetryResolution(
                    CertificationTelemetryResolutionStatus.Ambiguous,
                    CertificationRolloutResolutionMethod.None,
                    context.InvocationId,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    candidates.Select(Candidate).ToArray(),
                    [],
                    "The failed CLI invocation recorded more than one provider turn and no exact failed-turn hint selected one."),
                null,
                false);
        }

        CertificationTelemetryRecord selected = candidates
            .OrderBy(record => record.Timestamp)
            .ThenBy(record => record.TurnIndex)
            .Last();
        string? recorded = CanonicalAllowedPath(selected.CodexLogPath, context.CodexHome);
        CodexRolloutReadResult? exact = null;
        if (selected.ProviderThreadId is { Length: > 0 } threadId)
        {
            exact = await rolloutResolver.ReadExactAsync(
                context.CodexHome,
                threadId,
                cancellationToken);
        }

        string? exactPath = exact?.Location is { Length: > 0 } location
            ? Path.GetFullPath(location)
            : null;
        if (exactPath is not null && recorded is not null && !SamePath(exactPath, recorded))
        {
            return new CertificationCorrelationResult(
                Resolution(
                    CertificationTelemetryResolutionStatus.Ambiguous,
                    CertificationRolloutResolutionMethod.None,
                    selected,
                    recorded,
                    exactPath,
                    records,
                    [],
                    "Exact thread resolution and the telemetry-recorded path identify different rollout files."),
                null,
                false);
        }

        if (exact is not null && exact.Status is CodexRolloutReadStatus.Complete or CodexRolloutReadStatus.Partial
            && exactPath is not null)
        {
            return new CertificationCorrelationResult(
                Resolution(
                    exact.Status == CodexRolloutReadStatus.Partial
                        ? CertificationTelemetryResolutionStatus.Partial
                        : CertificationTelemetryResolutionStatus.Resolved,
                    CertificationRolloutResolutionMethod.ExactThread,
                    selected,
                    recorded,
                    exactPath,
                    records,
                    exact.Omissions,
                    exact.Diagnostic),
                exactPath,
                exact.Status == CodexRolloutReadStatus.Partial);
        }

        if ((exact is null || exact.Status == CodexRolloutReadStatus.Absent)
            && recorded is not null
            && File.Exists(recorded))
        {
            return new CertificationCorrelationResult(
                Resolution(
                    CertificationTelemetryResolutionStatus.Resolved,
                    CertificationRolloutResolutionMethod.RecordedPath,
                    selected,
                    recorded,
                    recorded,
                    records,
                    [],
                    exact?.Diagnostic),
                recorded,
                false);
        }

        CertificationTelemetryResolutionStatus status = exact?.Status switch
        {
            CodexRolloutReadStatus.Partial => CertificationTelemetryResolutionStatus.Partial,
            CodexRolloutReadStatus.Corrupt => CertificationTelemetryResolutionStatus.Corrupt,
            CodexRolloutReadStatus.PermissionDenied => CertificationTelemetryResolutionStatus.PermissionDenied,
            CodexRolloutReadStatus.Ambiguous => CertificationTelemetryResolutionStatus.Ambiguous,
            _ => CertificationTelemetryResolutionStatus.Absent,
        };
        return new CertificationCorrelationResult(
            Resolution(
                status,
                CertificationRolloutResolutionMethod.None,
                selected,
                selected.CodexLogPath,
                exactPath,
                records,
                exact?.Omissions ?? [],
                exact?.Diagnostic ?? "Neither an exact rollout nor an allowed recorded rollout path resolved."),
            null,
            false);
    }

    private static CertificationTelemetryResolution Resolution(
        CertificationTelemetryResolutionStatus status,
        CertificationRolloutResolutionMethod method,
        CertificationTelemetryRecord selected,
        string? recorded,
        string? resolved,
        IReadOnlyList<CertificationTelemetryRecord> records,
        IReadOnlyList<string> warnings,
        string? diagnostic) => new(
            status,
            method,
            selected.CertificationInvocationId!,
            selected.SessionId,
            selected.TurnIndex,
            selected.ProviderThreadId,
            selected.ProviderTurnId,
            recorded is null ? null : RedactPath(recorded),
            resolved is null ? null : RedactPath(resolved),
            records.Select(Candidate).ToArray(),
            warnings,
            diagnostic);

    private static CertificationTelemetryResolution Unresolved(
        string invocationId,
        CertificationTelemetryResolutionStatus status,
        string diagnostic) => new(
            status,
            CertificationRolloutResolutionMethod.None,
            invocationId,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            diagnostic);

    private static async Task<CertificationTelemetryRecord[]> ReadTelemetryAsync(
        string repository,
        string invocationId,
        CancellationToken cancellationToken)
    {
        string directory = Path.Combine(repository, ".LoopRelay", "telemetry");
        if (!Directory.Exists(directory)) return [];
        var records = new List<CertificationTelemetryRecord>();
        foreach (string path in Directory.EnumerateFiles(directory, "*.jsonl", SearchOption.TopDirectoryOnly)
                     .Order(StringComparer.Ordinal))
        {
            int lineNumber = 0;
            foreach (string line in await File.ReadAllLinesAsync(path, cancellationToken))
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    using JsonDocument document = JsonDocument.Parse(line);
                    JsonElement root = document.RootElement;
                    if (String(root, "certificationInvocationId") != invocationId) continue;
                    records.Add(new CertificationTelemetryRecord(
                        DateTimeOffset.TryParse(String(root, "timestamp"), out DateTimeOffset timestamp)
                            ? timestamp
                            : DateTimeOffset.MinValue,
                        String(root, "sessionId") ?? string.Empty,
                        Int32(root, "turnIndex") ?? 0,
                        String(root, "providerThreadId"),
                        String(root, "providerTurnId"),
                        String(root, "codexLogPath"),
                        String(root, "certificationInvocationId"),
                        String(root, "invocationRole"),
                        $"{RedactPath(path)}#line:{lineNumber}"));
                }
                catch (JsonException)
                {
                    // One malformed compatibility-export row does not authorize guessing.
                }
            }
        }

        return records.ToArray();
    }

    private static async Task PersistSegmentAsync(
        string path,
        CertificationPrivateSessionSegment segment,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        foreach (CertificationPrivateSessionEvent item in segment.Events)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(item, JsonLineOptions).AsMemory(), cancellationToken);
        }
    }

    private static async Task<CertificationPrivateSessionSegment> ReadPersistedSegmentAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var events = new List<CertificationPrivateSessionEvent>();
        int bytes = 0;
        foreach (string line in await File.ReadAllLinesAsync(path, cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            CertificationPrivateSessionEvent item = JsonSerializer.Deserialize<CertificationPrivateSessionEvent>(
                line,
                JsonLineOptions)
                ?? throw new InvalidDataException("A retained private session event was empty.");
            events.Add(item);
            bytes += Encoding.UTF8.GetByteCount(item.Content);
        }
        return new CertificationPrivateSessionSegment(
            true,
            false,
            false,
            events.Count,
            bytes,
            ["reused-retained-session-evidence"],
            events);
    }

    private static async Task<string> BuildDiagnosticInputsAsync(
        CertificationFailureContext context,
        string attemptRoot,
        string scratch,
        CertificationPrivateSessionSegment segment,
        CancellationToken cancellationToken)
    {
        string input = Path.Combine(scratch, "input");
        string retainedInput = Path.Combine(input, "retained-case");
        string sourceInput = Path.Combine(input, "source");
        Directory.CreateDirectory(retainedInput);
        Directory.CreateDirectory(sourceInput);
        File.Copy(Path.Combine(attemptRoot, "failure.json"), Path.Combine(input, "failure.json"));
        File.Copy(
            Path.Combine(attemptRoot, "session-segment.private.jsonl"),
            Path.Combine(input, "session-segment.private.jsonl"));
        long copiedInputBytes = new FileInfo(Path.Combine(input, "failure.json")).Length
            + new FileInfo(Path.Combine(input, "session-segment.private.jsonl")).Length;

        string evidenceText = context.ExistingExplanation + "\n"
            + string.Join('\n', context.DeterministicEvidence) + "\n"
            + string.Join('\n', segment.Events.Select(item => item.Content));
        string[] namedPaths = FindNamedPaths(evidenceText, context.RetainedFixtureSource).Take(16).ToArray();
        foreach (string relative in namedPaths.Where(relative => File.Exists(Path.Combine(
                     context.RetainedFixtureSource,
                     relative.Replace('/', Path.DirectorySeparatorChar)))).Take(8))
        {
            string source = Path.Combine(context.RetainedFixtureSource,
                relative.Replace('/', Path.DirectorySeparatorChar));
            string destination = Path.Combine(retainedInput,
                relative.Replace('/', Path.DirectorySeparatorChar));
            TryCopyBounded(source, destination, ref copiedInputBytes);
        }

        CertificationRetainedPathObservation[] observations = namedPaths.Select(relative =>
        {
            string path = Path.Combine(context.RetainedFixtureSource,
                relative.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path)
                ? new CertificationRetainedPathObservation(
                    relative,
                    true,
                    new FileInfo(path).Length,
                    new FileInfo(path).Length <= MaxDiagnosticInputFileBytes ? Sha256File(path) : null)
                : new CertificationRetainedPathObservation(relative, false, null, null);
        }).ToArray();
        string observationsPath = Path.Combine(attemptRoot, "retained-case-observations.json");
        if (!File.Exists(observationsPath))
            await WriteJsonAsync(observationsPath, observations, cancellationToken);
        File.Copy(observationsPath, Path.Combine(input, "retained-case-observations.json"));

        int sourceIndex = 0;
        foreach (string source in context.RelevantSourceFiles
                     .Where(File.Exists)
                     .Distinct(PathComparer())
                     .Take(12))
        {
            sourceIndex++;
            string destination = Path.Combine(sourceInput, $"{sourceIndex:D2}-{Path.GetFileName(source)}");
            TryCopyBounded(source, destination, ref copiedInputBytes);
        }

        var manifest = new
        {
            context.InvocationId,
            context.FailedTransition,
            Failure = "input/failure.json",
            Session = "input/session-segment.private.jsonl",
            RetainedCaseObservations = "input/retained-case-observations.json",
            RetainedCase = Directory.EnumerateFiles(retainedInput, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(scratch, path).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToArray(),
            Source = Directory.EnumerateFiles(sourceInput, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(scratch, path).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToArray(),
            segment.Partial,
            segment.Truncated,
            segment.Warnings,
        };
        await WriteJsonAsync(Path.Combine(input, "manifest.json"), manifest, cancellationToken);

        return $$"""
            You are diagnosing one failed LoopRelay certification attempt. Work read-only. Do not repair files,
            run certification, commit, publish, or make any assertion without a citation. Inspect only files under
            `input/`. The failure result remains authoritative and cannot be changed by this diagnosis.

            Identify the first observed contract divergence. Keep observed facts separate from inferences. Do not
            claim an ultimate causal defect unless the supplied evidence supports it. Include missing evidence and
            plausible alternatives (record alternatives in `missingEvidence`). The summary must name the likely cause
            and the directly implicated validator, workflow, source, or prompt area. Hidden or encrypted reasoning is
            intentionally absent.

            Return exactly one JSON object, with no code fence, using this shape:
            {
              "disposition": "Completed" | "Inconclusive",
              "invocationId": "{{context.InvocationId}}",
              "summary": "concise explanation",
              "facts": [{"text":"observed fact","citations":[{"source":"failure|session|retained-case|source","location":"input/... or rollout#line:N"}]}],
              "inferences": [{"text":"labeled inference","citations":[{"source":"failure|session|retained-case|source","location":"input/... or rollout#line:N"}]}],
              "missingEvidence": ["what remains unknown"],
              "firstObservedContractDivergence": {"text":"first divergence","citations":[{"source":"...","location":"..."}]}
            }

            A Completed result requires at least two cited facts, at least one cited inference, and a cited first
            divergence. Cite retained path absence through `retained-case-observations.json#/...`; cite an existing
            retained file through `input/retained-case/<path>`. Use Inconclusive when the bounded evidence cannot
            support the required claims.
            """;
    }

    private static CertificationFailureDiagnosis ValidateDiagnosis(
        string raw,
        string invocationId,
        CertificationTelemetryResolution resolution,
        string attemptRoot,
        string scratchRoot)
    {
        DiagnosticAgentResponse response;
        try
        {
            response = JsonSerializer.Deserialize<DiagnosticAgentResponse>(raw, JsonOptions)
                ?? throw new InvalidDataException("Diagnostic output was empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Diagnostic output was not valid JSON.", exception);
        }

        if (response.InvocationId != invocationId)
        {
            throw new InvalidDataException("Diagnostic invocation id did not match the retained attempt.");
        }

        CertificationDiagnosisDisposition disposition = response.Disposition switch
        {
            "Completed" => CertificationDiagnosisDisposition.Completed,
            "Inconclusive" => CertificationDiagnosisDisposition.Inconclusive,
            _ => throw new InvalidDataException("Diagnostic disposition must be Completed or Inconclusive."),
        };
        DiagnosticAgentStatement[] facts = response.Facts ?? [];
        DiagnosticAgentStatement[] inferences = response.Inferences ?? [];
        ValidateStatements(facts, "fact", attemptRoot, scratchRoot);
        ValidateStatements(inferences, "inference", attemptRoot, scratchRoot);
        if (string.IsNullOrWhiteSpace(response.Summary))
        {
            throw new InvalidDataException("Diagnostic summary is required.");
        }

        if (disposition == CertificationDiagnosisDisposition.Completed)
        {
            if (facts.Length < 2 || inferences.Length == 0 || response.FirstObservedContractDivergence is null)
            {
                throw new InvalidDataException(
                    "Completed diagnosis requires two facts, an inference, and the first observed divergence.");
            }
            ValidateStatements([response.FirstObservedContractDivergence], "first divergence", attemptRoot, scratchRoot);
        }

        IReadOnlyList<CertificationDiagnosticStatement> acceptedFacts = facts
            .Select(statement => Convert(statement, attemptRoot)).ToArray();
        IReadOnlyList<CertificationDiagnosticStatement> acceptedInferences = inferences
            .Select(statement => Convert(statement, attemptRoot)).ToArray();
        CertificationDiagnosticStatement? divergence = response.FirstObservedContractDivergence is null
            ? null
            : Convert(response.FirstObservedContractDivergence, attemptRoot);
        string summary = Promote(response.Summary, attemptRoot);
        string[] missingEvidence = (response.MissingEvidence ?? [])
            .Select(item => Promote(item, attemptRoot))
            .ToArray();
        string promoted = summary + "\n"
            + string.Join('\n', acceptedFacts.Select(item => item.Text)) + "\n"
            + string.Join('\n', acceptedInferences.Select(item => item.Text)) + "\n"
            + divergence?.Text;
        IReadOnlyList<string> privacy = PrivacyScanner.Scan(promoted, attemptRoot);
        if (privacy.Count > 0)
        {
            throw new InvalidDataException($"Diagnostic output failed privacy validation: {string.Join(',', privacy)}");
        }

        return new CertificationFailureDiagnosis(
            disposition,
            invocationId,
            resolution,
            summary,
            acceptedFacts,
            acceptedInferences,
            missingEvidence,
            divergence,
            DateTimeOffset.UtcNow);
    }

    private static void ValidateStatements(
        IReadOnlyList<DiagnosticAgentStatement> statements,
        string kind,
        string attemptRoot,
        string scratchRoot)
    {
        foreach (DiagnosticAgentStatement statement in statements)
        {
            if (string.IsNullOrWhiteSpace(statement.Text) || statement.Citations is not { Length: > 0 })
            {
                throw new InvalidDataException($"Every diagnostic {kind} requires text and a citation.");
            }

            foreach (CertificationEvidenceCitation citation in statement.Citations)
            {
                if (citation.Source is not ("failure" or "session" or "retained-case" or "source")
                    || string.IsNullOrWhiteSpace(citation.Location)
                    || citation.Location.Contains("..", StringComparison.Ordinal)
                    || !CitationExists(citation, attemptRoot, scratchRoot))
                {
                    throw new InvalidDataException($"Diagnostic {kind} contains an invalid citation.");
                }
            }
        }
    }

    private static bool CitationExists(
        CertificationEvidenceCitation citation,
        string attemptRoot,
        string scratchRoot)
    {
        string normalized = citation.Location.Replace('\\', '/');
        switch (citation.Source)
        {
            case "failure":
                return normalized.StartsWith("input/failure.json", StringComparison.Ordinal)
                    && File.Exists(Path.Combine(attemptRoot, "failure.json"));
            case "retained-case":
                if (normalized.StartsWith("retained-case-observations.json", StringComparison.Ordinal))
                    return File.Exists(Path.Combine(attemptRoot, "retained-case-observations.json"));
                const string retainedPrefix = "input/retained-case/";
                if (!normalized.StartsWith(retainedPrefix, StringComparison.Ordinal)) return false;
                string retained = Path.GetFullPath(Path.Combine(
                    attemptRoot,
                    "retained-case",
                    normalized[retainedPrefix.Length..].Replace('/', Path.DirectorySeparatorChar)));
                return IsWithin(retained, Path.Combine(attemptRoot, "retained-case")) && File.Exists(retained);
            case "source":
                const string sourcePrefix = "input/source/";
                if (!normalized.StartsWith(sourcePrefix, StringComparison.Ordinal)) return false;
                string source = Path.GetFullPath(Path.Combine(
                    scratchRoot,
                    normalized.Replace('/', Path.DirectorySeparatorChar)));
                return IsWithin(source, Path.Combine(scratchRoot, "input", "source")) && File.Exists(source);
            case "session":
                Match match = Regex.Match(normalized, @"line:(\d+)$");
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out int line)) return false;
                string segment = Path.Combine(attemptRoot, "session-segment.private.jsonl");
                if (!File.Exists(segment)) return false;
                foreach (string item in File.ReadLines(segment))
                {
                    try
                    {
                        using JsonDocument document = JsonDocument.Parse(item);
                        if (Int32(document.RootElement, "rolloutLine") == line) return true;
                    }
                    catch (JsonException)
                    {
                        return false;
                    }
                }
                return false;
            default:
                return false;
        }
    }

    private static CertificationDiagnosticStatement Convert(
        DiagnosticAgentStatement statement,
        string attemptRoot) =>
        new(Promote(statement.Text!, attemptRoot), statement.Citations!);

    private static string Promote(string value, string attemptRoot) =>
        EvidenceNormalizer.Normalize(CertificationEvidenceRedactor.Redact(value), attemptRoot);

    private static string RenderDiagnosis(CertificationFailureDiagnosis diagnosis)
    {
        var text = new StringBuilder()
            .AppendLine("# Certification failure diagnosis")
            .AppendLine()
            .AppendLine($"Disposition: {diagnosis.Disposition}")
            .AppendLine()
            .AppendLine(diagnosis.Summary)
            .AppendLine()
            .AppendLine("## Facts")
            .AppendLine();
        foreach (CertificationDiagnosticStatement fact in diagnosis.Facts)
        {
            text.Append("- ").Append(fact.Text).Append(" — ")
                .AppendLine(string.Join(", ", fact.Citations.Select(Citation)));
        }

        text.AppendLine().AppendLine("## Inferences").AppendLine();
        foreach (CertificationDiagnosticStatement inference in diagnosis.Inferences)
        {
            text.Append("- ").Append(inference.Text).Append(" — ")
                .AppendLine(string.Join(", ", inference.Citations.Select(Citation)));
        }

        if (diagnosis.FirstObservedContractDivergence is { } divergence)
        {
            text.AppendLine().AppendLine("## First observed contract divergence").AppendLine()
                .Append(divergence.Text).Append(" — ")
                .AppendLine(string.Join(", ", divergence.Citations.Select(Citation)));
        }

        if (diagnosis.MissingEvidence.Count > 0)
        {
            text.AppendLine().AppendLine("## Missing evidence").AppendLine();
            foreach (string missing in diagnosis.MissingEvidence) text.Append("- ").AppendLine(missing);
        }

        return text.ToString();
    }

    private static string Citation(CertificationEvidenceCitation item) =>
        $"{item.Source}:{item.Location}";

    private static IEnumerable<string> FindNamedPaths(string text, string retainedRoot)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(
                     text.Replace('\\', '/'),
                     @"(?<![A-Za-z0-9_.-])(?:\.?[A-Za-z0-9_-]+/)+[A-Za-z0-9_.-]+"))
        {
            string relative = match.Value.TrimStart('/');
            string candidate = Path.GetFullPath(Path.Combine(
                retainedRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (IsWithin(candidate, retainedRoot) && seen.Add(relative))
            {
                yield return relative;
            }
        }
    }

    private static string? CanonicalAllowedPath(string? value, string codexHome)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value)) return null;
        string full = Path.GetFullPath(value);
        if (!string.Equals(Path.GetExtension(full), ".jsonl", StringComparison.OrdinalIgnoreCase)) return null;
        return new[] { "sessions", "archived_sessions", "archived" }
            .Select(directory => Path.Combine(codexHome, directory))
            .Any(root => IsWithin(full, root))
                ? full
                : null;
    }

    private static bool IsWithin(string path, string root)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(fullRoot,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string Candidate(CertificationTelemetryRecord record) =>
        $"{record.SessionId}:{record.TurnIndex}:{record.ProviderThreadId ?? "(thread-missing)"}:{record.ProviderTurnId ?? "(turn-missing)"}:{record.Source}";

    private static string RedactPath(string path)
    {
        string normalized = path.Replace('\\', '/');
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace('\\', '/').TrimEnd('/');
        if (profile.Length > 0)
        {
            normalized = normalized.Replace(profile, "<USER_PROFILE>",
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        return normalized;
    }

    private static string SafeName(string value) =>
        Regex.Replace(value, "[^A-Za-z0-9_.-]", "-");

    private static bool TryCopyBounded(string source, string destination, ref long copiedBytes)
    {
        long length = new FileInfo(source).Length;
        if (length > MaxDiagnosticInputFileBytes || copiedBytes + length > MaxDiagnosticInputsBytes)
            return false;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination);
        copiedBytes += length;
        return true;
    }

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return System.Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static void CopyDirectory(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"Retained fixture source does not exist: {source}");
        }

        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }

    private static void DeleteScratch(string scratch)
    {
        if (!Directory.Exists(scratch)) return;
        foreach (string file in Directory.EnumerateFiles(scratch, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        Directory.Delete(scratch, recursive: true);
    }

    private static void ProtectDirectory(string root)
    {
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            ProtectFile(file);
    }

    private static void ProtectFile(string path)
    {
        if (File.Exists(path))
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
    }

    private static async Task WriteJsonAsync(
        string path,
        object value,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, value.GetType(), JsonOptions, cancellationToken);
    }

    private static async Task<CertificationTelemetryResolution> ReadResolutionOrDefaultAsync(
        string attemptRoot,
        string invocationId,
        CancellationToken cancellationToken)
    {
        string path = Path.Combine(attemptRoot, "telemetry-reference.json");
        if (!File.Exists(path))
        {
            return Unresolved(invocationId, CertificationTelemetryResolutionStatus.NotAttempted,
                "Correlation did not reach a persisted result.");
        }

        try
        {
            await using FileStream stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<CertificationTelemetryResolution>(
                stream,
                JsonOptions,
                cancellationToken)
                ?? Unresolved(invocationId, CertificationTelemetryResolutionStatus.NotAttempted,
                    "Persisted correlation result was empty.");
        }
        catch
        {
            return Unresolved(invocationId, CertificationTelemetryResolutionStatus.Corrupt,
                "Persisted correlation result could not be read.");
        }
    }

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? Int32(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.TryGetInt32(out int parsed)
            ? parsed
            : null;

    private static StringComparer PathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private sealed record DiagnosticAgentResponse(
        string? Disposition,
        string? InvocationId,
        string? Summary,
        DiagnosticAgentStatement[]? Facts,
        DiagnosticAgentStatement[]? Inferences,
        string[]? MissingEvidence,
        DiagnosticAgentStatement? FirstObservedContractDivergence);

    private sealed record DiagnosticAgentStatement(
        string? Text,
        CertificationEvidenceCitation[]? Citations);
}

internal sealed class CertificationPrivateRolloutReader
{
    private readonly int maxEvents;
    private readonly int maxBytes;
    private readonly int adjacentEvents;

    public CertificationPrivateRolloutReader(
        int maxEvents = 160,
        int maxBytes = 262_144,
        int adjacentEvents = 0)
    {
        if (maxEvents <= 0) throw new ArgumentOutOfRangeException(nameof(maxEvents));
        if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        if (adjacentEvents < 0) throw new ArgumentOutOfRangeException(nameof(adjacentEvents));
        this.maxEvents = maxEvents;
        this.maxBytes = maxBytes;
        this.adjacentEvents = adjacentEvents;
    }

    public async Task<CertificationPrivateSessionSegment> ReadAsync(
        string rolloutPath,
        string providerTurnId,
        bool partialFromResolution,
        CancellationToken cancellationToken)
    {
        string[] lines = await File.ReadAllLinesAsync(rolloutPath, cancellationToken);
        var parsed = new List<ParsedLine>();
        bool partial = partialFromResolution;
        for (int index = 0; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index])) continue;
            try
            {
                using JsonDocument document = JsonDocument.Parse(lines[index]);
                parsed.Add(new ParsedLine(index + 1, document.RootElement.Clone(), FindTurnId(document.RootElement)));
            }
            catch (JsonException)
            {
                partial = true;
                break;
            }
        }

        int[] matches = parsed.Select((line, index) => (line, index))
            .Where(item => item.line.TurnId == providerTurnId)
            .Select(item => item.index)
            .ToArray();
        if (matches.Length == 0)
        {
            return new CertificationPrivateSessionSegment(false, partial, false, parsed.Count, 0,
                partial ? ["partial-rollout"] : [], []);
        }

        int start = Math.Max(0, matches.Min() - adjacentEvents);
        int end = Math.Min(parsed.Count - 1, matches.Max() + adjacentEvents);
        while (start < matches.Min() && parsed[start].TurnId is { } prior && prior != providerTurnId) start++;
        while (end > matches.Max() && parsed[end].TurnId is { } next && next != providerTurnId) end--;

        var events = new List<CertificationPrivateSessionEvent>();
        var calls = new Dictionary<string, int>(StringComparer.Ordinal);
        int bytes = 0;
        bool truncated = false;
        for (int index = start; index <= end; index++)
        {
            ParsedLine line = parsed[index];
            if (Hidden(line.Root)) continue;
            foreach (UnpairedEvent candidate in Extract(line.Root, line.LineNumber))
            {
                string content = CertificationEvidenceRedactor.Redact(candidate.Content);
                string source = $"<CODEX_HOME>/{Path.GetFileName(rolloutPath)}#line:{line.LineNumber}";
                int size = Encoding.UTF8.GetByteCount(content);
                if (events.Count >= maxEvents || bytes + size > maxBytes)
                {
                    truncated = true;
                    break;
                }

                int ordinal = events.Count + 1;
                int? paired = null;
                if (candidate.Kind == "tool-call" && candidate.CallId is { Length: > 0 } callId)
                {
                    calls[callId] = ordinal;
                }
                else if (candidate.Kind == "tool-output" && candidate.CallId is { Length: > 0 } outputId
                    && calls.TryGetValue(outputId, out int callOrdinal))
                {
                    paired = callOrdinal;
                }

                events.Add(new CertificationPrivateSessionEvent(
                    ordinal,
                    line.LineNumber,
                    candidate.Kind,
                    candidate.Role,
                    candidate.Name,
                    candidate.CallId,
                    paired,
                    content,
                    source));
                bytes += size;
            }
            if (truncated) break;
        }

        var warnings = new List<string>();
        if (partial) warnings.Add("partial-rollout");
        if (truncated) warnings.Add("bounded-segment-truncated");
        return new CertificationPrivateSessionSegment(
            true,
            partial,
            truncated,
            parsed.Count,
            bytes,
            warnings,
            events);
    }

    private static IEnumerable<UnpairedEvent> Extract(JsonElement root, int line)
    {
        string? outerType = String(root, "type");
        JsonElement payload = root.TryGetProperty("payload", out JsonElement value) ? value : root;
        string? type = String(payload, "type") ?? outerType;
        if (outerType == "response_item" && type == "message")
        {
            string role = String(payload, "role") ?? "unknown";
            if (role is "user" or "assistant")
            {
                string text = ContentText(payload);
                if (text.Length > 0) yield return new UnpairedEvent("message", role, null, null, text);
            }
            yield break;
        }

        if (outerType == "event_msg" && type == "agent_message")
        {
            string? message = String(payload, "message");
            if (!string.IsNullOrWhiteSpace(message))
                yield return new UnpairedEvent("message", "assistant", null, null, message);
            yield break;
        }

        if (type is "function_call" or "custom_tool_call" or "local_shell_call")
        {
            string content = String(payload, "arguments") ?? String(payload, "input")
                ?? String(payload, "command") ?? "(arguments unavailable)";
            yield return new UnpairedEvent(
                "tool-call",
                "assistant",
                String(payload, "name") ?? type,
                String(payload, "call_id") ?? String(payload, "id"),
                content);
            yield break;
        }

        if (type is "function_call_output" or "custom_tool_call_output")
        {
            yield return new UnpairedEvent(
                "tool-output",
                "tool",
                type,
                String(payload, "call_id") ?? String(payload, "id"),
                String(payload, "output") ?? String(payload, "content") ?? "(output unavailable)");
            yield break;
        }

        if (outerType == "event_msg" && type is
            "exec_command_begin" or "exec_command_end" or "patch_apply_begin" or "patch_apply_end")
        {
            yield return new UnpairedEvent(
                type.EndsWith("_end", StringComparison.Ordinal) ? "tool-output" : "tool-call",
                type.EndsWith("_end", StringComparison.Ordinal) ? "tool" : "assistant",
                type,
                String(payload, "call_id") ?? String(payload, "id"),
                payload.GetRawText());
        }
    }

    private static string? FindTurnId(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Name is "turn_id" or "turnId"
                    && property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }
            }
            foreach (JsonProperty property in element.EnumerateObject())
            {
                string? found = FindTurnId(property.Value);
                if (found is not null) return found;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                string? found = FindTurnId(item);
                if (found is not null) return found;
            }
        }
        return null;
    }

    private static bool Hidden(JsonElement root)
    {
        string text = root.GetRawText();
        return text.Contains("encrypted_content", StringComparison.Ordinal)
            || String(root, "type") is "reasoning" or "analysis"
            || (root.TryGetProperty("payload", out JsonElement payload)
                && String(payload, "type") is "reasoning" or "analysis");
    }

    private static string ContentText(JsonElement payload)
    {
        if (!payload.TryGetProperty("content", out JsonElement content)
            || content.ValueKind != JsonValueKind.Array)
        {
            return String(payload, "text") ?? string.Empty;
        }

        return string.Concat(content.EnumerateArray()
            .Where(part => String(part, "type") is "input_text" or "output_text" or "text")
            .Select(part => String(part, "text"))
            .Where(text => text is not null));
    }

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private sealed record ParsedLine(int LineNumber, JsonElement Root, string? TurnId);
    private sealed record UnpairedEvent(string Kind, string Role, string? Name, string? CallId, string Content);
}

internal static class CertificationEvidenceRedactor
{
    private static readonly Regex Secret = new(
        @"(?i)(api[_-]?key|access[_-]?token|authorization|client[_-]?secret|password|secret[_-]?access[_-]?key|[A-Z][A-Z0-9_]*(?:TOKEN|SECRET|PASSWORD|API_KEY))[\""']?\s*[:=]\s*[\""']?([^\s,;\""']+)",
        RegexOptions.Compiled);
    private static readonly Regex Bearer = new(@"(?i)\bBearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.Compiled);
    private static readonly Regex OpenAiKey = new(@"\bsk-[A-Za-z0-9_-]{8,}", RegexOptions.Compiled);
    private static readonly Regex WindowsProfile = new(@"(?i)\b[A-Z]:[\\/]Users[\\/][^\\/\s\""']+", RegexOptions.Compiled);
    private static readonly Regex UnixProfile = new(@"/(?:home|Users)/[^/\s\""']+", RegexOptions.Compiled);

    public static string Redact(string value)
    {
        string redacted = Secret.Replace(value, "$1=<REDACTED>");
        redacted = Bearer.Replace(redacted, "Bearer <REDACTED>");
        redacted = OpenAiKey.Replace(redacted, "<REDACTED_KEY>");
        redacted = WindowsProfile.Replace(redacted, "<USER_PROFILE>");
        redacted = UnixProfile.Replace(redacted, "<USER_PROFILE>");
        return redacted;
    }
}

public sealed class CodexCertificationDiagnosticAgent(string codexExecutable) : ICertificationDiagnosticAgent
{
    public async Task<string> AnalyzeAsync(
        CertificationDiagnosticRequest request,
        CancellationToken cancellationToken)
    {
        var process = await new ProcessRunner().StartInteractiveAsync(
            codexExecutable,
            ["app-server", "--listen", "stdio://"],
            request.ScratchRoot,
            cancellationToken);
        var spec = new AgentSessionSpec(
            SessionIdentity.New(),
            $"certification-diagnosis:{request.InvocationId}",
            SessionRole.Decision,
            new SandboxProfile("read-only", false, false, false),
            CertificationFixtureSettings.BrainAgentModel,
            AgentEffort.Medium,
            AgentConfigurationAuthority.Brain,
            request.ScratchRoot);
        await using var session = new CodexAppServerSession(
            spec,
            process,
            new DeterministicAgentTokenEstimator());
        AgentTurnResult result = await session.RunTurnAsync(
            request.Prompt,
            cancellationToken: cancellationToken);
        if (result.State != AgentTurnState.Completed || string.IsNullOrWhiteSpace(result.Output))
        {
            throw new InvalidOperationException(
                $"Diagnostic provider did not complete: {result.Diagnostics ?? result.State.ToString()}");
        }
        return result.Output;
    }
}

public sealed class CertificationRetentionException : IOException
{
    public CertificationRetentionException(string message) : base(message) { }
    public CertificationRetentionException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class CertificationAttemptRediagnoser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<CertificationDiagnosisOutcome> RunAsync(
        string attemptRecord,
        string codexExecutable,
        string authFile,
        CancellationToken cancellationToken = default)
    {
        string attempt = Path.GetFullPath(attemptRecord);
        string retained = Path.Combine(attempt, "retained-case");
        string failurePath = Path.Combine(attempt, "failure.json");
        string statusPath = Path.Combine(attempt, "diagnosis-status.json");
        if (!Directory.Exists(retained) || !File.Exists(failurePath) || !File.Exists(statusPath))
            throw new InvalidDataException("The attempt record is missing failure, status, or retained-case evidence.");

        PreservePriorDiagnosis(attempt);

        CertificationDiagnosisStatus status;
        await using (FileStream stream = File.OpenRead(statusPath))
        {
            status = await JsonSerializer.DeserializeAsync<CertificationDiagnosisStatus>(
                stream,
                JsonOptions,
                cancellationToken)
                ?? throw new InvalidDataException("The retained diagnosis status was empty.");
        }
        JsonElement failure;
        await using (FileStream stream = File.OpenRead(failurePath))
        {
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            failure = document.RootElement.Clone();
        }

        string runtime = Path.Combine(Path.GetDirectoryName(attempt)!,
            $"diagnostic-runtime-{Guid.NewGuid():N}");
        string codexHome = Path.Combine(runtime, "codex-home");
        Directory.CreateDirectory(codexHome);
        File.Copy(authFile, Path.Combine(codexHome, "auth.json"));
        string? priorHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        string? priorExecutable = Environment.GetEnvironmentVariable("CODEX_EXECUTABLE");
        try
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", codexHome);
            Environment.SetEnvironmentVariable("CODEX_EXECUTABLE", codexExecutable);
            return await new CertificationFailureDiagnoser().DiagnoseIfNeededAsync(
                new CertificationFailureContext(
                    status.InvocationId,
                    true,
                    CertificationClassification.ProductRegression,
                    false,
                    "Explicit operator diagnosis of a retained failed attempt.",
                    [],
                    null,
                    failure,
                    Path.GetFullPath(Path.Combine(attempt, "..", "..", "..")),
                    retained,
                    codexHome,
                    codexExecutable,
                    CertificationSourceSelection.ResolveExisting(
                    [
                        "src/LoopRelay.Certification/CertificationFailureDiagnosis.cs",
                    ]),
                    ExplicitRequest: true,
                    ExistingAttemptRecord: attempt),
                cancellationToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", priorHome);
            Environment.SetEnvironmentVariable("CODEX_EXECUTABLE", priorExecutable);
            if (Directory.Exists(runtime))
            {
                foreach (string file in Directory.EnumerateFiles(runtime, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(runtime, recursive: true);
            }
        }
    }

    private static void PreservePriorDiagnosis(string attempt)
    {
        string history = Path.Combine(
            attempt,
            "diagnosis-history",
            $"diagnosis-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(history);
        foreach (string name in new[] { "diagnosis-status.json", "diagnosis.json", "diagnosis.md" })
        {
            string source = Path.Combine(attempt, name);
            if (File.Exists(source)) File.Copy(source, Path.Combine(history, name));
        }
    }
}

public static class CertificationRepeatGuard
{
    public static bool MayAutomaticallyAdvance(CertificationDiagnosisOutcome? outcome) =>
        outcome?.Status.Disposition is CertificationDiagnosisDisposition.Completed
            or CertificationDiagnosisDisposition.Inconclusive
            or CertificationDiagnosisDisposition.Unavailable;
}

public static class CertificationDiagnosisPolicy
{
    public static bool RequiresSessionInspection(CertificationFailureContext context) =>
        context.Classification != CertificationClassification.Passed
        && (context.ExplicitRequest
            || context.LiveProviderInvoked && BypassReason(context) is null);

    public static string? BypassReason(CertificationFailureContext context)
    {
        if (context.Classification == CertificationClassification.Passed) return "successful-certification";
        if (!context.ExplicitRequest && !context.LiveProviderInvoked) return "no-live-provider-invoked";
        if (!context.ExplicitRequest
            && context.QuotaExhaustionConfirmed
            && context.Classification == CertificationClassification.ProviderRegression
            && context.DeterministicEvidence.Count > 0
            && !string.IsNullOrWhiteSpace(context.ActionableNextStep))
            return "confirmed-quota-exhaustion";
        return null;
    }
}

internal static class CertificationSourceSelection
{
    private static string? configuredWorkspace;

    public static void Configure(string workspaceRoot) =>
        configuredWorkspace = Path.GetFullPath(workspaceRoot);

    public static IReadOnlyList<string> ResolveExisting(IReadOnlyList<string> relativePaths)
    {
        string? root = FindWorkspaceRoot();
        if (root is null) return [];
        return relativePaths
            .Select(relative => Path.GetFullPath(Path.Combine(
                root,
                relative.Replace('/', Path.DirectorySeparatorChar))))
            .Where(File.Exists)
            .ToArray();
    }

    private static string? FindWorkspaceRoot()
    {
        if (configuredWorkspace is { Length: > 0 }
            && File.Exists(Path.Combine(configuredWorkspace, "LoopRelay.slnx")))
            return configuredWorkspace;
        foreach (string origin in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(Path.GetFullPath(origin));
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "LoopRelay.slnx")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }
        return null;
    }
}

internal static class CertificationInvocation
{
    public static string NewId() => Guid.NewGuid().ToString("N");

    public static void Apply(ProcessStartInfo start, string invocationId, string role = "product")
    {
        start.Environment["LOOPRELAY_CERTIFICATION_INVOCATION_ID"] = invocationId;
        start.Environment["LOOPRELAY_CERTIFICATION_INVOCATION_ROLE"] = role;
    }

    public static async Task RecordDirectTurnAsync(
        string repository,
        string codexHome,
        string invocationId,
        string sessionId,
        int turnIndex,
        string? providerThreadId,
        string? providerTurnId,
        CancellationToken cancellationToken)
    {
        string? rollout = null;
        if (providerThreadId is { Length: > 0 })
        {
            CodexRolloutReadResult read = await new CodexRolloutRepository().ReadExactAsync(
                codexHome,
                providerThreadId,
                cancellationToken);
            rollout = read.Location;
        }

        string directory = Path.Combine(repository, ".LoopRelay", "telemetry");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "sessions.certification-direct.0000.jsonl");
        string json = JsonSerializer.Serialize(new
        {
            timestamp = DateTimeOffset.UtcNow,
            repoName = Path.GetFileName(repository),
            codexLogPath = rollout,
            sessionId,
            sessionType = "CertificationDirect",
            turnIndex,
            providerThreadId,
            providerTurnId,
            certificationInvocationId = invocationId,
            invocationRole = "product",
        });
        await File.AppendAllTextAsync(path, json + "\n", cancellationToken);
    }
}
