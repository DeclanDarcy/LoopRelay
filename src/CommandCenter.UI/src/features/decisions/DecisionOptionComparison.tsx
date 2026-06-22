import { Badge, EmptyState } from '../../components/design'
import type { DecisionEvidence, DecisionOptionComparison as DecisionOptionComparisonModel } from '../../types'

type DecisionOptionComparisonProps = {
  comparison: DecisionOptionComparisonModel | null
  isLoading: boolean
}

export function DecisionOptionComparison({ comparison, isLoading }: DecisionOptionComparisonProps) {
  if (!comparison) {
    return (
      <section className="decision-lifecycle-panel decision-option-comparison" aria-label="Option comparison">
        <h5>Option Comparison</h5>
        <EmptyState className="empty-state">
          {isLoading ? 'Loading option comparison...' : 'Select a proposal to compare options.'}
        </EmptyState>
      </section>
    )
  }

  return (
    <section className="decision-lifecycle-panel decision-option-comparison" aria-label="Option comparison">
      <div className="decision-panel-heading">
        <h5>Option Comparison</h5>
        <span>{comparison.proposalId}</span>
      </div>

      <div className="decision-option-grid" aria-label="Option comparison rows">
        {comparison.options.map((option) => (
          <article className="decision-inspection-card" key={option.optionId}>
            <div>
              <span>{option.optionId}</span>
              <strong>{option.title}</strong>
            </div>
            <p>{option.description}</p>
            {option.isRecommended ? (
              <div className="decision-badge-row">
                <Badge tone="done">Recommended</Badge>
              </div>
            ) : null}
            <DecisionList title="Benefits" items={option.benefits} />
            <DecisionList title="Costs" items={option.costs} />
            <EvidenceSummaries title="Comparison Evidence" evidence={option.evidence} />
          </article>
        ))}
      </div>
    </section>
  )
}

function DecisionList({ title, items }: { title: string; items: string[] }) {
  if (items.length === 0) {
    return null
  }

  return (
    <div className="decision-inspection-list" aria-label={title}>
      <h6>{title}</h6>
      <ul className="decision-source-list">
        {items.map((item) => (
          <li key={item}>
            <span>{item}</span>
          </li>
        ))}
      </ul>
    </div>
  )
}

function EvidenceSummaries({ title, evidence }: { title: string; evidence: DecisionEvidence[] }) {
  if (evidence.length === 0) {
    return null
  }

  return (
    <div className="decision-evidence-block" aria-label={title}>
      <span>{title}</span>
      {evidence.map((item) => (
        <article key={`${title}-${item.summary}`}>
          <p>{item.summary}</p>
        </article>
      ))}
    </div>
  )
}
