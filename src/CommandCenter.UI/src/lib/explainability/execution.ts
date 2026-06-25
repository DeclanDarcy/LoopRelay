import type {
  ExecutionContextArtifactDiagnostic,
  ExecutionEvent,
  ExecutionGitActionEligibility,
  ExecutionGovernedConflictDiagnostic,
  ExecutionPromptManifest,
  ExecutionRepositorySnapshot,
  ExecutionSessionSummary,
  ExecutionSessionTransparency,
  ExplanationAction,
  ExplanationConstraint,
  ExplanationDiagnostic,
  ExplanationEvidence,
  ExplanationTone,
} from '../../types'
import { decisionSourceReferencesToEvidence } from './decisions'

function toneFromSeverity(severity: string): ExplanationTone {
  const normalized = severity.toLowerCase()

  if (normalized.includes('block') || normalized.includes('critical') || normalized.includes('error')) {
    return 'danger'
  }

  if (normalized.includes('warning') || normalized.includes('failed') || normalized.includes('failure') || normalized.includes('stale')) {
    return 'warning'
  }

  if (normalized.includes('info')) {
    return 'info'
  }

  return 'neutral'
}

function stringDiagnostics(values: string[], label: string, tone?: ExplanationTone): ExplanationDiagnostic[] {
  return values.map((value) => ({
    label,
    detail: value,
    tone,
  }))
}

export function executionPromptManifestToEvidence(
  manifest: ExecutionPromptManifest,
): ExplanationEvidence[] {
  return [
    {
      id: `${manifest.sessionId}-prompt-artifact`,
      label: 'Prompt artifact',
      detail: manifest.providerDeliveryStatus || 'Provider delivery status not recorded',
      source: manifest.promptArtifactPath,
    },
    {
      id: `${manifest.sessionId}-requested-context`,
      label: 'Requested context',
      detail: `${manifest.requestedContextBytes} bytes | ${manifest.requestedContextCharacters} chars | ${manifest.governedDecisionCountRequested} governed decisions`,
    },
    {
      id: `${manifest.sessionId}-delivered-context`,
      label: 'Delivered context',
      detail: `${manifest.deliveredContextBytes} bytes | ${manifest.deliveredContextCharacters} chars | ${manifest.governedDecisionCountDelivered} governed decisions`,
    },
    ...manifest.requestedArtifacts.map((artifact) => ({
      id: `${manifest.sessionId}-requested-${artifact.role}-${artifact.relativePath}`,
      label: `Requested ${artifact.role}`,
      detail: `${artifact.delivered ? 'Delivered' : 'Missing'} | ${artifact.byteCount ?? 'unknown'} bytes | ${artifact.characterCount ?? 'unknown'} chars`,
      source: artifact.relativePath,
    })),
    ...manifest.deliveredArtifacts.map((artifact) => ({
      id: `${manifest.sessionId}-delivered-${artifact.role}-${artifact.relativePath}`,
      label: `Delivered ${artifact.role}`,
      detail: `${artifact.byteCount ?? 'unknown'} bytes | ${artifact.characterCount ?? 'unknown'} chars`,
      source: artifact.relativePath,
    })),
  ]
}

export function executionPromptManifestToDiagnostics(
  manifest: ExecutionPromptManifest,
): ExplanationDiagnostic[] {
  return [
    ...(manifest.divergenceReason
      ? [
          {
            label: 'Prompt divergence',
            detail: manifest.divergenceReason,
            tone: 'warning' as const,
          },
        ]
      : []),
    ...stringDiagnostics(manifest.providerAdjustments, 'Provider adjustment', 'info'),
    ...stringDiagnostics(manifest.diagnostics, 'Prompt diagnostic'),
  ]
}

export function executionRepositorySnapshotToEvidence(
  snapshot: ExecutionRepositorySnapshot,
): ExplanationEvidence[] {
  const dirtyState = snapshot.dirtyState

  return [
    {
      label: 'Repository snapshot',
      detail: `${snapshot.branch || '(detached)'} | ${dirtyState.isClean ? 'Clean' : 'Dirty'} | ${snapshot.capturedAt}`,
    },
    ...dirtyState.stagedPaths.map((path) => ({ label: 'Staged path', detail: path, source: path })),
    ...dirtyState.modifiedPaths.map((path) => ({ label: 'Modified path', detail: path, source: path })),
    ...dirtyState.addedPaths.map((path) => ({ label: 'Added path', detail: path, source: path })),
    ...dirtyState.deletedPaths.map((path) => ({ label: 'Deleted path', detail: path, source: path })),
    ...dirtyState.renamedPaths.map((path) => ({ label: 'Renamed path', detail: path, source: path })),
    ...dirtyState.untrackedPaths.map((path) => ({ label: 'Untracked path', detail: path, source: path })),
  ]
}

export function executionArtifactDiagnosticsToExplanation(
  diagnostics: ExecutionContextArtifactDiagnostic[],
): ExplanationDiagnostic[] {
  return diagnostics.map((diagnostic) => ({
    label: `${diagnostic.role}: ${diagnostic.relativePath}`,
    detail: `${diagnostic.byteCount} bytes | ${diagnostic.characterCount} chars | warning ${diagnostic.warningThresholdBytes} | hard limit ${diagnostic.hardLimitBytes}`,
    tone: diagnostic.hardLimitExceeded ? 'danger' : diagnostic.warningThresholdExceeded ? 'warning' : 'neutral',
    evidence: [
      {
        label: 'Artifact',
        detail: diagnostic.relativePath,
        source: diagnostic.relativePath,
      },
    ],
  }))
}

export function executionEventsToDiagnostics(events: ExecutionEvent[]): ExplanationDiagnostic[] {
  return events.map((executionEvent) => ({
    label: `${executionEvent.category?.trim() || 'Monitoring'}: ${executionEvent.type}`,
    detail: executionEvent.consequence?.trim() || 'Execution monitoring recorded activity.',
    tone: toneFromSeverity(executionEvent.category ?? executionEvent.type),
    evidence: [
      {
        label: 'Event sequence',
        detail: `#${executionEvent.sequence}`,
      },
      {
        label: 'Event timestamp',
        detail: executionEvent.timestamp,
      },
      {
        label: 'Event message',
        detail: executionEvent.message,
      },
    ],
  }))
}

export function executionSessionSummaryToEvidence(
  session: ExecutionSessionSummary,
): ExplanationEvidence[] {
  return [
    {
      id: `${session.sessionId}-state`,
      label: 'Session state',
      detail: `${session.state} | ${session.repositoryState}`,
    },
    {
      id: `${session.sessionId}-milestone`,
      label: 'Milestone',
      detail: session.milestonePath ?? 'Milestone not recorded',
      source: session.milestonePath,
    },
    {
      id: `${session.sessionId}-provider`,
      label: 'Provider',
      detail: session.providerName || 'Provider not recorded',
      source: session.providerExecutablePath,
    },
    {
      id: `${session.sessionId}-handoff`,
      label: 'Handoff',
      detail: session.handoffPath ?? 'Handoff not recorded',
      source: session.handoffPath,
    },
    {
      id: `${session.sessionId}-commit`,
      label: 'Commit',
      detail: session.commitSha ?? 'Commit not recorded',
      fingerprint: session.commitSha ?? undefined,
    },
    {
      id: `${session.sessionId}-push`,
      label: 'Push',
      detail: session.pushedAt
        ? `${session.pushRemoteName ?? 'remote'}:${session.pushBranchName ?? 'branch'} at ${session.pushedAt}`
        : session.pushAttemptedAt
          ? `Attempted at ${session.pushAttemptedAt}`
          : 'Push not recorded',
      fingerprint: session.pushedCommitSha ?? undefined,
    },
  ]
}

export function executionSessionSummaryToDiagnostics(
  session: ExecutionSessionSummary,
): ExplanationDiagnostic[] {
  return [
    ...(session.failureReason
      ? [
          {
            label: 'Session failure',
            detail: session.failureReason,
            tone: 'danger' as const,
          },
        ]
      : []),
    ...(session.decisionNote
      ? [
          {
            label: 'Handoff decision note',
            detail: session.decisionNote,
            tone: 'info' as const,
          },
        ]
      : []),
  ]
}

export function generatedHandoffReviewToActions(
  session: ExecutionSessionSummary,
  isDecisionPending: boolean,
): ExplanationAction[] {
  const reason = isDecisionPending ? null : 'Generated handoff is not awaiting a review decision.'

  return [
    {
      label: 'Accept generated handoff',
      detail: session.handoffPath ?? 'Generated handoff path not recorded.',
      eligible: isDecisionPending,
      reason,
      command: 'acceptExecutionHandoff',
      constraints: generatedHandoffReviewConstraints(session, isDecisionPending),
    },
    {
      label: 'Reject generated handoff',
      detail: session.handoffPath ?? 'Generated handoff path not recorded.',
      eligible: isDecisionPending,
      reason,
      command: 'rejectExecutionHandoff',
      constraints: generatedHandoffReviewConstraints(session, isDecisionPending),
    },
  ]
}

export function executionGovernedConflictsToDiagnostics(
  conflicts: ExecutionGovernedConflictDiagnostic[],
): ExplanationDiagnostic[] {
  return conflicts.map((conflict) => ({
    label: `${conflict.severity}: ${conflict.decisionId}`,
    detail: conflict.conflictReason,
    tone: toneFromSeverity(conflict.severity),
    evidence: [
      {
        id: conflict.id,
        label: conflict.title,
        detail: conflict.statement,
      },
      {
        label: 'Conflicting excerpt',
        detail: conflict.conflictingExcerpt,
      },
      {
        label: 'Affected context',
        detail: conflict.affectedContext,
        source: conflict.affectedContext,
      },
      {
        label: 'Affected prompt section',
        detail: conflict.affectedPromptSection,
      },
      {
        label: 'Recommended resolution',
        detail: conflict.recommendedResolution,
      },
      {
        label: 'Originating authority',
        detail: conflict.originatingAuthority,
      },
      ...conflict.evidence.map((item) => ({
        label: 'Conflict evidence',
        detail: item,
      })),
      ...decisionSourceReferencesToEvidence(conflict.sources),
    ],
  }))
}

export function executionValidationErrorsToDiagnostics(
  validationErrors: string[],
): ExplanationDiagnostic[] {
  return stringDiagnostics(validationErrors, 'Validation error', 'warning')
}

export function executionGitEligibilityToActions(
  eligibility: ExecutionGitActionEligibility,
): ExplanationAction[] {
  return [
    {
      label: 'Commit execution changes',
      detail: `${eligibility.selectedPathCount} selected paths from ${eligibility.preparedPathCount} prepared paths.`,
      eligible: eligibility.canCommit,
      reason: eligibility.commitDisabledReasons.join(' ') || null,
      command: 'executionCommit',
      constraints: gitEligibilityConstraints(eligibility, 'commit'),
    },
    {
      label: 'Push execution commit',
      detail: eligibility.commitSha ?? 'No commit SHA recorded.',
      eligible: eligibility.canPush,
      reason: eligibility.pushDisabledReasons.join(' ') || null,
      command: 'executionPush',
      constraints: gitEligibilityConstraints(eligibility, 'push'),
    },
  ]
}

export function executionGitEligibilityToDiagnostics(
  eligibility: ExecutionGitActionEligibility,
): ExplanationDiagnostic[] {
  return [
    ...stringDiagnostics(eligibility.commitDisabledReasons, 'Commit disabled reason', 'warning'),
    ...stringDiagnostics(eligibility.pushDisabledReasons, 'Push disabled reason', 'warning'),
    ...eligibility.unknownSelectedPaths.map((path) => ({
      label: 'Unknown selected path',
      detail: path,
      tone: 'warning' as const,
      evidence: [{ label: 'Selected path', detail: path, source: path }],
    })),
    ...stringDiagnostics(eligibility.diagnostics, 'Git eligibility diagnostic'),
  ]
}

export function executionSessionTransparencyToDiagnostics(
  transparency: ExecutionSessionTransparency,
): ExplanationDiagnostic[] {
  const { recovery, monitoring, handoffProcessing } = transparency

  return [
    {
      label: 'Recovery state',
      detail: [
        recovery.recoveryRan ? 'Recovery ran' : 'Recovery not run',
        recovery.recoveryTrigger,
        recovery.recoveryMessage,
      ].filter(Boolean).join(' | '),
      tone: recovery.sessionMarkedFailedByRecovery || recovery.orphanedProviderState ? 'warning' : 'neutral',
    },
    {
      label: 'Monitoring state',
      detail: `${monitoring.providerProcessState} | retained events ${monitoring.retainedEventCount}`,
      tone: monitoring.staleActivity || monitoring.eventRetentionTrimmingDetected ? 'warning' : 'neutral',
    },
    ...stringDiagnostics(monitoring.monitoringWarnings, 'Monitoring warning', 'warning'),
    {
      label: 'Handoff processing',
      detail: `${handoffProcessing.resultingSessionState} | ${handoffProcessing.resultingRepositoryState}`,
      tone: handoffProcessing.validationFailure || handoffProcessing.archiveFailed ? 'warning' : 'neutral',
    },
    ...stringDiagnostics(handoffProcessing.diagnostics, 'Handoff diagnostic'),
  ]
}

function gitEligibilityConstraints(
  eligibility: ExecutionGitActionEligibility,
  mode: 'commit' | 'push',
): ExplanationConstraint[] {
  const disabledReasons =
    mode === 'commit' ? eligibility.commitDisabledReasons : eligibility.pushDisabledReasons

  return [
    {
      label: 'Session exists',
      detail: eligibility.sessionId,
      satisfied: eligibility.sessionExists,
    },
    {
      label: 'Commit preparation loaded',
      detail: eligibility.commitPreparationId ?? 'No preparation loaded',
      satisfied: eligibility.commitPreparationLoaded,
    },
    {
      label: 'Commit preparation current',
      detail: `${eligibility.preparedStatusSnapshotId ?? 'No prepared snapshot'} -> ${eligibility.currentStatusSnapshotId ?? 'No current snapshot'}`,
      satisfied: eligibility.commitPreparationCurrent,
    },
    {
      label: 'Commit message present',
      detail: eligibility.commitMessagePresent ? 'Commit message supplied' : 'Commit message missing',
      satisfied: eligibility.commitMessagePresent,
    },
    {
      label: 'Repository allows commit',
      detail: eligibility.repositoryState,
      satisfied: eligibility.repositoryAllowsCommit,
    },
    {
      label: 'Awaiting push',
      detail: eligibility.awaitingPush ? 'Repository is awaiting push' : 'Repository is not awaiting push',
      satisfied: mode === 'push' ? eligibility.awaitingPush : null,
    },
    {
      label: 'Commit SHA exists',
      detail: eligibility.commitSha ?? 'No commit SHA recorded',
      satisfied: mode === 'push' ? eligibility.commitShaExists : null,
    },
    ...disabledReasons.map((reason) => ({
      label: 'Backend disabled reason',
      detail: reason,
      satisfied: false,
    })),
  ]
}

function generatedHandoffReviewConstraints(
  session: ExecutionSessionSummary,
  isDecisionPending: boolean,
): ExplanationConstraint[] {
  return [
    {
      label: 'Session exists',
      detail: session.sessionId,
      satisfied: true,
    },
    {
      label: 'Handoff path recorded',
      detail: session.handoffPath ?? 'No handoff path recorded',
      satisfied: Boolean(session.handoffPath),
    },
    {
      label: 'Review decision pending',
      detail: isDecisionPending ? 'Awaiting review decision' : 'No review decision pending',
      satisfied: isDecisionPending,
    },
  ]
}
