import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ExecutionStreamView } from '../../features/planning/ExecutionStreamView'
import { DecisionRuntimeView } from '../../features/decision/DecisionRuntimeView'
import type { DecisionRunState, ExecutionRunState } from '../../types'

afterEach(() => {
  cleanup()
})

const runningExecution: ExecutionRunState = {
  status: 'Running',
  phase: 'StartExecution',
  streamedText: 'Working…\n',
  milestoneCount: 3,
  commit: { commitSha: 'abc', pushed: true },
  handoff: null,
  completion: null,
  failure: null,
}

const completedExecution: ExecutionRunState = {
  status: 'Completed',
  phase: null,
  streamedText: 'Done.\n',
  milestoneCount: 3,
  commit: { commitSha: 'abc', pushed: true },
  handoff: { sequence: 1, path: '.agents/handoff.md' },
  completion: {
    commitSha: 'abc',
    milestoneCount: 3,
    handoffPath: '.agents/handoff.md',
    promptTokens: 100,
    outputTokens: 50,
  },
  failure: null,
}

const runningDecision: DecisionRunState = {
  status: 'Running',
  phase: 'GetNextDecisions',
  streamedText: 'Proposing…\n',
  diagnostics: { sandbox: 'read-only', approvals: 'never', seeded: true },
  proposedDecisions: null,
  editableDecisions: null,
  completion: null,
  submittedPath: null,
  submittedNumberedPath: null,
  submittedSequence: null,
  iteration: 1,
  transferring: false,
  failure: null,
}

describe('ExecutionStreamView transport states', () => {
  it('shows a Reconnecting pill in the output panel while reconnecting', () => {
    render(<ExecutionStreamView state={runningExecution} isReconnecting />)

    expect(screen.getByText('Reconnecting…')).toBeInTheDocument()
  })

  it('surfaces a recoverable transport failure instead of an indefinite spinner', () => {
    render(
      <ExecutionStreamView
        state={runningExecution}
        transportFailed
        onDismissFailure={() => undefined}
      />,
    )

    const alert = screen.getByRole('alert', { name: 'Execution connection lost' })
    expect(alert).toHaveTextContent(/connection/i)
    expect(screen.getByRole('button', { name: 'Back to plan' })).toBeInTheDocument()
  })

  it('keeps the success summary and ignores a late transport error on a Completed run', () => {
    render(
      <ExecutionStreamView
        state={completedExecution}
        transportFailed
        onDismissFailure={() => undefined}
      />,
    )

    // A transport drop after the run already completed must not contradict the success summary.
    expect(
      screen.queryByRole('alert', { name: 'Execution connection lost' }),
    ).not.toBeInTheDocument()
    expect(screen.getByRole('group', { name: 'Execution result' })).toBeInTheDocument()
  })
})

describe('DecisionRuntimeView transport states', () => {
  it('shows a Reconnecting pill in the output panel while reconnecting', () => {
    render(
      <DecisionRuntimeView
        state={runningDecision}
        isReconnecting
        onGenerate={() => undefined}
        onEditDecisions={() => undefined}
        onSubmitDecisions={() => undefined}
      />,
    )

    expect(screen.getByText('Reconnecting…')).toBeInTheDocument()
  })

  it('surfaces a recoverable transport failure instead of an indefinite spinner', () => {
    render(
      <DecisionRuntimeView
        state={runningDecision}
        transportFailed
        onGenerate={() => undefined}
        onEditDecisions={() => undefined}
        onSubmitDecisions={() => undefined}
        onDismissFailure={() => undefined}
      />,
    )

    const alert = screen.getByRole('alert', { name: 'Decision connection lost' })
    expect(alert).toHaveTextContent(/connection/i)
    expect(screen.getByRole('button', { name: 'Back to plan' })).toBeInTheDocument()
  })

  it('keeps the Continuing banner and ignores a late transport error after submit', () => {
    const submittedDecision: DecisionRunState = {
      ...runningDecision,
      status: 'Submitted',
      phase: null,
      submittedPath: '.agents/decisions/decisions.md',
    }

    render(
      <DecisionRuntimeView
        state={submittedDecision}
        transportFailed
        onGenerate={() => undefined}
        onEditDecisions={() => undefined}
        onSubmitDecisions={() => undefined}
        onDismissFailure={() => undefined}
      />,
    )

    // A transport drop after the decisions were submitted must not contradict the Continuing banner.
    expect(
      screen.queryByRole('alert', { name: 'Decision connection lost' }),
    ).not.toBeInTheDocument()
    expect(screen.getByRole('status', { name: 'Decisions submitted' })).toBeInTheDocument()
  })
})
