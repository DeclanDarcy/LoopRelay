using System.Text;

namespace CommandCenter.Roadmap.Cli;

internal sealed class RoadmapPromptContextBuilder(RoadmapArtifacts artifacts)
{
    private const string NorthStarMarker = "<!-- BEGIN NORTH-STAR FILE:";

    public async Task<string> BuildSelectionContextAsync(string projectionContent, IReadOnlyList<string> retiredEpicExclusions)
    {
        string completion = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.RoadmapCompletionContext);
        string roadmap = await artifacts.ReadRoadmapSourceAsync();
        return ValidateNoRawNorthStar(Build([
            Section("Projection Content", projectionContent),
            Section("Current Roadmap Completion Context", completion),
            Section("Roadmap Source", roadmap),
            Section("Retired Epic Exclusions", retiredEpicExclusions.Count == 0 ? "None" : string.Join(Environment.NewLine, retiredEpicExclusions.Select(item => "- " + item))),
        ]));
    }

    public string BuildAuditContext(string projectionContent, string selectedEpicContent) =>
        ValidateNoRawNorthStar(Build([
            Section("Projection Content", projectionContent),
            Section("Selected Epic", selectedEpicContent),
            Section("Repository Inspection Instructions", "Inspect the repository in read-only mode and report only evidence relevant to the selected epic audit."),
        ]));

    public string BuildRealignOrReimagineContext(string projectionContent, string activeEpicContent, string auditContent) =>
        ValidateNoRawNorthStar(Build([
            Section("Projection Content", projectionContent),
            Section("Current Epic", activeEpicContent),
            Section("Audit Output", auditContent),
            Section("Repository Inspection Instructions", "Inspect the repository in read-only mode and update the epic only from audit-grounded evidence."),
        ]));

    public string BuildCreateOrSplitContext(string projectionContent, string selectionProposalContent) =>
        ValidateNoRawNorthStar(Build([
            Section("Projection Content", projectionContent),
            Section("Selection Proposal", selectionProposalContent),
            Section("Repository Inspection Instructions", "Inspect the repository in read-only mode only where the prompt requires codebase reality."),
        ]));

    public async Task<string> BuildMilestoneContextAsync(string projectionContent)
    {
        string activeEpic = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.ActiveEpic);
        return ValidateNoRawNorthStar(Build([
            Section("Projection Content", projectionContent),
            Section("Active Epic", activeEpic),
        ]));
    }

    public async Task<string> BuildCompletionEvaluationContextAsync(string projectionContent)
    {
        string activeEpic = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.ActiveEpic);
        IReadOnlyList<string> specs = await artifacts.ListAsync(RoadmapArtifactPaths.SpecsDirectory, "*.md");
        var sections = new List<ContextSection>
        {
            Section("Projection Content", projectionContent),
            Section("Active Epic", activeEpic),
            Section("Repository Inspection Instructions", "Inspect the repository in read-only mode and verify implementation reality before certifying completion."),
        };

        foreach (string spec in specs.Order(StringComparer.Ordinal))
        {
            sections.Add(Section($"Milestone Spec: {spec}", await artifacts.ReadRequiredAsync(spec)));
        }

        return ValidateNoRawNorthStar(Build(sections));
    }

    public async Task<string> BuildCompletionUpdateContextAsync(string projectionContent, string latestEvaluationPath)
    {
        string completion = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.RoadmapCompletionContext);
        string activeEpic = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.ActiveEpic);
        string evaluation = await artifacts.ReadRequiredAsync(latestEvaluationPath);
        return ValidateNoRawNorthStar(Build([
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

    private static string ValidateNoRawNorthStar(string context)
    {
        if (context.Contains(NorthStarMarker, StringComparison.Ordinal))
        {
            throw new RoadmapStepException("Runtime prompt context contains raw Core North-Star markers.");
        }

        return context;
    }

    private sealed record ContextSection(string Title, string Content);
}
