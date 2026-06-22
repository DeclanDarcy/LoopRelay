import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DecisionLifecycleTab } from '../../features/decisions/DecisionLifecycleTab'
import type {
  DecisionCandidate,
  DecisionContextSnapshot,
  DecisionProposalBrowserItem,
  DecisionReviewWorkspace,
} from '../../types'

const useDecisionProposalReviewMock = vi.hoisted(() => vi.fn())

vi.mock('../../hooks', () => ({
  useDecisionProposalReview: useDecisionProposalReviewMock,
}))

afterEach(() => {
  cleanup()
  useDecisionProposalReviewMock.mockReset()
})

describe('DecisionLifecycleTab navigation', () => {
  it('loads the selected proposal review workspace from proposal selection', async () => {
    useDecisionProposalReviewMock.mockImplementation((repositoryId: string | null, proposalId: string | null) => ({
      data: repositoryId && proposalId ? createWorkspace(repositoryId, proposalId) : null,
      isLoading: false,
    }))

    render(
      <DecisionLifecycleTab
        context={createContext()}
        candidates={[createCandidate()]}
        proposals={createProposals()}
        selectedProposalStates={[]}
        hasSelectedRepository
        isLoading={false}
        repositoryId="repo-alpha"
        onSelectedProposalStatesChange={vi.fn()}
        onRefresh={vi.fn()}
      />,
    )

    await waitFor(() => {
      expect(screen.getByText('Workspace for PROP-0001')).toBeInTheDocument()
    })

    fireEvent.click(screen.getByRole('button', { name: /Second proposal/ }))

    await waitFor(() => {
      expect(screen.getByText('Workspace for PROP-0002')).toBeInTheDocument()
    })
    expect(useDecisionProposalReviewMock).toHaveBeenLastCalledWith('repo-alpha', 'PROP-0002')
  })
})

function createContext(): DecisionContextSnapshot {
  return {
    snapshotId: 'context-1',
    repositoryId: 'repo-alpha',
    createdAt: '2026-06-22T17:00:00.000Z',
    fingerprint: 'context-fingerprint',
    context: {
      repositoryId: 'repo-alpha',
      fingerprint: 'context-fingerprint',
      items: [],
      diagnostics: { sources: [], warnings: [] },
      validation: { isValid: true, errors: [], warnings: [] },
    },
    diagnostics: { sources: [], warnings: [] },
    validation: { isValid: true, errors: [], warnings: [] },
  }
}

function createCandidate(): DecisionCandidate {
  return {
    id: 'CAND-0001',
    repositoryId: 'repo-alpha',
    state: 'Promoted',
    priority: 'High',
    classification: 'Architectural',
    title: 'Review workspace boundary',
    summary: 'Backend-owned read models feed the UI.',
    sourceFingerprint: 'candidate-fingerprint',
    signals: [],
    evidence: [],
    sources: [],
    diagnostics: [],
    history: [],
  }
}

function createProposals(): DecisionProposalBrowserItem[] {
  return [
    createProposal('PROP-0001', 'First proposal'),
    createProposal('PROP-0002', 'Second proposal'),
  ]
}

function createProposal(proposalId: string, title: string): DecisionProposalBrowserItem {
  return {
    proposalId,
    candidateId: 'CAND-0001',
    state: 'Generated',
    title,
    classification: 'Architectural',
    priority: 'High',
    createdAt: '2026-06-22T17:00:00.000Z',
    updatedAt: '2026-06-22T17:01:00.000Z',
    reviewState: 'NotStarted',
    reviewUpdatedAt: '2026-06-22T17:01:00.000Z',
    isResolved: false,
  }
}

function createWorkspace(repositoryId: string, proposalId: string): DecisionReviewWorkspace {
  return {
    proposal: {
      id: proposalId,
      repositoryId,
      candidateId: 'CAND-0001',
      state: 'Generated',
      title: `Workspace for ${proposalId}`,
      context: `Review workspace context for ${proposalId}.`,
      options: [],
      tradeoffs: [],
      recommendation: null,
      assumptions: [],
      evidence: [],
      history: [],
    },
    review: {
      repositoryId,
      proposalId,
      state: 'NotStarted',
      updatedAt: '2026-06-22T17:01:00.000Z',
      reason: null,
      sources: [],
    },
    notes: [],
    revisions: [],
    diagnostics: {
      hasRecommendation: false,
      hasEvidence: false,
      optionCount: 0,
      tradeoffCount: 0,
      assumptionCount: 0,
      noteCount: 0,
      warnings: [],
    },
  }
}
