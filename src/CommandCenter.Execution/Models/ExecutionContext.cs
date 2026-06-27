using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Models;

namespace CommandCenter.Execution.Models;

/// <summary>
/// Execution-flavored <see cref="RepoContext"/>: the shared repository context (identity, artifacts,
/// snapshot) plus the execution-specific governance projection and size/validation diagnostics. These
/// extra concerns stay in the Execution layer — they reference Decisions/Execution types that cannot
/// move into Core — while the reusable substrate lives on the <see cref="RepoContext"/> base.
/// System.Text.Json flattens the inherited properties, so the wire shape stays flat.
/// </summary>
public sealed class ExecutionContext : RepoContext
{
    public ExecutionDecisionProjection? DecisionProjection { get; init; }

    public ExecutionContextDiagnostics Diagnostics { get; init; } = new();
}
