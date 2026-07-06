using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Services;

public sealed class DecisionQualityAssessmentService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository,
    IDecisionQualitySignalService signalService,
    IHumanAuthoringBurdenService humanAuthoringBurdenService,
    IDecisionArtifactProjectionService projectionService) : IDecisionQualityAssessmentService
{
    public async Task<DecisionQualityAssessment> AssessDecisionAsync(Guid repositoryId, string decisionId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        Decision decision = await GetDecisionAsync(repository, decisionId);
        IReadOnlyList<DecisionQualitySignal> signals = await signalService.ExtractSignalsAsync(repositoryId, decisionId);
        IReadOnlyList<HumanAuthoringBurdenSignal> burdenSignals =
            await humanAuthoringBurdenService.ExtractSignalsAsync(repositoryId, decisionId);
        IReadOnlyList<DecisionQualitySignalContribution> signalContributions = signals
            .Select(signal => new DecisionQualitySignalContribution(
                signal.Id,
                signal.Category,
                signal.Direction,
                signal.Severity,
                ScoreContribution(signal),
                signal.Summary))
            .ToArray();
        int rawScore = 50 + signalContributions.Sum(contribution => contribution.ScoreContribution);
        int score = Math.Clamp(rawScore, 0, 100);
        string? overrideReason = QualityOverrideReason(signals);
        DecisionQualityRating rating = RatingFor(score, signals);
        DecisionQualityExplanation qualityExplanation = BuildQualityExplanation(
            score,
            rawScore,
            rating,
            overrideReason,
            signalContributions);
        HumanAuthoringBurdenExplanation burdenExplanation = BuildBurdenExplanation(decision.Id.Value, burdenSignals);
        DateTimeOffset assessedAt = DateTimeOffset.UtcNow;
        return new DecisionQualityAssessment(
            $"assessment.{assessedAt:yyyyMMddHHmmssfffffff}",
            repository.Id,
            decision.Id.Value,
            assessedAt,
            rating,
            score,
            signals,
            burdenSignals,
            BuildDiagnostics(decision, signals, burdenSignals),
            qualityExplanation,
            burdenExplanation);
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

    public async Task<DecisionQualityAssessment> AssessAndSaveDecisionAsync(Guid repositoryId, string decisionId)
    {
        DecisionQualityAssessment assessment = await AssessDecisionAsync(repositoryId, decisionId);
        return await SaveAssessmentAsync(repositoryId, assessment);
    }

    public async Task<IReadOnlyList<DecisionQualityAssessment>> AssessAndSaveRepositoryAsync(Guid repositoryId)
    {
        IReadOnlyList<DecisionQualityAssessment> assessments = await AssessRepositoryAsync(repositoryId);
        var savedAssessments = new List<DecisionQualityAssessment>();
        foreach (DecisionQualityAssessment assessment in assessments.OrderBy(assessment => assessment.DecisionId, StringComparer.Ordinal))
        {
            savedAssessments.Add(await SaveAssessmentAsync(repositoryId, assessment));
        }

        return savedAssessments;
    }

    public async Task<IReadOnlyList<DecisionQualityAssessment>> ListAssessmentsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await decisionRepository.ListQualityAssessmentsAsync(repository);
    }

    public async Task<DecisionQualityAssessment> SaveAssessmentAsync(Guid repositoryId, DecisionQualityAssessment assessment)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        if (assessment.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException(
                $"Quality assessment {assessment.Id} belongs to repository {assessment.RepositoryId}, not {repository.Id}.");
        }

        DecisionQualityAssessment saved = await decisionRepository.SaveQualityAssessmentAsync(repository, assessment);
        await projectionService.ProjectQualityAssessmentAsync(repository, saved);
        return saved;
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
        if (QualityOverrideReason(signals) is not null)
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

    private static string? QualityOverrideReason(IReadOnlyList<DecisionQualitySignal> signals)
    {
        return signals.Any(signal => signal.Direction == QualitySignalDirection.Negative && signal.Severity == QualitySignalSeverity.Critical)
            ? "Critical negative quality signal forces Poor rating regardless of score threshold."
            : null;
    }

    private static DecisionQualityExplanation BuildQualityExplanation(
        int score,
        int rawScore,
        DecisionQualityRating rating,
        string? overrideReason,
        IReadOnlyList<DecisionQualitySignalContribution> signalContributions)
    {
        return new DecisionQualityExplanation(
            50,
            rawScore,
            score,
            ThresholdFor(score, rating, overrideReason),
            overrideReason,
            signalContributions,
            [
                "Quality score starts at 50 and adds each signal contribution.",
                "Raw score is clamped to the inclusive 0-100 range before threshold rating is applied."
            ]);
    }

    private static DecisionQualityThresholdExplanation ThresholdFor(
        int score,
        DecisionQualityRating rating,
        string? overrideReason)
    {
        if (overrideReason is not null)
        {
            return new DecisionQualityThresholdExplanation(
                rating,
                null,
                null,
                overrideReason);
        }

        return rating switch
        {
            DecisionQualityRating.Excellent => new DecisionQualityThresholdExplanation(
                rating,
                85,
                100,
                $"Clamped score {score} crossed the Excellent threshold of 85."),
            DecisionQualityRating.Good => new DecisionQualityThresholdExplanation(
                rating,
                65,
                84,
                $"Clamped score {score} crossed the Good threshold of 65."),
            DecisionQualityRating.Mixed => new DecisionQualityThresholdExplanation(
                rating,
                40,
                64,
                $"Clamped score {score} crossed the Mixed threshold of 40."),
            DecisionQualityRating.Poor => new DecisionQualityThresholdExplanation(
                rating,
                0,
                39,
                $"Clamped score {score} remained below the Mixed threshold of 40."),
            _ => new DecisionQualityThresholdExplanation(
                rating,
                null,
                null,
                $"Clamped score {score} did not match a known quality threshold.")
        };
    }

    private static HumanAuthoringBurdenExplanation BuildBurdenExplanation(
        string decisionId,
        IReadOnlyList<HumanAuthoringBurdenSignal> burdenSignals)
    {
        HumanAuthoringBurdenSignal? winningSignal = burdenSignals
            .OrderByDescending(signal => BurdenWeight(signal.Burden))
            .ThenBy(signal => signal.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        HumanAuthoringBurden effectiveBurden = winningSignal?.Burden ?? HumanAuthoringBurden.Unknown;
        return new HumanAuthoringBurdenExplanation(
            decisionId,
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
