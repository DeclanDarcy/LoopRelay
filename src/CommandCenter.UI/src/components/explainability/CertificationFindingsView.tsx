import { Badge, EmptyState } from '../design'
import type { ExplanationDiagnostic, ExplanationEvidence } from '../../types'
import { DiagnosticList } from './DiagnosticList'
import { EvidenceList } from './EvidenceList'

export type CertificationFindingExplanation = {
  id: string
  title: string
  category?: string | null
  passed: boolean
  detail: string
  evidence: ExplanationEvidence[]
  diagnostics: ExplanationDiagnostic[]
}

type CertificationFindingsViewProps = {
  findings: CertificationFindingExplanation[]
  emptyLabel?: string
  title?: string
}

export function CertificationFindingsView({
  findings,
  emptyLabel = 'No certification findings projected.',
  title = 'Findings',
}: CertificationFindingsViewProps) {
  return (
    <div className="explainability-list explainability-certification-findings">
      <h5>{title}</h5>
      {findings.length === 0 ? (
        <EmptyState className="empty-state">{emptyLabel}</EmptyState>
      ) : (
        <ul>
          {findings.map((finding) => (
            <li key={finding.id}>
              <div className="explainability-row-header">
                <strong>{finding.title}</strong>
                <Badge tone={finding.passed ? 'success' : 'danger'}>{finding.passed ? 'Passed' : 'Failed'}</Badge>
              </div>
              {finding.category ? <small>Category: {finding.category}</small> : null}
              <span>{finding.detail}</span>
              <EvidenceList evidence={finding.evidence} emptyLabel="No finding evidence projected." />
              <DiagnosticList diagnostics={finding.diagnostics} emptyLabel="No finding diagnostics projected." />
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
