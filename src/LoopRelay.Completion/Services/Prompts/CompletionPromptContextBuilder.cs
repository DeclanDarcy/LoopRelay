using System.Text;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationReview;

namespace LoopRelay.Completion.Services.Prompts;

internal sealed class CompletionPromptContextBuilder(
    ArtifactStorage.CompletionArtifacts _artifacts,
    ILogicalArtifactResolver? logicalResolver = null)
{
    private readonly ILogicalArtifactResolver _logicalResolver =
        logicalResolver ?? CompletionLogicalArtifactServices.CreateResolver(_artifacts);

    public async Task<string> BuildEvaluationContextAsync(
        CompletionCertificationRequest request,
        string projectionContent,
        string executionEvidencePath)
    {
        var sections = new List<ContextSection>
        {
            Section("Projection Content", projectionContent),
            Section("Active Epic", await _artifacts.ReadRequiredAsync(request.ActiveEpicPath)),
            Section("Execution Plan", await _artifacts.ReadRequiredAsync(request.ExecutionPlanPath)),
        };

        if (!string.IsNullOrWhiteSpace(request.DetailsPath) &&
            await _artifacts.ReadAsync(request.DetailsPath) is { } details)
        {
            sections.Add(Section("Execution Details", details));
        }

        IReadOnlyList<string> milestonePaths = await _artifacts.ListAsync(
            request.MilestoneDirectory,
            CompletionArtifactPaths.MilestoneSearchPattern);
        foreach (string milestonePath in milestonePaths.Order(StringComparer.Ordinal))
        {
            sections.Add(Section($"Executed Milestone Evidence: {milestonePath}", await _artifacts.ReadRequiredAsync(milestonePath)));
        }

        sections.Add(Section($"Execution Completion Claim: {executionEvidencePath}", await ReadRequiredLogicalAsync(executionEvidencePath)));

        foreach (ContextSection handoff in await BuildHandoffSectionsAsync())
        {
            sections.Add(handoff);
        }

        await AddNonImplementationReviewSummarySectionsAsync(sections, request.NonImplementationReviewEvidencePaths);

        sections.Add(Section(
            "Repository Inspection Instructions",
            "Inspect the repository in read-only mode and verify implementation reality before certifying completion. Treat checked milestone boxes as a completion claim, not as proof of closure."));

        return Build(sections);
    }

    public async Task<string> BuildCompletionUpdateContextAsync(
        string projectionContent,
        string roadmapCompletionContextPath,
        string completedEpicSynthesisPath,
        string completedEpic,
        string evaluationEvidencePath,
        IReadOnlyList<string>? nonImplementationReviewEvidencePaths = null)
    {
        var sections = new List<ContextSection>
        {
            Section("Projection Content", projectionContent),
            Section("Current Roadmap Completion Context", await _artifacts.ReadRequiredAsync(roadmapCompletionContextPath)),
            Section($"Completed Epic Synthesis: {completedEpicSynthesisPath}", completedEpic),
            Section($"Latest Completion Evaluation: {evaluationEvidencePath}", await _artifacts.ReadRequiredAsync(evaluationEvidencePath)),
        };

        await AddNonImplementationReviewSummarySectionsAsync(sections, nonImplementationReviewEvidencePaths);

        sections.Add(Section(
            "Repository Inspection Instructions",
            "Inspect repository reality in read-only mode before updating strategic state."));

        return Build(sections);
    }

    public string BuildRoadmapCompletionBootstrapContext(string projectionContent) =>
        Build(
        [
            Section("Projection Content", projectionContent),
            Section("Repository Inspection Instructions", "Use the supplied completed epic evidence and inspect only where needed to create the initial strategic completion state."),
        ]);

    private async Task<IReadOnlyList<ContextSection>> BuildHandoffSectionsAsync()
    {
        IReadOnlyList<string> paths = await _artifacts.ListAsync(
            CompletionArtifactPaths.HandoffsDirectory,
            "*.md");
        var sections = new List<ContextSection>();
        foreach (string path in paths.Order(StringComparer.Ordinal).TakeLast(5))
        {
            if (await ReadOptionalLogicalAsync(path) is { } content)
            {
                sections.Add(Section($"Recent Handoff: {path}", content));
            }
        }

        return sections;
    }

    private async Task<string?> ReadOptionalLogicalAsync(string relativePath)
    {
        LogicalArtifactResolutionResult result = await _logicalResolver.ResolveAsync(relativePath);
        return result.IsResolved && !string.IsNullOrWhiteSpace(result.Content?.Text)
            ? result.Content.Text
            : null;
    }

    private async Task AddNonImplementationReviewSummarySectionsAsync(
        List<ContextSection> sections,
        IReadOnlyList<string>? explicitEvidencePaths)
    {
        foreach (NonImplementationReviewPromptEvidenceSection evidenceSection in
            NonImplementationReviewPromptEvidence.BuildSections(explicitEvidencePaths))
        {
            await AddOptionalSectionAsync(
                sections,
                evidenceSection.Title,
                evidenceSection.Path);
        }
    }

    private async Task AddOptionalSectionAsync(List<ContextSection> sections, string title, string relativePath)
    {
        string? content = await _artifacts.ReadAsync(relativePath);
        if (!string.IsNullOrWhiteSpace(content))
        {
            sections.Add(Section(title, content));
        }
    }

    private async Task<string> ReadRequiredLogicalAsync(string relativePath)
    {
        LogicalArtifactResolutionResult result = await _logicalResolver.ResolveAsync(relativePath);
        if (!result.IsResolved || string.IsNullOrWhiteSpace(result.Content?.Text))
        {
            throw new CompletionCertificationException(
                result.Message ?? $"Required artifact is missing or empty: {relativePath}");
        }

        return result.Content.Text;
    }

    private static string Build(IReadOnlyList<ContextSection> sections)
    {
        var builder = new StringBuilder("# Completion Runtime Prompt Context");
        foreach (ContextSection section in sections)
        {
            builder
                .AppendLine()
                .AppendLine()
                .Append("## ")
                .AppendLine(section.Title)
                .AppendLine()
                .AppendLine(section.Content.Trim());
        }

        return builder.ToString();
    }

    private static ContextSection Section(string title, string content) => new(title, content);

    private sealed record ContextSection(string Title, string Content);
}
