import { EmptyState } from '../../components/design'
import type { DecisionRecommendation } from '../../types'
import { DecisionEvidenceBlock } from './DecisionEvidenceFragments'

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
      <DecisionFactList title="Supporting factors" facts={recommendation.supportingFactors ?? []} />
      <DecisionFactList title="Concerns" facts={recommendation.concerns ?? []} />
      <DecisionFactList title="Recommendation assumptions" facts={recommendation.assumptions ?? []} />
      <DecisionFactList title="Alternative explanations" facts={recommendation.alternativeExplanations ?? []} />
      {recommendation.recommendationEvidence?.length ? (
        <div className="decision-inspection-list" aria-label="Recommendation evidence categories">
          <h6>Recommendation Evidence Categories</h6>
          {recommendation.recommendationEvidence.map((item) => (
            <article className="decision-tradeoff" key={`${item.type}-${item.optionId}-${item.summary}`}>
              <div>
                <span>{item.type}</span>
                <strong>{item.optionId}</strong>
              </div>
              <p>{item.summary}</p>
              <DecisionEvidenceBlock title={`${item.type} Evidence`} evidence={item.evidence} />
            </article>
          ))}
        </div>
      ) : null}
      <DecisionEvidenceBlock title="Recommendation Evidence" evidence={recommendation.evidence} />
    </article>
  )
}

function DecisionFactList({ title, facts }: { title: string; facts: string[] }) {
  if (facts.length === 0) {
    return null
  }

  return (
    <div className="decision-warning-list" aria-label={title}>
      {facts.map((fact) => (
        <span key={fact}>{fact}</span>
      ))}
    </div>
  )
}
