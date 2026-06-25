import {
  ConstraintViewer,
  DiagnosticList,
  EvidenceList,
  UncertaintyView,
} from '../../components/explainability'
import {
  decisionAssimilationConsequencesToEvidence,
  decisionAssimilationRecordToConstraints,
  decisionAssimilationRecordToDiagnostics,
  decisionAssimilationRecordToEvidence,
  decisionAssimilationRecordToUncertainty,
} from '../../lib/explainability'
import type { DecisionAssimilationProjection, DecisionAssimilationRecord } from '../../types'

type OperationalContextAssimilationPanelProps = {
  decisionAssimilation: DecisionAssimilationProjection
}

export function OperationalContextAssimilationPanel({
  decisionAssimilation,
}: OperationalContextAssimilationPanelProps) {
  if (decisionAssimilation.decisions.length === 0) {
    return null
  }

  return (
    <div className="assimilation-panel">
      <h5>Decision Assimilation</h5>
      <ul>
        {decisionAssimilation.decisions.map((decision) => (
          <AssimilationDecisionItem
            key={`${decision.decisionId}-${decision.sourceRelativePath}`}
            decision={decision}
          />
        ))}
      </ul>
    </div>
  )
}

function AssimilationDecisionItem({ decision }: { decision: DecisionAssimilationRecord }) {
  const constraints = decisionAssimilationRecordToConstraints(decision)
  const diagnostics = decisionAssimilationRecordToDiagnostics(decision)
  const consequences = decisionAssimilationConsequencesToEvidence(decision)
  const uncertainty = decisionAssimilationRecordToUncertainty(decision)

  return (
    <li>
      <div className="assimilation-header">
        <strong>{decision.status}</strong>
        <span>{decision.taxonomy}</span>
        <span>{decision.isDurable ? 'Durable' : 'Not durable'}</span>
      </div>
      <p>{decision.statement}</p>
      <dl className="assimilation-metadata">
        <div>
          <dt>Decision</dt>
          <dd>{decision.decisionId}</dd>
        </div>
        <div>
          <dt>Source</dt>
          <dd>{decision.sourceRelativePath}</dd>
        </div>
        <div>
          <dt>Qualifies</dt>
          <dd>{decision.qualifiesForAssimilation ? 'Yes' : 'No'}</dd>
        </div>
        <div>
          <dt>Operational statement</dt>
          <dd>{decision.operationalStatement ?? 'None'}</dd>
        </div>
        <div>
          <dt>Exclusion reason</dt>
          <dd>{decision.exclusionReason ?? 'None'}</dd>
        </div>
        <div>
          <dt>Omission reason</dt>
          <dd>{decision.omissionReason ?? 'None'}</dd>
        </div>
        <div>
          <dt>Rationale</dt>
          <dd>{decision.rationale ?? 'None'}</dd>
        </div>
      </dl>
      <EvidenceList
        title="Assimilation Evidence"
        evidence={decisionAssimilationRecordToEvidence(decision)}
      />
      {constraints.length > 0 ? (
        <ConstraintViewer title="Assimilation Constraints" constraints={constraints} />
      ) : null}
      {consequences.length > 0 ? (
        <EvidenceList title="Assimilation Consequences" evidence={consequences} />
      ) : null}
      {diagnostics.length > 0 ? (
        <DiagnosticList title="Assimilation Diagnostics" diagnostics={diagnostics} />
      ) : null}
      {uncertainty.length > 0 ? (
        <UncertaintyView title="Assimilation Open Questions" uncertainty={uncertainty} />
      ) : null}
    </li>
  )
}
