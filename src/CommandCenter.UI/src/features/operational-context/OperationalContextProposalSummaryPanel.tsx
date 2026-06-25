import { formatDateTime } from '../../lib'
import { EmptyState } from '../../components/design'
import { EvidenceList } from '../../components/explainability'
import { operationalContextProposalSummaryToEvidence } from '../../lib/explainability'
import type { OperationalContextProjection, OperationalContextProposalSummary } from '../../types'

type OperationalContextProposalSummaryPanelProps = {
  operationalContext: OperationalContextProjection
  proposalSummary: OperationalContextProposalSummary
}

export function OperationalContextProposalSummaryPanel({
  operationalContext,
  proposalSummary,
}: OperationalContextProposalSummaryPanelProps) {
  if (!proposalSummary.latestProposalId) {
    return (
      <EmptyState className="empty-state">
        No operational-context proposal has been generated.
      </EmptyState>
    )
  }

  return (
    <>
      <div className="context-summary-grid">
        <span>Latest: {proposalSummary.latestProposalId}</span>
        <span>Status: {proposalSummary.status ?? 'Unknown'}</span>
        <span>Generated: {formatDateTime(proposalSummary.generatedAt)}</span>
        <span>Inputs: {proposalSummary.sourceInputCount}</span>
        <span>Size: {proposalSummary.contentByteCount} bytes</span>
        <span>Current revisions: {operationalContext.revisionCount}</span>
        <span>Last promoted: {formatDateTime(proposalSummary.lastPromotedAt)}</span>
        <span>Archived prior: {proposalSummary.lastArchivedRelativePath ?? 'None'}</span>
      </div>
      <EvidenceList
        title="Proposal Summary Evidence"
        evidence={operationalContextProposalSummaryToEvidence(
          proposalSummary,
          operationalContext.revisionCount,
        )}
      />
    </>
  )
}
