import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DecisionRefinementPanel } from '../../features/decisions/DecisionRefinementPanel'
import type { DecisionProposal, DecisionProposalLineage, DecisionReviewWorkspace } from '../../types'

const refineMock = vi.hoisted(() => vi.fn())
const useDecisionProposalRefinementMock = vi.hoisted(() => vi.fn())

vi.mock('../../hooks', () => ({
  useDecisionProposalRefinement: useDecisionProposalRefinementMock,
}))

afterEach(() => {
  cleanup()
  refineMock.mockReset()
  useDecisionProposalRefinementMock.mockReset()
})

describe('DecisionRefinementPanel', () => {
  it('submits a structured backend refinement request and refreshes after success', async () => {
    const workspace = createWorkspace('NeedsRefinement')
    const refinedProposal = { ...workspace.proposal, state: 'Refined' as const }
    const onRefined = vi.fn()
    refineMock.mockResolvedValue(refinedProposal)
    useDecisionProposalRefinementMock.mockReturnValue({
      refine: refineMock,
      isSubmitting: false,
      error: null,
    })

    render(
      <DecisionRefinementPanel
        repositoryId="repo-alpha"
        workspace={workspace}
        lineage={createLineage(workspace)}
        isLoading={false}
        onRefined={onRefined}
      />,
    )

    fireEvent.change(screen.getByLabelText('Reason'), {
      target: { value: 'Narrow recommendation rationale.' },
    })
    fireEvent.change(screen.getByLabelText('Requested by'), {
      target: { value: 'reviewer' },
    })
    fireEvent.change(screen.getByLabelText('Replacement context'), {
      target: { value: 'Refined context from reviewer.' },
    })
    fireEvent.change(screen.getByLabelText('Recommendation rationale'), {
      target: { value: 'Refined rationale from reviewer.' },
    })
    fireEvent.change(screen.getByLabelText('Rejected changes'), {
      target: { value: 'Do not resolve during refinement.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Submit Refinement' }))

    await waitFor(() => {
      expect(refineMock).toHaveBeenCalledWith({
        reason: 'Narrow recommendation rationale.',
        requestedBy: 'reviewer',
        baseProposalFingerprint: 'fingerprint-PROP-0001',
        context: 'Refined context from reviewer.',
        recommendation: {
          ...workspace.proposal.recommendation,
          rationale: 'Refined rationale from reviewer.',
        },
        rejectedChanges: ['Do not resolve during refinement.'],
      })
    })
    await waitFor(() => {
      expect(onRefined).toHaveBeenCalledWith(refinedProposal)
    })
    expect(screen.getByText('Refinement submitted for PROP-0001.')).toBeInTheDocument()
  })

  it('disables submission until backend proposal state is needs refinement', () => {
    useDecisionProposalRefinementMock.mockReturnValue({
      refine: refineMock,
      isSubmitting: false,
      error: null,
    })

    render(
      <DecisionRefinementPanel
        repositoryId="repo-alpha"
        workspace={createWorkspace('Generated')}
        lineage={null}
        isLoading={false}
        onRefined={vi.fn()}
      />,
    )

    expect(screen.getByText('Backend state Generated is not open for refinement.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Submit Refinement' })).toBeDisabled()
  })

  it('renders backend refinement errors without mutating local proposal state', () => {
    useDecisionProposalRefinementMock.mockReturnValue({
      refine: refineMock,
      isSubmitting: false,
      error: 'Refinement base proposal fingerprint is stale.',
    })

    render(
      <DecisionRefinementPanel
        repositoryId="repo-alpha"
        workspace={createWorkspace('NeedsRefinement')}
        lineage={createLineage(createWorkspace('NeedsRefinement'))}
        isLoading={false}
        onRefined={vi.fn()}
      />,
    )

    expect(screen.getByRole('alert')).toHaveTextContent('Refinement base proposal fingerprint is stale.')
    expect(screen.getByText('NeedsRefinement')).toBeInTheDocument()
  })
})

function createLineage(workspace: DecisionReviewWorkspace): DecisionProposalLineage {
  return {
    repositoryId: workspace.proposal.repositoryId,
    proposalId: workspace.proposal.id,
    currentState: workspace.proposal.state,
    currentProposalFingerprint: `fingerprint-${workspace.proposal.id}`,
    currentProposal: workspace.proposal,
    review: workspace.review,
    events: [],
    revisions: [],
    reviewNotes: [],
    diagnostics: [],
  }
}

function createWorkspace(state: DecisionProposal['state']): DecisionReviewWorkspace {
  const proposal: DecisionProposal = {
    id: 'PROP-0001',
    repositoryId: 'repo-alpha',
    candidateId: 'CAND-0001',
    state,
    title: 'Refine backend-owned proposal',
    context: 'Current context.',
    options: [
      {
        id: 'OPT-A',
        title: 'Keep backend authority',
        description: 'Submit structured requests only.',
        evidence: [],
      },
    ],
    tradeoffs: [],
    recommendation: {
      optionId: 'OPT-A',
      rationale: 'Current rationale.',
      evidence: [],
    },
    assumptions: [],
    evidence: [],
    history: [],
  }

  return {
    proposal,
    review: {
      repositoryId: 'repo-alpha',
      proposalId: proposal.id,
      state: state === 'NeedsRefinement' ? 'NeedsRefinement' : 'NotStarted',
      updatedAt: '2026-06-22T17:00:00.000Z',
      reason: null,
      sources: [],
    },
    notes: [],
    revisions: [],
    diagnostics: {
      hasRecommendation: true,
      hasEvidence: false,
      optionCount: 1,
      tradeoffCount: 0,
      assumptionCount: 0,
      noteCount: 0,
      warnings: [],
    },
  }
}
