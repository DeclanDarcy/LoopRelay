import type { DecisionQualityAssessment, DecisionQualityExplanation as QualityExplanation } from '../../types'

export function DecisionQualityExplanation({
  assessment,
}: {
  assessment: DecisionQualityAssessment
}) {
  const explanation = assessment.qualityExplanation

  if (!explanation) {
    return (
      <article className="decision-inspection-card" aria-label={`Quality explanation for ${assessment.decisionId}`}>
        <div>
          <span>{assessment.decisionId}</span>
          <strong>No quality explanation is projected.</strong>
        </div>
        <p>Assessment score: {assessment.score}</p>
      </article>
    )
  }

  return (
    <article className="decision-inspection-card" aria-label={`Quality explanation for ${assessment.decisionId}`}>
      <div>
        <span>{assessment.decisionId}</span>
        <strong>
          {assessment.rating} | score {assessment.score}
        </strong>
      </div>
      <div className="decision-badge-row" aria-label="Quality score basis">
        <span>Base score: {explanation.baseScore}</span>
        <span>Raw score: {explanation.rawScore}</span>
        <span>Clamped score: {explanation.clampedScore}</span>
        <span>{formatThreshold(explanation)}</span>
      </div>
      <p>{explanation.threshold.reason}</p>
      {explanation.overrideReason ? <p>Override: {explanation.overrideReason}</p> : null}
      {explanation.signalContributions.length > 0 ? (
        <div className="decision-inspection-list" aria-label={`Quality signal contributions for ${assessment.decisionId}`}>
          <h6>Signal Contributions</h6>
          {explanation.signalContributions.map((contribution) => (
            <article className="decision-quality-signal" key={`${assessment.id}-${contribution.signalId}`}>
              <div>
                <span>
                  {contribution.category} / {contribution.direction} / {contribution.severity}
                </span>
                <strong>
                  {formatSignedNumber(contribution.scoreContribution)} score | {contribution.signalId}
                </strong>
              </div>
              <p>{contribution.summary}</p>
            </article>
          ))}
        </div>
      ) : null}
      {explanation.diagnostics.length > 0 ? (
        <div className="decision-warning-list" aria-label={`Quality explanation diagnostics for ${assessment.decisionId}`}>
          {explanation.diagnostics.map((diagnostic) => (
            <span key={`${assessment.id}-${diagnostic}`}>{diagnostic}</span>
          ))}
        </div>
      ) : null}
    </article>
  )
}

function formatThreshold(explanation: QualityExplanation) {
  const minimum = explanation.threshold.minimumScore ?? 'none'
  const maximum = explanation.threshold.maximumScore ?? 'none'
  return `Threshold: ${explanation.threshold.rating} (${minimum}-${maximum})`
}

function formatSignedNumber(value: number) {
  return value > 0 ? `+${value}` : `${value}`
}
