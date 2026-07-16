using System.Runtime.CompilerServices;
using System.Text.Json;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Codex;
using LoopRelay.Agents.Services.Codex.Compatibility;
using LoopRelay.Agents.Services.Process;
using LoopRelay.Agents.Services.Usage;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Services.Codex;
using LoopRelay.Permissions.Services.Evaluation;
using LoopRelay.Permissions.Services.Parsing;
using LoopRelay.Permissions.Services.Security;

namespace LoopRelay.Certification;

public sealed class ProviderProfileRunner(ICertificationFailureDiagnoser? failureDiagnoser = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<ProviderProfileCertificationResult> RunAsync(
        string codexExecutable,
        string authFile,
        string authorityRoot,
        CancellationToken cancellationToken = default)
    {
        string root = Path.Combine(authorityRoot, "provider-profile", Guid.NewGuid().ToString("N"));
        string repository = Path.Combine(root, "repository");
        string codexHome = Path.Combine(root, "codex-home");
        Directory.CreateDirectory(Path.Combine(repository, ".agents"));
        Directory.CreateDirectory(codexHome);
        File.Copy(authFile, Path.Combine(codexHome, "auth.json"), overwrite: false);
        string? priorHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        string? priorExecutable = Environment.GetEnvironmentVariable("CODEX_EXECUTABLE");
        Environment.SetEnvironmentVariable("CODEX_HOME", codexHome);
        Environment.SetEnvironmentVariable("CODEX_EXECUTABLE", codexExecutable);
        Environment.SetEnvironmentVariable("CODEX_ANALYTICS_ENABLED", "false");
        var checks = new List<LiveProviderCheck>();
        var scrubbedEvidence = new List<string>();
        string version = "unknown";
        string schemaDigest = "unknown";
        string? failedInvocationId = null;
        string? lastInvocationId = null;
        bool providerAttempted = false;
        try
        {
            CodexInstalledCompatibilityIdentity identity = CodexCompatibilityIdentityProbe.Resolve();
            version = identity.ServerVersion ?? "unknown";
            schemaDigest = identity.SchemaDigest ?? "unknown";
            bool manifestMatch = CodexCompatibilityManifest.LoadEmbedded().Entries.Any(item =>
                    item.ServerVersion == version && item.SchemaDigest == schemaDigest);
            bool compatible = version != "unknown" && schemaDigest != "unknown";
            checks.Add(new LiveProviderCheck(
                "exact-profile-gate",
                compatible,
                compatible ? manifestMatch ? "pass" : "candidate-profile" : "provider-incompatible",
                [
                    $"version:{version}",
                    $"schema:{schemaDigest}",
                    $"manifest-match:{manifestMatch}",
                    $"model:{CertificationFixtureSettings.BrainModel}",
                ]));
            if (!compatible)
            {
                return await Finish(CertificationClassification.UnsupportedCapability);
            }

            providerAttempted = true;
            CertificationDirectTurn readOnlyTurn = await RunSession(
                ReadOnlySpec(repository, AgentEffort.XHigh),
                permissionGateway: null,
                "Reply with exactly LIVE_OK. Do not call tools or inspect files.",
                cancellationToken);
            AgentTurnResult readOnly = readOnlyTurn.Result;
            lastInvocationId = readOnlyTurn.InvocationId;
            bool readOnlyPass = readOnly.State == AgentTurnState.Completed &&
                readOnly.Output.Contains("LIVE_OK", StringComparison.Ordinal);
            checks.Add(new LiveProviderCheck(
                "xhigh-read-only-live",
                readOnlyPass,
                readOnlyPass ? "pass" : "product-or-provider-regression",
                [$"state:{readOnly.State}", "sandbox:read-only", "effort:xhigh", $"diagnostic:{ScrubDiagnostic(readOnly.Diagnostics, repository, codexHome)}"]));
            if (!readOnlyPass) failedInvocationId = readOnlyTurn.InvocationId;

            string allowedRelative = ".agents/allowed.md";
            string deniedRelative = ".agents/denied.md";
            OperationPermissionProfile profile = new(
                "live-scoped-write",
                repository,
                ["README.md"],
                [],
                [allowedRelative],
                []);
            PermissionGateway gateway = CreateGateway();
            var acceptedRecorder = new List<ApprovalObservation>();
            CertificationDirectTurn acceptedTurn = await RunSession(
                ScopedSpec(repository, profile),
                gateway,
                $"Create {allowedRelative} containing exactly ALLOWED followed by a newline. Use the file-edit tool, do not run commands, and do not touch any other path.",
                cancellationToken,
                acceptedRecorder);
            AgentTurnResult accepted = acceptedTurn.Result;
            lastInvocationId = acceptedTurn.InvocationId;
            ApprovalObservation? allowedApproval = acceptedRecorder.FirstOrDefault(item => item.Method.Contains("fileChange", StringComparison.Ordinal));
            bool requestBeforeMutation = allowedApproval is not null && !allowedApproval.TargetExistedWhenRequested;
            string? normalizedTarget = allowedApproval?.TargetPath?.Replace('\\', '/');
            bool exactTarget = normalizedTarget == allowedRelative ||
                (normalizedTarget?.EndsWith('/' + allowedRelative, StringComparison.Ordinal) ?? false);
            bool allowedWritten = File.Exists(Path.Combine(repository, ".agents", "allowed.md")) &&
                (await File.ReadAllTextAsync(Path.Combine(repository, ".agents", "allowed.md"), cancellationToken)).Trim() == "ALLOWED";
            checks.Add(new LiveProviderCheck(
                "approval-before-file-edit",
                requestBeforeMutation,
                requestBeforeMutation ? "pass" : "product-or-provider-regression",
                ["method:item/fileChange/requestApproval", $"pre-mutation:{requestBeforeMutation}"]));
            checks.Add(new LiveProviderCheck(
                "approval-exact-target-path",
                exactTarget,
                exactTarget ? "pass" : "provider-incompatible",
                [$"target:{ScrubTarget(allowedApproval?.TargetPath, repository)}"]));
            checks.Add(new LiveProviderCheck(
                "accepted-scoped-write",
                accepted.State == AgentTurnState.Completed && allowedWritten,
                accepted.State == AgentTurnState.Completed && allowedWritten ? "pass" : "product-or-provider-regression",
                [$"state:{accepted.State}", $"allowed-written:{allowedWritten}", $"diagnostic:{ScrubDiagnostic(accepted.Diagnostics, repository, codexHome)}"]));
            if (!requestBeforeMutation || !exactTarget || accepted.State != AgentTurnState.Completed || !allowedWritten)
                failedInvocationId = acceptedTurn.InvocationId;

            var declinedRecorder = new List<ApprovalObservation>();
            using var declineTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            declineTimeout.CancelAfter(TimeSpan.FromMinutes(2));
            CertificationDirectTurn declinedTurn = await RunSession(
                ScopedSpec(repository, profile),
                gateway,
                $"Create {deniedRelative} containing DENIED. Use the file-edit tool, do not run commands, and stop if permission is declined.",
                declineTimeout.Token,
                declinedRecorder);
            AgentTurnResult declined = declinedTurn.Result;
            lastInvocationId = declinedTurn.InvocationId;
            bool deniedAbsent = !File.Exists(Path.Combine(repository, ".agents", "denied.md"));
            bool denialObserved = declinedRecorder.Any(item =>
                item.Method.Contains("fileChange", StringComparison.Ordinal) &&
                (item.TargetPath?.Replace('\\', '/').EndsWith('/' + deniedRelative, StringComparison.Ordinal) == true ||
                 item.TargetPath?.Replace('\\', '/') == deniedRelative));
            checks.Add(new LiveProviderCheck(
                "declined-scoped-write-does-not-hang",
                denialObserved && deniedAbsent,
                denialObserved && deniedAbsent ? "pass" : "product-or-provider-regression",
                [$"terminal-state:{declined.State}", $"denial-observed:{denialObserved}", $"denied-absent:{deniedAbsent}", $"diagnostic:{ScrubDiagnostic(declined.Diagnostics, repository, codexHome)}"]));
            checks.Add(new LiveProviderCheck(
                "accepted-write-remains-exactly-scoped",
                allowedWritten && deniedAbsent,
                allowedWritten && deniedAbsent ? "pass" : "product-regression",
                [$"allowed:{allowedWritten}", $"outside-profile-absent:{deniedAbsent}"]));
            if (!denialObserved || !deniedAbsent || !allowedWritten)
                failedInvocationId = declinedTurn.InvocationId;

            scrubbedEvidence.AddRange(checks.SelectMany(item => item.Evidence));
            return await Finish(checks.All(item => item.Passed)
                ? CertificationClassification.Passed
                : CertificationClassification.ProviderRegression);
        }
        catch (OperationCanceledException)
        {
            checks.Add(new LiveProviderCheck("live-suite-completion", false, "environment-failure", ["timeout-or-cancellation"]));
            return await Finish(CertificationClassification.EnvironmentFailure);
        }
        catch (Exception exception) when (exception is not CertificationRetentionException)
        {
            checks.Add(new LiveProviderCheck("live-suite-completion", false, "environment-failure", [exception.GetType().Name]));
            return await Finish(CertificationClassification.EnvironmentFailure);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", priorHome);
            Environment.SetEnvironmentVariable("CODEX_EXECUTABLE", priorExecutable);
            string authCopy = Path.Combine(codexHome, "auth.json");
            if (File.Exists(authCopy))
            {
                File.Delete(authCopy);
            }

            if (Directory.Exists(root))
            {
                foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(root, recursive: true);
            }
        }

        async Task<ProviderProfileCertificationResult> Finish(CertificationClassification classification)
        {
            IReadOnlyList<string> privacy = PrivacyScanner.Scan(string.Join("\n", scrubbedEvidence), authorityRoot);
            if (privacy.Count > 0)
            {
                classification = CertificationClassification.OracleDrift;
            }

            string? invocationId = classification == CertificationClassification.Passed
                ? null
                : failedInvocationId ?? lastInvocationId ?? $"provider-profile-{Guid.NewGuid():N}";
            var result = new ProviderProfileCertificationResult(
                CertificationEvidenceSchema.Version,
                classification,
                version,
                schemaDigest,
                checks,
                privacy,
                invocationId);
            if (classification != CertificationClassification.Passed)
            {
                bool quota = LiveProviderFailureClassifier.HasQuotaExhaustion(codexHome);
                CertificationDiagnosisOutcome diagnosis = await (failureDiagnoser ?? new CertificationFailureDiagnoser())
                    .DiagnoseIfNeededAsync(
                        new CertificationFailureContext(
                            invocationId!,
                            providerAttempted,
                            classification,
                            quota,
                            checks.LastOrDefault(item => !item.Passed)?.Identity
                                ?? "Provider-profile certification failed before a live check completed.",
                            quota
                                ? ["codex-rollout:used-percent:100", "codex-rollout:last-agent-message:null"]
                                : checks.Where(item => !item.Passed).SelectMany(item => item.Evidence).ToArray(),
                            quota ? "Wait until the confirmed provider quota window resets before an explicit rerun." : null,
                            result,
                            authorityRoot,
                            repository,
                            codexHome,
                            codexExecutable,
                            CertificationSourceSelection.ResolveExisting(
                            [
                                "src/LoopRelay.Certification/ProviderProfileRunner.cs",
                                "src/LoopRelay.Agents/Services/Codex/CodexAppServerSession.cs",
                            ]),
                            checks.LastOrDefault(item => !item.Passed)?.Identity),
                        cancellationToken);
                result = result with { AttemptRecord = diagnosis.AttemptRecord, Diagnosis = diagnosis };
            }
            string evidencePath = Path.Combine(authorityRoot, "evidence", "provider-profile.latest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
            await using FileStream stream = File.Create(evidencePath);
            await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
            return result;
        }
    }

    private static async Task<CertificationDirectTurn> RunSession(
        AgentSessionSpec spec,
        PermissionGateway? permissionGateway,
        string prompt,
        CancellationToken cancellationToken,
        List<ApprovalObservation>? observations = null)
    {
        string invocationId = CertificationInvocation.NewId();
        IAgentProcess process = await new ProcessRunner().StartInteractiveAsync(
            Environment.GetEnvironmentVariable("CODEX_EXECUTABLE")!,
            ["app-server", "--listen", "stdio://"],
            spec.WorkingDirectory!,
            cancellationToken);
        var recording = new RecordingAgentProcess(process, spec.WorkingDirectory!, observations ?? []);
        await using var session = new CodexAppServerSession(
            spec,
            recording,
            new DeterministicAgentTokenEstimator(),
            permissionGateway);
        AgentTurnResult result = await session.RunTurnAsync(prompt, cancellationToken: cancellationToken);
        await CertificationInvocation.RecordDirectTurnAsync(
            spec.WorkingDirectory!,
            Environment.GetEnvironmentVariable("CODEX_HOME")!,
            invocationId,
            session.SessionId.Value.ToString(),
            result.TurnIndex,
            session.ThreadId,
            result.ProviderTurnId,
            cancellationToken);
        return new CertificationDirectTurn(result, invocationId);
    }

    private static AgentSessionSpec ReadOnlySpec(string repository, AgentEffort effort) => new(
        SessionIdentity.New(), "live-provider-certification", SessionRole.Decision,
        new SandboxProfile("read-only", false, false, false),
        CertificationFixtureSettings.BrainAgentModel, effort, AgentConfigurationAuthority.Brain, repository);

    private static AgentSessionSpec ScopedSpec(string repository, OperationPermissionProfile profile) => new(
        SessionIdentity.New(), "live-provider-certification", SessionRole.OperationalExecution,
        new SandboxProfile("read-only", false, false, true),
        CertificationFixtureSettings.BrainAgentModel, AgentEffort.Low, AgentConfigurationAuthority.Brain, repository,
        operationPermissionProfile: profile);

    private static PermissionGateway CreateGateway() => new(
        new CodexPermissionAdapter(),
        new PermissionHandler(
            new CommandParser(), new CommandCanonicalizer(), new Sha256FingerprintService(),
            new InMemoryPermissionCache(), new PermissionEvaluatorEngine(), new InvariantGuard()),
        new OperationPermissionHandler());

    private static string ScrubTarget(string? target, string repository) => string.IsNullOrWhiteSpace(target)
        ? "(missing)"
        : target.Replace(repository, "<CASE>", StringComparison.OrdinalIgnoreCase).Replace('\\', '/');

    private static string ScrubDiagnostic(string? diagnostic, string repository, string codexHome)
    {
        if (string.IsNullOrWhiteSpace(diagnostic))
        {
            return "(none)";
        }

        string scrubbed = diagnostic.Replace(repository, "<CASE>", StringComparison.OrdinalIgnoreCase)
            .Replace(codexHome, "<CODEX_HOME>", StringComparison.OrdinalIgnoreCase)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
        return scrubbed.Length <= 500 ? scrubbed : scrubbed[..500];
    }

    private sealed record ApprovalObservation(string Method, string? TargetPath, bool TargetExistedWhenRequested);
    private sealed record CertificationDirectTurn(AgentTurnResult Result, string InvocationId);

    private sealed class RecordingAgentProcess(
        IAgentProcess inner,
        string repository,
        List<ApprovalObservation> observations) : IAgentProcess
    {
        private readonly Dictionary<string, string> _fileChangeTargets = new(StringComparer.Ordinal);
        public int ProcessId => inner.ProcessId;
        public AgentProcessState State => inner.State;
        public int? ExitCode => inner.ExitCode;
        public bool HasExited => inner.HasExited;
        public Task Completion => inner.Completion;
        public string? ErrorSnapshot => inner.ErrorSnapshot;
        public Task WriteStandardInputAsync(string value, CancellationToken token = default) => inner.WriteStandardInputAsync(value, token);
        public Task WritePromptAsync(string value, CancellationToken token = default) => inner.WritePromptAsync(value, token);
        public Task CompleteInputAsync(CancellationToken token = default) => inner.CompleteInputAsync(token);

        public async IAsyncEnumerable<string> ReadOutputLinesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (string line in inner.ReadOutputLinesAsync(cancellationToken))
            {
                Observe(line);
                yield return line;
            }
        }

        private void Observe(string line)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                if (root.TryGetProperty("method", out JsonElement notificationMethod) &&
                    notificationMethod.GetString() == "item/started" &&
                    root.TryGetProperty("params", out JsonElement notificationParameters) &&
                    notificationParameters.TryGetProperty("item", out JsonElement item) &&
                    item.TryGetProperty("type", out JsonElement itemType) && itemType.GetString() == "fileChange" &&
                    item.TryGetProperty("id", out JsonElement itemId) &&
                    item.TryGetProperty("changes", out JsonElement changes))
                {
                    string[] paths = changes.EnumerateArray()
                        .Where(change => change.TryGetProperty("path", out JsonElement path) && path.ValueKind == JsonValueKind.String)
                        .Select(change => change.GetProperty("path").GetString()!)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    if (paths.Length == 1)
                    {
                        _fileChangeTargets[itemId.GetString()!] = paths[0];
                    }
                }

                if (!root.TryGetProperty("method", out JsonElement methodElement) ||
                    methodElement.ValueKind != JsonValueKind.String ||
                    !methodElement.GetString()!.Contains("requestApproval", StringComparison.Ordinal))
                {
                    return;
                }

                JsonElement parameters = root.GetProperty("params");
                string? target = parameters.TryGetProperty("targetPath", out JsonElement targetElement)
                    ? targetElement.GetString()
                    : parameters.TryGetProperty("grantRoot", out JsonElement grantElement)
                        ? grantElement.GetString()
                        : null;
                if (target is null && parameters.TryGetProperty("itemId", out JsonElement approvalItemId))
                {
                    _fileChangeTargets.TryGetValue(approvalItemId.GetString() ?? string.Empty, out target);
                }
                string? resolved = target is null ? null : Path.IsPathRooted(target) ? target : Path.Combine(repository, target);
                observations.Add(new ApprovalObservation(
                    methodElement.GetString()!,
                    target,
                    resolved is not null && File.Exists(resolved)));
            }
            catch (JsonException) { }
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
