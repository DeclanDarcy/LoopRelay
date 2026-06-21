import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ExecutionHistoryPanel } from '../../features/execution/ExecutionHistoryPanel'
import type { ExecutionSessionSummary } from '../../types'

afterEach(() => {
  cleanup()
})

function sessionSummary(overrides: Partial<ExecutionSessionSummary> = {}): ExecutionSessionSummary {
  return {
    sessionId: 'session-alpha',
    state: 'Completed',
    repositoryState: 'AwaitingCommit',
    milestonePath: '.agents/milestones/m0.md',
    startedAt: '2026-06-21T16:00:00.000Z',
    completedAt: '2026-06-21T16:20:00.000Z',
    duration: '00:20:00',
    acceptedAt: null,
    rejectedAt: null,
    decisionNote: null,
    lastActivityAt: '2026-06-21T16:20:00.000Z',
    providerName: 'codex',
    providerExecutablePath: null,
    providerProcessId: null,
    providerStartedAt: null,
    handoffPath: '.agents/handoffs/handoff.md',
    commitSha: 'abc1234',
    committedAt: '2026-06-21T16:25:00.000Z',
    commitMessage: 'Update handoff',
    preparationSnapshotId: 'snapshot-alpha',
    pushAttemptedAt: null,
    pushedAt: null,
    pushedCommitSha: null,
    pushRemoteName: null,
    pushBranchName: null,
    failureReason: null,
    ...overrides,
  }
}

describe('execution history panel rendering characterization', () => {
  it('omits the panel when no sessions are provided', () => {
    render(<ExecutionHistoryPanel sessions={[]} />)

    expect(screen.queryByRole('region', { name: 'Execution history' })).not.toBeInTheDocument()
  })

  it('renders history rows in provided order with current labels and fallbacks', () => {
    render(
      <ExecutionHistoryPanel
        sessions={[
          sessionSummary({
            sessionId: 'session-first',
            milestonePath: '.agents/milestones/m1.md',
            repositoryState: 'AwaitingPush',
            commitSha: 'def5678',
            pushedAt: '2026-06-21T17:00:00.000Z',
          }),
          sessionSummary({
            sessionId: 'session-second',
            milestonePath: null,
            repositoryState: 'Failed',
            startedAt: null,
            duration: null,
            commitSha: null,
            pushedAt: null,
          }),
        ]}
      />,
    )

    expect(screen.getByRole('region', { name: 'Execution history' })).toBeInTheDocument()
    expect(screen.getByText('Session History')).toHaveClass('eyebrow')
    expect(screen.getByRole('heading', { level: 4, name: '2 recent sessions' })).toBeInTheDocument()

    const rows = document.querySelectorAll('.execution-history-row')
    expect(Array.from(rows).map((row) => row.querySelector('span')?.textContent)).toEqual([
      '.agents/milestones/m1.md',
      'Milestone not recorded',
    ])

    expect(within(rows[0] as HTMLElement).getByText('Awaiting push')).toBeInTheDocument()
    expect(within(rows[0] as HTMLElement).getByText('Commit def5678')).toBeInTheDocument()
    expect(within(rows[0] as HTMLElement).getByText(/^Push /)).toBeInTheDocument()
    expect(within(rows[1] as HTMLElement).getByText('Failed')).toBeInTheDocument()
    expect(within(rows[1] as HTMLElement).getByText('Started Not recorded')).toBeInTheDocument()
    expect(within(rows[1] as HTMLElement).getByText('Duration Not recorded')).toBeInTheDocument()
    expect(within(rows[1] as HTMLElement).getByText('Commit Not recorded')).toBeInTheDocument()
    expect(within(rows[1] as HTMLElement).getByText('Push Not recorded')).toBeInTheDocument()
  })

  it('can render rows as navigation-only controls when a session callback is supplied', () => {
    const session = sessionSummary({ sessionId: 'session-link' })
    const onOpenSession = vi.fn()

    render(<ExecutionHistoryPanel sessions={[session]} onOpenSession={onOpenSession} />)

    fireEvent.click(screen.getByRole('button', { name: /m0\.md/ }))

    expect(onOpenSession).toHaveBeenCalledTimes(1)
    expect(onOpenSession).toHaveBeenCalledWith(session)
  })
})
