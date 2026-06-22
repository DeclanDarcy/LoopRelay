namespace CommandCenter.Decisions.Models;

public sealed record DecisionReviewNoteRequest(
    string Body,
    string? Reviewer = null,
    IReadOnlyList<DecisionSourceReference>? Sources = null);
