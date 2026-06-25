import {
  DiagnosticList,
  EvidenceList,
} from '../../components/explainability'
import {
  decisionTaxonomyBasisToDiagnostics,
  decisionTaxonomyBasisToEvidence,
} from '../../lib/explainability'
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
            <EvidenceList
              title="Taxonomy Evidence"
              evidence={decisionTaxonomyBasisToEvidence(decision.decisionId, decision.taxonomyBasis)}
            />
            <DiagnosticList
              title="Taxonomy Diagnostics"
              diagnostics={decisionTaxonomyBasisToDiagnostics(decision.taxonomyBasis)}
              emptyLabel="No taxonomy diagnostics projected."
            />
          </li>
        ))}
      </ul>
    </div>
  )
}
