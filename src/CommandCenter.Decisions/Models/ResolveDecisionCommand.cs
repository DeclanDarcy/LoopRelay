using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record ResolveDecisionCommand(
    string? Rationale,
    string? Resolver,
    string? SelectedOptionId,
    DecisionOutcome Outcome = DecisionOutcome.Accepted);
