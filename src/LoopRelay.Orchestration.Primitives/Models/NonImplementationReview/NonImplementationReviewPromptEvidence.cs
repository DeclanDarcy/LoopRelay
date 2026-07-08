using LoopRelay.Orchestration;

namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record NonImplementationReviewPromptEvidenceSection(
    string Title,
    string Path);

public static class NonImplementationReviewPromptEvidence
{
    public static IReadOnlyList<NonImplementationReviewPromptEvidenceSection> BuildSections(
        IReadOnlyList<string>? explicitEvidencePaths = null)
    {
        var sections = new List<NonImplementationReviewPromptEvidenceSection>();
        var included = new HashSet<string>(StringComparer.Ordinal);

        if (explicitEvidencePaths is not null)
        {
            foreach (string path in explicitEvidencePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                Add(sections, included, path.Trim());
            }
        }

        Add(sections, included, OrchestrationArtifactPaths.NonImplementationReview);
        Add(sections, included, OrchestrationArtifactPaths.NonImplementationSynthesis);
        return sections;
    }

    private static void Add(
        List<NonImplementationReviewPromptEvidenceSection> sections,
        HashSet<string> included,
        string path)
    {
        if (included.Add(path))
        {
            sections.Add(new NonImplementationReviewPromptEvidenceSection(SectionTitle(path), path));
        }
    }

    private static string SectionTitle(string path)
    {
        string fileName = Path.GetFileName(path);
        if (string.Equals(
                fileName,
                Path.GetFileName(OrchestrationArtifactPaths.NonImplementationReview),
                StringComparison.Ordinal))
        {
            return $"Non-Implementation Review Summary: {path}";
        }

        if (string.Equals(
                fileName,
                Path.GetFileName(OrchestrationArtifactPaths.NonImplementationSynthesis),
                StringComparison.Ordinal))
        {
            return $"Non-Implementation Review Synthesis: {path}";
        }

        return $"Non-Implementation Review Evidence: {path}";
    }
}
