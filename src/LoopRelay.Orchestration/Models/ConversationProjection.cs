namespace LoopRelay.Orchestration.Models;

/// <summary>
/// The kind of a conversation entry in the Plan Authoring -> Execution -> Decision loop (m6). This vocabulary
/// is deliberately narrow to THIS flow — a transcript of the loop's turns, NOT a repository knowledge platform.
/// </summary>
public enum ConversationEntryKind
{
    /// <summary>A plan was authored (or revised) by the held-open Operational planning process.</summary>
    Planning,

    /// <summary>An operational start-execution turn produced and rotated the first handoff.</summary>
    OperationalOutput,

    /// <summary>
    /// The Decision process proposed decisions that became editable on <c>review-ready</c> — this covers the
    /// first proposal AND every subsequent "next decision" proposal the loop produces after a continuation.
    /// </summary>
    DecisionOutput,

    /// <summary>The human submitted edited decisions through the only required review gate.</summary>
    Submit,

    /// <summary>A ContinueExecution turn consumed the submitted decisions and produced the next handoff.</summary>
    Continuation,
}

/// <summary>
/// One ordered entry in the repository conversation projection. It carries only a short summary and the
/// repository-relative artifact <see cref="Reference"/> it concerns (never full content), so the projection
/// stays a lightweight timeline rather than a content store.
/// </summary>
public sealed record ConversationEntry(int Sequence, ConversationEntryKind Kind, int Iteration, string Summary, string? Reference);

/// <summary>The whole flow-specific conversation projection: an ordered, append-only list of entries (m6).</summary>
public sealed record ConversationProjection(IReadOnlyList<ConversationEntry> Entries);
