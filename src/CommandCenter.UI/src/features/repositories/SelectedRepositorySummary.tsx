import type { ReactNode } from 'react'
import { formatDateTime, formatDuration } from '../../lib'
import { StatusBadge } from '../../components/design'
import {
  executionReadinessStatus,
  repositoryAvailabilityStatus,
  repositoryExecutionStatus,
} from '../../lib/status'
import type {
  ExecutionSessionSummary,
  RepositoryDashboardProjection,
  RepositoryDecisionSessionSummary,
  RepositoryExecutionState,
  RepositoryReasoningSummary,
  RepositoryWorkspaceProjection,
  WorkflowInstance,
} from '../../types'

// The backend computes these summaries from snapshots that can transiently fail
// to rebuild (e.g. a locked file), in which case the field is serialized as null
// or omitted. On first paint the workspace is also still null. Fall back to a
// fully-populated empty summary so a missing sub-projection degrades to
// "Not projected" instead of white-screening the whole dashboard.
const EMPTY_REASONING_SUMMARY: RepositoryReasoningSummary = {
  eventCount: 0,
  threadCount: 0,
  relationshipCount: 0,
  hypothesisEventCount: 0,
  alternativeEventCount: 0,
  contradictionEventCount: 0,
  directionEventCount: 0,
  decisionEvolutionEventCount: 0,
  assumptionEvolutionEventCount: 0,
  constraintEvolutionEventCount: 0,
  evidenceEventCount: 0,
  lastEventAt: null,
  lastThreadActivityAt: null,
  lastRelationshipAt: null,
  lastActivityAt: null,
  lastReconstructionAt: null,
  lastCertificationAt: null,
  certificationResult: null,
}

const EMPTY_DECISION_SESSION_SUMMARY: RepositoryDecisionSessionSummary = {
  decisionSessionId: null,
  state: null,
  lifecycleDecision: null,
  transferEligibilityStatus: null,
  estimatedTokenCount: null,
  estimatedCacheTtl: null,
  cacheMissRisk: null,
  coherenceScore: null,
  transferPressure: null,
  healthDimensions: [],
  recentTransferLineage: [],
  diagnostics: [],
  generatedAt: null,
}

type SelectedRepositorySummaryProps = {
  repository: RepositoryDashboardProjection
  workspace: RepositoryWorkspaceProjection | null
  workflow: WorkflowInstance | null
  executionDisplay: ExecutionSessionSummary | null
  currentExecutionState: RepositoryExecutionState
  onOpenExecution?: () => void
  onOpenGovernance?: () => void
  onOpenReasoning?: () => void
  onOpenContinuity?: () => void
  onOpenMilestones?: () => void
  onOpenOperationalContext?: () => void
  onOpenHandoffArtifact?: (handoffPath: string) => void
}

type DashboardSectionProps = {
  title: string
  action?: {
    label: string
    onClick: () => void
  }
  children: ReactNode
}

function DashboardSection({ title, action, children }: DashboardSectionProps) {
  return (
    <section className="operational-dashboard-section" aria-label={`${title} dashboard summary`}>
      <div className="operational-dashboard-section-header">
        <h4>{title}</h4>
        {action ? (
          <button
            type="button"
            className="workspace-cross-link inline-cross-link"
            onClick={action.onClick}
          >
            {action.label}
          </button>
        ) : null}
      </div>
      <div className="operational-dashboard-facts">{children}</div>
    </section>
  )
}

function DashboardFact({ label, value }: { label: string; value: ReactNode }) {
  return (
    <span>
      <strong>{label}</strong>
      {value}
    </span>
  )
}

export function SelectedRepositorySummary({
  repository,
  workspace,
  workflow,
  executionDisplay,
  currentExecutionState,
  onOpenExecution,
  onOpenGovernance,
  onOpenReasoning,
  onOpenContinuity,
  onOpenMilestones,
  onOpenOperationalContext,
  onOpenHandoffArtifact,
}: SelectedRepositorySummaryProps) {
  const readiness = workspace?.readiness ?? repository.readiness
  const milestoneCount = workspace?.milestoneCount ?? repository.milestoneCount
  const hasOperationalContext = Boolean(workspace?.hasOperationalContext)
  const governanceSummary =
    workspace?.decisionSessionSummary ??
    repository.decisionSessionSummary ??
    EMPTY_DECISION_SESSION_SUMMARY
  const reasoningSummary =
    workspace?.reasoningSummary ?? repository.reasoningSummary ?? EMPTY_REASONING_SUMMARY
  const continuityRevisionCount =
    workspace?.operationalContext.revisionCount ??
    repository.continuitySummary.operationalContextRevisionCount
  const continuityWarningCount = workspace?.operationalContext.continuityWarnings.length ?? 0
  const continuityPendingProposal =
    workspace?.operationalContextProposalSummary.pendingProposalExists ??
    repository.continuitySummary.pendingProposalExists
  const continuityLatestActivity =
    workspace?.operationalContext.lastUpdatedAt ??
    repository.continuitySummary.operationalContextLastUpdatedAt
  const governanceHealthFindingCount = governanceSummary.healthDimensions.reduce(
    (count, dimension) => count + dimension.findings.length,
    0,
  )
  const workflowDiagnosticCount =
    (workflow?.diagnostics?.unknownStates.length ?? 0) +
    (workflow?.diagnostics?.conflicts.length ?? 0) +
    (workflow?.gateDiagnostics?.missingEvidence.length ?? 0) +
    (workflow?.gateDiagnostics?.conflicts.length ?? 0) +
    (workflow?.executionDiagnostics?.missingEvidence.length ?? 0) +
    (workflow?.executionDiagnostics?.conflicts.length ?? 0) +
    (workflow?.handoffDiagnostics?.missingEvidence.length ?? 0) +
    (workflow?.handoffDiagnostics?.conflicts.length ?? 0) +
    (workflow?.decisionDiagnostics?.conflicts.length ?? 0) +
    (workflow?.operationalContextDiagnostics?.conflicts.length ?? 0) +
    (workflow?.gitDiagnostics?.missingEvidence.length ?? 0) +
    (workflow?.gitDiagnostics?.conflicts.length ?? 0)
  const governanceDiagnosticCount = governanceSummary.diagnostics.length
  const continuityDiagnosticCount = continuityWarningCount
  const diagnosticCount =
    workflowDiagnosticCount + governanceDiagnosticCount + continuityDiagnosticCount
  const workflowHealthStatus =
    workflow?.decisionSession?.summary.healthStatus ??
    (workflow
      ? workflow.blockingGate === 'None' && workflow.progressState !== 'Failed'
        ? 'Healthy'
        : workflow.progressState
      : 'Not loaded')
  const certificationStatus =
    workflow?.currentDecision?.certificationStatus ??
    reasoningSummary.certificationResult ??
    'Not projected'

  return (
    <>
      <div className="details-title-row">
        <div>
          <p className="eyebrow">Selected repository</p>
          <h3>{repository.repository.name}</h3>
        </div>
        <StatusBadge status={repositoryAvailabilityStatus[repository.availability]} />
      </div>

      <dl className="details-list">
        <div>
          <dt>Path</dt>
          <dd>{repository.repository.path}</dd>
        </div>
        <div>
          <dt>Readiness</dt>
          <dd>
            <StatusBadge status={executionReadinessStatus[readiness]} />
          </dd>
        </div>
        <div>
          <dt>Execution</dt>
          <dd>
            {onOpenExecution ? (
              <button
                type="button"
                className="workspace-cross-link inline-cross-link"
                onClick={onOpenExecution}
              >
                <StatusBadge status={repositoryExecutionStatus[currentExecutionState]} />
              </button>
            ) : (
              <StatusBadge status={repositoryExecutionStatus[currentExecutionState]} />
            )}
          </dd>
        </div>
        {executionDisplay ? (
          <>
            <div>
              <dt>Session</dt>
              <dd>
                {onOpenExecution ? (
                  <button
                    type="button"
                    className="workspace-cross-link inline-cross-link"
                    onClick={onOpenExecution}
                  >
                    {executionDisplay.sessionId}
                  </button>
                ) : (
                  executionDisplay.sessionId
                )}
              </dd>
            </div>
            <div>
              <dt>Provider</dt>
              <dd>{executionDisplay.providerName || 'Unknown'}</dd>
            </div>
            <div>
              <dt>Started</dt>
              <dd>{formatDateTime(executionDisplay.startedAt)}</dd>
            </div>
            <div>
              <dt>Last activity</dt>
              <dd>{formatDateTime(executionDisplay.lastActivityAt)}</dd>
            </div>
            <div>
              <dt>Duration</dt>
              <dd>{formatDuration(executionDisplay.duration)}</dd>
            </div>
            <div>
              <dt>Accepted</dt>
              <dd>{formatDateTime(executionDisplay.acceptedAt)}</dd>
            </div>
            <div>
              <dt>Rejected</dt>
              <dd>{formatDateTime(executionDisplay.rejectedAt)}</dd>
            </div>
            <div>
              <dt>Decision note</dt>
              <dd>{executionDisplay.decisionNote || 'Not recorded'}</dd>
            </div>
            <div>
              <dt>PID</dt>
              <dd>{executionDisplay.providerProcessId ?? 'Not recorded'}</dd>
            </div>
            <div>
              <dt>Executable</dt>
              <dd>{executionDisplay.providerExecutablePath || 'Not recorded'}</dd>
            </div>
            <div>
              <dt>Failure</dt>
              <dd>{executionDisplay.failureReason || 'None'}</dd>
            </div>
            <div>
              <dt>Handoff</dt>
              <dd>
                {executionDisplay.handoffPath && onOpenHandoffArtifact ? (
                  <button
                    type="button"
                    className="workspace-cross-link inline-cross-link"
                    onClick={() => onOpenHandoffArtifact(executionDisplay.handoffPath as string)}
                  >
                    {executionDisplay.handoffPath}
                  </button>
                ) : (
                  executionDisplay.handoffPath || 'Not recorded'
                )}
              </dd>
            </div>
          </>
        ) : null}
        <div>
          <dt>Milestones</dt>
          <dd>
            {milestoneCount > 0 && onOpenMilestones ? (
              <button
                type="button"
                className="workspace-cross-link inline-cross-link"
                onClick={onOpenMilestones}
              >
                {milestoneCount}
              </button>
            ) : (
              milestoneCount
            )}
          </dd>
        </div>
      </dl>

      <div className="operational-dashboard" aria-label="Unified operational dashboard">
        <div className="operational-dashboard-heading">
          <p className="eyebrow">Operational dashboard</p>
          <h4>Repository operating picture</h4>
        </div>

        <DashboardSection title="Repository">
          <DashboardFact label="Plan" value={workspace?.hasPlan ? 'Present' : 'Missing'} />
          <DashboardFact label="Handoff" value={workspace?.hasCurrentHandoff ? 'Present' : 'Missing'} />
          <DashboardFact label="Decisions" value={workspace?.hasCurrentDecisions ? 'Present' : 'Missing'} />
          <DashboardFact
            label="Milestones"
            value={
              milestoneCount > 0 && onOpenMilestones ? (
                <button
                  type="button"
                  className="workspace-cross-link inline-cross-link"
                  onClick={onOpenMilestones}
                >
                  {milestoneCount}
                </button>
              ) : (
                milestoneCount
              )
            }
          />
        </DashboardSection>

        <DashboardSection title="Workflow">
          <DashboardFact label="Stage" value={workflow?.currentStage ?? 'Not loaded'} />
          <DashboardFact label="Gate" value={workflow?.blockingGate ?? 'Not loaded'} />
          <DashboardFact
            label="Required action"
            value={
              workflow?.requiredHumanAction && workflow.requiredHumanAction.trim().length > 0
                ? workflow.requiredHumanAction
                : workflow
                  ? 'None'
                  : 'Not loaded'
            }
          />
          <DashboardFact label="Timeline events" value={workflow ? workflow.timeline.length : 'Not loaded'} />
        </DashboardSection>

        <DashboardSection title="Execution" action={onOpenExecution ? { label: 'Open', onClick: onOpenExecution } : undefined}>
          <DashboardFact label="State" value={repositoryExecutionStatus[currentExecutionState].label} />
          <DashboardFact label="Session" value={executionDisplay?.sessionId ?? 'None'} />
          <DashboardFact label="Provider" value={executionDisplay?.providerName || 'Unknown'} />
          <DashboardFact label="Failure" value={executionDisplay?.failureReason || 'None'} />
        </DashboardSection>

        <DashboardSection title="Governance" action={onOpenGovernance ? { label: 'Open', onClick: onOpenGovernance } : undefined}>
          <DashboardFact
            label="Session"
            value={
              governanceSummary.decisionSessionId && onOpenGovernance ? (
                <button
                  type="button"
                  className="workspace-cross-link inline-cross-link"
                  onClick={onOpenGovernance}
                >
                  {governanceSummary.decisionSessionId}
                </button>
              ) : (
                governanceSummary.decisionSessionId ?? 'Not projected'
              )
            }
          />
          <DashboardFact label="State" value={governanceSummary.state ?? 'Not projected'} />
          <DashboardFact label="Lifecycle decision" value={governanceSummary.lifecycleDecision ?? 'Not projected'} />
          <DashboardFact label="Transfer eligibility" value={governanceSummary.transferEligibilityStatus ?? 'Not projected'} />
        </DashboardSection>

        <DashboardSection title="Operational context" action={onOpenOperationalContext ? { label: 'Open', onClick: onOpenOperationalContext } : undefined}>
          <DashboardFact label="Current context" value={hasOperationalContext ? 'Present' : 'Missing'} />
          <DashboardFact label="Revisions" value={continuityRevisionCount} />
          <DashboardFact label="Pending proposal" value={continuityPendingProposal ? 'Present' : 'None'} />
          <DashboardFact label="Latest activity" value={formatDateTime(continuityLatestActivity)} />
        </DashboardSection>

        <DashboardSection title="Reasoning" action={onOpenReasoning ? { label: 'Open', onClick: onOpenReasoning } : undefined}>
          <DashboardFact label="Events" value={reasoningSummary.eventCount} />
          <DashboardFact label="Threads" value={reasoningSummary.threadCount} />
          <DashboardFact label="Relationships" value={reasoningSummary.relationshipCount} />
          <DashboardFact label="Latest activity" value={formatDateTime(reasoningSummary.lastActivityAt)} />
        </DashboardSection>

        <DashboardSection title="Health" action={onOpenGovernance ? { label: 'Open', onClick: onOpenGovernance } : undefined}>
          <DashboardFact label="Workflow health" value={workflowHealthStatus} />
          <DashboardFact label="Governance dimensions" value={governanceSummary.healthDimensions.length} />
          <DashboardFact label="Governance findings" value={governanceHealthFindingCount} />
          <DashboardFact label="Assessed" value={formatDateTime(governanceSummary.generatedAt)} />
        </DashboardSection>

        <DashboardSection title="Certification" action={onOpenReasoning ? { label: 'Open', onClick: onOpenReasoning } : undefined}>
          <DashboardFact label="Decision certification" value={workflow?.currentDecision?.certificationStatus ?? 'Not projected'} />
          <DashboardFact label="Reasoning result" value={reasoningSummary.certificationResult ?? 'Not projected'} />
          <DashboardFact label="Latest run" value={formatDateTime(reasoningSummary.lastCertificationAt)} />
          <DashboardFact label="Current status" value={certificationStatus} />
        </DashboardSection>

        <DashboardSection title="Diagnostics" action={onOpenContinuity ? { label: 'Open', onClick: onOpenContinuity } : undefined}>
          <DashboardFact label="Workflow issues" value={workflow ? workflowDiagnosticCount : 'Not loaded'} />
          <DashboardFact label="Governance diagnostics" value={governanceDiagnosticCount} />
          <DashboardFact label="Continuity warnings" value={continuityWarningCount} />
          <DashboardFact label="Total surfaced" value={workflow ? diagnosticCount : governanceDiagnosticCount + continuityDiagnosticCount} />
        </DashboardSection>
      </div>
    </>
  )
}
