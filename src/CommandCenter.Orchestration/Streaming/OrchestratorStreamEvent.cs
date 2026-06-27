namespace CommandCenter.Orchestration.Streaming;

/// <summary>
/// One SSE frame on an orchestrator stream. <see cref="Sequence"/> is the monotonic id a client
/// echoes as <c>Last-Event-ID</c> to resume after a reconnect; <see cref="Type"/> is the SSE
/// <c>event:</c> name; <see cref="Data"/> is the JSON <c>data:</c> payload.
/// </summary>
public sealed record OrchestratorStreamEvent(long Sequence, string Type, string Data);
