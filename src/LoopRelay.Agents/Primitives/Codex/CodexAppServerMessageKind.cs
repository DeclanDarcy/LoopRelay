namespace LoopRelay.Agents.Primitives.Codex;

public enum CodexAppServerMessageKind
{
    /// <summary>A reply to one of our requests (carries <c>id</c> + <c>result</c>/<c>error</c>, no <c>method</c>).</summary>
    Response,

    /// <summary>A server-to-client request we must answer (carries <c>id</c> + <c>method</c>) — e.g. an approval callback.</summary>
    ServerRequest,

    /// <summary>A fire-and-forget event (carries <c>method</c>, no <c>id</c>) — the turn stream.</summary>
    Notification,

    /// <summary>Unrecognised or non-object line.</summary>
    Unknown
}
