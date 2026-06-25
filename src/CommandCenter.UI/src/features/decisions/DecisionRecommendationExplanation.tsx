import { EmptyState } from '../../components/design'
import { DecisionBasis } from '../../components/explainability'
import { decisionRecommendationToExplanation } from '../../lib/explainability'
import type { DecisionRecommendation } from '../../types'

export function DecisionRecommendationExplanation({
  recommendation,
}: {
  recommendation: DecisionRecommendation | null
}) {
  if (!recommendation) {
    return (
      <article className="decision-inspection-card" aria-label="Decision recommendation">
        <div>
          <span>Recommendation</span>
          <strong>No backend recommendation</strong>
        </div>
        <EmptyState className="empty-state">No recommendation explanation is attached to this proposal.</EmptyState>
      </article>
    )
  }

  return (
    <article className="decision-inspection-card decision-recommendation-explanation" aria-label="Decision recommendation">
      <div>
        <span>Recommendation</span>
        <strong>{recommendation.optionId}</strong>
      </div>
      <div className="decision-diagnostics-grid" aria-label="Recommendation metadata">
        <span>Mode {recommendation.mode ?? 'Unspecified'}</span>
        {recommendation.summary ? <span>{recommendation.summary}</span> : null}
      </div>
      <p>{recommendation.rationale}</p>
      <DecisionBasis explanation={decisionRecommendationToExplanation(recommendation)} />
    </article>
  )
}
