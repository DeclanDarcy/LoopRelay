using System.Text.RegularExpressions;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Reasoning.Persistence;

public static partial class ReasoningArtifactPaths
{
    public const string SchemaVersion = "1";

    private const string ReasoningRoot = ".agents/reasoning";
    private const string EventsRoot = $"{ReasoningRoot}/events";
    private const string ThreadsRoot = $"{ReasoningRoot}/threads";
    private const string RelationshipsRoot = $"{ReasoningRoot}/relationships";
    private const string ReportsRoot = $"{ReasoningRoot}/reports";

    public static string EventsRootPath() => EventsRoot;

    public static string ThreadsRootPath() => ThreadsRoot;

    public static string RelationshipsRootPath() => RelationshipsRoot;

    public static string ReportsRootPath() => ReportsRoot;

    public static string EventDirectory(string id) => ArtifactPath.CombineRelative(EventsRoot, ValidateEventId(id));

    public static string ThreadDirectory(string id) => ArtifactPath.CombineRelative(ThreadsRoot, ValidateThreadId(id));

    public static string RelationshipDirectory(string id) => ArtifactPath.CombineRelative(RelationshipsRoot, ValidateRelationshipId(id));

    public static string EventJson(string id) => ArtifactPath.CombineRelative(EventDirectory(id), "event.json");

    public static string EventMarkdown(string id) => ArtifactPath.CombineRelative(EventDirectory(id), "event.md");

    public static string ThreadJson(string id) => ArtifactPath.CombineRelative(ThreadDirectory(id), "thread.json");

    public static string ThreadMarkdown(string id) => ArtifactPath.CombineRelative(ThreadDirectory(id), "thread.md");

    public static string RelationshipJson(string id) => ArtifactPath.CombineRelative(RelationshipDirectory(id), "relationship.json");

    public static string RelationshipMarkdown(string id) => ArtifactPath.CombineRelative(RelationshipDirectory(id), "relationship.md");

    public static string CertificationReportJson(string id) =>
        ArtifactPath.CombineRelative(ReportsRoot, $"{ValidateCertificationReportId(id)}.json");

    public static string CertificationReportMarkdown(string id) =>
        ArtifactPath.CombineRelative(ReportsRoot, $"{ValidateCertificationReportId(id)}.md");

    public static string ReconstructionReportJson(string id) =>
        ArtifactPath.CombineRelative(ReportsRoot, $"{ValidateReconstructionReportId(id)}.json");

    public static string ReconstructionReportMarkdown(string id) =>
        ArtifactPath.CombineRelative(ReportsRoot, $"{ValidateReconstructionReportId(id)}.md");

    public static string Resolve(Repository repository, string relativePath) => ArtifactPath.ResolveRepositoryPath(repository, relativePath);

    public static string ValidateEventId(string id) => ValidateId(id, "EVT");

    public static string ValidateThreadId(string id) => ValidateId(id, "THR");

    public static string ValidateRelationshipId(string id) => ValidateId(id, "REL");

    public static string ValidateCertificationReportId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !CertificationReportIdPattern().IsMatch(id))
        {
            throw new ArgumentException("Certification report id must match certification.YYYYMMDDHHMMSSFFFFFFF.", nameof(id));
        }

        return id;
    }

    public static string ValidateReconstructionReportId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !ReconstructionReportIdPattern().IsMatch(id))
        {
            throw new ArgumentException("Reconstruction report id must match reconstruction.YYYYMMDDHHMMSSFFFFFFF.", nameof(id));
        }

        return id;
    }

    public static string ValidateId(string id, string prefix)
    {
        if (string.IsNullOrWhiteSpace(id) || !IdPattern().IsMatch(id))
        {
            throw new ArgumentException($"{prefix} id must match {prefix}-NNNN.", nameof(id));
        }

        if (!id.StartsWith($"{prefix}-", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{prefix} id must start with {prefix}-.", nameof(id));
        }

        return id;
    }

    [GeneratedRegex("^[A-Z]+-[0-9]{4}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^certification\\.[0-9]{21}$")]
    private static partial Regex CertificationReportIdPattern();

    [GeneratedRegex("^reconstruction\\.[0-9]{21}$")]
    private static partial Regex ReconstructionReportIdPattern();
}
