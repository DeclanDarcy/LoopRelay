using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Prompts;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;

namespace CommandCenter.Execution.Services;

/// <summary>
/// Renders the operational execution prompt from the <c>CommandCenter.Core.Prompts</c> catalog —
/// the single prompt authority. This is the catalog's first runtime consumer.
/// <para>
/// The exact text of <c>.agents/plan.md</c> fills the <c>{plan}</c> hole; the current handoff
/// (<c>.agents/handoffs/handoff.md</c>) fills <c>{handoff}</c>; and the exact raw text of
/// <c>.agents/decisions/decisions.md</c> fills <c>{decisions}</c> on continuation. The prompt carries
/// the verbatim artifacts the agent reads — no composition, no structured rendering.
/// </para>
/// <para>
/// Operational context (<c>.agents/operational_context.md</c>) belongs to the decision/codex session
/// where decisions are made and is deliberately NOT injected into any prompt hole. It is still loaded
/// onto the execution context (so the persisted manifest can record its delivery), but never composed
/// into the prompt text. Likewise the governed decision projection stays on the context for
/// launch-blocking conflict gating only; the agent receives the raw <c>decisions.md</c>, not the
/// structured projection. The previous literal/<see cref="System.Text.StringBuilder"/> chrome
/// (repository headers, instruction block, snapshot, diagnostics, inline projection section) was
/// deleted — structured diagnostics survive only in <see cref="ExecutionPromptMetadata"/> and the
/// persisted manifest.
/// </para>
/// </summary>
public sealed class ExecutionPromptBuilder : IExecutionPromptBuilder
{
    // Canonical artifact ordering for the manifest's IncludedArtifactPaths. OperationalContext is loaded
    // for manifest bookkeeping (it is NOT injected into the prompt); Decisions feeds the {decisions} hole.
    private static readonly string[] ArtifactRoleOrder =
    [
        "Plan",
        "OperationalContext",
        "CurrentHandoff",
        "Decisions"
    ];

    public ExecutionPrompt Build(ExecutionContext context)
    {
        // {plan} is the exact text of .agents/plan.md; {handoff} the exact current handoff; {decisions}
        // the exact raw .agents/decisions/decisions.md. Operational context is excluded by design.
        string? plan = ContentForRole(context.Artifacts, "Plan");
        string? handoff = ContentForRole(context.Artifacts, "CurrentHandoff");
        string? decisions = ContentForRole(context.Artifacts, "Decisions");

        string[] inputIdentities = OrderedArtifacts(context.Artifacts)
            .Select(artifact => artifact.RelativePath)
            .ToArray();

        // A prior handoff means we are continuing an in-flight milestone; otherwise this is a
        // first-milestone start (StartExecution has no handoff/decisions holes).
        bool continuing = handoff is not null;
        string text = continuing
            ? ContinueExecution.Render(plan, handoff, decisions)
            : StartExecution.Render(plan);

        return new ExecutionPrompt
        {
            Text = text,
            Provenance = BuildProvenance(continuing, inputIdentities),
            Metadata = new ExecutionPromptMetadata
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                RepositoryPath = context.Path,
                IncludedArtifactPaths = inputIdentities,
                TotalContextBytes = context.Diagnostics.TotalBytes,
                TotalContextCharacters = context.Diagnostics.TotalCharacters,
                DirtyRepository = context.Snapshot?.DirtyState.IsClean == false
            }
        };
    }

    // Records which canonical catalog prompt rendered this operational turn (name, generated type,
    // content SourceHash), the role/phase it ran under, and the artifact identities it consumed and
    // is directed to produce. Both Start and Continue operational turns write the current handoff,
    // so it is the declared produced artifact.
    private static PromptProvenance BuildProvenance(bool continuing, IReadOnlyList<string> inputIdentities)
    {
        string[] outputIdentities = [HandoffService.CurrentHandoffPath];

        return continuing
            ? new PromptProvenance
            {
                PromptName = nameof(ContinueExecution),
                PromptType = typeof(ContinueExecution).FullName!,
                SourceHash = ContinueExecution.SourceHash,
                SessionRole = PromptSessionRole.OperationalExecution,
                WorkflowPhase = "Continue",
                InputArtifactIdentities = inputIdentities,
                OutputArtifactIdentities = outputIdentities
            }
            : new PromptProvenance
            {
                PromptName = nameof(StartExecution),
                PromptType = typeof(StartExecution).FullName!,
                SourceHash = StartExecution.SourceHash,
                SessionRole = PromptSessionRole.OperationalExecution,
                WorkflowPhase = "Start",
                InputArtifactIdentities = inputIdentities,
                OutputArtifactIdentities = outputIdentities
            };
    }

    private static string? ContentForRole(IReadOnlyList<LoadedArtifact> artifacts, string role)
    {
        return artifacts.FirstOrDefault(artifact => artifact.Role == role)?.Content;
    }

    private static IEnumerable<LoadedArtifact> OrderedArtifacts(IReadOnlyList<LoadedArtifact> artifacts)
    {
        return artifacts.OrderBy(artifact =>
            {
                int index = Array.IndexOf(ArtifactRoleOrder, artifact.Role);
                return index < 0 ? ArtifactRoleOrder.Length : index;
            })
            .ThenBy(artifact => artifact.RelativePath, StringComparer.Ordinal);
    }
}
