import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ExecutionWorkflowRail } from '../../features/execution/ExecutionWorkflowRail'
import type { WorkflowInstance } from '../../types'

afterEach(() => {
  cleanup()
})

describe('execution workflow rail rendering characterization', () => {
  it('renders a contextual workflow summary without duplicating primary workflow lifecycle rows', async () => {
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
    const onOpenWorkflow = vi.fn()

    render(<ExecutionWorkflowRail workflow={workflow} onOpenWorkflow={onOpenWorkflow} />)

    const summary = screen.getByLabelText('Execution workflow summary')
    const rows = summary.querySelectorAll('.execution-workflow-step')

    expect(rows).toHaveLength(0)
    expect(screen.getByText('Commit')).toBeInTheDocument()
    expect(screen.getByText('WaitingForHuman')).toBeInTheDocument()
    expect(summary).toHaveTextContent('Blocking gate: CommitApproval')
    expect(summary).toHaveTextContent('Required action: Review and approve the prepared commit.')
    expect(summary).toHaveTextContent('Open gates: 1')
    expect(summary).toHaveTextContent('Next stages: Push')

    fireEvent.click(screen.getByRole('button', { name: 'Workflow' }))

    expect(onOpenWorkflow).toHaveBeenCalledTimes(1)
  })

  it('renders projection loading and error state without falling back to execution-derived steps', () => {
    render(<ExecutionWorkflowRail workflow={null} error="workflow unavailable" />)

    expect(screen.getByLabelText('Execution workflow summary')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Workflow' })).toBeInTheDocument()
    expect(screen.getByText('workflow unavailable')).toBeInTheDocument()
  })
})
