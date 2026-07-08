using System.Text;
using LoopRelay.Orchestration;

namespace LoopRelay.Completion;

internal sealed class CompletionPromptContextBuilder(CompletionArtifacts artifacts)
{
    public async Task<string> BuildEvaluationContextAsync(
        CompletionCertificationRequest request,
        string projectionContent,
        string executionEvidencePath)
    {
        var sections = new List<ContextSection>
        {
            Section("Projection Content", projectionContent),
            Section("Active Epic", await artifacts.ReadRequiredAsync(request.ActiveEpicPath)),
            Section("Execution Plan", await artifacts.ReadRequiredAsync(request.ExecutionPlanPath)),
        };

        if (!string.IsNullOrWhiteSpace(request.DetailsPath) &&
            await artifacts.ReadAsync(request.DetailsPath) is { } details)
        {
            sections.Add(Section("Execution Details", details));
        }

        IReadOnlyList<string> milestonePaths = await artifacts.ListAsync(
            request.MilestoneDirectory,
            CompletionArtifactPaths.MilestoneSearchPattern);
        foreach (string milestonePath in milestonePaths.Order(StringComparer.Ordinal))
        {
            sections.Add(Section($"Executed Milestone Evidence: {milestonePath}", await artifacts.ReadRequiredAsync(milestonePath)));
        }

        sections.Add(Section($"Execution Completion Claim: {executionEvidencePath}", await artifacts.ReadRequiredAsync(executionEvidencePath)));

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
            Section("Current Roadmap Completion Context", await artifacts.ReadRequiredAsync(roadmapCompletionContextPath)),
            Section($"Completed Epic Synthesis: {completedEpicSynthesisPath}", completedEpic),
            Section($"Latest Completion Evaluation: {evaluationEvidencePath}", await artifacts.ReadRequiredAsync(evaluationEvidencePath)),
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
        IReadOnlyList<string> paths = await artifacts.ListAsync(
            CompletionArtifactPaths.HandoffsDirectory,
            "*.md");
        var sections = new List<ContextSection>();
        foreach (string path in paths.Order(StringComparer.Ordinal).TakeLast(5))
        {
            if (await artifacts.ReadAsync(path) is { } content)
            {
                sections.Add(Section($"Recent Handoff: {path}", content));
            }
        }

        return sections;
    }

    private async Task AddNonImplementationReviewSummarySectionsAsync(
        List<ContextSection> sections,
        IReadOnlyList<string>? explicitEvidencePaths)
    {
        var included = new HashSet<string>(StringComparer.Ordinal);
        if (explicitEvidencePaths is not null)
        {
            foreach (string path in explicitEvidencePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                if (included.Add(path))
                {
                    await AddOptionalSectionAsync(
                        sections,
                        NonImplementationReviewSectionTitle(path),
                        path);
                }
            }
        }

        if (included.Add(OrchestrationArtifactPaths.NonImplementationReview))
        {
            await AddOptionalSectionAsync(
                sections,
                $"Non-Implementation Review Summary: {OrchestrationArtifactPaths.NonImplementationReview}",
                OrchestrationArtifactPaths.NonImplementationReview);
        }

        if (included.Add(OrchestrationArtifactPaths.NonImplementationSynthesis))
        {
            await AddOptionalSectionAsync(
                sections,
                $"Non-Implementation Review Synthesis: {OrchestrationArtifactPaths.NonImplementationSynthesis}",
                OrchestrationArtifactPaths.NonImplementationSynthesis);
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

    private static string NonImplementationReviewSectionTitle(string path)
    {
        string fileName = Path.GetFileName(path);
        if (string.Equals(fileName, Path.GetFileName(OrchestrationArtifactPaths.NonImplementationReview), StringComparison.Ordinal))
        {
            return $"Non-Implementation Review Summary: {path}";
        }

        if (string.Equals(fileName, Path.GetFileName(OrchestrationArtifactPaths.NonImplementationSynthesis), StringComparison.Ordinal))
        {
            return $"Non-Implementation Review Synthesis: {path}";
        }

        return $"Non-Implementation Review Evidence: {path}";
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
