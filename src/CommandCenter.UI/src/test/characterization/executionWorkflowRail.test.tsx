import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ExecutionWorkflowRail } from '../../features/execution/ExecutionWorkflowRail'
import type { WorkflowInstance } from '../../types'

afterEach(() => {
  cleanup()
})

describe('execution workflow rail rendering characterization', () => {
  it('renders authoritative workflow projection facts in order', () => {
    const workflow = {
      currentStage: 'Commit',
      progressState: 'WaitingForHuman',
      blockingGate: 'CommitApproval',
      requiredHumanAction: 'Review and approve the prepared commit.',
      nextPossibleStages: ['Push'],
      validTransitions: [
        {
          transition: {
            fromStage: 'Commit',
            toStage: 'Push',
            requiredGate: 'CommitApproval',
            blockingCondition: null,
            description: 'Commit approval advances the workflow to push review.',
          },
          isValid: true,
          isBlocked: false,
          gateResolution: null,
          blockingCondition: null,
          reason: 'Commit can advance after approval.',
        },
      ],
      blockedTransitions: [],
      openGates: [
        {
          gateId: 'gate-commit',
          type: 'CommitApproval',
          repositoryId: 'repo-alpha',
          stage: 'Commit',
          status: 'Open',
          requiredAction: 'Review and approve the prepared commit.',
          satisfyingCommand: 'commit_execution',
          satisfyingCommands: ['commit_execution'],
          sourceDomain: 'Execution',
          sourceArtifact: '.agents/handoffs/handoff.md',
          createdAt: '2026-01-01T00:00:00Z',
          satisfiedAt: null,
          satisfiedActor: null,
          reason: 'Prepared commit scope requires human approval.',
          evidence: [],
        },
      ],
      diagnostics: {
        reasoning: ['Workflow selected commit because execution handoff was accepted.'],
      },
    } as unknown as WorkflowInstance

    render(<ExecutionWorkflowRail workflow={workflow} />)

    const rail = screen.getByLabelText('Execution lifecycle')
    const rows = rail.querySelectorAll('.execution-workflow-step')

    expect(Array.from(rows).map((row) => row.querySelector('span')?.textContent)).toEqual([
      'Stage: Commit',
      'Progress',
      'Gate: CommitApproval',
      'Required Action',
      'Current Transition',
    ])
    expect(rows[0]).toHaveClass('execution-workflow-step-current')
    expect(rows[1]).toHaveClass('execution-workflow-step-current')
    expect(rows[2]).toHaveClass('execution-workflow-step-blocked')
    expect(rows[3]).toHaveClass('execution-workflow-step-current')
    expect(screen.getByText('Workflow selected commit because execution handoff was accepted.')).toBeInTheDocument()
    expect(screen.getByText('Prepared commit scope requires human approval.')).toBeInTheDocument()
    expect(screen.getByText('commit_execution')).toBeInTheDocument()
    expect(screen.getByText('Review and approve the prepared commit.')).toBeInTheDocument()
    expect(screen.getByText('Commit approval advances the workflow to push review.')).toBeInTheDocument()
  })

  it('renders projection loading and error state without falling back to execution-derived steps', () => {
    render(<ExecutionWorkflowRail workflow={null} error="workflow unavailable" />)

    expect(screen.getByLabelText('Execution lifecycle')).toBeInTheDocument()
    expect(screen.getByText('Workflow')).toBeInTheDocument()
    expect(screen.getByText('workflow unavailable')).toBeInTheDocument()
  })
})
