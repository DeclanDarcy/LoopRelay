import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
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
        eligibility={createEligibility(createWorkspace())}
        isLoading={false}
        onResolved={onResolved}
      />,
    )

    fireEvent.change(screen.getByLabelText('Selected option'), {
      target: { value: 'OPT-B' },
    })
    expect(screen.getByText(/Selected option overrides the recommendation/)).toBeInTheDocument()
    const interactionSummary = screen.getByLabelText('Resolution interaction summary')
    expect(within(interactionSummary).getByText('Resolve proposal')).toBeInTheDocument()
    expect(within(interactionSummary).getByText('Selected option differs from the backend recommendation and will be recorded.')).toBeInTheDocument()

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
        expectedProposalFingerprint: 'proposal-fingerprint-current',
        expectedPackageId: 'PKG-0001',
        expectedPackageFingerprint: 'package-fingerprint-current',
      })
    })
    expect(onResolved).toHaveBeenCalledWith(decision)
  })

  it('shows reviewed package authority and stale authority conflicts', () => {
    useDecisionResolutionMock.mockReturnValue({
      decision: null,
      assimilationRecommendation: null,
      isSubmitting: false,
      isAssimilationLoading: false,
      error: 'Resolution authority is stale: reviewed package content does not match the current proposal.',
      resolve: resolveMock,
      loadAssimilationRecommendation: vi.fn(),
      proposeAssimilationRecommendation: vi.fn(),
      reset: resetMock,
    })

    render(
      <DecisionResolutionPanel
        repositoryId="repo-alpha"
        workspace={createWorkspace({
          packageSourceProposalFingerprint: 'old-proposal-fingerprint',
          isPackageCurrentForProposalContent: false,
        })}
        isLoading={false}
        onResolved={vi.fn()}
      />,
    )

    expect(screen.getByLabelText('Reviewed package authority')).toHaveTextContent('PKG-0001')
    expect(screen.getByLabelText('Package authority warning')).toHaveTextContent(
      'Reviewed package content does not match the current proposal.',
    )
    expect(screen.getByRole('alert', { name: 'Resolution error' })).toHaveTextContent(
      'Resolution authority is stale',
    )
  })
})

function createWorkspace(
  authority: Partial<DecisionReviewWorkspace['authority']> = {},
): DecisionReviewWorkspace {
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
    authority: {
      proposalFingerprint: 'proposal-fingerprint-current',
      packageId: 'PKG-0001',
      packageFingerprint: 'package-fingerprint-current',
      packageVersionCreatedAt: '2026-06-22T17:00:30.000Z',
      packageSourceProposalFingerprint: 'proposal-fingerprint-current',
      isPackageCurrentForProposalContent: true,
      ...authority,
    },
  }
}

function createEligibility(workspace: DecisionReviewWorkspace) {
  return {
    entityKind: 'Proposal',
    entityId: workspace.proposal.id,
    currentState: workspace.proposal.state,
    allowedActions: [
      {
        commandName: 'resolve_decision_proposal',
        displayName: 'Resolve proposal',
        targetState: 'Resolved',
        isAllowed: workspace.proposal.state === 'ReadyForResolution',
        requiredInputs: ['resolver', 'rationale', 'selectedOptionId'],
        reason: null,
        governingRule: 'proposal-resolution',
      },
    ],
    blockedActions: [],
    allowedNextStates: ['Resolved'],
    blockedNextStates: [],
    diagnostics: ['Resolution eligibility projected by backend lifecycle rules.'],
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
