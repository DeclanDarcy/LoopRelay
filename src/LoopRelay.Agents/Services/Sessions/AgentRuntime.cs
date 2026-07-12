using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Codex;
using LoopRelay.Agents.Services.Codex.Compatibility;
using LoopRelay.Permissions.Abstractions.Evaluation;

namespace LoopRelay.Agents.Services.Sessions;

public sealed class AgentRuntime(
    IAgentProcessLauncher _launcher,
    IAgentTurnBoundaryDetector _boundaryDetector,
    IAgentTokenEstimator _tokenEstimator,
    AgentSessionRegistry _registry,
    IPermissionGateway? _permissionGateway = null,
    CodexSessionContinuityProfileResolver? _continuityProfileResolver = null) : IAgentRuntime, IAgentSessionContinuityRuntime
{
    // The Codex provider identity lives here, on the provider implementation itself — session
    // evidence reads it from this declaration instead of repeating the literal (D5/M7).
    public AgentRuntimeCapabilities Capabilities { get; } = new(
        Provider: "codex",
        OneShotExecution: true,
        PersistentSessions: true,
        SessionResume: true);

    public async Task<IAgentSession> OpenSessionAsync(
        AgentSessionSpec spec,
        CancellationToken cancellationToken = default)
    {
        if (spec.ResumeThreadId is not null)
        {
            throw new SessionOperationProfileGateException(
                "Resuming through IAgentRuntime.OpenSessionAsync is disabled. Use IAgentSessionContinuityRuntime.ResumeSessionAsync with a captured profile.");
        }

        IAgentProcess process = await _launcher.LaunchAsync(spec, AgentSessionMode.Persistent, cancellationToken);
        // Held-open sessions speak the Codex app-server JSON-RPC protocol over the process's stdio.
        var session = new CodexAppServerSession(spec, process, _tokenEstimator, _permissionGateway);

        if (!_registry.TryAdd(new AgentSessionKey(spec.RepositoryId, spec.SessionId), session))
        {
            await session.DisposeAsync();
            throw new InvalidOperationException(
                $"An agent session already exists for repository '{spec.RepositoryId}' and session '{spec.SessionId}'.");
        }

        return session;
    }

    public Task<SessionContinuityNegotiationResult> NegotiateAsync(
        SessionContinuityNegotiationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CodexSessionContinuityProfileResolver resolver = _continuityProfileResolver
            ?? new CodexSessionContinuityProfileResolver(CodexCompatibilityManifest.LoadEmbedded());
        return Task.FromResult(resolver.Resolve(request));
    }

    public async Task<SessionCreateResult> CreateSessionAsync(
        SessionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SessionSpec.ResumeThreadId is not null)
        {
            return CreateFailed(request, SessionResumeOutcome.ConfigurationFailure, SessionOperationStage.ProfileGate,
                "thread/start", null, "Fresh creation cannot carry a resume thread id.");
        }

        CodexAppServerSession? session = null;
        try
        {
            session = await OpenRegisteredSessionAsync(request.SessionSpec, request.Profile, cancellationToken);
            await EnsureReadyWithTimeoutAsync(session, request.Timeout, cancellationToken);
            if (session.ThreadId is not { Length: > 0 } threadId)
            {
                throw new InvalidOperationException("Codex thread/start produced no provider thread id.");
            }

            return new SessionCreateResult(
                true,
                session,
                new ProviderSessionReference(request.Profile.Provider, threadId),
                SessionOperationStage.Completed,
                null,
                CompletedTransport(session),
                request.Profile.Digest);
        }
        catch (Exception exception)
        {
            if (session is not null)
            {
                await CloseSessionAsync(session);
            }

            return CreateFailedFromException(request, exception, cancellationToken);
        }
    }

    public async Task<SessionResumeResult> ResumeSessionAsync(
        SessionResumeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.SessionSpec.ResumeThreadId, request.Original.ThreadId, StringComparison.Ordinal))
        {
            return ResumeFailed(request, SessionResumeOutcome.ConfigurationFailure, SessionOperationStage.ProfileGate,
                "thread/resume", null, "The session specification and original provider thread id do not match.");
        }

        if (request.Profile.Operation(SessionContinuityOperation.Resume).Status != SessionOperationSupport.Supported)
        {
            return ResumeFailed(request, SessionResumeOutcome.DeterministicProtocolFailure, SessionOperationStage.ProfileGate,
                "thread/resume", null, "The captured continuity profile does not support resume.");
        }

        CodexAppServerSession? session = null;
        try
        {
            session = await OpenRegisteredSessionAsync(request.SessionSpec, request.Profile, cancellationToken);
            await EnsureReadyWithTimeoutAsync(session, request.Timeout, cancellationToken);
            if (!string.Equals(session.ThreadId, request.Original.ThreadId, StringComparison.Ordinal))
            {
                await CloseSessionAsync(session);
                return ResumeFailed(request, SessionResumeOutcome.DeterministicProtocolFailure, SessionOperationStage.Validation,
                    "thread/resume", null, "The provider returned a different thread id for resume.");
            }

            return new SessionResumeResult(
                SessionResumeOutcome.SuccessfulResume,
                session,
                request.Original,
                new ProviderSessionReference(request.Profile.Provider, session.ThreadId!),
                SessionOperationStage.Completed,
                null,
                CompletedTransport(session),
                request.Profile.Digest,
                request.Attempt);
        }
        catch (Exception exception)
        {
            string codexHome = ResolveCodexHome(session?.InitializeResult ?? default);
            if (session is not null)
            {
                await CloseSessionAsync(session);
            }
            SessionResumeResult failed = ResumeFailedFromException(request, exception, cancellationToken);
            return exception is CodexAppServerRequestException
                ? await RefineResumeFailureFromExactStorageAsync(
                    request, failed, codexHome, cancellationToken)
                : failed;
        }
    }

    public async Task<SessionContentResult> ReadSessionAsync(
        SessionContentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Profile.Operation(SessionContinuityOperation.ConversationRead).Status != SessionOperationSupport.Supported)
        {
            return new SessionContentResult(
                false, null, GatedFailure("thread/read", request.Profile, SessionContinuityOperation.ConversationRead));
        }

        IAgentProcess? process = null;
        CodexAppServerSession? session = null;
        try
        {
            process = await _launcher.LaunchAsync(request.SessionSpec, AgentSessionMode.Persistent, cancellationToken);
            session = new CodexAppServerSession(
                request.SessionSpec, process, _tokenEstimator, _permissionGateway, request.Profile);
            Task<CodexThreadReadResult> operation = session.ReadThreadAsync(request.Session.ThreadId, cancellationToken);
            CodexThreadReadResult result = request.Timeout is null
                ? await operation
                : await operation.WaitAsync(request.Timeout.Value, cancellationToken);
            if (result.Status is CodexThreadReadStatus.Corrupt or CodexThreadReadStatus.IdentityMismatch)
            {
                return new SessionContentResult(
                    false,
                    result.Digest,
                    new SessionOperationFailure(
                        result.Status.ToString(), "thread/read", null, null, default,
                        result.Diagnostic, false, false),
                    result.Records,
                    result.VerifiedBoundary,
                    result.Status == CodexThreadReadStatus.Partial);
            }

            return new SessionContentResult(
                true,
                result.Digest,
                null,
                result.Records,
                result.VerifiedBoundary,
                result.Status == CodexThreadReadStatus.Partial);
        }
        catch (Exception exception)
        {
            (_, _, SessionOperationFailure failure, _) = Classify(exception, "thread/read", cancellationToken);
            return new SessionContentResult(false, null, failure);
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync();
            }
            else if (process is not null)
            {
                await process.DisposeAsync();
            }
        }
    }

    public async Task<SessionSeedResult> SeedSessionAsync(
        SessionSeedRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Profile.Operation(SessionContinuityOperation.ConversationWrite).Status != SessionOperationSupport.Supported)
        {
            return new SessionSeedResult(
                false, GatedFailure("turn/start", request.Profile, SessionContinuityOperation.ConversationWrite));
        }

        if (!string.Equals(request.Target.ThreadId, request.Session.ThreadId, StringComparison.Ordinal))
        {
            return new SessionSeedResult(false, new SessionOperationFailure(
                "IdentityMismatch", "turn/start", null, null, default,
                "The seed target does not match the replacement provider thread id.", false, false));
        }

        try
        {
            Task<AgentTurnResult> operation = request.Target.RunTurnAsync(request.Content, cancellationToken: cancellationToken);
            AgentTurnResult result = request.Timeout is null
                ? await operation
                : await operation.WaitAsync(request.Timeout.Value, cancellationToken);
            bool accepted = result.State == AgentTurnState.Completed
                && result.Output.Contains(request.Marker, StringComparison.Ordinal);
            return new SessionSeedResult(
                accepted,
                accepted ? null : new SessionOperationFailure(
                    "MarkerValidationFailed", "turn/start", null, null, default,
                    "The context-injection turn did not complete with the exact recovery marker.", false, false),
                result.ProviderTurnId,
                result.Output,
                new SessionTransportProgress(true, true, true, true, null, null));
        }
        catch (Exception exception)
        {
            (_, _, SessionOperationFailure failure, SessionTransportProgress transport) =
                Classify(exception, "turn/start", cancellationToken);
            return new SessionSeedResult(false, failure, Transport: transport with { TurnSubmitted = transport.RequestSubmitted });
        }
    }

    public async Task<SessionForkResult> ForkSessionAsync(
        SessionForkRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Profile.Operation(SessionContinuityOperation.Fork).Status != SessionOperationSupport.Supported)
        {
            return new SessionForkResult(
                false, null, request.Parent, null,
                GatedFailure("thread/fork", request.Profile, SessionContinuityOperation.Fork));
        }

        if (request.SessionSpec.ResumeThreadId is not null)
        {
            return new SessionForkResult(
                false, null, request.Parent, null,
                new SessionOperationFailure(
                    "ConfigurationFailure", "thread/fork", null, null, default,
                    "The fork operation session must not carry a resume thread id.", false, false));
        }

        CodexAppServerSession? session = null;
        try
        {
            session = await OpenRegisteredSessionAsync(request.SessionSpec, request.Profile, cancellationToken);
            Task<(string ChildThreadId, string? ParentThreadId, string? HistoryDigest)> operation =
                session.ForkThreadAsync(request.Parent.ThreadId, cancellationToken);
            (string childThreadId, _, string? historyDigest) = request.Timeout is null
                ? await operation
                : await operation.WaitAsync(request.Timeout.Value, cancellationToken);
            var child = new ProviderSessionReference(request.Profile.Provider, childThreadId);
            return new SessionForkResult(
                true, session, request.Parent, child, null,
                CompletedTransport(session), historyDigest);
        }
        catch (Exception exception)
        {
            if (session is not null)
            {
                await CloseSessionAsync(session);
            }
            (_, _, SessionOperationFailure failure, SessionTransportProgress transport) =
                Classify(exception, "thread/fork", cancellationToken);
            return new SessionForkResult(false, null, request.Parent, null, failure, transport);
        }
    }

    public Task<SessionReconcileResult> ReconcileAsync(SessionReconcileRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SessionReconcileResult(false, null,
            new SessionOperationFailure("NotImplemented", "reconcile", null, null, default,
                "Reconciliation is not implemented for this operation profile.", false, false)));

    public async ValueTask CloseSessionAsync(IAgentSession session)
    {
        // Single-sited teardown (m10): deregister from the registry AND dispose, so a held-open session is owned
        // in exactly one place. RemoveAsync both removes the entry and disposes the stored session; if the session
        // is not (or no longer) registered — already removed, or never added — fall back to disposing it directly
        // so it is still disposed exactly once.
        var key = new AgentSessionKey(session.RepositoryId, session.SessionId);
        if (!await _registry.RemoveAsync(key).ConfigureAwait(false))
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<CodexAppServerSession> OpenRegisteredSessionAsync(
        AgentSessionSpec spec,
        SessionContinuityProfile profile,
        CancellationToken cancellationToken)
    {
        IAgentProcess process = await _launcher.LaunchAsync(spec, AgentSessionMode.Persistent, cancellationToken);
        var session = new CodexAppServerSession(spec, process, _tokenEstimator, _permissionGateway, profile);
        if (!_registry.TryAdd(new AgentSessionKey(spec.RepositoryId, spec.SessionId), session))
        {
            await session.DisposeAsync();
            throw new InvalidOperationException(
                $"An agent session already exists for repository '{spec.RepositoryId}' and session '{spec.SessionId}'.");
        }

        return session;
    }

    private static async Task EnsureReadyWithTimeoutAsync(
        CodexAppServerSession session,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        if (timeout is null)
        {
            await session.EnsureReadyAsync(cancellationToken);
            return;
        }

        try
        {
            await session.EnsureReadyAsync(cancellationToken).WaitAsync(timeout.Value, cancellationToken);
        }
        catch (TimeoutException)
        {
            throw;
        }
    }

    private static SessionTransportProgress CompletedTransport(IAgentSession session) =>
        new(true, true, true, false, session.State == LoopRelay.Agents.Primitives.Process.AgentProcessState.Exited ? 0 : null, null);

    private static SessionOperationFailure GatedFailure(
        string method,
        SessionContinuityProfile profile,
        SessionContinuityOperation operation) =>
        new(
            profile.Operation(operation).Status == SessionOperationSupport.Supported ? "NotImplemented" : "OperationProfileGate",
            method,
            null,
            null,
            default,
            $"{operation} is {profile.Operation(operation).Status}; no provider request was emitted.",
            false,
            false);

    private static SessionResumeResult ResumeFailedFromException(
        SessionResumeRequest request,
        Exception exception,
        CancellationToken callerToken)
    {
        (SessionResumeOutcome outcome, SessionOperationStage stage, SessionOperationFailure failure, SessionTransportProgress transport) =
            Classify(exception, "thread/resume", callerToken);
        return new SessionResumeResult(outcome, null, request.Original, null, stage, failure, transport, request.Profile.Digest, request.Attempt);
    }

    private static SessionResumeResult ResumeFailed(
        SessionResumeRequest request,
        SessionResumeOutcome outcome,
        SessionOperationStage stage,
        string method,
        int? code,
        string diagnostic) =>
        new(outcome, null, request.Original, null, stage,
            new SessionOperationFailure(outcome.ToString(), method, null, code, default, diagnostic, false, false),
            new SessionTransportProgress(false, false, false, false, null, null), request.Profile.Digest, request.Attempt);

    private static SessionCreateResult CreateFailedFromException(
        SessionCreateRequest request,
        Exception exception,
        CancellationToken callerToken)
    {
        (_, SessionOperationStage stage, SessionOperationFailure failure, SessionTransportProgress transport) =
            Classify(exception, "thread/start", callerToken);
        return new SessionCreateResult(false, null, null, stage, failure, transport, request.Profile.Digest);
    }

    private static SessionCreateResult CreateFailed(
        SessionCreateRequest request,
        SessionResumeOutcome outcome,
        SessionOperationStage stage,
        string method,
        int? code,
        string diagnostic) =>
        new(false, null, null, stage,
            new SessionOperationFailure(outcome.ToString(), method, null, code, default, diagnostic, false, false),
            new SessionTransportProgress(false, false, false, false, null, null), request.Profile.Digest);

    private static (SessionResumeOutcome, SessionOperationStage, SessionOperationFailure, SessionTransportProgress) Classify(
        Exception exception,
        string method,
        CancellationToken callerToken)
    {
        if (exception is SessionOperationProfileGateException)
        {
            return Failure(SessionResumeOutcome.DeterministicProtocolFailure, SessionOperationStage.ProfileGate,
                "OperationProfileGate", method, null, null, exception.Message, false, false, false);
        }

        if (exception is CodexAppServerRequestException requestException)
        {
            int? code = requestException.Response.ErrorCode;
            SessionResumeOutcome outcome = code is -32601 or -32602
                ? SessionResumeOutcome.DeterministicProtocolFailure
                : SessionResumeOutcome.UnknownOutcome;
            return Failure(outcome, SessionOperationStage.OperationResponse, outcome.ToString(),
                requestException.ProviderMethod, requestException.RequestId, code,
                "The provider returned a structured JSON-RPC error.", false, false, true,
                requestException.Response.ErrorData);
        }

        if (exception is TimeoutException)
        {
            return Failure(SessionResumeOutcome.UnknownOutcome, SessionOperationStage.OperationWrite,
                "Timeout", method, null, null, "The operation timed out; its outcome must be reconciled.", true, false, true);
        }

        if (exception is OperationCanceledException)
        {
            bool cancelled = callerToken.IsCancellationRequested;
            return Failure(cancelled ? SessionResumeOutcome.Cancelled : SessionResumeOutcome.UnknownOutcome,
                SessionOperationStage.OperationWrite, cancelled ? "Cancellation" : "UnknownCancellation",
                method, null, null, cancelled ? "The caller cancelled the operation." : "The operation was cancelled by the transport.",
                false, cancelled, true);
        }

        if (exception is IOException)
        {
            return Failure(SessionResumeOutcome.UnknownOutcome, SessionOperationStage.OperationWrite,
                "TransportFailure", method, null, null, "The transport ended before the outcome was known.", false, false, true);
        }

        return Failure(SessionResumeOutcome.ProgrammingFailure, SessionOperationStage.Validation,
            "ProgrammingFailure", method, null, null, exception.GetType().Name, false, false, false);
    }

    private static async Task<SessionResumeResult> RefineResumeFailureFromExactStorageAsync(
        SessionResumeRequest request,
        SessionResumeResult failed,
        string codexHome,
        CancellationToken cancellationToken)
    {
        if (failed.Outcome != SessionResumeOutcome.UnknownOutcome)
        {
            return failed;
        }

        CodexRolloutReadResult storage = await new CodexRolloutRepository().ReadExactAsync(
            codexHome, request.Original.ThreadId, cancellationToken);
        (SessionResumeOutcome Outcome, string Classification, string Diagnostic)? refinement = storage.Status switch
        {
            CodexRolloutReadStatus.Absent => (
                SessionResumeOutcome.UnavailableSession,
                "UnavailableSession",
                "Exact provider thread storage evidence is absent."),
            CodexRolloutReadStatus.Corrupt or CodexRolloutReadStatus.Partial => (
                SessionResumeOutcome.CorruptedState,
                "CorruptedState",
                $"Exact provider thread storage is {storage.Status} at {storage.VerifiedBoundary ?? "an unknown boundary"}."),
            CodexRolloutReadStatus.PermissionDenied => (
                SessionResumeOutcome.PermissionFailure,
                "PermissionFailure",
                "Exact provider thread storage could not be read due to permissions."),
            _ => null,
        };
        if (refinement is null)
        {
            return failed;
        }

        SessionOperationFailure? failure = failed.Failure is null
            ? null
            : failed.Failure with
            {
                Classification = refinement.Value.Classification,
                RedactedDiagnostic = refinement.Value.Diagnostic,
            };
        return failed with
        {
            Outcome = refinement.Value.Outcome,
            Stage = SessionOperationStage.Validation,
            Failure = failure,
        };
    }

    private static string ResolveCodexHome(System.Text.Json.JsonElement initializeResult)
    {
        if (initializeResult.ValueKind == System.Text.Json.JsonValueKind.Object
            && initializeResult.TryGetProperty("codexHome", out System.Text.Json.JsonElement home)
            && home.ValueKind == System.Text.Json.JsonValueKind.String
            && home.GetString() is { Length: > 0 } reported)
        {
            return reported;
        }

        return Environment.GetEnvironmentVariable("CODEX_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
    }

    private static (SessionResumeOutcome, SessionOperationStage, SessionOperationFailure, SessionTransportProgress) Failure(
        SessionResumeOutcome outcome,
        SessionOperationStage stage,
        string classification,
        string method,
        long? requestId,
        int? code,
        string diagnostic,
        bool timedOut,
        bool cancelled,
        bool requestSubmitted,
        System.Text.Json.JsonElement errorData = default) =>
        (outcome, stage,
            new SessionOperationFailure(classification, method, requestId, code, errorData, diagnostic, timedOut, cancelled),
            new SessionTransportProgress(requestSubmitted, requestSubmitted, code is not null, false, null, null));

    public async Task<AgentTurnResult> RunOneShotAsync(
        AgentSessionSpec spec,
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        IAgentProcess process = await _launcher.LaunchAsync(spec, AgentSessionMode.OneShot, cancellationToken);
        await using var session = new AgentSession(
            spec,
            AgentSessionMode.OneShot,
            process,
            _boundaryDetector,
            _tokenEstimator);

        return await session.RunTurnAsync(prompt, onChunk, cancellationToken);
    }
}
