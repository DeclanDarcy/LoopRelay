using System.Text;

namespace LoopRelay.Roadmap.Cli;

internal sealed class RoadmapPromptContextBuilder(
    RoadmapArtifacts artifacts,
    ExecutionPreparationProvenanceService executionPreparation)
{
    private const string ProjectContextMarker = "<!-- BEGIN PROJECT-CONTEXT FILE:";

    public async Task<string> BuildSelectionContextAsync(string projectionContent, IReadOnlyList<RetiredEpic> retiredEpics)
    {
        string completion = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.RoadmapCompletionContext);
        string roadmap = await artifacts.ReadRoadmapSourceAsync();
        return ValidateNoRawProjectContext(Build([
            Section("Projection Content", projectionContent),
            Section("Current Roadmap Completion Context", completion),
            Section("Roadmap Source", roadmap),
            Section("Retired Epics", RenderRetiredEpics(retiredEpics)),
        ]));
    }

    public string BuildAuditContext(string projectionContent, string selectedEpicContent) =>
        ValidateNoRawProjectContext(Build([
            Section("Projection Content", projectionContent),
            Section("Selected Epic", selectedEpicContent),
            Section("Repository Inspection Instructions", "Inspect the repository in read-only mode and report only evidence relevant to the selected epic audit."),
        ]));

    public string BuildRealignOrReimagineContext(string projectionContent, string activeEpicContent, string auditContent) =>
        ValidateNoRawProjectContext(Build([
            Section("Projection Content", projectionContent),
            Section("Current Epic", activeEpicContent),
            Section("Audit Output", auditContent),
            Section("Repository Inspection Instructions", "Inspect the repository in read-only mode and update the epic only from audit-grounded evidence."),
        ]));

    public string BuildCreateOrSplitContext(string projectionContent, string selectionProposalContent) =>
        ValidateNoRawProjectContext(Build([
            Section("Projection Content", projectionContent),
            Section("Selection Proposal", selectionProposalContent),
            Section("Repository Inspection Instructions", "Inspect the repository in read-only mode only where the prompt requires codebase reality."),
        ]));

    public async Task<string> BuildMilestoneContextAsync(string projectionContent)
    {
        string activeEpic = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.ActiveEpic);
        return ValidateNoRawProjectContext(Build([
            Section("Projection Content", projectionContent),
            Section("Active Epic", activeEpic),
        ]));
    }

    public async Task<string> BuildCompletionEvaluationContextAsync(string projectionContent, string executionEvidencePath)
    {
        string activeEpic = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.ActiveEpic);
        string executionEvidence = await artifacts.ReadRequiredAsync(executionEvidencePath);
        IReadOnlyList<string> specs = await executionPreparation.RequireFreshMilestoneSpecPathsAsync();
        var sections = new List<ContextSection>
        {
            Section("Projection Content", projectionContent),
            Section("Active Epic", activeEpic),
            Section($"Execution Evidence: {executionEvidencePath}", executionEvidence),
            Section("Repository Inspection Instructions", "Inspect the repository in read-only mode and verify implementation reality before certifying completion."),
        };

        foreach (string spec in specs.Order(StringComparer.Ordinal))
        {
            sections.Add(Section($"Milestone Spec: {spec}", await artifacts.ReadRequiredAsync(spec)));
        }

        return ValidateNoRawProjectContext(Build(sections));
    }

    public async Task<string> BuildCompletionUpdateContextAsync(string projectionContent, string latestEvaluationPath)
    {
        string completion = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.RoadmapCompletionContext);
        string activeEpic = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.ActiveEpic);
        string evaluation = await artifacts.ReadRequiredAsync(latestEvaluationPath);
        return ValidateNoRawProjectContext(Build([
            Section("Projection Content", projectionContent),
            Section("Current Roadmap Completion Context", completion),
            Section("Completed Epic", activeEpic),
            Section("Latest Completion Evaluation", evaluation),
            Section("Repository Inspection Instructions", "Inspect repository reality in read-only mode before updating strategic state."),
        ]));
    }

    private static string Build(IReadOnlyList<ContextSection> sections)
    {
        var builder = new StringBuilder("# Roadmap Runtime Prompt Context");
        foreach (ContextSection section in sections)
        {
            builder.AppendLine().AppendLine().Append("## ").AppendLine(section.Title).AppendLine().Append(section.Content.Trim()).AppendLine();
        }

        return builder.ToString();
    }

    private static ContextSection Section(string title, string content) => new(title, content);

    private static string RenderRetiredEpics(IReadOnlyList<RetiredEpic> retiredEpics)
    {
        if (retiredEpics.Count == 0)
        {
            return "None";
        }

        var builder = new StringBuilder();
        builder.AppendLine("| Identity Kind | Stable Identity | Epic Name | Audit Evidence | Primary Reason |");
        builder.AppendLine("|---|---|---|---|---|");
        foreach (RetiredEpic retired in retiredEpics)
        {
            builder
                .Append("| ")
                .Append(Escape(retired.IdentityKind))
                .Append(" | ")
                .Append(Escape(retired.StableIdentity))
                .Append(" | ")
                .Append(Escape(retired.DisplayName))
                .Append(" | ")
                .Append(Escape(retired.AuditEvidencePath))
                .Append(" | ")
                .Append(Escape(retired.PrimaryReason))
                .AppendLine(" |");
        }

        return builder.ToString().TrimEnd();
    }

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

    private static string ValidateNoRawProjectContext(string context)
    {
        if (context.Contains(ProjectContextMarker, StringComparison.Ordinal))
        {
            throw new RoadmapStepException("Runtime prompt context contains raw Project Context markers.");
        }

        return context;
    }

    private sealed record ContextSection(string Title, string Content);
}
