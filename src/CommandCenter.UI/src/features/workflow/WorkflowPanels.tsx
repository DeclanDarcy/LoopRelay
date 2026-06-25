import { Button, EmptyState, Panel, SectionHeader } from '../../components/design'
import { CertificationFindingsView, DiagnosticList, HealthView } from '../../components/explainability'
import { formatDateTime } from '../../lib'
import {
  workflowCertificationFindingsToExplanation,
  workflowDiagnosticsToExplanation,
  workflowHealthDimensionsToExplanation,
} from '../../lib/explainability'
import {
  useWorkflowCertification,
  useWorkflowContinuation,
  useWorkflowGates,
  useWorkflowHealth,
  useWorkflowHistory,
  useWorkflowRecovery,
} from '../../hooks'
import type {
  WorkflowCertificationResult,
  WorkflowContinuationEvaluation,
  WorkflowGateCatalogProjection,
  WorkflowHealthAssessment,
  WorkflowHistoryProjection,
  WorkflowInstance,
  WorkflowRecoveryDiagnostics,
  WorkflowTimeline,
} from '../../types'

type WorkflowPanelStateProps = {
  isLoading?: boolean
  error?: string | null
}

function WorkflowPanelState({ isLoading = false, error = null }: WorkflowPanelStateProps) {
  if (error) {
    return <EmptyState className="empty-state">Workflow data unavailable: {error}</EmptyState>
  }

  if (isLoading) {
    return <EmptyState className="empty-state">Loading workflow data...</EmptyState>
  }

  return null
}

function listItems(values: string[], emptyLabel: string) {
  if (values.length === 0) {
    return <li>{emptyLabel}</li>
  }

  return values.map((value) => <li key={value}>{value}</li>)
}

type WorkflowOverviewPanelProps = WorkflowPanelStateProps & {
  workflow: WorkflowInstance | null
}

export function WorkflowOverviewPanel({
  workflow,
  isLoading = false,
  error = null,
}: WorkflowOverviewPanelProps) {
  const state = <WorkflowPanelState isLoading={isLoading} error={error} />

  return (
    <Panel className="workflow-panel workflow-overview-panel" aria-label="Workflow overview">
      <SectionHeader eyebrow="Workflow" title={workflow?.currentStage ?? 'Projection'} headingLevel={4} />
      {workflow ? (
        <div className="workflow-fact-grid">
          <span>Progress: {workflow.progressState}</span>
          <span>Blocking gate: {workflow.blockingGate}</span>
          <span>Required action: {workflow.requiredHumanAction || 'None'}</span>
          <span>Open gates: {workflow.openGates.length}</span>
          <span>Timeline entries: {workflow.timeline.length}</span>
          <span>Next stages: {workflow.nextPossibleStages.join(', ') || 'None'}</span>
        </div>
      ) : (
        state ?? <EmptyState className="empty-state">Workflow projection is not loaded.</EmptyState>
      )}
    </Panel>
  )
}

type WorkflowRecoveryPanelProps = WorkflowPanelStateProps & {
  diagnostics: WorkflowRecoveryDiagnostics | null
  isRecovering?: boolean
  onRecover?: () => void
}

export function WorkflowRecoveryPanel({
  diagnostics,
  isLoading = false,
  isRecovering = false,
  error = null,
  onRecover,
}: WorkflowRecoveryPanelProps) {
  const state = <WorkflowPanelState isLoading={isLoading} error={error} />

  return (
    <Panel className="workflow-panel workflow-recovery-panel" aria-label="Workflow recovery">
      <SectionHeader
        eyebrow="Recovery"
        title={diagnostics?.rebuilt ? 'Rebuilt' : 'Diagnostics'}
        headingLevel={4}
        actions={
          onRecover ? (
            <Button type="button" variant="secondary" onClick={onRecover} disabled={isRecovering}>
              {isRecovering ? 'Recovering...' : 'Recover'}
            </Button>
          ) : null
        }
      />
      {diagnostics ? (
        <div className="workflow-panel-stack">
          <div className="workflow-fact-grid">
            <span>Recovered: {formatDateTime(diagnostics.recoveredAt)}</span>
            <span>Rebuilt: {diagnostics.rebuilt ? 'Yes' : 'No'}</span>
            <span>Evidence matched: {diagnostics.persistedEvidenceMatchedDomain ? 'Yes' : 'No'}</span>
            <span>Domain fingerprint: {diagnostics.domainFingerprint}</span>
          </div>
          <div className="workflow-panel-list">
            <h5>Diagnostics</h5>
            <ul>{listItems(diagnostics.diagnostics, 'No recovery diagnostics projected.')}</ul>
          </div>
          <div className="workflow-panel-list">
            <h5>Recovered Artifacts</h5>
            <ul>{listItems(diagnostics.recoveredArtifacts, 'No artifacts recovered.')}</ul>
          </div>
          <div className="workflow-panel-list">
            <h5>Discarded Artifacts</h5>
            <ul>{listItems(diagnostics.discardedArtifacts, 'No artifacts discarded.')}</ul>
          </div>
        </div>
      ) : (
        state ?? <EmptyState className="empty-state">No recovery diagnostics are projected.</EmptyState>
      )}
    </Panel>
  )
}

type WorkflowHealthPanelProps = WorkflowPanelStateProps & {
  health: WorkflowHealthAssessment | null
}

export function WorkflowHealthPanel({
  health,
  isLoading = false,
  error = null,
}: WorkflowHealthPanelProps) {
  const state = <WorkflowPanelState isLoading={isLoading} error={error} />

  return (
    <Panel className="workflow-panel workflow-health-panel" aria-label="Workflow health">
      <SectionHeader eyebrow="Health" title={health?.overallStatus ?? 'Assessment'} headingLevel={4} />
      {health ? (
        <div className="workflow-panel-stack">
          <div className="workflow-fact-grid">
            <span>Generated: {formatDateTime(health.generatedAt)}</span>
            <span>Dimensions: {health.dimensions.length}</span>
            <span>Conflicts: {health.influenceTrace.conflicts.length}</span>
            <span>Evidence paths: {health.influenceTrace.evidencePaths.length}</span>
          </div>
          <HealthView dimensions={workflowHealthDimensionsToExplanation(health)} />
          <DiagnosticList
            title="Assessment Diagnostics"
            diagnostics={workflowDiagnosticsToExplanation(health.diagnostics)}
            emptyLabel="No assessment diagnostics projected."
          />
        </div>
      ) : (
        state ?? <EmptyState className="empty-state">No workflow health assessment is projected.</EmptyState>
      )}
    </Panel>
  )
}

type WorkflowCertificationPanelProps = WorkflowPanelStateProps & {
  certification: WorkflowCertificationResult | null
  isRunning?: boolean
  onRun?: () => void
}

export function WorkflowCertificationPanel({
  certification,
  isLoading = false,
  isRunning = false,
  error = null,
  onRun,
}: WorkflowCertificationPanelProps) {
  const state = <WorkflowPanelState isLoading={isLoading} error={error} />

  return (
    <Panel className="workflow-panel workflow-certification-panel" aria-label="Workflow certification">
      <SectionHeader
        eyebrow="Certification"
        title={certification ? (certification.certified ? 'Certified' : 'Findings') : 'Certification'}
        headingLevel={4}
        actions={
          onRun ? (
            <Button type="button" variant="secondary" onClick={onRun} disabled={isRunning}>
              {isRunning ? 'Running...' : 'Run'}
            </Button>
          ) : null
        }
      />
      {certification ? (
        <div className="workflow-panel-stack">
          <div className="workflow-fact-grid">
            <span>Passed: {certification.passedFindingCount}</span>
            <span>Failed: {certification.failedFindingCount}</span>
            <span>Stage: {certification.currentStage}</span>
            <span>Gate: {certification.blockingGate}</span>
          </div>
          <CertificationFindingsView findings={workflowCertificationFindingsToExplanation(certification)} />
          <div className="workflow-panel-list">
            <h5>Failures</h5>
            <ul>{listItems(certification.failures, 'No certification failures projected.')}</ul>
          </div>
          <DiagnosticList
            title="Certification Diagnostics"
            diagnostics={workflowDiagnosticsToExplanation(certification.diagnostics)}
            emptyLabel="No certification diagnostics projected."
          />
        </div>
      ) : (
        state ?? <EmptyState className="empty-state">No workflow certification result is projected.</EmptyState>
      )}
    </Panel>
  )
}

type WorkflowGatePanelProps = WorkflowPanelStateProps & {
  gates: WorkflowGateCatalogProjection | null
}

export function WorkflowGatePanel({ gates, isLoading = false, error = null }: WorkflowGatePanelProps) {
  const state = <WorkflowPanelState isLoading={isLoading} error={error} />

  return (
    <Panel className="workflow-panel workflow-gate-panel" aria-label="Workflow gates">
      <SectionHeader eyebrow="Gates" title={gates ? `${gates.openGates.length} open` : 'Catalog'} headingLevel={4} />
      {gates ? (
        <div className="workflow-panel-stack">
          {gates.openGates.length === 0 ? (
            <EmptyState className="empty-state">No open gates.</EmptyState>
          ) : (
            gates.openGates.map((gate) => (
              <article className="workflow-dimension-card" key={gate.gateId}>
                <div className="workflow-dimension-card-header">
                  <strong>{gate.type}</strong>
                  <span>{gate.status}</span>
                </div>
                <p>{gate.reason}</p>
                <div className="workflow-fact-grid">
                  <span>Required action: {gate.requiredAction}</span>
                  <span>Command: {gate.satisfyingCommands[0] ?? gate.satisfyingCommand}</span>
                </div>
              </article>
            ))
          )}
          <div className="workflow-panel-list">
            <h5>Gate Reasoning</h5>
            <ul>{listItems(gates.diagnostics.reasoning, 'No gate reasoning projected.')}</ul>
          </div>
        </div>
      ) : (
        state ?? <EmptyState className="empty-state">No workflow gate catalog is projected.</EmptyState>
      )}
    </Panel>
  )
}

type WorkflowHistoryPanelProps = WorkflowPanelStateProps & {
  history: WorkflowHistoryProjection | null
  timeline: WorkflowTimeline | null
}

export function WorkflowHistoryPanel({
  history,
  timeline,
  isLoading = false,
  error = null,
}: WorkflowHistoryPanelProps) {
  const state = <WorkflowPanelState isLoading={isLoading} error={error} />
  const entries = timeline?.entries ?? history?.timeline.entries ?? []

  return (
    <Panel className="workflow-panel workflow-history-panel" aria-label="Workflow history">
      <SectionHeader eyebrow="History" title={`${entries.length} entries`} headingLevel={4} />
      {history || timeline ? (
        <div className="workflow-panel-list">
          <ul>
            {entries.length === 0
              ? listItems([], 'No timeline entries projected.')
              : entries.slice(0, 5).map((entry) => (
                  <li key={`${entry.occurredAt}-${entry.eventType}`}>
                    {entry.eventType}: {entry.summary}
                  </li>
                ))}
          </ul>
        </div>
      ) : (
        state ?? <EmptyState className="empty-state">No workflow history is projected.</EmptyState>
      )}
    </Panel>
  )
}

type WorkflowContinuationPanelProps = WorkflowPanelStateProps & {
  evaluation: WorkflowContinuationEvaluation | null
  isRunning?: boolean
  onRun?: () => void
}

export function WorkflowContinuationPanel({
  evaluation,
  isLoading = false,
  isRunning = false,
  error = null,
  onRun,
}: WorkflowContinuationPanelProps) {
  const state = <WorkflowPanelState isLoading={isLoading} error={error} />

  return (
    <Panel className="workflow-panel workflow-continuation-panel" aria-label="Workflow continuation">
      <SectionHeader
        eyebrow="Continuation"
        title={evaluation?.outcome ?? 'Evaluation'}
        headingLevel={4}
        actions={
          onRun ? (
            <Button type="button" variant="secondary" onClick={onRun} disabled={isRunning}>
              {isRunning ? 'Advancing...' : 'Run'}
            </Button>
          ) : null
        }
      />
      {evaluation ? (
        <div className="workflow-panel-stack">
          <div className="workflow-fact-grid">
            <span>From: {evaluation.fromStage}</span>
            <span>To: {evaluation.toStage ?? 'None'}</span>
            <span>Can advance: {evaluation.canAdvanceMechanically ? 'Yes' : 'No'}</span>
            <span>Waiting for human: {evaluation.isWaitingForHuman ? 'Yes' : 'No'}</span>
            <span>Required action: {evaluation.requiredHumanAction || 'None'}</span>
            <span>Stop reason: {evaluation.stopReason || 'None'}</span>
          </div>
          <div className="workflow-panel-list">
            <h5>Reasoning</h5>
            <ul>{listItems(evaluation.diagnostics.reasoning, 'No continuation reasoning projected.')}</ul>
          </div>
        </div>
      ) : (
        state ?? <EmptyState className="empty-state">No workflow continuation evaluation is projected.</EmptyState>
      )}
    </Panel>
  )
}

type WorkflowOperationsPanelProps = {
  repositoryId: string | null
  workflow: WorkflowInstance | null
  isWorkflowLoading?: boolean
  workflowError?: string | null
}

export function WorkflowOperationsPanel({
  repositoryId,
  workflow,
  isWorkflowLoading = false,
  workflowError = null,
}: WorkflowOperationsPanelProps) {
  const recovery = useWorkflowRecovery(repositoryId)
  const health = useWorkflowHealth(repositoryId)
  const certification = useWorkflowCertification(repositoryId)
  const gates = useWorkflowGates(repositoryId)
  const history = useWorkflowHistory(repositoryId)
  const continuation = useWorkflowContinuation(repositoryId)

  return (
    <section className="workflow-panels" aria-label="Workflow operations">
      <WorkflowOverviewPanel workflow={workflow} isLoading={isWorkflowLoading} error={workflowError} />
      <WorkflowRecoveryPanel
        diagnostics={recovery.diagnostics}
        isLoading={recovery.isLoading}
        isRecovering={recovery.isRecovering}
        error={recovery.error}
        onRecover={() => void recovery.recover()}
      />
      <WorkflowHealthPanel health={health.health} isLoading={health.isLoading} error={health.error} />
      <WorkflowCertificationPanel
        certification={certification.certification}
        isLoading={certification.isLoading}
        isRunning={certification.isRunning}
        error={certification.error}
        onRun={() => void certification.run()}
      />
      <WorkflowGatePanel gates={gates.gates} isLoading={gates.isLoading} error={gates.error} />
      <WorkflowHistoryPanel
        history={history.history}
        timeline={history.timeline}
        isLoading={history.isLoading}
        error={history.error}
      />
      <WorkflowContinuationPanel
        evaluation={continuation.evaluation}
        isLoading={continuation.isLoading}
        isRunning={continuation.isRunning}
        error={continuation.error}
        onRun={() => void continuation.run()}
      />
    </section>
  )
}
