using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class DecisionQualityReportService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository,
    IDecisionQualityAssessmentService assessmentService) : IDecisionQualityReportService
{
    public async Task<DecisionQualityReport> GenerateReportAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        IReadOnlyList<Decision> decisions = await decisionRepository.ListDecisionsAsync(repository);
        IReadOnlyList<DecisionQualityAssessment> assessments = await assessmentService.AssessRepositoryAsync(repositoryId);
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
        int reviewOnly = EffectiveBurdenCount(assessments, HumanAuthoringBurden.ReviewOnly);
        int minorEdit = EffectiveBurdenCount(assessments, HumanAuthoringBurden.MinorEdit);
        int majorRefinement = EffectiveBurdenCount(assessments, HumanAuthoringBurden.MajorRefinement);
        int fullRewrite = EffectiveBurdenCount(assessments, HumanAuthoringBurden.FullRewrite);
        int generationBypassed = EffectiveBurdenCount(assessments, HumanAuthoringBurden.GenerationBypassed);
        double averageScore = assessments.Count == 0 ? 0 : assessments.Average(assessment => assessment.Score);

        return new DecisionQualityReport(
            $"quality.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            repository.Id,
            DateTimeOffset.UtcNow,
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
            ["Quality report is advisory and does not mutate lifecycle artifacts."]);
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

        return new DecisionQualityTrend(
            $"trend.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            repositoryId,
            DateTimeOffset.UtcNow,
            currentAssessments.Count,
            RatingForAverage(currentAverage),
            RatingForAverage(previousAverage),
            currentAverage,
            previousAverage,
            direction,
            ["Quality trend compares supplied assessment sets and is advisory only."]);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static int EffectiveBurdenCount(
        IReadOnlyList<DecisionQualityAssessment> assessments,
        HumanAuthoringBurden burden)
    {
        return assessments.Count(assessment => EffectiveBurden(assessment) == burden);
    }

    private static HumanAuthoringBurden EffectiveBurden(DecisionQualityAssessment assessment)
    {
        return assessment.HumanAuthoringBurdenSignals
            .Select(signal => signal.Burden)
            .DefaultIfEmpty(HumanAuthoringBurden.Unknown)
            .OrderByDescending(BurdenWeight)
            .First();
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
