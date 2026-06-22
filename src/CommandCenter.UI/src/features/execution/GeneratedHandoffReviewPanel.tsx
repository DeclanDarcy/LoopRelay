import { Panel, SectionHeader } from '../../components/design'
import { formatDateTime, formatDuration } from '../../lib'
import { repositoryExecutionStatus } from '../../lib/status'
import type { ExecutionSessionSummary } from '../../types'
import { GeneratedHandoffContent } from './GeneratedHandoffContent'

type GeneratedHandoffReviewPanelProps = {
  canReview: boolean
  execution: ExecutionSessionSummary | null
  content: string
  isContentLoading: boolean
  isDecisionPending: boolean
  isAccepting: boolean
  isRejecting: boolean
  onAccept: () => void
  onReject: () => void
}

export function GeneratedHandoffReviewPanel({
  canReview,
  execution,
  content,
  isContentLoading,
  isDecisionPending,
  isAccepting,
  isRejecting,
  onAccept,
  onReject,
}: GeneratedHandoffReviewPanelProps) {
  if (!canReview || !execution) {
    return null
  }

  return (
    <Panel
      id="generated-handoff-review"
      className="handoff-review-panel"
      aria-label="Generated handoff review"
    >
      <div className="handoff-review-header">
        <SectionHeader
          eyebrow="Handoff Review"
          title={execution.handoffPath ?? 'Generated handoff'}
          headingLevel={4}
        />
        <div className="handoff-review-metadata">
          <span>State: {repositoryExecutionStatus[execution.repositoryState].label}</span>
          <span>Completed: {formatDateTime(execution.completedAt)}</span>
          <span>Duration: {formatDuration(execution.duration)}</span>
          <span>Decision: Awaiting review</span>
          <span>{content.length} characters</span>
        </div>
      </div>
      <div className="handoff-review-actions">
        <button
          type="button"
          className="primary-action"
          onClick={onAccept}
          disabled={!isDecisionPending}
        >
          {isAccepting ? 'Accepting...' : 'Accept Handoff'}
        </button>
        <button
          type="button"
          className="danger-action"
          onClick={onReject}
          disabled={!isDecisionPending}
        >
          {isRejecting ? 'Rejecting...' : 'Reject Handoff'}
        </button>
      </div>
      <GeneratedHandoffContent content={content} isLoading={isContentLoading} />
    </Panel>
  )
}
