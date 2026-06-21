import { formatDateTime } from '../../lib'
import { StatusBadge } from '../../components/design'
import {
  operationalContextProposalStatus,
  operationalContextReviewStatus,
} from '../../lib/status'
import type { OperationalContextProposal } from '../../types'

type OperationalContextProposalStatusPanelProps = {
  proposal: OperationalContextProposal
}

export function OperationalContextProposalStatusPanel({
  proposal,
}: OperationalContextProposalStatusPanelProps) {
  return (
    <>
      <div className="context-summary-grid">
        <span>Proposal: {proposal.proposalId}</span>
        <span>
          Status: <StatusBadge status={operationalContextProposalStatus[proposal.status]} />
        </span>
        <span>
          Review: <StatusBadge status={operationalContextReviewStatus[proposal.review.reviewState]} />
        </span>
        <span>Reviewed: {formatDateTime(proposal.review.reviewedAt)}</span>
        <span>Promoted: {formatDateTime(proposal.promotion.promotedAt)}</span>
        <span>Archived: {proposal.promotion.archivedRelativePath ?? 'None'}</span>
      </div>
      {proposal.review.staleReason ? (
        <p className="empty-state">Review blocked: {proposal.review.staleReason}</p>
      ) : null}
      {proposal.promotion.archiveFailureReason ? (
        <p className="empty-state">
          Promotion archive failed: {proposal.promotion.archiveFailureReason}
        </p>
      ) : null}
      {proposal.promotion.writeFailureReason ? (
        <p className="empty-state">Promotion write failed: {proposal.promotion.writeFailureReason}</p>
      ) : null}
    </>
  )
}
