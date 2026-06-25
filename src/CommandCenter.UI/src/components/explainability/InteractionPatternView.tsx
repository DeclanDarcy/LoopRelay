import { EmptyState } from '../design'
import type { ExplanationAction, ExplanationDiagnostic, ExplanationEvidence } from '../../types'
import { ActionEligibilityView } from './ActionEligibilityView'
import { DiagnosticList } from './DiagnosticList'
import { EvidenceList } from './EvidenceList'

type InteractionPatternViewProps = {
  actions: ExplanationAction[]
  diagnostics?: ExplanationDiagnostic[]
  evidence?: ExplanationEvidence[]
  result?: string | null
  subject: string
  title?: string
}

export function InteractionPatternView({
  actions,
  diagnostics = [],
  evidence = [],
  result = null,
  subject,
  title = 'Interaction Summary',
}: InteractionPatternViewProps) {
  return (
    <div className="interaction-pattern-view" aria-label={title}>
      <div className="interaction-pattern-summary">
        <div>
          <span>Action subject</span>
          <strong>{subject}</strong>
        </div>
        <div>
          <span>Result</span>
          <strong>{result ?? 'No command result recorded.'}</strong>
        </div>
      </div>
      <ActionEligibilityView
        actions={actions}
        emptyLabel="Lifecycle eligibility has not projected any actions."
        title="Action Eligibility"
      />
      <EvidenceList
        evidence={evidence}
        emptyLabel="No interaction evidence projected."
        title="Interaction Evidence"
      />
      <DiagnosticList
        diagnostics={diagnostics}
        emptyLabel="No interaction diagnostics projected."
        title="Interaction Diagnostics"
      />
      {actions.length === 0 && evidence.length === 0 && diagnostics.length === 0 && !result ? (
        <EmptyState className="empty-state">Interaction projection is waiting for backend data.</EmptyState>
      ) : null}
    </div>
  )
}
