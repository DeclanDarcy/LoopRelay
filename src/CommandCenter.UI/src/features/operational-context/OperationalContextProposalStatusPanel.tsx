import { formatDateTime } from '../../lib'
import { EmptyState, StatusBadge } from '../../components/design'
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
        <span>Generated: {formatDateTime(proposal.generatedAt)}</span>
        <span>Generated path: {proposal.generatedContentRelativePath}</span>
        <span>Edited path: {proposal.editedContentRelativePath ?? 'None'}</span>
        <span>
          Review: <StatusBadge status={operationalContextReviewStatus[proposal.review.reviewState]} />
        </span>
        <span>Reviewed: {formatDateTime(proposal.review.reviewedAt)}</span>
        <span>Promoted: {formatDateTime(proposal.promotion.promotedAt)}</span>
        <span>Promotion source: {proposal.promotion.promotedContentSourceRelativePath ?? 'None'}</span>
        <span>Revision: {proposal.promotion.revisionNumber ?? 'None'}</span>
        <span>Archived: {proposal.promotion.archivedRelativePath ?? 'None'}</span>
      </div>
      {proposal.review.staleReason ? (
        <EmptyState className="empty-state">Review blocked: {proposal.review.staleReason}</EmptyState>
      ) : null}
      {proposal.promotion.archiveFailureReason ? (
        <EmptyState className="empty-state">
          Promotion archive failed: {proposal.promotion.archiveFailureReason}
        </EmptyState>
      ) : null}
      {proposal.promotion.writeFailureReason ? (
        <EmptyState className="empty-state">
          Promotion write failed: {proposal.promotion.writeFailureReason}
        </EmptyState>
      ) : null}
    </>
  )
}
