import type { DecisionAssimilationProjection } from '../../types'

type OperationalContextTaxonomyPanelProps = {
  decisionAssimilation: DecisionAssimilationProjection
}

export function OperationalContextTaxonomyPanel({
  decisionAssimilation,
}: OperationalContextTaxonomyPanelProps) {
  const decisions = decisionAssimilation.decisions.filter(
    (decision) =>
      decision.taxonomyBasis.matchedRules.length > 0 ||
      decision.taxonomyBasis.matchedEvidence.length > 0 ||
      decision.taxonomyBasis.diagnostics.length > 0 ||
      decision.taxonomyBasis.isHeuristicFallback,
  )

  if (decisions.length === 0) {
    return null
  }

  return (
    <div className="assimilation-panel">
      <h5>Taxonomy Basis</h5>
      <ul>
        {decisions.map((decision) => (
          <li key={`${decision.decisionId}-${decision.taxonomyBasis.taxonomy}`}>
            <div className="assimilation-header">
              <strong>{decision.taxonomyBasis.taxonomy}</strong>
              <span>{decision.decisionId}</span>
              <span>{decision.taxonomyBasis.isHeuristicFallback ? 'Heuristic fallback' : 'Rule matched'}</span>
            </div>
            <p>{decision.statement}</p>
            {decision.taxonomyBasis.fallbackReason ? (
              <p>{decision.taxonomyBasis.fallbackReason}</p>
            ) : null}
            <TaxonomyList title="Matched rules" items={decision.taxonomyBasis.matchedRules} />
            <TaxonomyList title="Matched evidence" items={decision.taxonomyBasis.matchedEvidence} />
            <TaxonomyList title="Diagnostics" items={decision.taxonomyBasis.diagnostics} />
          </li>
        ))}
      </ul>
    </div>
  )
}

type TaxonomyListProps = {
  title: string
  items: string[]
}

function TaxonomyList({ title, items }: TaxonomyListProps) {
  if (items.length === 0) {
    return null
  }

  return (
    <>
      <h6>{title}</h6>
      <ul className="assimilation-detail-list" aria-label={`Taxonomy ${title.toLowerCase()}`}>
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    </>
  )
}
