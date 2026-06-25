import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ExecutionSessionPanel } from '../../features/execution/ExecutionSessionPanel'
import type { ExecutionPromptManifest, ExecutionSessionSummary } from '../../types'

afterEach(() => {
  cleanup()
})

function sessionSummary(overrides: Partial<ExecutionSessionSummary> = {}): ExecutionSessionSummary {
  return {
    sessionId: 'session-alpha',
    state: 'Executing',
    repositoryState: 'Executing',
    milestonePath: '.agents/milestones/m0.md',
    startedAt: '2026-06-21T16:00:00.000Z',
    completedAt: null,
    duration: null,
    acceptedAt: null,
    rejectedAt: null,
    decisionNote: null,
    lastActivityAt: '2026-06-21T16:12:00.000Z',
    providerName: 'codex',
    providerExecutablePath: 'C:\\tools\\codex.exe',
    providerProcessId: 42,
    providerStartedAt: '2026-06-21T16:00:10.000Z',
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

function promptManifest(): ExecutionPromptManifest {
  return {
    sessionId: 'session-alpha',
    generatedAt: '2026-06-21T16:00:30.000Z',
    promptText: 'Launched prompt text',
    promptArtifactPath: null,
    requestedArtifacts: [
      {
        role: 'Milestone',
        relativePath: '.agents/milestones/m0.md',
        byteCount: 128,
        characterCount: 128,
        delivered: true,
      },
      {
        role: 'CurrentHandoff',
        relativePath: '.agents/handoffs/handoff.md',
        byteCount: null,
        characterCount: null,
        delivered: false,
      },
    ],
    requestedContextBytes: 128,
    requestedContextCharacters: 128,
    deliveredArtifacts: [
      {
        role: 'Milestone',
        relativePath: '.agents/milestones/m0.md',
        byteCount: 128,
        characterCount: 128,
        delivered: true,
      },
    ],
    deliveredContextBytes: 128,
    deliveredContextCharacters: 128,
    dirtyRepositoryAtRequestTime: true,
    dirtyRepositoryAtDeliveryTime: true,
    governedDecisionCountRequested: 4,
    governedDecisionCountDelivered: 4,
    operationalContextSourceRequested: '.agents/operational_context.md',
    operationalContextSourceDelivered: '.agents/operational_context.md',
    handoffSourceRequested: '.agents/handoffs/handoff.md',
    handoffSourceDelivered: null,
    milestoneSourceRequested: '.agents/milestones/m0.md',
    milestoneSourceDelivered: '.agents/milestones/m0.md',
    providerDeliveryStatus: 'Delivered',
    providerAdjustments: [],
    divergenceReason: null,
    diagnostics: ['NoProviderDivergenceSignal'],
  }
}

describe('execution session panel rendering characterization', () => {
  it('renders the active execution session summary fields', () => {
    render(<ExecutionSessionPanel session={sessionSummary()} />)

    expect(screen.getByRole('region', { name: 'Execution session' })).toBeInTheDocument()
    expect(screen.getByText('Active Execution')).toHaveClass('eyebrow')
    expect(screen.getByRole('heading', { level: 4, name: '.agents/milestones/m0.md' })).toBeInTheDocument()
    expect(screen.getByText('Session: session-alpha')).toBeInTheDocument()
    expect(screen.getByText('Provider: codex')).toBeInTheDocument()
    expect(screen.getAllByText('Executing')[0]).toHaveClass('cc-badge', 'cc-badge-warning')
    expect(screen.getAllByText('Executing')[1]).toHaveClass('cc-badge', 'cc-badge-warning')
    expect(screen.getByText('PID: 42')).toBeInTheDocument()
    expect(screen.getByText('Executable: C:\\tools\\codex.exe')).toBeInTheDocument()
    expect(screen.getByText('Handoff: .agents/handoffs/handoff.md')).toBeInTheDocument()
  })

  it('preserves fallback values for completed sessions with missing optional fields', () => {
    render(
      <ExecutionSessionPanel
        session={sessionSummary({
          state: 'Completed',
          repositoryState: 'AwaitingCommit',
          milestonePath: null,
          providerName: '',
          providerExecutablePath: null,
          providerProcessId: null,
          handoffPath: null,
          commitSha: null,
          pushedCommitSha: null,
          failureReason: 'Commit preparation failed',
        })}
      />,
    )

    expect(screen.getByText('Execution Session')).toHaveClass('eyebrow')
    expect(screen.getByRole('heading', { level: 4, name: 'Selected milestone' })).toBeInTheDocument()
    expect(screen.getByText('Provider: Unknown')).toBeInTheDocument()
    expect(screen.getByText('Awaiting commit')).toHaveClass('cc-badge', 'cc-badge-warning')
    expect(screen.getByText('Duration: Not recorded')).toBeInTheDocument()
    expect(screen.getByText('PID: Not recorded')).toBeInTheDocument()
    expect(screen.getByText('Executable: Not recorded')).toBeInTheDocument()
    expect(screen.getByText('Handoff: Not recorded')).toBeInTheDocument()
    expect(screen.getByText('Commit: Not recorded')).toBeInTheDocument()
    expect(screen.getByText('Pushed commit: Not recorded')).toBeInTheDocument()
    expect(screen.getByText('Failure: Commit preparation failed')).toHaveClass('execution-failure')
  })

  it('renders requested and delivered launched prompt manifest context distinctly', () => {
    render(<ExecutionSessionPanel session={sessionSummary()} promptManifest={promptManifest()} />)

    expect(screen.getByLabelText('Launched prompt manifest')).toBeInTheDocument()
    expect(screen.getByText('Launched Prompt')).toBeInTheDocument()
    expect(screen.getByText('Provider delivery: Delivered')).toBeInTheDocument()
    expect(screen.getByText('Requested Context')).toBeInTheDocument()
    expect(screen.getByText('Delivered Context')).toBeInTheDocument()
    expect(screen.getAllByText('Context bytes: 128')).toHaveLength(2)
    expect(screen.getAllByText('Governed decisions: 4')).toHaveLength(2)
    expect(screen.getByText('Missing - unknown bytes - unknown chars')).toBeInTheDocument()
    expect(screen.getByText('No provider adjustments recorded.')).toBeInTheDocument()
    expect(screen.getByText('NoProviderDivergenceSignal')).toBeInTheDocument()
  })
})
