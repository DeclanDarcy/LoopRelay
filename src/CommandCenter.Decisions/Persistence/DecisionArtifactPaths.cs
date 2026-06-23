using System.Text.RegularExpressions;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Decisions.Persistence;

internal static partial class DecisionArtifactPaths
{
    public const string SchemaVersion = "1";

    private const string DecisionsRoot = ".agents/decisions";
    private const string RecordsRoot = $"{DecisionsRoot}/records";
    private const string CandidatesRoot = $"{DecisionsRoot}/candidates";
    private const string ProposalsRoot = $"{DecisionsRoot}/proposals";
    private const string AssimilationRoot = $"{DecisionsRoot}/assimilation";
    private const string GovernanceRoot = $"{DecisionsRoot}/governance";
    private const string CertificationRoot = $"{DecisionsRoot}/certification";
    private const string ProjectionsRoot = $"{DecisionsRoot}/projections";
    private const string InfluenceRoot = $"{DecisionsRoot}/influence";
    private const string QualityRoot = $"{DecisionsRoot}/quality";
    private const string QualityAssessmentsRoot = $"{QualityRoot}/assessments";
    private const string QualityReportsRoot = $"{QualityRoot}/reports";
    private const string QualityTrendsRoot = $"{QualityRoot}/trends";

    public static string DecisionDirectory(string id)
    {
        return ArtifactPath.CombineRelative(RecordsRoot, ValidateId(id, "DEC"));
    }

    public static string CandidateDirectory(string id)
    {
        return ArtifactPath.CombineRelative(CandidatesRoot, ValidateId(id, "CAND"));
    }

    public static string ProposalDirectory(string id)
    {
        return ArtifactPath.CombineRelative(ProposalsRoot, ValidateId(id, "PROP"));
    }

    public static string AssimilationDirectory(string decisionId)
    {
        return ArtifactPath.CombineRelative(AssimilationRoot, ValidateId(decisionId, "DEC"));
    }

    public static string DecisionJson(string id)
    {
        return ArtifactPath.CombineRelative(DecisionDirectory(id), "decision.json");
    }

    public static string DecisionMarkdown(string id)
    {
        return ArtifactPath.CombineRelative(DecisionDirectory(id), "decision.md");
    }

    public static string CandidateJson(string id)
    {
        return ArtifactPath.CombineRelative(CandidateDirectory(id), "candidate.json");
    }

    public static string CandidateMarkdown(string id)
    {
        return ArtifactPath.CombineRelative(CandidateDirectory(id), "candidate.md");
    }

    public static string ProposalJson(string id)
    {
        return ArtifactPath.CombineRelative(ProposalDirectory(id), "proposal.json");
    }

    public static string ProposalMarkdown(string id)
    {
        return ArtifactPath.CombineRelative(ProposalDirectory(id), "proposal.md");
    }

    public static string ProposalRevisionsDirectory(string proposalId)
    {
        return ArtifactPath.CombineRelative(ProposalDirectory(proposalId), "revisions");
    }

    public static string ProposalVersionsDirectory(string proposalId)
    {
        return ArtifactPath.CombineRelative(ProposalDirectory(proposalId), "versions");
    }

    public static string ProposalRefinementsDirectory(string proposalId)
    {
        return ArtifactPath.CombineRelative(ProposalDirectory(proposalId), "refinements");
    }

    public static string ProposalRefinementJson(string proposalId, string refinementId)
    {
        return ArtifactPath.CombineRelative(ProposalRefinementsDirectory(proposalId), $"{ValidateId(refinementId, "REF")}.json");
    }

    public static string ProposalRefinementMarkdown(string proposalId, string refinementId)
    {
        return ArtifactPath.CombineRelative(ProposalRefinementsDirectory(proposalId), $"{ValidateId(refinementId, "REF")}.md");
    }

    public static string ProposalPackageJson(string proposalId, string packageId)
    {
        return ArtifactPath.CombineRelative(ProposalVersionsDirectory(proposalId), $"{ValidateId(packageId, "PKG")}.json");
    }

    public static string ProposalPackageMarkdown(string proposalId, string packageId)
    {
        return ArtifactPath.CombineRelative(ProposalVersionsDirectory(proposalId), $"{ValidateId(packageId, "PKG")}.md");
    }

    public static string ProposalPackageComparisonMarkdown(string proposalId, string leftPackageId, string rightPackageId)
    {
        return ArtifactPath.CombineRelative(
            ProposalVersionsDirectory(proposalId),
            $"{ValidateId(leftPackageId, "PKG")}..{ValidateId(rightPackageId, "PKG")}.comparison.md");
    }

    public static string ProposalRevisionJson(string proposalId, string revisionId)
    {
        return ArtifactPath.CombineRelative(ProposalRevisionsDirectory(proposalId), $"{ValidateId(revisionId, "REV")}.json");
    }

    public static string ProposalRevisionMarkdown(string proposalId, string revisionId)
    {
        return ArtifactPath.CombineRelative(ProposalRevisionsDirectory(proposalId), $"{ValidateId(revisionId, "REV")}.md");
    }

    public static string ProposalRevisionComparisonMarkdown(string proposalId, string revisionId)
    {
        return ArtifactPath.CombineRelative(ProposalRevisionsDirectory(proposalId), $"{ValidateId(revisionId, "REV")}.comparison.md");
    }

    public static string ProposalReviewJson(string proposalId)
    {
        return ArtifactPath.CombineRelative(ProposalDirectory(proposalId), "review.json");
    }

    public static string ProposalReviewNotesJson(string proposalId)
    {
        return ArtifactPath.CombineRelative(ProposalDirectory(proposalId), "notes.json");
    }

    public static string AssimilationRecommendationJson(string decisionId)
    {
        return ArtifactPath.CombineRelative(AssimilationDirectory(decisionId), "recommendation.json");
    }

    public static string AssimilationRecommendationMarkdown(string decisionId)
    {
        return ArtifactPath.CombineRelative(AssimilationDirectory(decisionId), "recommendation.md");
    }

    public static string GovernanceReportJson(string reportId)
    {
        return ArtifactPath.CombineRelative(GovernanceRoot, $"{ValidateReportId(reportId)}.json");
    }

    public static string CertificationReportJson(string reportId)
    {
        return ArtifactPath.CombineRelative(CertificationRoot, $"{ValidateCertificationReportId(reportId)}.json");
    }

    public static string ExecutionProjectionJson(string projectionId)
    {
        return ArtifactPath.CombineRelative(ProjectionsRoot, $"{ValidateExecutionProjectionId(projectionId)}.json");
    }

    public static string ExecutionProjectionMarkdown(string projectionId)
    {
        return ArtifactPath.CombineRelative(ProjectionsRoot, $"{ValidateExecutionProjectionId(projectionId)}.md");
    }

    public static string DecisionInfluenceJson(string influenceId)
    {
        return ArtifactPath.CombineRelative(InfluenceRoot, $"{ValidateInfluenceId(influenceId)}.json");
    }

    public static string DecisionInfluenceMarkdown(string influenceId)
    {
        return ArtifactPath.CombineRelative(InfluenceRoot, $"{ValidateInfluenceId(influenceId)}.md");
    }

    public static string QualityAssessmentJson(string assessmentId)
    {
        return ArtifactPath.CombineRelative(QualityAssessmentsRoot, $"{ValidateQualityAssessmentId(assessmentId)}.json");
    }

    public static string QualityAssessmentMarkdown(string assessmentId)
    {
        return ArtifactPath.CombineRelative(QualityAssessmentsRoot, $"{ValidateQualityAssessmentId(assessmentId)}.md");
    }

    public static string QualityReportJson(string reportId)
    {
        return ArtifactPath.CombineRelative(QualityReportsRoot, $"{ValidateQualityReportId(reportId)}.json");
    }

    public static string QualityReportMarkdown(string reportId)
    {
        return ArtifactPath.CombineRelative(QualityReportsRoot, $"{ValidateQualityReportId(reportId)}.md");
    }

    public static string QualityTrendJson(string trendId)
    {
        return ArtifactPath.CombineRelative(QualityTrendsRoot, $"{ValidateQualityTrendId(trendId)}.json");
    }

    public static string QualityTrendMarkdown(string trendId)
    {
        return ArtifactPath.CombineRelative(QualityTrendsRoot, $"{ValidateQualityTrendId(trendId)}.md");
    }

    public static string GovernanceRootPath()
    {
        return GovernanceRoot;
    }

    public static string CertificationRootPath()
    {
        return CertificationRoot;
    }

    public static string ProjectionsRootPath()
    {
        return ProjectionsRoot;
    }

    public static string InfluenceRootPath()
    {
        return InfluenceRoot;
    }

    public static string QualityAssessmentsRootPath()
    {
        return QualityAssessmentsRoot;
    }

    public static string QualityReportsRootPath()
    {
        return QualityReportsRoot;
    }

    public static string QualityTrendsRootPath()
    {
        return QualityTrendsRoot;
    }

    public static string DecisionsIndex()
    {
        return ArtifactPath.CombineRelative(DecisionsRoot, "decisions.md");
    }

    public static string HistoryJsonForDirectory(string relativeDirectory)
    {
        return ArtifactPath.CombineRelative(relativeDirectory, "history.json");
    }

    public static string Resolve(Repository repository, string relativePath)
    {
        return ArtifactPath.ResolveRepositoryPath(repository, relativePath);
    }

    public static string ResolveRoot(Repository repository, DecisionArtifactKind kind)
    {
        string relativePath = kind switch
        {
            DecisionArtifactKind.Decision => RecordsRoot,
            DecisionArtifactKind.Candidate => CandidatesRoot,
            DecisionArtifactKind.Proposal => ProposalsRoot,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported decision artifact kind.")
        };

        return Resolve(repository, relativePath);
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

    public static string ValidateReportId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !ReportIdPattern().IsMatch(id))
        {
            throw new ArgumentException("Governance report id must match governance.YYYYMMDDHHMMSSFFFFFFF.", nameof(id));
        }

        return id;
    }

    public static string ValidateCertificationReportId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !CertificationReportIdPattern().IsMatch(id))
        {
            throw new ArgumentException("Certification report id must match certification.YYYYMMDDHHMMSSFFFFFFF.", nameof(id));
        }

        return id;
    }

    public static string ValidateExecutionProjectionId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !ExecutionProjectionIdPattern().IsMatch(id))
        {
            throw new ArgumentException("Execution projection id must match execution.YYYYMMDDHHMMSSFFFFFFF.", nameof(id));
        }

        return id;
    }

    public static string ValidateInfluenceId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !InfluenceIdPattern().IsMatch(id))
        {
            throw new ArgumentException("Influence id must match execution-<32 lowercase hex chars>.", nameof(id));
        }

        return id;
    }

    public static string ValidateQualityAssessmentId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !QualityAssessmentIdPattern().IsMatch(id))
        {
            throw new ArgumentException("Quality assessment id must match assessment.DEC-NNNN or assessment.YYYYMMDDHHMMSS[FFFFFFF].", nameof(id));
        }

        return id;
    }

    public static string ValidateQualityReportId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !QualityReportIdPattern().IsMatch(id))
        {
            throw new ArgumentException("Quality report id must match quality.YYYYMMDDHHMMSS[FFFFFFF].", nameof(id));
        }

        return id;
    }

    public static string ValidateQualityTrendId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !QualityTrendIdPattern().IsMatch(id))
        {
            throw new ArgumentException("Quality trend id must match trend.YYYYMMDDHHMMSS[FFFFFFF].", nameof(id));
        }

        return id;
    }

    [GeneratedRegex("^[A-Z]+-[0-9]{4}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^governance\\.[0-9]{21}$")]
    private static partial Regex ReportIdPattern();

    [GeneratedRegex("^certification\\.[0-9]{21}$")]
    private static partial Regex CertificationReportIdPattern();

    [GeneratedRegex("^execution\\.[0-9]{21}$")]
    private static partial Regex ExecutionProjectionIdPattern();

    [GeneratedRegex("^execution-[0-9a-f]{32}$")]
    private static partial Regex InfluenceIdPattern();

    [GeneratedRegex("^assessment\\.(DEC-[0-9]{4}|[0-9]{14}([0-9]{7})?)$")]
    private static partial Regex QualityAssessmentIdPattern();

    [GeneratedRegex("^quality\\.[0-9]{14}([0-9]{7})?$")]
    private static partial Regex QualityReportIdPattern();

    [GeneratedRegex("^trend\\.[0-9]{14}([0-9]{7})?$")]
    private static partial Regex QualityTrendIdPattern();
}

internal enum DecisionArtifactKind
{
    Decision,
    Candidate,
    Proposal
}
