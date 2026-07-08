using System.Text;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.ExecutionPreparation;

namespace LoopRelay.Roadmap.Cli.Services.Prompts;

internal sealed class RoadmapPromptContextBuilder(
    RoadmapArtifacts artifacts,
    ExecutionPreparationProvenanceService executionPreparation)
{
    private const string ProjectContextMarker = "<!-- BEGIN PROJECT-CONTEXT FILE:";

    public async Task<string> BuildSelectionContextAsync(string projectionContent, IReadOnlyList<RetiredEpic> retiredEpics)
    {
        string completion = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.RoadmapCompletionContext);
        string roadmapSources = await RenderRoadmapSourceReferencesAsync();
        return ValidateNoRawProjectContext(Build([
            Section("Projection Content", projectionContent),
            Section("Current Roadmap Completion Context", completion),
            Section("Roadmap Source References", roadmapSources),
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

        await AddNonImplementationReviewSectionsAsync(sections);

        return ValidateNoRawProjectContext(Build(sections));
    }

    public async Task<string> BuildCompletionUpdateContextAsync(
        string projectionContent,
        string latestEvaluationPath,
        string completedEpicSynthesisPath,
        string completedEpicSynthesis)
    {
        string completion = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.RoadmapCompletionContext);
        string evaluation = await artifacts.ReadRequiredAsync(latestEvaluationPath);
        var sections = new List<ContextSection>
        {
            Section("Projection Content", projectionContent),
            Section("Current Roadmap Completion Context", completion),
            Section($"Completed Epic Synthesis: {completedEpicSynthesisPath}", completedEpicSynthesis),
            Section("Latest Completion Evaluation", evaluation),
            Section("Repository Inspection Instructions", "Inspect repository reality in read-only mode before updating strategic state."),
        };
        await AddNonImplementationReviewSectionsAsync(sections);
        return ValidateNoRawProjectContext(Build(sections));
    }

    private async Task AddNonImplementationReviewSectionsAsync(List<ContextSection> sections)
    {
        foreach (NonImplementationReviewPromptEvidenceSection evidenceSection in
            NonImplementationReviewPromptEvidence.BuildSections())
        {
            await AddOptionalSectionAsync(
                sections,
                evidenceSection.Title,
                evidenceSection.Path);
        }
    }

    private async Task AddOptionalSectionAsync(List<ContextSection> sections, string title, string relativePath)
    {
        string? content = await artifacts.ReadAsync(relativePath);
        if (!string.IsNullOrWhiteSpace(content))
        {
            sections.Add(Section(title, content));
        }
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

    private async Task<string> RenderRoadmapSourceReferencesAsync()
    {
        IReadOnlyList<string> sourcePaths = await artifacts.RequireRoadmapSourcePathsAsync();
        var builder = new StringBuilder();
        builder.AppendLine("Roadmap epic bodies are intentionally not embedded in this prompt context.");
        builder
            .Append("Read roadmap epics directly from `").Append((string?)RoadmapArtifactPaths.RoadmapDirectoryPattern)
            .AppendLine("` before evaluating existing roadmap candidates.");
        builder.AppendLine();
        builder.AppendLine("| Source Path | Source Kind |");
        builder.AppendLine("|---|---|");
        builder
            .Append("| ")
            .Append(Escape(RoadmapArtifactPaths.RoadmapDirectoryPattern))
            .AppendLine(" | Primary roadmap epic glob |");

        foreach (string path in sourcePaths)
        {
            builder
                .Append("| ")
                .Append(Escape(path))
                .AppendLine(" | Roadmap epic source |");
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
