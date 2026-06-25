import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { DecisionProposalViewer } from '../../features/decisions/DecisionProposalViewer'
import type { DecisionLifecycleEntityEligibility, DecisionReviewWorkspace } from '../../types'

afterEach(() => {
  cleanup()
})

describe('DecisionProposalViewer', () => {
  it('renders the full proposal review workspace without mutation controls', () => {
    render(<DecisionProposalViewer workspace={createWorkspace()} isLoading={false} />)

    expect(screen.getByText('Use backend-owned review read models')).toBeInTheDocument()
    expect(screen.getByText('Proposal context from backend read model.')).toBeInTheDocument()
    expect(screen.getByText('Render a read-only backend workspace')).toBeInTheDocument()
    expect(screen.getByText('Reviewers can inspect the proposal before mutation controls exist.')).toBeInTheDocument()
    expect(screen.getByText('Use the backend review workspace as the source of truth.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /resolve/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /refine/i })).not.toBeInTheDocument()
  })

  it('keeps evidence and source attribution visible near supported content', () => {
    render(<DecisionProposalViewer workspace={createWorkspace()} isLoading={false} />)

    const recommendation = screen.getByLabelText('Decision recommendation')
    expect(
      within(recommendation).getByText('Human authority should see evidence before mutation.'),
    ).toBeInTheDocument()
    expect(within(recommendation).getByText('.agents/plan.md')).toBeInTheDocument()

    const optionEvidence = screen.getByLabelText('Option Evidence')
    expect(within(optionEvidence).getByText('The current slice authorizes inspection-only UI.')).toBeInTheDocument()
    expect(within(optionEvidence).getByText('Milestone 4')).toBeInTheDocument()
  })

  it('shows review notes separately from proposal revisions', () => {
    render(<DecisionProposalViewer workspace={createWorkspace()} isLoading={false} />)

    const notes = screen.getByLabelText('Review notes')
    expect(within(notes).getByText('Keep mutation controls out until evidence is visible.')).toBeInTheDocument()

    const revisions = screen.getByLabelText('Proposal revisions')
    expect(within(revisions).getByText('REV-0001')).toBeInTheDocument()
    expect(within(revisions).getByText('context, options')).toBeInTheDocument()
  })

  it('renders backend review state, last transition, and unavailable transition reasons', () => {
    render(
      <DecisionProposalViewer
        workspace={createWorkspace()}
        eligibility={createEligibility()}
        isLoading={false}
      />,
    )

    const reviewState = screen.getByLabelText('Proposal review state')

    expect(within(reviewState).getByText('Current review state')).toBeInTheDocument()
    expect(within(reviewState).getAllByText('Viewed')).toHaveLength(2)
    expect(within(reviewState).getByText(/Generated -> Viewed/)).toBeInTheDocument()
    expect(within(reviewState).getByText('Reviewer opened the generated proposal.')).toBeInTheDocument()
    expect(within(reviewState).getByText('Allowed transitions')).toBeInTheDocument()
    expect(within(reviewState).getByText('NeedsRefinement')).toBeInTheDocument()

    const unavailableReasons = screen.getByLabelText('Proposal unavailable transition reasons')
    expect(within(unavailableReasons).getByText('Ready for resolution')).toBeInTheDocument()
    expect(
      within(unavailableReasons).getByText('Proposal must be refined before resolution readiness.'),
    ).toBeInTheDocument()
    expect(
      within(unavailableReasons).getByText('DecisionLifecycleRules.ValidateProposalTransition'),
    ).toBeInTheDocument()
  })
})

function createWorkspace(): DecisionReviewWorkspace {
  const source = {
    sourceKind: 'Plan',
    relativePath: '.agents/plan.md',
    section: 'Milestone 4',
    itemId: null,
    decisionId: null,
    proposalId: 'PROP-0001',
    candidateId: 'CAND-0001',
    excerpt: 'Provide full proposal inspection before refinement or resolution.',
  }

  return {
    proposal: {
      id: 'PROP-0001',
      repositoryId: 'repo-alpha',
      candidateId: 'CAND-0001',
      state: 'Generated',
      title: 'Use backend-owned review read models',
      context: 'Proposal context from backend read model.',
      options: [
        {
          id: 'OPT-A',
          title: 'Render a read-only backend workspace',
          description: 'Load and display the selected proposal review workspace.',
          evidence: [
            {
              summary: 'The current slice authorizes inspection-only UI.',
              sources: [source],
            },
          ],
        },
      ],
      tradeoffs: [
        {
          optionId: 'OPT-A',
          benefit: 'Reviewers can inspect the proposal before mutation controls exist.',
          cost: 'The UI needs a larger read-only surface.',
          evidence: [
            {
              summary: 'M4 exit criteria require full proposal inspection.',
              sources: [source],
            },
          ],
        },
      ],
      recommendation: {
        optionId: 'OPT-A',
        rationale: 'Use the backend review workspace as the source of truth.',
        evidence: [
          {
            summary: 'Human authority should see evidence before mutation.',
            sources: [source],
          },
        ],
      },
      assumptions: [
        {
          id: 'ASM-1',
          statement: 'The backend review workspace includes diagnostics.',
          evidence: [
            {
              summary: 'Diagnostics are returned with the workspace.',
              sources: [source],
            },
          ],
        },
      ],
      evidence: [
        {
          summary: 'Proposal-level evidence remains visible.',
          sources: [source],
        },
      ],
      history: [
        {
          at: '2026-06-22T17:02:00.000Z',
          actor: 'DecisionProposalReviewService',
          action: 'MarkViewed',
          fromState: 'Generated',
          toState: 'Viewed',
          reason: 'Reviewer opened the generated proposal.',
          sources: [source],
        },
      ],
    },
    review: {
      repositoryId: 'repo-alpha',
      proposalId: 'PROP-0001',
      state: 'Viewed',
      updatedAt: '2026-06-22T17:00:00.000Z',
      reason: 'Review workspace loaded.',
      sources: [source],
    },
    notes: [
      {
        id: 'NOTE-0001',
        repositoryId: 'repo-alpha',
        proposalId: 'PROP-0001',
        createdAt: '2026-06-22T17:00:00.000Z',
        reviewer: 'reviewer',
        body: 'Keep mutation controls out until evidence is visible.',
        sources: [source],
      },
    ],
    revisions: [
      {
        id: 'REV-0001',
        repositoryId: 'repo-alpha',
        proposalId: 'PROP-0001',
        createdAt: '2026-06-22T17:01:00.000Z',
        reason: 'Initial generated review workspace revision.',
        changedFields: ['context', 'options'],
        sourceProposalFingerprint: 'mock-fingerprint',
        sources: [source],
        requestedBy: 'reviewer',
        acceptedChanges: [],
        rejectedChanges: [],
        diagnostics: [],
        previousOptions: [],
        retiredOptions: [],
        previousAssumptions: [],
        retiredAssumptions: [],
        previousRecommendationRationale: null,
        revisedRecommendationRationale: null,
        previousContext: null,
        revisedContext: null,
        revisedOptions: [],
        previousTradeoffs: [],
        revisedTradeoffs: [],
        revisedAssumptions: [],
        humanAuthoringBurden: 'MinorEdit',
      },
    ],
    diagnostics: {
      hasRecommendation: true,
      hasEvidence: true,
      optionCount: 1,
      tradeoffCount: 1,
      assumptionCount: 1,
      noteCount: 1,
      warnings: [],
    },
    authority: {
      proposalFingerprint: 'proposal-fingerprint-current',
      packageId: 'PKG-0001',
      packageFingerprint: 'package-fingerprint-current',
      packageVersionCreatedAt: '2026-06-22T17:01:30.000Z',
      packageSourceProposalFingerprint: 'proposal-fingerprint-current',
      isPackageCurrentForProposalContent: true,
    },
  }
}

function createEligibility(): DecisionLifecycleEntityEligibility {
  return {
    entityKind: 'Proposal',
    entityId: 'PROP-0001',
    currentState: 'Viewed',
    allowedActions: [
      {
        commandName: 'mark_decision_proposal_needs_refinement',
        displayName: 'Needs refinement',
        targetState: 'NeedsRefinement',
        isAllowed: true,
        requiredInputs: ['reason'],
        reason: null,
        governingRule: 'DecisionLifecycleRules.ValidateProposalTransition',
      },
    ],
    blockedActions: [
      {
        commandName: 'mark_decision_proposal_ready_for_resolution',
        displayName: 'Ready for resolution',
        targetState: 'ReadyForResolution',
        isAllowed: false,
        requiredInputs: ['reason'],
        reason: 'Proposal must be refined before resolution readiness.',
        governingRule: 'DecisionLifecycleRules.ValidateProposalTransition',
      },
    ],
    allowedNextStates: ['NeedsRefinement'],
    blockedNextStates: [
      {
        state: 'ReadyForResolution',
        reason: 'Proposal must be refined before resolution readiness.',
        governingRule: 'DecisionLifecycleRules.ValidateProposalTransition',
      },
    ],
    diagnostics: ['Review transition eligibility loaded from backend lifecycle rules.'],
  }
}
