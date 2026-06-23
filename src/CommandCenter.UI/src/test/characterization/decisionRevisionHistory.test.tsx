import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { DecisionRevisionHistory } from '../../features/decisions/DecisionRevisionHistory'
import type { DecisionProposalLineage } from '../../types'

afterEach(() => {
  cleanup()
})

describe('DecisionRevisionHistory', () => {
  it('renders lineage as navigation and keeps current proposal authority separate', () => {
    render(<DecisionRevisionHistory lineage={createLineage()} isLoading={false} />)

    expect(screen.getByLabelText('Current proposal authority')).toHaveTextContent(
      'Authoritative proposal content is loaded from the backend current proposal projection.',
    )
    expect(screen.getByText('Current proposal is authoritative; revisions are historical.')).toBeInTheDocument()

    const revisionList = screen.getByLabelText('Revision list')
    expect(within(revisionList).getByRole('button', { name: /REV-0001/ })).toBeInTheDocument()
    expect(within(revisionList).getByText(/1 retired items/)).toBeInTheDocument()

    const comparison = screen.getByLabelText('Revision comparison')
    expect(within(comparison).getByText('Historical')).toBeInTheDocument()
    expect(within(comparison).getByText('Source fingerprint differs from current proposal')).toBeInTheDocument()
    expect(within(comparison).getByText('backend summary before refinement')).toBeInTheDocument()
    expect(within(comparison).getByText('backend summary after refinement')).toBeInTheDocument()
  })

  it('switches comparison details from backend-provided revision snapshots', () => {
    render(<DecisionRevisionHistory lineage={createLineage()} isLoading={false} />)

    fireEvent.click(screen.getByRole('button', { name: /REV-0002/ }))

    const comparison = screen.getByLabelText('Revision comparison')
    expect(within(comparison).getByText('recommendationRationale')).toBeInTheDocument()
    expect(within(comparison).getByText('old rationale from backend comparison')).toBeInTheDocument()
    expect(within(comparison).getByText('new rationale from backend comparison')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /refine/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /resolve/i })).not.toBeInTheDocument()
  })
})

function createLineage(): DecisionProposalLineage {
  const source = {
    sourceKind: 'Plan',
    relativePath: '.agents/plan.md',
    section: 'Milestone 5',
    itemId: null,
    decisionId: null,
    proposalId: 'PROP-0001',
    candidateId: 'CAND-0001',
    excerpt: 'Render proposal lineage without client-side lifecycle authority.',
  }

  const currentProposal = {
    id: 'PROP-0001',
    repositoryId: 'repo-alpha',
    candidateId: 'CAND-0001',
    state: 'Refined' as const,
    title: 'Use backend lineage projection',
    context: 'Current proposal context from backend.',
    options: [],
    tradeoffs: [],
    recommendation: null,
    assumptions: [],
    evidence: [],
    history: [],
  }

  return {
    repositoryId: 'repo-alpha',
    proposalId: 'PROP-0001',
    currentState: 'Refined',
    currentProposalFingerprint: 'current-fingerprint',
    currentProposal,
    review: {
      repositoryId: 'repo-alpha',
      proposalId: 'PROP-0001',
      state: 'NeedsRefinement',
      updatedAt: '2026-06-22T17:00:00.000Z',
      reason: 'Reviewer requested clarity.',
      sources: [source],
    },
    events: [
      {
        occurredAt: '2026-06-22T17:00:00.000Z',
        kind: 'Revision',
        itemId: 'REV-0001',
        summary: 'Added current/historical boundary.',
        fromState: null,
        toState: 'Refined',
        sources: [source],
      },
    ],
    revisions: [
      {
        revision: {
          id: 'REV-0001',
          repositoryId: 'repo-alpha',
          proposalId: 'PROP-0001',
          createdAt: '2026-06-22T17:01:00.000Z',
          reason: 'Added current/historical boundary.',
          changedFields: ['context'],
          sourceProposalFingerprint: 'source-fingerprint-1',
          sources: [source],
          requestedBy: 'reviewer',
          acceptedChanges: ['Clarified boundary'],
          rejectedChanges: [],
          diagnostics: [],
          previousOptions: [],
          retiredOptions: [],
          previousAssumptions: [],
          retiredAssumptions: [],
          previousRecommendationRationale: null,
          revisedRecommendationRationale: null,
          previousContext: 'backend summary before refinement',
          revisedContext: 'backend summary after refinement',
          revisedOptions: [],
          previousTradeoffs: [],
          revisedTradeoffs: [],
          revisedAssumptions: [],
          humanAuthoringBurden: 'MinorEdit',
        },
        comparison: {
          proposalId: 'PROP-0001',
          revisionId: 'REV-0001',
          repositoryId: 'repo-alpha',
          sourceProposalFingerprint: 'source-fingerprint-1',
          currentProposalFingerprint: 'current-fingerprint',
          sourceMatchesCurrentProposal: false,
          changedFields: ['context'],
          fieldComparisons: [
            {
              field: 'context',
              changeType: 'Changed',
              previousValue: 'backend summary before refinement',
              revisedValue: 'backend summary after refinement',
            },
          ],
          acceptedChanges: ['Clarified boundary'],
          rejectedChanges: [],
          diagnostics: [],
          previousOptions: [],
          revisedOptions: [],
          retiredOptions: [{ id: 'OPT-OLD', title: 'Retired option', description: 'Old path.', evidence: [] }],
          previousAssumptions: [],
          revisedAssumptions: [],
          retiredAssumptions: [],
          previousTradeoffs: [],
          revisedTradeoffs: [],
          humanAuthoringBurden: 'MinorEdit',
          sources: [source],
        },
        isCurrentProposal: false,
        authorityBoundary: 'Historical revision is read-only; current proposal remains authoritative.',
      },
      {
        revision: {
          id: 'REV-0002',
          repositoryId: 'repo-alpha',
          proposalId: 'PROP-0001',
          createdAt: '2026-06-22T17:02:00.000Z',
          reason: 'Changed recommendation rationale.',
          changedFields: ['recommendationRationale'],
          sourceProposalFingerprint: 'source-fingerprint-2',
          sources: [source],
          requestedBy: 'reviewer',
          acceptedChanges: ['Updated rationale'],
          rejectedChanges: [],
          diagnostics: [],
          previousOptions: [],
          retiredOptions: [],
          previousAssumptions: [],
          retiredAssumptions: [],
          previousRecommendationRationale: 'old rationale from backend comparison',
          revisedRecommendationRationale: 'new rationale from backend comparison',
          previousContext: null,
          revisedContext: null,
          revisedOptions: [],
          previousTradeoffs: [],
          revisedTradeoffs: [],
          revisedAssumptions: [],
          humanAuthoringBurden: 'MinorEdit',
        },
        comparison: {
          proposalId: 'PROP-0001',
          revisionId: 'REV-0002',
          repositoryId: 'repo-alpha',
          sourceProposalFingerprint: 'source-fingerprint-2',
          currentProposalFingerprint: 'current-fingerprint',
          sourceMatchesCurrentProposal: false,
          changedFields: ['recommendationRationale'],
          fieldComparisons: [
            {
              field: 'recommendationRationale',
              changeType: 'Changed',
              previousValue: 'old rationale from backend comparison',
              revisedValue: 'new rationale from backend comparison',
            },
          ],
          acceptedChanges: ['Updated rationale'],
          rejectedChanges: [],
          diagnostics: [],
          previousOptions: [],
          revisedOptions: [],
          retiredOptions: [],
          previousAssumptions: [],
          revisedAssumptions: [],
          retiredAssumptions: [],
          previousTradeoffs: [],
          revisedTradeoffs: [],
          humanAuthoringBurden: 'MinorEdit',
          sources: [source],
        },
        isCurrentProposal: false,
        authorityBoundary: 'Historical revision is read-only; current proposal remains authoritative.',
      },
    ],
    reviewNotes: [],
    diagnostics: ['Current proposal is authoritative; revisions are historical.'],
  }
}
