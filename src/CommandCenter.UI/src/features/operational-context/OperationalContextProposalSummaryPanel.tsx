import { formatDateTime } from '../../lib'
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
    return <p className="empty-state">No operational-context proposal has been generated.</p>
  }

  return (
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
  )
}
