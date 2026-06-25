import { formatDateTime, formatDuration } from '../../lib'
import { Button, EmptyState, Panel, SectionHeader, StatusBadge } from '../../components/design'
import { DiagnosticList, EvidenceList } from '../../components/explainability'
import {
  executionPromptManifestToDiagnostics,
  executionPromptManifestToEvidence,
  executionSessionTransparencyToDiagnostics,
} from '../../lib/explainability'
import { executionSessionStatus, repositoryExecutionStatus } from '../../lib/status'
import type {
  ExecutionPromptManifest,
  ExecutionPromptManifestArtifact,
  ExecutionSessionSummary,
  ExecutionSessionTransparency,
} from '../../types'

type ExecutionSessionPanelProps = {
  session: ExecutionSessionSummary
  promptManifest?: ExecutionPromptManifest | null
  transparency?: ExecutionSessionTransparency | null
  isPromptManifestLoading?: boolean
  isTransparencyLoading?: boolean
  promptManifestError?: string | null
  transparencyError?: string | null
  onOpenMilestone?: () => void
  onOpenHandoff?: () => void
}

export function ExecutionSessionPanel({
  session,
  promptManifest = null,
  transparency = null,
  isPromptManifestLoading = false,
  isTransparencyLoading = false,
  promptManifestError = null,
  transparencyError = null,
  onOpenMilestone,
  onOpenHandoff,
}: ExecutionSessionPanelProps) {
  return (
    <Panel className="execution-session-panel" aria-label="Execution session">
      <SectionHeader
        eyebrow={session.repositoryState === 'Executing' ? 'Active Execution' : 'Execution Session'}
        title={session.milestonePath ?? 'Selected milestone'}
        headingLevel={4}
        actions={
          <div className="execution-session-actions">
            {onOpenMilestone ? (
              <Button
                type="button"
                variant="secondary"
                className="secondary-action"
                onClick={onOpenMilestone}
              >
                Milestone
              </Button>
            ) : null}
            {onOpenHandoff ? (
              <Button
                type="button"
                variant="secondary"
                className="secondary-action"
                onClick={onOpenHandoff}
              >
                Handoff
              </Button>
            ) : null}
          </div>
        }
      />
      <div className="execution-session-grid">
        <span>Session: {session.sessionId}</span>
        <span>Provider: {session.providerName || 'Unknown'}</span>
        <span>
          State: <StatusBadge status={executionSessionStatus[session.state]} />
        </span>
        <span>
          Repository state: <StatusBadge status={repositoryExecutionStatus[session.repositoryState]} />
        </span>
        <span>Started: {formatDateTime(session.startedAt)}</span>
        <span>Completed: {formatDateTime(session.completedAt)}</span>
        <span>Duration: {formatDuration(session.duration)}</span>
        <span>Accepted: {formatDateTime(session.acceptedAt)}</span>
        <span>Rejected: {formatDateTime(session.rejectedAt)}</span>
        <span>Last activity: {formatDateTime(session.lastActivityAt)}</span>
        <span>Provider start: {formatDateTime(session.providerStartedAt)}</span>
        <span>PID: {session.providerProcessId ?? 'Not recorded'}</span>
        <span>Executable: {session.providerExecutablePath || 'Not recorded'}</span>
        <span>Handoff: {session.handoffPath || 'Not recorded'}</span>
        <span>Commit: {session.commitSha || 'Not recorded'}</span>
        <span>Committed: {formatDateTime(session.committedAt)}</span>
        <span>Pushed: {formatDateTime(session.pushedAt)}</span>
        <span>Pushed commit: {session.pushedCommitSha || 'Not recorded'}</span>
        {session.failureReason ? <span className="execution-failure">Failure: {session.failureReason}</span> : null}
      </div>
      <PromptManifestSection
        manifest={promptManifest}
        isLoading={isPromptManifestLoading}
        error={promptManifestError}
      />
      <TransparencySection
        transparency={transparency}
        isLoading={isTransparencyLoading}
        error={transparencyError}
      />
    </Panel>
  )
}

type TransparencySectionProps = {
  transparency: ExecutionSessionTransparency | null
  isLoading: boolean
  error: string | null
}

function TransparencySection({ transparency, isLoading, error }: TransparencySectionProps) {
  if (isLoading && !transparency) {
    return <EmptyState className="empty-state">Loading execution transparency.</EmptyState>
  }

  if (error && !transparency) {
    return <div className="execution-rail-warning">Execution transparency: {error}</div>
  }

  if (!transparency) {
    return <EmptyState className="empty-state">No execution transparency recorded.</EmptyState>
  }

  const { recovery, monitoring, promptMetadata, handoffProcessing } = transparency
  const transparencyDiagnostics = executionSessionTransparencyToDiagnostics(transparency)

  return (
    <div className="execution-transparency" aria-label="Execution transparency">
      <DiagnosticList
        diagnostics={transparencyDiagnostics}
        title="Execution Transparency Diagnostics"
        emptyLabel="No execution transparency diagnostics recorded."
      />
      <div className="execution-rail-list">
        <h5>Recovery</h5>
        <div className="execution-rail-summary">
          <span>Recovery ran: {formatNullableBoolean(recovery.recoveryRan)}</span>
          <span>Trigger: {recovery.recoveryTrigger || 'Not recorded'}</span>
          <span>Reattach attempted: {formatNullableBoolean(recovery.reattachAttempted)}</span>
          <span>Reattach succeeded: {formatNullableBoolean(recovery.reattachSucceeded)}</span>
          <span>Orphaned provider: {formatNullableBoolean(recovery.orphanedProviderState)}</span>
          <span>Marked failed by recovery: {formatNullableBoolean(recovery.sessionMarkedFailedByRecovery)}</span>
          <span>Recovery event: {formatDateTime(recovery.recoveryEventTimestamp)}</span>
          <span>Message: {recovery.recoveryMessage || 'Not recorded'}</span>
        </div>
      </div>

      <div className="execution-rail-list">
        <h5>Monitoring</h5>
        <div className="execution-rail-summary">
          <span>Provider process: {monitoring.providerProcessState}</span>
          <span>Exit code: {monitoring.exitCode ?? 'Not recorded'}</span>
          <span>Last activity: {formatDateTime(monitoring.lastActivityAt)}</span>
          <span>Stale activity: {formatNullableBoolean(monitoring.staleActivity)}</span>
          <span>Retained events: {monitoring.retainedEventCount}</span>
          <span>First event: {monitoring.firstRetainedEventSequence ?? 'Not recorded'}</span>
          <span>Last event: {monitoring.lastRetainedEventSequence ?? 'Not recorded'}</span>
          <span>Retention trimmed: {formatNullableBoolean(monitoring.eventRetentionTrimmingDetected)}</span>
        </div>
      </div>

      <div className="execution-rail-list">
        <h5>Handoff Processing</h5>
        <div className="execution-rail-summary">
          <span>Produced: {formatNullableBoolean(handoffProcessing.handoffProduced)}</span>
          <span>Missing: {formatNullableBoolean(handoffProcessing.handoffMissing)}</span>
          <span>Archived previous: {formatNullableBoolean(handoffProcessing.handoffArchived)}</span>
          <span>Archive path: {handoffProcessing.archivePath || 'Not recorded'}</span>
          <span>Archive sequence: {handoffProcessing.archiveSequence ?? 'Not recorded'}</span>
          <span>Archive failed: {formatNullableBoolean(handoffProcessing.archiveFailed)}</span>
          <span>Validated: {formatNullableBoolean(handoffProcessing.handoffValidated)}</span>
          <span>Validation failure: {handoffProcessing.validationFailure || 'Not recorded'}</span>
          <span>Resulting session: {handoffProcessing.resultingSessionState}</span>
          <span>Resulting repository: {repositoryExecutionStatus[handoffProcessing.resultingRepositoryState]?.label ?? handoffProcessing.resultingRepositoryState}</span>
          <span>Processed: {formatDateTime(handoffProcessing.processedAt)}</span>
          <span>
            Provider failure differs:{' '}
            {formatNullableBoolean(handoffProcessing.providerFailureDistinctFromHandoffFailure)}
          </span>
          <span>Provider failure: {handoffProcessing.providerFailureReason || 'Not recorded'}</span>
          <span>Handoff failure: {handoffProcessing.handoffFailureReason || 'Not recorded'}</span>
        </div>
      </div>

      {promptMetadata ? (
        <div className="execution-rail-list">
          <h5>Prompt Metadata</h5>
          <div className="execution-rail-summary">
            <span>Generated: {formatDateTime(promptMetadata.generatedAt)}</span>
            <span>Milestone: {promptMetadata.milestonePath}</span>
            <span>Included artifacts: {promptMetadata.includedArtifactPaths.length}</span>
          </div>
        </div>
      ) : null}
    </div>
  )
}

type PromptManifestSectionProps = {
  manifest: ExecutionPromptManifest | null
  isLoading: boolean
  error: string | null
}

function PromptManifestSection({ manifest, isLoading, error }: PromptManifestSectionProps) {
  if (isLoading && !manifest) {
    return <EmptyState className="empty-state">Loading launched prompt manifest.</EmptyState>
  }

  if (error && !manifest) {
    return <div className="execution-rail-warning">Prompt manifest: {error}</div>
  }

  if (!manifest) {
    return <EmptyState className="empty-state">No launched prompt manifest recorded.</EmptyState>
  }

  return (
    <div className="execution-prompt-manifest" aria-label="Launched prompt manifest">
      <EvidenceList
        evidence={executionPromptManifestToEvidence(manifest)}
        title="Prompt Manifest Evidence"
      />
      <DiagnosticList
        diagnostics={executionPromptManifestToDiagnostics(manifest)}
        title="Prompt Manifest Diagnostics"
        emptyLabel="No prompt diagnostics recorded."
      />
      <div className="execution-rail-list">
        <h5>Launched Prompt</h5>
        <div className="execution-rail-summary">
          <span>Generated: {formatDateTime(manifest.generatedAt)}</span>
          <span>Prompt artifact: {manifest.promptArtifactPath || 'Inline manifest text'}</span>
          <span>Provider delivery: {manifest.providerDeliveryStatus || 'Not recorded'}</span>
          <span>Divergence: {manifest.divergenceReason || 'No divergence recorded'}</span>
        </div>
      </div>

      <div className="execution-prompt-context-grid">
        <PromptContextColumn
          title="Requested Context"
          byteCount={manifest.requestedContextBytes}
          characterCount={manifest.requestedContextCharacters}
          dirtyRepository={manifest.dirtyRepositoryAtRequestTime}
          governedDecisionCount={manifest.governedDecisionCountRequested}
          operationalContextSource={manifest.operationalContextSourceRequested}
          handoffSource={manifest.handoffSourceRequested}
          milestoneSource={manifest.milestoneSourceRequested}
          artifacts={manifest.requestedArtifacts}
        />
        <PromptContextColumn
          title="Delivered Context"
          byteCount={manifest.deliveredContextBytes}
          characterCount={manifest.deliveredContextCharacters}
          dirtyRepository={manifest.dirtyRepositoryAtDeliveryTime}
          governedDecisionCount={manifest.governedDecisionCountDelivered}
          operationalContextSource={manifest.operationalContextSourceDelivered}
          handoffSource={manifest.handoffSourceDelivered}
          milestoneSource={manifest.milestoneSourceDelivered}
          artifacts={manifest.deliveredArtifacts}
        />
      </div>

      <PromptStringList title="Provider Adjustments" values={manifest.providerAdjustments} empty="No provider adjustments recorded." />
    </div>
  )
}

type PromptContextColumnProps = {
  title: string
  byteCount: number
  characterCount: number
  dirtyRepository: boolean | null
  governedDecisionCount: number
  operationalContextSource: string | null
  handoffSource: string | null
  milestoneSource: string | null
  artifacts: ExecutionPromptManifestArtifact[]
}

function PromptContextColumn({
  title,
  byteCount,
  characterCount,
  dirtyRepository,
  governedDecisionCount,
  operationalContextSource,
  handoffSource,
  milestoneSource,
  artifacts,
}: PromptContextColumnProps) {
  return (
    <div className="execution-prompt-context-column">
      <h5>{title}</h5>
      <div className="execution-rail-summary">
        <span>Context bytes: {byteCount}</span>
        <span>Context characters: {characterCount}</span>
        <span>Dirty repository: {formatNullableBoolean(dirtyRepository)}</span>
        <span>Governed decisions: {governedDecisionCount}</span>
        <span>Operational context: {operationalContextSource || 'Not requested'}</span>
        <span>Handoff: {handoffSource || 'Not requested'}</span>
        <span>Milestone: {milestoneSource || 'Not requested'}</span>
      </div>
      <ArtifactManifestList artifacts={artifacts} />
    </div>
  )
}

function ArtifactManifestList({ artifacts }: { artifacts: ExecutionPromptManifestArtifact[] }) {
  if (artifacts.length === 0) {
    return <EmptyState className="empty-state">No artifacts recorded.</EmptyState>
  }

  return (
    <ul className="execution-prompt-artifact-list">
      {artifacts.map((artifact) => (
        <li key={`${artifact.role}-${artifact.relativePath}`}>
          <strong>{artifact.role}</strong>
          <span>{artifact.relativePath}</span>
          <small>
            {artifact.delivered ? 'Delivered' : 'Missing'} - {formatNullableCount(artifact.byteCount)} bytes -{' '}
            {formatNullableCount(artifact.characterCount)} chars
          </small>
        </li>
      ))}
    </ul>
  )
}

function PromptStringList({
  title,
  values,
  empty,
}: {
  title: string
  values: string[]
  empty: string
}) {
  return (
    <div className="execution-rail-list">
      <h5>{title}</h5>
      {values.length > 0 ? (
        <ul className="execution-prompt-string-list">
          {values.map((value) => (
            <li key={value}>{value}</li>
          ))}
        </ul>
      ) : (
        <EmptyState className="empty-state">{empty}</EmptyState>
      )}
    </div>
  )
}

function formatNullableBoolean(value: boolean | null) {
  if (value === null) {
    return 'Unknown'
  }

  return value ? 'Yes' : 'No'
}

function formatNullableCount(value: number | null) {
  return value === null ? 'unknown' : value
}
