import { EmptyState, Panel, SectionHeader } from '../../components/design'
import { DiagnosticList, EvidenceList, UncertaintyView } from '../../components/explainability'
import { formatDateTime } from '../../lib'
import {
  decisionDiagnosticsToExplanation,
  decisionInfluenceMissingStatementUncertainty,
  decisionInfluenceStatementAdherenceToDiagnostics,
  decisionInfluenceStatementsToEvidence,
} from '../../lib/explainability'
import type { DecisionInfluenceStatement, DecisionInfluenceTrace } from '../../types'
import { DecisionInfluenceExplorer } from '../decisions/DecisionInfluenceExplorer'

type ExecutionDecisionInfluencePanelProps = {
  trace: DecisionInfluenceTrace | null
  isLoading?: boolean
  error?: string | null
}

const statementGroups = [
  { type: 'Constraint', label: 'Projected Constraints' },
  { type: 'Directive', label: 'Projected Directives' },
  { type: 'Priority', label: 'Projected Priorities' },
  { type: 'ArchitectureRule', label: 'Architecture Rules' },
]

export function ExecutionDecisionInfluencePanel({
  trace,
  isLoading = false,
  error = null,
}: ExecutionDecisionInfluencePanelProps) {
  const decisionIds = trace
    ? Array.from(new Set(trace.statements.map((statement) => statement.decisionId))).sort((left, right) =>
        left.localeCompare(right),
      )
    : []

  return (
    <Panel className="execution-decision-influence-panel" aria-label="Decision influence">
      <SectionHeader
        eyebrow="Decision Influence"
        title={trace ? `${decisionIds.length} influencing decisions` : 'No trace loaded'}
        headingLevel={4}
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
          </div>

          <div className="execution-influence-section">
            <h5>Influencing Decisions</h5>
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

          {statementGroups.map((group) => (
            <InfluenceStatementGroup
              key={group.type}
              title={group.label}
              statements={trace.statements.filter((statement) => statement.statementType === group.type)}
            />
          ))}

          <DecisionInfluenceExplorer
            includedDecisions={trace.includedDecisions}
            excludedDecisions={trace.excludedDecisions}
            supersededDecisions={trace.supersededDecisions}
            conflictingDecisions={trace.conflictingDecisions}
            ignoredDecisions={trace.ignoredDecisions}
            blockedDecisions={trace.blockedDecisions}
          />

          {trace.diagnostics.length > 0 ? (
            <div className="execution-influence-section">
              <DiagnosticList
                title="Diagnostics"
                diagnostics={decisionDiagnosticsToExplanation(trace.diagnostics, 'Decision Influence')}
              />
            </div>
          ) : null}
        </div>
      ) : null}
    </Panel>
  )
}

function InfluenceStatementGroup({
  title,
  statements,
}: {
  title: string
  statements: DecisionInfluenceStatement[]
}) {
  return (
    <div className="execution-influence-section">
      <h5>{title}</h5>
      {statements.length > 0 ? (
        <>
          <EvidenceList
            title={`${title} Evidence`}
            evidence={decisionInfluenceStatementsToEvidence(statements, title)}
          />
          <DiagnosticList
            title={`${title} Adherence`}
            diagnostics={decisionInfluenceStatementAdherenceToDiagnostics(statements)}
          />
        </>
      ) : (
        <UncertaintyView
          title={`${title} Uncertainty`}
          uncertainty={decisionInfluenceMissingStatementUncertainty(statements, title)}
        />
      )}
    </div>
  )
}
