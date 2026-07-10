using LoopRelay.Agents.Models.Sessions;

namespace LoopRelay.Agents.Abstractions;

public interface IAgentSessionContinuityRuntime
{
    Task<SessionContinuityNegotiationResult> NegotiateAsync(
        SessionContinuityNegotiationRequest request,
        CancellationToken cancellationToken = default);

    Task<SessionCreateResult> CreateSessionAsync(
        SessionCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<SessionResumeResult> ResumeSessionAsync(
        SessionResumeRequest request,
        CancellationToken cancellationToken = default);

    Task<SessionContentResult> ReadSessionAsync(SessionContentRequest request, CancellationToken cancellationToken = default);
    Task<SessionSeedResult> SeedSessionAsync(SessionSeedRequest request, CancellationToken cancellationToken = default);
    Task<SessionForkResult> ForkSessionAsync(SessionForkRequest request, CancellationToken cancellationToken = default);
    Task<SessionReconcileResult> ReconcileAsync(SessionReconcileRequest request, CancellationToken cancellationToken = default);
    ValueTask CloseSessionAsync(IAgentSession session) => session.DisposeAsync();
}
