import type { ReactNode } from 'react'
import { Button, EmptyState, Panel, SectionHeader, StatusBadge } from '../../components/design'
import { formatDateTime } from '../../lib'
import { executionSessionStatus, repositoryExecutionStatus } from '../../lib/status'
import type {
  ExecutionContextPreview,
  DecisionInfluenceTrace,
  ExecutionEvent,
  ExecutionPromptManifest,
  ExecutionSessionSummary,
  ExecutionSessionTransparency,
  RepositoryExecutionState,
  WorkflowInstance,
} from '../../types'
import { ExecutionContextArtifactDiagnosticsList } from './ExecutionContextArtifactDiagnosticsList'
import { ExecutionContextMissingOptionalList } from './ExecutionContextMissingOptionalList'
import { ExecutionContextValidationList } from './ExecutionContextValidationList'
import { ExecutionDecisionInfluencePanel } from './ExecutionDecisionInfluencePanel'
import { ExecutionEventFeed } from './ExecutionEventFeed'
import { ExecutionHistoryPanel } from './ExecutionHistoryPanel'
import { ExecutionSessionPanel } from './ExecutionSessionPanel'
import { ExecutionWorkflowRail } from './ExecutionWorkflowRail'

type ExecutionTabProps = {
  execution: ExecutionSessionSummary | null
  executionPromptManifest: ExecutionPromptManifest | null
  executionTransparency: ExecutionSessionTransparency | null
  isExecutionPromptManifestLoading?: boolean
  isExecutionTransparencyLoading?: boolean
  executionPromptManifestError?: string | null
  executionTransparencyError?: string | null
  executionContext: ExecutionContextPreview | null
  decisionInfluenceTrace: DecisionInfluenceTrace | null
  isDecisionInfluenceLoading?: boolean
  decisionInfluenceError?: string | null
  executionEvents: ExecutionEvent[]
  executionHistory: ExecutionSessionSummary[]
  workflow: WorkflowInstance | null
  isWorkflowLoading?: boolean
  workflowError?: string | null
  currentExecutionState: RepositoryExecutionState
  selectedMilestonePath: string | null
  contextPanel: ReactNode
  gitWorkflow: ReactNode
  handoffReview: ReactNode
  launchReadiness: string
  onOpenWorkspaceExecutionContext: () => void
  onOpenWorkspaceWorkflow: () => void
  onOpenDecisions: () => void
  onOpenHandoffArtifact: (handoffPath: string) => void
  onOpenWorkspaceGit: () => void
}

export function ExecutionTab({
  execution,
  executionPromptManifest,
  executionTransparency,
  isExecutionPromptManifestLoading = false,
  isExecutionTransparencyLoading = false,
  executionPromptManifestError = null,
  executionTransparencyError = null,
  executionContext,
  decisionInfluenceTrace,
  isDecisionInfluenceLoading = false,
  decisionInfluenceError = null,
  executionEvents,
  executionHistory,
  workflow,
  isWorkflowLoading = false,
  workflowError = null,
  currentExecutionState,
  selectedMilestonePath,
  contextPanel,
  gitWorkflow,
  handoffReview,
  launchReadiness,
  onOpenWorkspaceExecutionContext,
  onOpenWorkspaceWorkflow,
  onOpenDecisions,
  onOpenHandoffArtifact,
  onOpenWorkspaceGit,
}: ExecutionTabProps) {
  const title = selectedMilestonePath ?? 'Select a milestone'

  return (
    <section
      id="execution-context"
      className="execution-workspace execution-tab tab-panel tab-execution"
      aria-label="Execution workspace"
    >
      <SectionHeader
        className="execution-workspace-header"
        eyebrow="Execution Workspace"
        title={title}
        headingLevel={4}
        actions={<StatusBadge status={repositoryExecutionStatus[currentExecutionState]} />}
      />

      <ExecutionWorkflowRail
        workflow={workflow}
        isLoading={isWorkflowLoading}
        error={workflowError}
        onOpenWorkflow={onOpenWorkspaceWorkflow}
      />

      <div className="execution-tab-grid">
        <div className="execution-tab-main">
          <div id="execution-events">
            <ExecutionEventFeed
              events={executionEvents}
              session={execution}
              ariaLabel="Full execution stream"
              eyebrow="Execution Stream"
            />
          </div>
          {handoffReview}
          {gitWorkflow}
          <ExecutionHistoryPanel sessions={executionHistory} />
        </div>

        <aside className="execution-tab-rail" aria-label="Execution inspector">
          {execution ? (
            <ExecutionSessionPanel
              session={execution}
              promptManifest={executionPromptManifest}
              transparency={executionTransparency}
              isPromptManifestLoading={isExecutionPromptManifestLoading}
              isTransparencyLoading={isExecutionTransparencyLoading}
              promptManifestError={executionPromptManifestError}
              transparencyError={executionTransparencyError}
              onOpenHandoff={
                execution.handoffPath
                  ? () => onOpenHandoffArtifact(execution.handoffPath as string)
                  : undefined
              }
            />
          ) : (
            <Panel className="execution-session-panel" aria-label="Execution session">
              <SectionHeader eyebrow="Execution Session" title="No session selected" headingLevel={4} />
              <EmptyState className="empty-state">No execution session is available.</EmptyState>
            </Panel>
          )}

          <ExecutionDiagnosticsPanel
            execution={execution}
            transparency={executionTransparency}
            currentExecutionState={currentExecutionState}
            onOpenWorkspaceGit={onOpenWorkspaceGit}
          />

          <ContextDiagnosticsPanel
            executionContext={executionContext}
            onOpenWorkspaceExecutionContext={onOpenWorkspaceExecutionContext}
          />

          <ExecutionDecisionInfluencePanel
            trace={decisionInfluenceTrace}
            isLoading={isDecisionInfluenceLoading}
            error={decisionInfluenceError}
            onOpenDecisions={onOpenDecisions}
          />

          <LaunchReadinessPanel
            executionContext={executionContext}
            launchReadiness={launchReadiness}
          />
        </aside>
      </div>

      <div className="execution-context-drawer">{contextPanel}</div>
    </section>
  )
}

type ExecutionDiagnosticsPanelProps = {
  execution: ExecutionSessionSummary | null
  transparency: ExecutionSessionTransparency | null
  currentExecutionState: RepositoryExecutionState
  onOpenWorkspaceGit: () => void
}

function ExecutionDiagnosticsPanel({
  execution,
  transparency,
  currentExecutionState,
  onOpenWorkspaceGit,
}: ExecutionDiagnosticsPanelProps) {
  const recentFailure = execution?.failureReason ?? null
  const monitoring = transparency?.monitoring ?? null

  return (
    <Panel className="execution-diagnostics-panel" aria-label="Execution diagnostics">
      <SectionHeader
        eyebrow="Execution Diagnostics"
        title={repositoryExecutionStatus[currentExecutionState].label}
        headingLevel={4}
        actions={
          <Button type="button" variant="secondary" className="secondary-action" onClick={onOpenWorkspaceGit}>
            Git
          </Button>
        }
      />
      <div className="execution-rail-summary">
        <span>
          Session state:{' '}
          {execution ? (
            <StatusBadge status={executionSessionStatus[execution.state]} />
          ) : (
            'Not recorded'
          )}
        </span>
        <span>Repository state: {repositoryExecutionStatus[currentExecutionState].label}</span>
        <span>Last activity: {formatDateTime(execution?.lastActivityAt ?? null)}</span>
        <span>Provider process: {monitoring?.providerProcessState ?? 'Not projected'}</span>
        <span>Monitoring warnings: {monitoring?.monitoringWarnings.length ?? 'Not projected'}</span>
        <span>Stale activity: {monitoring ? (monitoring.staleActivity ? 'Yes' : 'No') : 'Not projected'}</span>
      </div>
      {recentFailure ? (
        <div className="execution-rail-warning">
          <span>Recent failure</span>
          <strong>{recentFailure}</strong>
        </div>
      ) : null}
    </Panel>
  )
}

type ContextDiagnosticsPanelProps = {
  executionContext: ExecutionContextPreview | null
  onOpenWorkspaceExecutionContext: () => void
}

function ContextDiagnosticsPanel({
  executionContext,
  onOpenWorkspaceExecutionContext,
}: ContextDiagnosticsPanelProps) {
  return (
    <Panel className="context-diagnostics-panel" aria-label="Context diagnostics">
      <SectionHeader
        eyebrow="Context Diagnostics"
        title={executionContext?.name ?? 'Preview not built'}
        headingLevel={4}
        actions={
          <Button
            type="button"
            variant="secondary"
            className="secondary-action"
            onClick={onOpenWorkspaceExecutionContext}
            disabled={!executionContext}
          >
            Context
          </Button>
        }
      />
      {executionContext ? (
        <div className="execution-context-diagnostics">
          <div className="execution-rail-summary">
            <span>Artifacts: {executionContext.artifacts.length}</span>
            <span>Aggregate size: {executionContext.diagnostics.totalBytes} bytes</span>
            <span>Warning threshold: {executionContext.diagnostics.warningThresholdBytes} bytes</span>
            <span>Hard limit: {executionContext.diagnostics.hardLimitBytes} bytes</span>
            <span>Launch blocked: {executionContext.diagnostics.launchBlocked ? 'Yes' : 'No'}</span>
          </div>
          <div className="execution-rail-list">
            <h5>Validation Errors</h5>
            <ExecutionContextValidationList
              validationErrors={executionContext.diagnostics.validationErrors}
              governedConflicts={executionContext.diagnostics.governedConflicts}
            />
          </div>
          <div className="execution-rail-list">
            <h5>Missing Optional</h5>
            <ExecutionContextMissingOptionalList
              paths={executionContext.diagnostics.missingOptionalArtifacts}
            />
          </div>
          <div className="execution-rail-list">
            <h5>Per-Artifact Diagnostics</h5>
            <ExecutionContextArtifactDiagnosticsList
              diagnostics={executionContext.diagnostics.artifactDiagnostics}
            />
          </div>
        </div>
      ) : (
        <EmptyState className="empty-state">Build a context preview to inspect diagnostics.</EmptyState>
      )}
    </Panel>
  )
}

type LaunchReadinessPanelProps = {
  executionContext: ExecutionContextPreview | null
  launchReadiness: string
}

function LaunchReadinessPanel({
  executionContext,
  launchReadiness,
}: LaunchReadinessPanelProps) {
  const diagnostics = executionContext?.diagnostics ?? null

  return (
    <Panel className="launch-readiness-panel" aria-label="Launch readiness">
      <SectionHeader eyebrow="Launch Readiness" title={diagnostics?.launchBlocked ? 'Blocked' : 'Status'} headingLevel={4} />
      <div className="execution-rail-summary">
        <span>Readiness: {launchReadiness}</span>
        <span>Hard limit exceeded: {diagnostics?.hardLimitExceeded ? 'Yes' : 'No'}</span>
        <span>Warning threshold exceeded: {diagnostics?.warningThresholdExceeded ? 'Yes' : 'No'}</span>
        <span>Validation errors: {diagnostics?.validationErrors.length ?? 'Not loaded'}</span>
        <span>Governed conflicts: {diagnostics?.governedConflicts.length ?? 'Not loaded'}</span>
      </div>
    </Panel>
  )
}
