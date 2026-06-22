import { formatDateTime } from '../../lib'
import { EmptyState } from '../../components/design'
import type { OperationalContextProjection, OperationalContextProposalSummary } from '../../types'

type OperationalContextCurrentPanelProps = {
  operationalContext: OperationalContextProjection
  proposalSummary: OperationalContextProposalSummary
  executionStatus: string
  reviewStatus: string
  onOpenSection?: (sectionId: string) => void
  onOpenContinuityWarnings?: () => void
}

type OperationalContextItemListProps = {
  title: string
  emptyLabel: string
  items: { id: string; text: string }[]
  sectionId?: string
  onOpenSection?: (sectionId: string) => void
}

function OperationalContextItemList({
  title,
  emptyLabel,
  items,
  sectionId,
  onOpenSection,
}: OperationalContextItemListProps) {
  return (
    <div id={sectionId}>
      <h5>{title}</h5>
      {items.length > 0 ? (
        <ul>
          {items.map((item) => (
            <li key={item.id}>
              {sectionId && onOpenSection ? (
                <button
                  type="button"
                  className="workspace-cross-link inline-cross-link"
                  onClick={() => onOpenSection(sectionId)}
                >
                  {item.text}
                </button>
              ) : (
                item.text
              )}
            </li>
          ))}
        </ul>
      ) : (
        <p>{emptyLabel}</p>
      )}
    </div>
  )
}

export function OperationalContextCurrentPanel({
  operationalContext,
  proposalSummary,
  executionStatus,
  reviewStatus,
  onOpenSection,
  onOpenContinuityWarnings,
}: OperationalContextCurrentPanelProps) {
  const proposalStatus = proposalSummary.latestProposalId
    ? proposalSummary.status ?? 'Unknown'
    : 'None'

  if (!operationalContext.exists) {
    return (
      <div className="context-artifact-previews">
        <div className="context-summary">
          <span>Execution context: {executionStatus}</span>
          <span>Review: {reviewStatus}</span>
          <span>Proposal: {proposalStatus}</span>
        </div>
        <EmptyState className="empty-state">No current operational context exists.</EmptyState>
      </div>
    )
  }

  return (
    <div className="context-artifact-previews">
      <div className="context-summary">
        <span>Path: {operationalContext.currentRelativePath}</span>
        <span>Execution context: {executionStatus}</span>
        <span>Revisions: {operationalContext.revisionCount}</span>
        <span>Current revision: {operationalContext.currentRevisionNumber}</span>
        <span>Updated: {formatDateTime(operationalContext.lastUpdatedAt)}</span>
        <span>Last promoted: {formatDateTime(operationalContext.lastPromotionAt)}</span>
        <span>Questions: {operationalContext.openQuestions.length}</span>
        <span>Risks: {operationalContext.activeRisks.length}</span>
        <span>Review: {reviewStatus}</span>
        <span>Proposal: {proposalStatus}</span>
      </div>

      <div className="context-columns">
        <div>
          <h5>Current Model</h5>
          {operationalContext.currentUnderstandingSummary.length > 0 ? (
            <ul>
              {operationalContext.currentUnderstandingSummary.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          ) : (
            <p>No current model items recorded.</p>
          )}
        </div>
        <OperationalContextItemList
          title="Stable Decisions"
          emptyLabel="No stable decisions recorded."
          items={operationalContext.stableDecisions}
          sectionId="operational-stable-decisions"
          onOpenSection={onOpenSection}
        />
        <OperationalContextItemList
          title="Decision Rationale"
          emptyLabel="No decision rationale recorded."
          items={operationalContext.decisionRationale}
          sectionId="operational-decision-rationale"
          onOpenSection={onOpenSection}
        />
        <OperationalContextItemList
          title="Architecture"
          emptyLabel="No architecture items recorded."
          items={operationalContext.architecture}
          sectionId="operational-architecture"
          onOpenSection={onOpenSection}
        />
        <OperationalContextItemList
          title="Authority Boundaries"
          emptyLabel="No authority boundaries recorded."
          items={operationalContext.authorityBoundaries}
        />
        <OperationalContextItemList
          title="Constraints"
          emptyLabel="No constraints recorded."
          items={operationalContext.constraints}
          sectionId="operational-constraints"
          onOpenSection={onOpenSection}
        />
        <OperationalContextItemList
          title="Open Questions"
          emptyLabel="No open questions recorded."
          items={operationalContext.openQuestions}
          sectionId="operational-open-questions"
          onOpenSection={onOpenSection}
        />
        <OperationalContextItemList
          title="Active Risks"
          emptyLabel="No active risks recorded."
          items={operationalContext.activeRisks}
          sectionId="operational-active-risks"
          onOpenSection={onOpenSection}
        />
        <OperationalContextItemList
          title="Recent Changes"
          emptyLabel="No recent understanding changes recorded."
          items={operationalContext.recentUnderstandingChanges}
        />
        <div>
          <h5>Continuity Warnings</h5>
          {operationalContext.continuityWarnings.length > 0 ? (
            <ul>
              {operationalContext.continuityWarnings.map((warning) => (
                <li key={warning}>
                  {onOpenContinuityWarnings ? (
                    <button
                      type="button"
                      className="workspace-cross-link inline-cross-link warning-link"
                      onClick={onOpenContinuityWarnings}
                    >
                      {warning}
                    </button>
                  ) : (
                    warning
                  )}
                </li>
              ))}
            </ul>
          ) : (
            <p>No continuity warnings recorded.</p>
          )}
        </div>
      </div>
    </div>
  )
}
