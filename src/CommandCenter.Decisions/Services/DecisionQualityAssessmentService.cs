using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class DecisionQualityAssessmentService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository,
    IDecisionQualitySignalService signalService,
    IHumanAuthoringBurdenService humanAuthoringBurdenService) : IDecisionQualityAssessmentService
{
    public async Task<DecisionQualityAssessment> AssessDecisionAsync(Guid repositoryId, string decisionId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        Decision decision = await GetDecisionAsync(repository, decisionId);
        IReadOnlyList<DecisionQualitySignal> signals = await signalService.ExtractSignalsAsync(repositoryId, decisionId);
        IReadOnlyList<HumanAuthoringBurdenSignal> burdenSignals =
            await humanAuthoringBurdenService.ExtractSignalsAsync(repositoryId, decisionId);
        int score = Math.Clamp(50 + signals.Sum(ScoreContribution), 0, 100);
        DecisionQualityRating rating = RatingFor(score, signals);
        DateTimeOffset assessedAt = DateTimeOffset.UtcNow;
        return new DecisionQualityAssessment(
            $"assessment.{assessedAt:yyyyMMddHHmmssFFFFFFF}",
            repository.Id,
            decision.Id.Value,
            assessedAt,
            rating,
            score,
            signals,
            burdenSignals,
            BuildDiagnostics(decision, signals, burdenSignals));
    }

    public async Task<IReadOnlyList<DecisionQualityAssessment>> AssessRepositoryAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        IReadOnlyList<Decision> decisions = await decisionRepository.ListDecisionsAsync(repository);
        var assessments = new List<DecisionQualityAssessment>();
        foreach (Decision decision in decisions.OrderBy(decision => decision.Id.Value, StringComparer.Ordinal))
        {
            assessments.Add(await AssessDecisionAsync(repositoryId, decision.Id.Value));
        }

        return assessments;
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<Decision> GetDecisionAsync(Repository repository, string decisionId)
    {
        DecisionId id = DecisionId.Parse(decisionId);
        Decision? decision = await decisionRepository.GetDecisionAsync(repository, id);
        return decision ?? throw new KeyNotFoundException($"Decision was not found: {id.Value}");
    }

    private static int ScoreContribution(DecisionQualitySignal signal)
    {
        int magnitude = signal.Severity switch
        {
            QualitySignalSeverity.Critical => 35,
            QualitySignalSeverity.High => 25,
            QualitySignalSeverity.Medium => 15,
            QualitySignalSeverity.Low => 8,
            _ => 3
        };
        return signal.Direction switch
        {
            QualitySignalDirection.Positive => magnitude,
            QualitySignalDirection.Negative => -magnitude,
            _ => 0
        };
    }

    private static DecisionQualityRating RatingFor(int score, IReadOnlyList<DecisionQualitySignal> signals)
    {
        if (signals.Any(signal => signal.Direction == QualitySignalDirection.Negative && signal.Severity == QualitySignalSeverity.Critical))
        {
            return DecisionQualityRating.Poor;
        }

        return score switch
        {
            >= 85 => DecisionQualityRating.Excellent,
            >= 65 => DecisionQualityRating.Good,
            >= 40 => DecisionQualityRating.Mixed,
            _ => DecisionQualityRating.Poor
        };
    }

    private static IReadOnlyList<string> BuildDiagnostics(
        Decision decision,
        IReadOnlyList<DecisionQualitySignal> signals,
        IReadOnlyList<HumanAuthoringBurdenSignal> burdenSignals)
    {
        return
        [
            $"Quality assessment is observational and did not mutate decision {decision.Id.Value}.",
            $"Extracted {signals.Count} quality signal(s).",
            $"Extracted {burdenSignals.Count} human-authoring burden signal(s)."
        ];
    }
}
