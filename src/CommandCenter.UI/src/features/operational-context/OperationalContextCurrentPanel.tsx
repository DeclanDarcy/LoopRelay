import { formatDateTime } from '../../lib'
import type { OperationalContextProjection, OperationalContextProposalSummary } from '../../types'

type OperationalContextCurrentPanelProps = {
  operationalContext: OperationalContextProjection
  proposalSummary: OperationalContextProposalSummary
  executionStatus: string
  reviewStatus: string
}

type OperationalContextItemListProps = {
  title: string
  emptyLabel: string
  items: { id: string; text: string }[]
}

function OperationalContextItemList({
  title,
  emptyLabel,
  items,
}: OperationalContextItemListProps) {
  return (
    <div>
      <h5>{title}</h5>
      {items.length > 0 ? (
        <ul>
          {items.map((item) => (
            <li key={item.id}>{item.text}</li>
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
        <p className="empty-state">No current operational context exists.</p>
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
        />
        <OperationalContextItemList
          title="Decision Rationale"
          emptyLabel="No decision rationale recorded."
          items={operationalContext.decisionRationale}
        />
        <OperationalContextItemList
          title="Architecture"
          emptyLabel="No architecture items recorded."
          items={operationalContext.architecture}
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
        />
        <OperationalContextItemList
          title="Open Questions"
          emptyLabel="No open questions recorded."
          items={operationalContext.openQuestions}
        />
        <OperationalContextItemList
          title="Active Risks"
          emptyLabel="No active risks recorded."
          items={operationalContext.activeRisks}
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
                <li key={warning}>{warning}</li>
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
