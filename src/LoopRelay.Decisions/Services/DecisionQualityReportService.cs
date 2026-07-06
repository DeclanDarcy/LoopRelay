using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Services;

public sealed class DecisionQualityReportService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository,
    IDecisionQualityAssessmentService assessmentService,
    IDecisionArtifactProjectionService projectionService) : IDecisionQualityReportService
{
    public async Task<DecisionQualityReport> GenerateReportAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        IReadOnlyList<Decision> decisions = await decisionRepository.ListDecisionsAsync(repository);
        IReadOnlyList<DecisionQualityAssessment> assessments = await assessmentService.AssessRepositoryAsync(repositoryId);
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;

        return BuildReport(repository.Id, generatedAt, decisions, assessments);
    }

    public async Task<DecisionQualityReport> GenerateAndSaveReportAsync(Guid repositoryId)
    {
        DecisionQualityReport report = await GenerateReportAsync(repositoryId);
        return await SaveReportAsync(repositoryId, report);
    }

    public async Task<IReadOnlyList<DecisionQualityReport>> ListReportsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await decisionRepository.ListQualityReportsAsync(repository);
    }

    public async Task<DecisionQualityReport> SaveReportAsync(Guid repositoryId, DecisionQualityReport report)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        if (report.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException(
                $"Quality report {report.Id} belongs to repository {report.RepositoryId}, not {repository.Id}.");
        }

        DecisionQualityReport saved = await decisionRepository.SaveQualityReportAsync(repository, report);
        await projectionService.ProjectQualityReportAsync(repository, saved);
        return saved;
    }

    public DecisionQualityTrend GenerateTrend(
        Guid repositoryId,
        IReadOnlyList<DecisionQualityAssessment> previousAssessments,
        IReadOnlyList<DecisionQualityAssessment> currentAssessments)
    {
        double previousAverage = previousAssessments.Count == 0 ? 0 : previousAssessments.Average(assessment => assessment.Score);
        double currentAverage = currentAssessments.Count == 0 ? 0 : currentAssessments.Average(assessment => assessment.Score);
        QualitySignalDirection direction = currentAverage > previousAverage
            ? QualitySignalDirection.Positive
            : currentAverage < previousAverage
                ? QualitySignalDirection.Negative
                : QualitySignalDirection.Neutral;
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;

        return new DecisionQualityTrend(
            $"trend.{generatedAt:yyyyMMddHHmmssfffffff}",
            repositoryId,
            generatedAt,
            currentAssessments.Count,
            RatingForAverage(currentAverage),
            RatingForAverage(previousAverage),
            currentAverage,
            previousAverage,
            direction,
            ["Quality trend compares supplied assessment sets and is advisory only."]);
    }

    public async Task<DecisionQualityTrend> GenerateTrendFromHistoryAsync(Guid repositoryId)
    {
        IReadOnlyList<DecisionQualityAssessment> persistedAssessments =
            await assessmentService.ListAssessmentsAsync(repositoryId);
        (IReadOnlyList<DecisionQualityAssessment> previousAssessments, IReadOnlyList<DecisionQualityAssessment> currentAssessments) =
            SplitAssessmentHistory(persistedAssessments);
        DecisionQualityTrend trend = GenerateTrend(repositoryId, previousAssessments, currentAssessments);
        return trend with
        {
            Diagnostics =
            [
                "Quality trend was generated from persisted assessment history and is advisory only.",
                $"Compared {previousAssessments.Count} previous assessment(s) with {currentAssessments.Count} current assessment(s)."
            ]
        };
    }

    public async Task<DecisionQualityTrend> GenerateAndSaveTrendFromHistoryAsync(Guid repositoryId)
    {
        DecisionQualityTrend trend = await GenerateTrendFromHistoryAsync(repositoryId);
        return await SaveTrendAsync(repositoryId, trend);
    }

    public async Task<IReadOnlyList<DecisionQualityTrend>> ListTrendsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await decisionRepository.ListQualityTrendsAsync(repository);
    }

    public async Task<DecisionQualityTrend> SaveTrendAsync(Guid repositoryId, DecisionQualityTrend trend)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        if (trend.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException(
                $"Quality trend {trend.Id} belongs to repository {trend.RepositoryId}, not {repository.Id}.");
        }

        DecisionQualityTrend saved = await decisionRepository.SaveQualityTrendAsync(repository, trend);
        await projectionService.ProjectQualityTrendAsync(repository, saved);
        return saved;
    }

    private static DecisionQualityReport BuildReport(
        Guid repositoryId,
        DateTimeOffset generatedAt,
        IReadOnlyList<Decision> decisions,
        IReadOnlyList<DecisionQualityAssessment> assessments)
    {
        int count = decisions.Count;
        int accepted = decisions.Count(decision => decision.Resolution?.Outcome == DecisionOutcome.Accepted);
        int rejected = decisions.Count(decision => decision.Resolution?.Outcome == DecisionOutcome.Rejected);
        int superseded = decisions.Count(decision => decision.State == DecisionState.Superseded);
        int generatedPackages = decisions.Count(decision => decision.Resolution?.SourceProposalSnapshot?.PackageId is not null);
        int divergence = decisions.Count(decision => decision.Resolution?.RecommendationDiverged == true);
        int alternativeUtilization = assessments.Count(assessment => assessment.Signals.Any(signal =>
            signal.Category == "OptionQuality" && signal.Direction == QualitySignalDirection.Positive));
        int modified = assessments.Count(assessment => assessment.HumanAuthoringBurdenSignals.Any(signal =>
            signal.Burden is HumanAuthoringBurden.MinorEdit or HumanAuthoringBurden.MajorRefinement or HumanAuthoringBurden.FullRewrite));
        IReadOnlyList<HumanAuthoringBurdenExplanation> burdenExplanations = assessments
            .Select(BuildBurdenExplanation)
            .OrderBy(explanation => explanation.DecisionId, StringComparer.Ordinal)
            .ToArray();
        int reviewOnly = EffectiveBurdenCount(burdenExplanations, HumanAuthoringBurden.ReviewOnly);
        int minorEdit = EffectiveBurdenCount(burdenExplanations, HumanAuthoringBurden.MinorEdit);
        int majorRefinement = EffectiveBurdenCount(burdenExplanations, HumanAuthoringBurden.MajorRefinement);
        int fullRewrite = EffectiveBurdenCount(burdenExplanations, HumanAuthoringBurden.FullRewrite);
        int generationBypassed = EffectiveBurdenCount(burdenExplanations, HumanAuthoringBurden.GenerationBypassed);
        double averageScore = assessments.Count == 0 ? 0 : assessments.Average(assessment => assessment.Score);

        return new DecisionQualityReport(
            $"quality.{generatedAt:yyyyMMddHHmmssfffffff}",
            repositoryId,
            generatedAt,
            count,
            generatedPackages,
            accepted,
            Rate(accepted, count),
            modified,
            Rate(modified, count),
            rejected,
            Rate(rejected, count),
            superseded,
            Rate(superseded, count),
            divergence,
            Rate(divergence, count),
            alternativeUtilization,
            Rate(alternativeUtilization, count),
            reviewOnly,
            Rate(reviewOnly, count),
            minorEdit,
            Rate(minorEdit, count),
            majorRefinement,
            Rate(majorRefinement, count),
            fullRewrite,
            Rate(fullRewrite, count),
            generationBypassed,
            Rate(generationBypassed, count),
            RatingForAverage(averageScore),
            assessments,
            ["Quality report is advisory and does not mutate lifecycle artifacts."],
            burdenExplanations);
    }

    private static (
        IReadOnlyList<DecisionQualityAssessment> PreviousAssessments,
        IReadOnlyList<DecisionQualityAssessment> CurrentAssessments) SplitAssessmentHistory(
            IReadOnlyList<DecisionQualityAssessment> assessments)
    {
        var previousAssessments = new List<DecisionQualityAssessment>();
        var currentAssessments = new List<DecisionQualityAssessment>();
        foreach (IGrouping<string, DecisionQualityAssessment> decisionAssessments in assessments
            .GroupBy(assessment => assessment.DecisionId, StringComparer.Ordinal))
        {
            DecisionQualityAssessment[] ordered = decisionAssessments
                .OrderByDescending(assessment => assessment.AssessedAt)
                .ThenByDescending(assessment => assessment.Id, StringComparer.Ordinal)
                .ToArray();
            if (ordered.Length > 0)
            {
                currentAssessments.Add(ordered[0]);
            }

            if (ordered.Length > 1)
            {
                previousAssessments.Add(ordered[1]);
            }
        }

        return (
            previousAssessments.OrderBy(assessment => assessment.DecisionId, StringComparer.Ordinal).ToArray(),
            currentAssessments.OrderBy(assessment => assessment.DecisionId, StringComparer.Ordinal).ToArray());
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static int EffectiveBurdenCount(
        IReadOnlyList<HumanAuthoringBurdenExplanation> explanations,
        HumanAuthoringBurden burden)
    {
        return explanations.Count(explanation => explanation.EffectiveBurden == burden);
    }

    private static HumanAuthoringBurdenExplanation BuildBurdenExplanation(DecisionQualityAssessment assessment)
    {
        if (assessment.HumanAuthoringBurdenExplanation is not null)
        {
            return assessment.HumanAuthoringBurdenExplanation;
        }

        HumanAuthoringBurdenSignal? winningSignal = assessment.HumanAuthoringBurdenSignals
            .OrderByDescending(signal => BurdenWeight(signal.Burden))
            .ThenBy(signal => signal.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        HumanAuthoringBurden effectiveBurden = winningSignal?.Burden ?? HumanAuthoringBurden.Unknown;
        return new HumanAuthoringBurdenExplanation(
            assessment.DecisionId,
            "Select the highest-weight human-authoring burden signal; GenerationBypassed > FullRewrite > MajorRefinement > MinorEdit > ReviewOnly > Unknown.",
            effectiveBurden,
            winningSignal,
            effectiveBurden == HumanAuthoringBurden.Unknown,
            winningSignal is null,
            [
                winningSignal is null
                    ? "No human-authoring burden signal was available, so burden is Unknown."
                    : $"Signal {winningSignal.Id} selected effective burden {effectiveBurden}."
            ]);
    }

    private static int BurdenWeight(HumanAuthoringBurden burden)
    {
        return burden switch
        {
            HumanAuthoringBurden.GenerationBypassed => 5,
            HumanAuthoringBurden.FullRewrite => 4,
            HumanAuthoringBurden.MajorRefinement => 3,
            HumanAuthoringBurden.MinorEdit => 2,
            HumanAuthoringBurden.ReviewOnly => 1,
            _ => 0
        };
    }

    private static double Rate(int numerator, int denominator)
    {
        return denominator == 0 ? 0 : Math.Round((double)numerator / denominator, 4);
    }

    private static DecisionQualityRating RatingForAverage(double average)
    {
        return average switch
        {
            >= 85 => DecisionQualityRating.Excellent,
            >= 65 => DecisionQualityRating.Good,
            >= 40 => DecisionQualityRating.Mixed,
            > 0 => DecisionQualityRating.Poor,
            _ => DecisionQualityRating.Unknown
        };
    }
}
