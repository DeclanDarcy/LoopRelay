import { InteractionPatternView } from '../../components/explainability'
import { formatDateTime } from '../../lib'
import type {
  ExecutionRecoveryTransparency,
  ExplanationAction,
  ExplanationDiagnostic,
  ExplanationEvidence,
} from '../../types'

type ExecutionRecoveryInteractionSummaryProps = {
  recovery: ExecutionRecoveryTransparency
  sessionId: string
}

export function ExecutionRecoveryInteractionSummary({
  recovery,
  sessionId,
}: ExecutionRecoveryInteractionSummaryProps) {
  return (
    <InteractionPatternView
      actions={executionRecoveryToActions(recovery)}
      diagnostics={executionRecoveryToDiagnostics(recovery)}
      evidence={executionRecoveryToEvidence(recovery, sessionId)}
      result={executionRecoveryResult(recovery)}
      subject="Execution recovery"
      title="Recovery Interaction Summary"
    />
  )
}

function executionRecoveryToActions(recovery: ExecutionRecoveryTransparency): ExplanationAction[] {
  return [
    {
      label: 'Automatic startup recovery',
      detail: recovery.recoveryTrigger ?? 'Backend startup recovery has not recorded a trigger.',
      eligible: recovery.recoveryRan,
      reason: recovery.recoveryRan ? null : 'No backend recovery event has been recorded for this session.',
      command: null,
      constraints: [
        {
          label: 'Backend recovery observed',
          detail: recovery.recoveryRan ? 'Recovery was recorded by execution authority.' : 'No recovery event recorded.',
          satisfied: recovery.recoveryRan,
        },
        {
          label: 'Provider reattach attempted',
          detail: formatNullableBoolean(recovery.reattachAttempted),
          satisfied: recovery.reattachAttempted,
        },
        {
          label: 'Provider reattach succeeded',
          detail: formatNullableBoolean(recovery.reattachSucceeded),
          satisfied: recovery.reattachSucceeded,
        },
        {
          label: 'Orphaned provider state',
          detail: formatNullableBoolean(recovery.orphanedProviderState),
          satisfied: recovery.orphanedProviderState === null ? null : !recovery.orphanedProviderState,
        },
        {
          label: 'Session marked failed by recovery',
          detail: formatNullableBoolean(recovery.sessionMarkedFailedByRecovery),
          satisfied: !recovery.sessionMarkedFailedByRecovery,
        },
      ],
    },
  ]
}

function executionRecoveryToEvidence(
  recovery: ExecutionRecoveryTransparency,
  sessionId: string,
): ExplanationEvidence[] {
  return [
    {
      id: `${sessionId}-recovery-trigger`,
      label: 'Recovery trigger',
      detail: recovery.recoveryTrigger ?? 'Not recorded',
    },
    {
      id: `${sessionId}-recovery-event`,
      label: 'Recovery event',
      detail: formatDateTime(recovery.recoveryEventTimestamp),
    },
    {
      id: `${sessionId}-recovery-message`,
      label: 'Recovery message',
      detail: recovery.recoveryMessage ?? 'Not recorded',
    },
    {
      id: `${sessionId}-recovery-reattach`,
      label: 'Provider reattach',
      detail: `attempted ${formatNullableBoolean(recovery.reattachAttempted)} | succeeded ${formatNullableBoolean(
        recovery.reattachSucceeded,
      )}`,
    },
    {
      id: `${sessionId}-recovery-orphaned-state`,
      label: 'Provider state',
      detail: `orphaned ${formatNullableBoolean(recovery.orphanedProviderState)} | failed by recovery ${formatNullableBoolean(
        recovery.sessionMarkedFailedByRecovery,
      )}`,
    },
  ]
}

function executionRecoveryToDiagnostics(
  recovery: ExecutionRecoveryTransparency,
): ExplanationDiagnostic[] {
  const diagnostics: ExplanationDiagnostic[] = [
    {
      label: 'Recovery state',
      detail: [
        recovery.recoveryRan ? 'Recovery ran' : 'Recovery not run',
        recovery.recoveryTrigger,
        recovery.recoveryMessage,
      ].filter(Boolean).join(' | '),
      tone: recovery.sessionMarkedFailedByRecovery || recovery.orphanedProviderState ? 'warning' : 'neutral',
    },
  ]

  if (recovery.orphanedProviderState) {
    diagnostics.push({
      label: 'Orphaned provider state',
      detail: 'Recovery detected provider state that could not be reattached.',
      tone: 'warning',
    })
  }

  if (recovery.sessionMarkedFailedByRecovery) {
    diagnostics.push({
      label: 'Session marked failed',
      detail: recovery.recoveryMessage ?? 'Recovery marked the execution session failed.',
      tone: 'warning',
    })
  }

  return diagnostics
}

function executionRecoveryResult(recovery: ExecutionRecoveryTransparency) {
  if (!recovery.recoveryRan) {
    return 'No recovery has been recorded for this execution session.'
  }

  if (recovery.reattachSucceeded) {
    return 'Provider process was reattached after backend startup recovery.'
  }

  if (recovery.sessionMarkedFailedByRecovery) {
    return 'Recovery marked the session failed because provider state could not be reattached.'
  }

  return recovery.recoveryMessage ?? 'Backend startup recovery completed.'
}

function formatNullableBoolean(value: boolean | null) {
  if (value === null) {
    return 'Unknown'
  }

  return value ? 'Yes' : 'No'
}
