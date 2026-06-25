import { Panel, SectionHeader } from '../../components/design'
import { ActionEligibilityView, EvidenceList } from '../../components/explainability'
import { formatDateTime, formatDuration } from '../../lib'
import {
  executionSessionSummaryToEvidence,
  generatedHandoffReviewToActions,
} from '../../lib/explainability'
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
        <ActionEligibilityView
          actions={generatedHandoffReviewToActions(execution, isDecisionPending)}
          title="Handoff Review Actions"
        />
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
      <EvidenceList
        evidence={executionSessionSummaryToEvidence(execution)}
        title="Generated Handoff Evidence"
      />
      <GeneratedHandoffContent content={content} isLoading={isContentLoading} />
    </Panel>
  )
}
