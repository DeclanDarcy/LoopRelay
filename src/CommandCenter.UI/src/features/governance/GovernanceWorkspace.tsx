import { Button, EmptyState, Panel, SectionHeader, StatusBadge } from '../../components/design'
import {
  ActionEligibilityView,
  CertificationFindingsView,
  DiagnosticList,
  HealthView,
} from '../../components/explainability'
import { formatDateTime } from '../../lib'
import {
  governanceCertificationDiagnosticsToExplanation,
  governanceCertificationFindingsToExplanation,
  governanceEligibilityFindingsToDiagnostics,
  governanceEligibilityToActions,
  governanceHealthDimensionsToExplanation,
  governanceRecoveryDiagnosticsToExplanation,
  governanceRecoveryFindingsToDiagnostics,
} from '../../lib/explainability'
import type {
  DecisionSessionCertificationReport,
  DecisionSessionCoherence,
  DecisionSessionContinuityArtifact,
  DecisionSessionEconomics,
  DecisionSessionGovernanceSnapshot,
  DecisionSessionLifecycleEvaluation,
  DecisionSessionRecoveryDiagnostics,
  DecisionSessionRecoveryResult,
  DecisionSessionTransferEligibility,
  DecisionSessionTransfer,
  RepositoryDecisionSessionSummary,
  WorkflowInstance,
} from '../../types'

type GovernanceWorkspaceProps = {
  repositorySummary: RepositoryDecisionSessionSummary | null
  snapshot: DecisionSessionGovernanceSnapshot
  workflow: WorkflowInstance | null
  isLoading?: boolean
  error?: string | null
  isTransferring?: boolean
  isRecovering?: boolean
  isCertifying?: boolean
  onRefresh?: () => void
  onExecuteTransfer?: () => void
  onRecover?: () => void
  onRunCertification?: () => void
}

function badge(label: string, tone: 'neutral' | 'success' | 'warning' | 'danger' = 'neutral') {
  return { label, tone, className: `status-${tone}` }
}

function formatNumber(value: number | null | undefined) {
  return value === null || value === undefined ? 'Not projected' : value.toLocaleString()
}

function formatScore(value: number | null | undefined) {
  if (value === null || value === undefined) {
    return 'Not projected'
  }

  return value.toFixed(2)
}

function listItems(values: string[] | null | undefined, emptyLabel: string) {
  const entries = values ?? []

  if (entries.length === 0) {
    return <li>{emptyLabel}</li>
  }

  return entries.map((value) => <li key={value}>{value}</li>)
}

function PanelState({
  isLoading = false,
  error = null,
  emptyLabel,
}: {
  isLoading?: boolean
  error?: string | null
  emptyLabel: string
}) {
  if (error) {
    return <EmptyState className="empty-state">Governance data unavailable: {error}</EmptyState>
  }

  if (isLoading) {
    return <EmptyState className="empty-state">Loading governance data...</EmptyState>
  }

  return <EmptyState className="empty-state">{emptyLabel}</EmptyState>
}

function transferCanExecute(eligibility: DecisionSessionTransferEligibility | null) {
  return eligibility?.status === 'Eligible'
}

function transferRecommended(policy: DecisionSessionLifecycleEvaluation | null) {
  return policy?.decision === 'Transfer'
}

export function GovernanceWorkspace({
  repositorySummary,
  snapshot,
  workflow,
  isLoading = false,
  error = null,
  isTransferring = false,
  isRecovering = false,
  isCertifying = false,
  onRefresh,
  onExecuteTransfer,
  onRecover,
  onRunCertification,
}: GovernanceWorkspaceProps) {
  return (
    <section
      id="governance-workspace"
      className="governance-workspace tab-panel tab-governance"
      aria-label="Governance workspace"
    >
      <SectionHeader
        className="execution-workspace-header"
        eyebrow="Governance Workspace"
        title={snapshot.activeSession?.id ?? repositorySummary?.decisionSessionId ?? 'No active session'}
        headingLevel={4}
        actions={
          onRefresh ? (
            <Button type="button" variant="secondary" className="secondary-action" onClick={onRefresh}>
              Refresh
            </Button>
          ) : null
        }
      />

      <div className="governance-workspace-grid">
        <div className="governance-workspace-main">
          <DecisionSessionLifecyclePanel
            summary={repositorySummary}
            policy={snapshot.lifecyclePolicy}
            economics={snapshot.economics}
            coherence={snapshot.coherence}
            workflow={workflow}
            isLoading={isLoading}
            error={error}
          />
          <DecisionSessionEligibilityPanel
            eligibility={snapshot.transferEligibility}
            workflow={workflow}
            isLoading={isLoading}
            error={error}
          />
          <DecisionSessionTransferPanel
            eligibility={snapshot.transferEligibility}
            transfers={snapshot.transfers}
            transferHistory={snapshot.transferHistory}
            isTransferring={isTransferring}
            onExecuteTransfer={onExecuteTransfer}
            isLoading={isLoading}
            error={error}
          />
          <DecisionSessionRecoveryPanel
            recovery={snapshot.recovery}
            diagnostics={snapshot.recoveryDiagnostics}
            isRecovering={isRecovering}
            onRecover={onRecover}
            isLoading={isLoading}
            error={error}
          />
        </div>

        <aside className="governance-workspace-rail" aria-label="Governance inspector">
          <DecisionSessionAnalysisPanel
            metrics={snapshot.metrics}
            economics={snapshot.economics}
            coherence={snapshot.coherence}
            diagnostics={snapshot.analysisDiagnostics?.warnings ?? []}
          />
          <DecisionSessionContinuityArtifactPanel artifacts={snapshot.continuityArtifacts} />
          <DecisionSessionHealthPanel
            summary={repositorySummary}
            snapshot={snapshot}
            isLoading={isLoading}
            error={error}
          />
          <DecisionSessionCertificationPanel
            certification={snapshot.certificationReport ?? snapshot.certification}
            isRunning={isCertifying}
            onRun={onRunCertification}
            isLoading={isLoading}
            error={error}
          />
        </aside>
      </div>
    </section>
  )
}

type DecisionSessionLifecyclePanelProps = {
  summary: RepositoryDecisionSessionSummary | null
  policy: DecisionSessionLifecycleEvaluation | null
  economics: DecisionSessionEconomics | null
  coherence: DecisionSessionCoherence | null
  workflow: WorkflowInstance | null
  isLoading?: boolean
  error?: string | null
}

export function DecisionSessionLifecyclePanel({
  summary,
  policy,
  economics,
  coherence,
  workflow,
  isLoading = false,
  error = null,
}: DecisionSessionLifecyclePanelProps) {
  return (
    <Panel className="governance-panel governance-lifecycle-panel" aria-label="Governance lifecycle">
      <SectionHeader
        eyebrow="Lifecycle"
        title={policy?.decision ?? summary?.lifecycleDecision ?? 'Decision not projected'}
        headingLevel={4}
        actions={
          policy ? <StatusBadge status={badge(policy.decision, policy.decision === 'Transfer' ? 'warning' : 'success')} /> : null
        }
      />
      {policy || summary ? (
        <div className="governance-panel-stack">
          <div className="governance-fact-grid">
            <span>Active session: {summary?.decisionSessionId ?? 'Not projected'}</span>
            <span>State: {summary?.state ?? 'Not projected'}</span>
            <span>Reuse score: {formatScore(policy?.reuseScore)}</span>
            <span>Transfer score: {formatScore(policy?.transferScore)}</span>
            <span>Transfer pressure: {formatScore(coherence?.transferPressure ?? summary?.transferPressure)}</span>
            <span>Cache risk: {formatScore(economics?.estimatedCacheMissRisk ?? summary?.cacheMissRisk)}</span>
            <span>Continuity benefit: {formatScore(economics?.estimatedContinuityBenefit)}</span>
            <span>Coherence: {formatScore(coherence?.coherenceScore ?? summary?.coherenceScore)}</span>
            <span>Fragmentation: {formatScore(coherence?.fragmentationScore)}</span>
            <span>Growth: {formatNumber(summary?.estimatedTokenCount)}</span>
          </div>
          <p className="governance-explanation">{policy?.reason ?? 'Lifecycle reason is not projected.'}</p>
          <div className="governance-panel-list">
            <h5>Contributing Factors</h5>
            <ul>{listItems(policy?.contributingFactors, 'No contributing factors projected.')}</ul>
          </div>
          <div className="governance-workflow-link">
            <span>Workflow gate: {workflow?.blockingGate ?? 'Not loaded'}</span>
            <span>Required action: {workflow?.requiredHumanAction || (workflow ? 'None' : 'Not loaded')}</span>
          </div>
        </div>
      ) : (
        <PanelState isLoading={isLoading} error={error} emptyLabel="No governance lifecycle projection is available." />
      )}
    </Panel>
  )
}

export function DecisionSessionAnalysisPanel({
  metrics,
  economics,
  coherence,
  diagnostics,
}: {
  metrics: DecisionSessionGovernanceSnapshot['metrics']
  economics: DecisionSessionEconomics | null
  coherence: DecisionSessionCoherence | null
  diagnostics: string[]
}) {
  return (
    <Panel className="governance-panel governance-analysis-panel" aria-label="Governance analysis">
      <SectionHeader eyebrow="Analysis" title="Signals" headingLevel={4} />
      <div className="governance-fact-grid">
        <span>Tokens: {formatNumber(metrics?.estimatedTokenCount)}</span>
        <span>Decisions: {formatNumber(metrics?.decisionCount)}</span>
        <span>Reasoning events: {formatNumber(metrics?.reasoningEventCount)}</span>
        <span>Context cost: {formatScore(economics?.estimatedContextCost)}</span>
        <span>Reuse value: {formatScore(economics?.estimatedReuseValue)}</span>
        <span>Density: {formatScore(coherence?.densityScore)}</span>
      </div>
      <div className="governance-panel-list">
        <h5>Diagnostics</h5>
        <ul>{listItems(diagnostics, 'No analysis diagnostics projected.')}</ul>
      </div>
    </Panel>
  )
}

export function DecisionSessionEligibilityPanel({
  eligibility,
  workflow,
  isLoading = false,
  error = null,
}: {
  eligibility: DecisionSessionTransferEligibility | null
  workflow: WorkflowInstance | null
  isLoading?: boolean
  error?: string | null
}) {
  const recommended = transferRecommended(eligibility?.policyEvaluation ?? null)
  const executable = transferCanExecute(eligibility)
  const actions = eligibility ? governanceEligibilityToActions(eligibility) : []
  const findings = eligibility ? governanceEligibilityFindingsToDiagnostics(eligibility) : []

  return (
    <Panel className="governance-panel governance-eligibility-panel" aria-label="Governance transfer eligibility">
      <SectionHeader
        eyebrow="Transfer Eligibility"
        title={eligibility?.status ?? 'Not projected'}
        headingLevel={4}
        actions={eligibility ? <StatusBadge status={badge(eligibility.status, executable ? 'success' : recommended ? 'warning' : 'neutral')} /> : null}
      />
      {eligibility ? (
        <div className="governance-panel-stack">
          <div className="governance-fact-grid">
            <span>Transfer recommended: {recommended ? 'Yes' : 'No'}</span>
            <span>Currently executable: {executable ? 'Yes' : 'No'}</span>
            <span>Source session: {eligibility.sourceSessionId ?? 'Not projected'}</span>
            <span>Checked: {formatDateTime(eligibility.checkedAt)}</span>
            <span>Workflow gate: {workflow?.blockingGate ?? 'Not loaded'}</span>
            <span>Required action: {workflow?.requiredHumanAction || (workflow ? 'None' : 'Not loaded')}</span>
          </div>
          <DiagnosticList
            diagnostics={findings}
            emptyLabel="No eligibility findings projected."
            title="Findings"
          />
          <ActionEligibilityView
            actions={actions}
            emptyLabel="No transfer actions projected."
            title="Eligible Actions"
          />
        </div>
      ) : (
        <PanelState isLoading={isLoading} error={error} emptyLabel="No transfer eligibility projection is available." />
      )}
    </Panel>
  )
}

export function DecisionSessionTransferPanel({
  eligibility,
  transfers,
  transferHistory,
  isTransferring = false,
  onExecuteTransfer,
  isLoading = false,
  error = null,
}: {
  eligibility: DecisionSessionTransferEligibility | null
  transfers: DecisionSessionTransfer[]
  transferHistory: DecisionSessionTransfer[]
  isTransferring?: boolean
  onExecuteTransfer?: () => void
  isLoading?: boolean
  error?: string | null
}) {
  const lineage = transfers.length > 0 ? transfers : transferHistory
  const executable = transferCanExecute(eligibility)

  return (
    <Panel className="governance-panel governance-transfer-panel" aria-label="Governance transfer">
      <SectionHeader
        eyebrow="Transfer"
        title={lineage[0]?.transferId ?? 'Lineage'}
        headingLevel={4}
        actions={
          onExecuteTransfer ? (
            <Button type="button" variant="secondary" onClick={onExecuteTransfer} disabled={!executable || isTransferring}>
              {isTransferring ? 'Transferring...' : 'Execute'}
            </Button>
          ) : null
        }
      />
      {eligibility || lineage.length > 0 ? (
        <div className="governance-panel-stack">
          <div className="governance-fact-grid">
            <span>Eligibility: {eligibility?.status ?? 'Not projected'}</span>
            <span>Executable now: {executable ? 'Yes' : 'No'}</span>
            <span>Recorded transfers: {lineage.length}</span>
          </div>
          <div className="governance-panel-list">
            <h5>Recent Lineage</h5>
            <ul>
              {lineage.length === 0
                ? listItems([], 'No transfer lineage projected.')
                : lineage.slice(0, 5).map((transfer) => (
                    <li key={transfer.transferId}>
                      {transfer.succeeded ? 'Succeeded' : 'Pending or failed'}: {transfer.sourceSessionId} to{' '}
                      {transfer.targetSessionId ?? 'not assigned'}
                    </li>
                  ))}
            </ul>
          </div>
        </div>
      ) : (
        <PanelState isLoading={isLoading} error={error} emptyLabel="No transfer projection is available." />
      )}
    </Panel>
  )
}

export function DecisionSessionContinuityArtifactPanel({
  artifacts,
}: {
  artifacts: DecisionSessionContinuityArtifact[]
}) {
  return (
    <Panel className="governance-panel governance-continuity-panel" aria-label="Governance continuity artifacts">
      <SectionHeader eyebrow="Continuity Artifacts" title={`${artifacts.length} projected`} headingLevel={4} />
      <div className="governance-panel-list">
        <ul>
          {artifacts.length === 0
            ? listItems([], 'No continuity artifacts projected.')
            : artifacts.slice(0, 5).map((artifact) => (
                <li key={artifact.artifactId}>
                  {artifact.artifactId}: {artifact.sourceSessionId} to {artifact.targetSessionId ?? 'not assigned'}
                </li>
              ))}
        </ul>
      </div>
    </Panel>
  )
}

export function DecisionSessionRecoveryPanel({
  recovery,
  diagnostics,
  isRecovering = false,
  onRecover,
  isLoading = false,
  error = null,
}: {
  recovery: DecisionSessionRecoveryResult | null
  diagnostics: DecisionSessionRecoveryDiagnostics | null
  isRecovering?: boolean
  onRecover?: () => void
  isLoading?: boolean
  error?: string | null
}) {
  const requiresIntervention = Boolean(recovery?.findings.some((finding) => finding.severity.toLowerCase() === 'error'))
  const findings = governanceRecoveryFindingsToDiagnostics(recovery)
  const recoveryDiagnostics = governanceRecoveryDiagnosticsToExplanation(diagnostics, recovery)

  return (
    <Panel className="governance-panel governance-recovery-panel" aria-label="Governance recovery">
      <SectionHeader
        eyebrow="Recovery"
        title={recovery ? (recovery.succeeded ? 'Recovered' : 'Requires review') : 'Assessment'}
        headingLevel={4}
        actions={
          onRecover ? (
            <Button type="button" variant="secondary" onClick={onRecover} disabled={isRecovering}>
              {isRecovering ? 'Recovering...' : 'Recover'}
            </Button>
          ) : null
        }
      />
      {recovery || diagnostics ? (
        <div className="governance-panel-stack">
          <div className="governance-fact-grid">
            <span>Recovered: {recovery ? (recovery.succeeded ? 'Yes' : 'No') : 'Not projected'}</span>
            <span>Diagnosed: {diagnostics ? 'Yes' : 'No'}</span>
            <span>Requires intervention: {requiresIntervention ? 'Yes' : 'No'}</span>
            <span>Duplicate active sessions: {diagnostics?.registryDiagnostics.activeSessionCount && diagnostics.registryDiagnostics.activeSessionCount > 1 ? 'Yes' : 'No'}</span>
            <span>Interrupted transfers: {diagnostics?.transferAssessments.filter((assessment) => assessment.status !== 'Completed').length ?? 'Not projected'}</span>
            <span>Discarded snapshots: {recovery?.findings.filter((finding) => finding.code.toLowerCase().includes('discard')).length ?? 0}</span>
            <span>Rebuilt snapshots: {recovery?.events.filter((event) => event.eventType.toLowerCase().includes('rebuilt')).length ?? 0}</span>
          </div>
          <DiagnosticList
            diagnostics={findings}
            emptyLabel="No recovery findings projected."
            title="Findings"
          />
          <DiagnosticList
            diagnostics={recoveryDiagnostics}
            emptyLabel="No recovery diagnostics projected."
            title="Diagnostics"
          />
        </div>
      ) : (
        <PanelState isLoading={isLoading} error={error} emptyLabel="No recovery projection is available." />
      )}
    </Panel>
  )
}

export function DecisionSessionHealthPanel({
  summary,
  snapshot,
  isLoading = false,
  error = null,
}: {
  summary: RepositoryDecisionSessionSummary | null
  snapshot: DecisionSessionGovernanceSnapshot
  isLoading?: boolean
  error?: string | null
}) {
  const dimensions = snapshot.health?.dimensions ?? summary?.healthDimensions ?? []
  const healthDimensions = governanceHealthDimensionsToExplanation(dimensions)

  return (
    <Panel className="governance-panel governance-health-panel" aria-label="Governance health">
      <SectionHeader eyebrow="Health" title={`${dimensions.length} dimensions`} headingLevel={4} />
      {dimensions.length > 0 ? (
        <div className="governance-panel-stack">
          <HealthView
            dimensions={healthDimensions}
            emptyLabel="No governance health dimensions are projected."
          />
        </div>
      ) : (
        <PanelState isLoading={isLoading} error={error} emptyLabel="No governance health dimensions are projected." />
      )}
    </Panel>
  )
}

export function DecisionSessionCertificationPanel({
  certification,
  isRunning = false,
  onRun,
  isLoading = false,
  error = null,
}: {
  certification: DecisionSessionCertificationReport | null
  isRunning?: boolean
  onRun?: () => void
  isLoading?: boolean
  error?: string | null
}) {
  const findings = certification ? governanceCertificationFindingsToExplanation(certification) : []
  const diagnostics = certification ? governanceCertificationDiagnosticsToExplanation(certification) : []

  return (
    <Panel className="governance-panel governance-certification-panel" aria-label="Governance certification">
      <SectionHeader
        eyebrow="Certification"
        title={certification ? (certification.result.passed ? 'Passed' : 'Findings') : 'Not run'}
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
        <div className="governance-panel-stack">
          <div className="governance-fact-grid">
            <span>Generated: {formatDateTime(certification.generatedAt)}</span>
            <span>Findings: {certification.result.findings.length}</span>
            <span>Diagnostics: {certification.result.diagnostics.length}</span>
          </div>
          <CertificationFindingsView
            findings={findings}
            emptyLabel="No certification findings projected."
          />
          <DiagnosticList
            diagnostics={diagnostics}
            emptyLabel="No certification diagnostics projected."
          />
        </div>
      ) : (
        <PanelState isLoading={isLoading} error={error} emptyLabel="No governance certification report is projected." />
      )}
    </Panel>
  )
}
