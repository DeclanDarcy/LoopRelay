namespace CommandCenter.Decisions.Models;

public sealed record DecisionContextItem(
    string Id,
    string Kind,
    string Title,
    string Content,
    bool Required,
    string Fingerprint,
    IReadOnlyList<DecisionSourceReference> Sources);
