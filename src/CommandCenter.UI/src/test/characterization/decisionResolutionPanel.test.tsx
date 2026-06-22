import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DecisionResolutionPanel } from '../../features/decisions/DecisionResolutionPanel'
import type { Decision, DecisionReviewWorkspace } from '../../types'

const useDecisionResolutionMock = vi.hoisted(() => vi.fn())
const resolveMock = vi.hoisted(() => vi.fn())
const resetMock = vi.hoisted(() => vi.fn())

vi.mock('../../hooks', () => ({
  useDecisionResolution: useDecisionResolutionMock,
}))

afterEach(() => {
  cleanup()
  useDecisionResolutionMock.mockReset()
  resolveMock.mockReset()
  resetMock.mockReset()
})

describe('DecisionResolutionPanel', () => {
  it('submits explicit resolution metadata and shows recommendation override', async () => {
    const decision = createDecision()
    resolveMock.mockResolvedValue(decision)
    const onResolved = vi.fn()
    useDecisionResolutionMock.mockReturnValue({
      decision: null,
      assimilationRecommendation: null,
      isSubmitting: false,
      isAssimilationLoading: false,
      error: null,
      resolve: resolveMock,
      loadAssimilationRecommendation: vi.fn(),
      proposeAssimilationRecommendation: vi.fn(),
      reset: resetMock,
    })

    render(
      <DecisionResolutionPanel
        repositoryId="repo-alpha"
        workspace={createWorkspace()}
        isLoading={false}
        onResolved={onResolved}
      />,
    )

    fireEvent.change(screen.getByLabelText('Selected option'), {
      target: { value: 'OPT-B' },
    })
    expect(screen.getByText(/Selected option overrides the recommendation/)).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('Resolver'), {
      target: { value: 'reviewer' },
    })
    fireEvent.change(screen.getByLabelText('Rationale'), {
      target: { value: 'Accept the divergent option with explicit rationale.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Resolve Proposal' }))

    await waitFor(() => {
      expect(resolveMock).toHaveBeenCalledWith({
        outcome: 'Accepted',
        resolver: 'reviewer',
        rationale: 'Accept the divergent option with explicit rationale.',
        selectedOptionId: 'OPT-B',
      })
    })
    expect(onResolved).toHaveBeenCalledWith(decision)
  })
})

function createWorkspace(): DecisionReviewWorkspace {
  return {
    proposal: {
      id: 'PROP-0001',
      repositoryId: 'repo-alpha',
      candidateId: 'CAND-0001',
      state: 'ReadyForResolution',
      title: 'Resolve proposal authority',
      context: 'Resolution must be explicit and backend-owned.',
      options: [
        {
          id: 'OPT-A',
          title: 'Use recommendation',
          description: 'Follow the generated recommendation.',
          evidence: [],
        },
        {
          id: 'OPT-B',
          title: 'Override recommendation',
          description: 'Record a deliberate override.',
          evidence: [],
        },
      ],
      tradeoffs: [],
      recommendation: {
        optionId: 'OPT-A',
        rationale: 'Generated recommendation prefers option A.',
        evidence: [],
      },
      assumptions: [],
      evidence: [],
      history: [],
    },
    review: {
      repositoryId: 'repo-alpha',
      proposalId: 'PROP-0001',
      state: 'ReadyForResolution',
      updatedAt: '2026-06-22T17:00:00.000Z',
      reason: null,
      sources: [],
    },
    notes: [],
    revisions: [],
    diagnostics: {
      hasRecommendation: true,
      hasEvidence: false,
      optionCount: 2,
      tradeoffCount: 0,
      assumptionCount: 0,
      noteCount: 0,
      warnings: [],
    },
  }
}

function createDecision(): Decision {
  return {
    id: 'DEC-0001',
    state: 'Resolved',
    classification: 'Architectural',
    title: 'Resolve proposal authority',
    context: 'Resolution must be explicit and backend-owned.',
    metadata: {
      repositoryId: 'repo-alpha',
      createdAt: '2026-06-22T17:01:00.000Z',
      updatedAt: '2026-06-22T17:01:00.000Z',
      schemaVersion: '1',
    },
    resolution: {
      outcome: 'Accepted',
      selectedOptionId: 'OPT-B',
      rationale: 'Accept the divergent option with explicit rationale.',
      resolvedBy: 'reviewer',
      recommendationDiverged: true,
      resolvedAt: '2026-06-22T17:01:00.000Z',
      sources: [],
      sourceProposalSnapshot: null,
    },
    relationships: [],
    evidence: [],
    history: [],
  }
}
