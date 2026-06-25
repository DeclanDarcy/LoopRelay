import { Button, EmptyState, Panel, SectionHeader } from '../../components/design'
import { formatDateTime } from '../../lib'
import type { DecisionInfluenceTrace } from '../../types'

type ExecutionDecisionInfluencePanelProps = {
  trace: DecisionInfluenceTrace | null
  isLoading?: boolean
  error?: string | null
  onOpenDecisions?: () => void
}

export function ExecutionDecisionInfluencePanel({
  trace,
  isLoading = false,
  error = null,
  onOpenDecisions,
}: ExecutionDecisionInfluencePanelProps) {
  const decisionIds = trace
    ? Array.from(new Set(trace.statements.map((statement) => statement.decisionId))).sort((left, right) =>
        left.localeCompare(right),
      )
    : []
  const categoryCounts = trace
    ? [
        trace.includedDecisions.length,
        trace.excludedDecisions.length,
        trace.supersededDecisions.length,
        trace.conflictingDecisions.length,
        trace.ignoredDecisions.length,
        trace.blockedDecisions.length,
      ]
    : []
  const decisionCategoryCount = categoryCounts.filter((count) => count > 0).length

  return (
    <Panel className="execution-decision-influence-panel" aria-label="Decision influence">
      <SectionHeader
        eyebrow="Decision Influence"
        title={trace ? `${decisionIds.length} influencing decisions` : 'No trace loaded'}
        headingLevel={4}
        actions={
          onOpenDecisions ? (
            <Button
              type="button"
              variant="secondary"
              className="secondary-action"
              onClick={onOpenDecisions}
            >
              Open in Decisions
            </Button>
          ) : null
        }
      />

      {isLoading ? <EmptyState className="empty-state">Loading decision influence.</EmptyState> : null}
      {!isLoading && error ? <EmptyState className="empty-state">{error}</EmptyState> : null}
      {!isLoading && !error && !trace ? (
        <EmptyState className="empty-state">No persisted influence trace is available.</EmptyState>
      ) : null}

      {!isLoading && !error && trace ? (
        <div className="execution-decision-influence">
          <div className="execution-rail-summary">
            <span>Session: {trace.executionSessionId}</span>
            <span>Projection fingerprint: {trace.projectionFingerprint}</span>
            <span>Projected: {formatDateTime(trace.projectionGeneratedAt)}</span>
            <span>Recorded: {formatDateTime(trace.recordedAt)}</span>
            <span>Projected statements: {trace.statements.length}</span>
            <span>Decision categories with findings: {decisionCategoryCount}</span>
            <span>Diagnostics: {trace.diagnostics.length}</span>
          </div>

          <div className="execution-influence-section" aria-label="Influencing decision summary">
            {decisionIds.length > 0 ? (
              <div className="execution-influence-decision-list">
                {decisionIds.map((decisionId) => (
                  <span key={decisionId}>{decisionId}</span>
                ))}
              </div>
            ) : (
              <EmptyState className="empty-state">No influencing decisions were recorded.</EmptyState>
            )}
          </div>

          <p className="execution-rail-note">
            Execution shows the persisted trace summary for this launched session. Decision influence
            statements, projection categories, adherence, and diagnostics are inspected in Decisions.
          </p>
        </div>
      ) : null}
    </Panel>
  )
}
