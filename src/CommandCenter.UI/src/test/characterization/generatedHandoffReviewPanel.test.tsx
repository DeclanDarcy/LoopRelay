import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { GeneratedHandoffReviewPanel } from '../../features/execution/GeneratedHandoffReviewPanel'
import type { ExecutionSessionSummary } from '../../types'

afterEach(() => {
  cleanup()
})

function sessionSummary(overrides: Partial<ExecutionSessionSummary> = {}): ExecutionSessionSummary {
  return {
    sessionId: 'session-alpha',
    state: 'Completed',
    repositoryState: 'AwaitingAcceptance',
    milestonePath: '.agents/milestones/m8-explainability-layer.md',
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
    commitSha: null,
    committedAt: null,
    commitMessage: null,
    preparationSnapshotId: null,
    pushAttemptedAt: null,
    pushedAt: null,
    pushedCommitSha: null,
    pushRemoteName: null,
    pushBranchName: null,
    failureReason: null,
    ...overrides,
  }
}

describe('generated handoff review panel explainability characterization', () => {
  it('renders generated handoff review actions and evidence through shared explainability components', () => {
    render(
      <GeneratedHandoffReviewPanel
        canReview={true}
        execution={sessionSummary()}
        content="# Generated Handoff"
        isContentLoading={false}
        isDecisionPending={true}
        isAccepting={false}
        isRejecting={false}
        onAccept={vi.fn()}
        onReject={vi.fn()}
      />,
    )

    expect(screen.getByText('Handoff Review Actions')).toBeInTheDocument()
    expect(screen.getByText('Accept generated handoff')).toBeInTheDocument()
    expect(screen.getByText('Reject generated handoff')).toBeInTheDocument()
    expect(screen.getAllByText('Eligible')).not.toHaveLength(0)
    expect(screen.getByText('Generated Handoff Evidence')).toBeInTheDocument()
    expect(screen.getByText('Handoff')).toBeInTheDocument()
    expect(screen.getAllByText('.agents/handoffs/handoff.md')).not.toHaveLength(0)
    expect(screen.getByRole('button', { name: 'Accept Handoff' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Reject Handoff' })).toBeEnabled()
  })

  it('marks handoff review actions blocked when no decision is pending', () => {
    render(
      <GeneratedHandoffReviewPanel
        canReview={true}
        execution={sessionSummary()}
        content="# Generated Handoff"
        isContentLoading={false}
        isDecisionPending={false}
        isAccepting={false}
        isRejecting={false}
        onAccept={vi.fn()}
        onReject={vi.fn()}
      />,
    )

    expect(screen.getAllByText('Blocked')).not.toHaveLength(0)
    expect(screen.getAllByText('Reason: Generated handoff is not awaiting a review decision.')).not.toHaveLength(0)
    expect(screen.getByRole('button', { name: 'Accept Handoff' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Reject Handoff' })).toBeDisabled()
  })
})
