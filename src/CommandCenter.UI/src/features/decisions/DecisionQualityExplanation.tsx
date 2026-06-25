import { ConstraintViewer, DiagnosticList } from '../../components/explainability'
import {
  decisionQualityAssessmentToExplanation,
} from '../../lib/explainability'
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
      <ConstraintViewer
        title="Quality Score Basis"
        constraints={decisionQualityAssessmentToExplanation(assessment).constraints ?? []}
      />
      <DiagnosticList
        title="Signal Contributions"
        diagnostics={decisionQualityAssessmentToExplanation(assessment).diagnostics ?? []}
      />
    </article>
  )
}

function formatThreshold(explanation: QualityExplanation) {
  const minimum = explanation.threshold.minimumScore ?? 'none'
  const maximum = explanation.threshold.maximumScore ?? 'none'
  return `Threshold: ${explanation.threshold.rating} (${minimum}-${maximum})`
}
