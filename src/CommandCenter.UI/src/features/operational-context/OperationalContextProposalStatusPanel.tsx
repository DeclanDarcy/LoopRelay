import { formatDateTime } from '../../lib'
import { EmptyState, StatusBadge } from '../../components/design'
import {
  operationalContextProposalStatus,
  operationalContextReviewStatus,
} from '../../lib/status'
import type { OperationalContextProposal } from '../../types'

type OperationalContextProposalStatusPanelProps = {
  proposal: OperationalContextProposal
  isArtifactPathAvailable?: (relativePath: string | null) => boolean
  onOpenArtifact?: (relativePath: string) => void
}

export function OperationalContextProposalStatusPanel({
  proposal,
  isArtifactPathAvailable,
  onOpenArtifact,
}: OperationalContextProposalStatusPanelProps) {
  return (
    <>
      <div className="context-summary-grid">
        <span>Proposal: {proposal.proposalId}</span>
        <span>
          Status: <StatusBadge status={operationalContextProposalStatus[proposal.status]} />
        </span>
        <span>Generated: {formatDateTime(proposal.generatedAt)}</span>
        <PathSummary
          label="Generated path"
          path={proposal.generatedContentRelativePath}
          isArtifactPathAvailable={isArtifactPathAvailable}
          onOpenArtifact={onOpenArtifact}
        />
        <PathSummary
          label="Edited path"
          path={proposal.editedContentRelativePath}
          isArtifactPathAvailable={isArtifactPathAvailable}
          onOpenArtifact={onOpenArtifact}
        />
        <span>
          Review: <StatusBadge status={operationalContextReviewStatus[proposal.review.reviewState]} />
        </span>
        <span>Reviewed: {formatDateTime(proposal.review.reviewedAt)}</span>
        <span>Promoted: {formatDateTime(proposal.promotion.promotedAt)}</span>
        <PathSummary
          label="Promotion source"
          path={proposal.promotion.promotedContentSourceRelativePath}
          isArtifactPathAvailable={isArtifactPathAvailable}
          onOpenArtifact={onOpenArtifact}
        />
        <span>Revision: {proposal.promotion.revisionNumber ?? 'None'}</span>
        <PathSummary
          label="Archived"
          path={proposal.promotion.archivedRelativePath}
          isArtifactPathAvailable={isArtifactPathAvailable}
          onOpenArtifact={onOpenArtifact}
        />
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

type PathSummaryProps = {
  label: string
  path: string | null
  isArtifactPathAvailable?: (relativePath: string | null) => boolean
  onOpenArtifact?: (relativePath: string) => void
}

function PathSummary({
  label,
  path,
  isArtifactPathAvailable,
  onOpenArtifact,
}: PathSummaryProps) {
  const canOpenPath = Boolean(path && onOpenArtifact && isArtifactPathAvailable?.(path))
  const openArtifact = onOpenArtifact

  return (
    <span>
      {label}:{' '}
      {path && canOpenPath ? (
        <button
          type="button"
          className="workspace-cross-link inline-cross-link"
          onClick={() => openArtifact?.(path)}
        >
          {path}
        </button>
      ) : (
        path ?? 'None'
      )}
    </span>
  )
}
