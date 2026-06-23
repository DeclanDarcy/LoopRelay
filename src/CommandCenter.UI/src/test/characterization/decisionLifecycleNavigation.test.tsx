import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DecisionLifecycleTab } from '../../features/decisions/DecisionLifecycleTab'
import type {
  DecisionCandidate,
  DecisionContextSnapshot,
  DecisionProposalBrowserItem,
  DecisionReviewWorkspace,
} from '../../types'

const useDecisionProposalReviewMock = vi.hoisted(() => vi.fn())
const useDecisionProposalLineageMock = vi.hoisted(() => vi.fn())
const useDecisionProposalRefinementMock = vi.hoisted(() => vi.fn())
const useDecisionResolutionMock = vi.hoisted(() => vi.fn())
const useDecisionOptionComparisonMock = vi.hoisted(() => vi.fn())
const useDecisionEvidenceInspectionMock = vi.hoisted(() => vi.fn())
const useDecisionSourceAttributionsMock = vi.hoisted(() => vi.fn())
const useDecisionGovernanceMock = vi.hoisted(() => vi.fn())
const useDecisionCertificationMock = vi.hoisted(() => vi.fn())

vi.mock('../../hooks', () => ({
  useDecisionCertification: useDecisionCertificationMock,
  useDecisionEvidenceInspection: useDecisionEvidenceInspectionMock,
  useDecisionGovernance: useDecisionGovernanceMock,
  useDecisionOptionComparison: useDecisionOptionComparisonMock,
  useDecisionProposalLineage: useDecisionProposalLineageMock,
  useDecisionProposalReview: useDecisionProposalReviewMock,
  useDecisionProposalRefinement: useDecisionProposalRefinementMock,
  useDecisionResolution: useDecisionResolutionMock,
  useDecisionSourceAttributions: useDecisionSourceAttributionsMock,
}))

afterEach(() => {
  cleanup()
  useDecisionProposalReviewMock.mockReset()
  useDecisionProposalLineageMock.mockReset()
  useDecisionProposalRefinementMock.mockReset()
  useDecisionResolutionMock.mockReset()
  useDecisionOptionComparisonMock.mockReset()
  useDecisionEvidenceInspectionMock.mockReset()
  useDecisionSourceAttributionsMock.mockReset()
  useDecisionGovernanceMock.mockReset()
  useDecisionCertificationMock.mockReset()
})

describe('DecisionLifecycleTab navigation', () => {
  it('loads the selected proposal review workspace from proposal selection', async () => {
    useDecisionProposalReviewMock.mockImplementation((repositoryId: string | null, proposalId: string | null) => ({
      data: repositoryId && proposalId ? createWorkspace(repositoryId, proposalId) : null,
      isLoading: false,
    }))
    useDecisionProposalLineageMock.mockImplementation((repositoryId: string | null, proposalId: string | null) => ({
      data: repositoryId && proposalId ? createLineage(repositoryId, proposalId) : null,
      isLoading: false,
    }))
    useDecisionOptionComparisonMock.mockReturnValue({ data: null, isLoading: false })
    useDecisionEvidenceInspectionMock.mockReturnValue({ data: null, isLoading: false })
    useDecisionSourceAttributionsMock.mockReturnValue({ data: [], isLoading: false })
    useDecisionGovernanceMock.mockReturnValue({
      currentReport: null,
      reports: [],
      isLoading: false,
      isGenerating: false,
      error: null,
      refresh: vi.fn(),
      generateReport: vi.fn(),
    })
    useDecisionCertificationMock.mockReturnValue({
      currentReport: null,
      reports: [],
      isLoading: false,
      isRunning: false,
      error: null,
      refresh: vi.fn(),
      runCertification: vi.fn(),
    })
    useDecisionProposalRefinementMock.mockReturnValue({
      refine: vi.fn(),
      isSubmitting: false,
      error: null,
    })
    useDecisionResolutionMock.mockReturnValue({
      decision: null,
      assimilationRecommendation: null,
      isSubmitting: false,
      isAssimilationLoading: false,
      error: null,
      resolve: vi.fn(),
      loadAssimilationRecommendation: vi.fn(),
      proposeAssimilationRecommendation: vi.fn(),
      reset: vi.fn(),
    })

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
      expect(within(screen.getByLabelText('Proposal viewer')).getByText('Workspace for PROP-0001')).toBeInTheDocument()
    })

    fireEvent.click(screen.getByRole('button', { name: /Second proposal/ }))

    await waitFor(() => {
      expect(within(screen.getByLabelText('Proposal viewer')).getByText('Workspace for PROP-0002')).toBeInTheDocument()
    })
    expect(useDecisionProposalReviewMock).toHaveBeenLastCalledWith('repo-alpha', 'PROP-0002')
    expect(useDecisionProposalLineageMock).toHaveBeenLastCalledWith('repo-alpha', 'PROP-0002')
    expect(useDecisionOptionComparisonMock).toHaveBeenLastCalledWith('repo-alpha', 'PROP-0002')
    expect(useDecisionEvidenceInspectionMock).toHaveBeenLastCalledWith('repo-alpha', 'PROP-0002')
    expect(useDecisionSourceAttributionsMock).toHaveBeenLastCalledWith('repo-alpha', 'PROP-0002')
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

function createLineage(repositoryId: string, proposalId: string) {
  const workspace = createWorkspace(repositoryId, proposalId)

  return {
    repositoryId,
    proposalId,
    currentState: workspace.proposal.state,
    currentProposalFingerprint: `fingerprint-${proposalId}`,
    currentProposal: workspace.proposal,
    review: workspace.review,
    events: [],
    revisions: [],
    reviewNotes: [],
    diagnostics: [],
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
    authority: {
      proposalFingerprint: `fingerprint-${proposalId}`,
      packageId: null,
      packageFingerprint: null,
      packageVersionCreatedAt: null,
      packageSourceProposalFingerprint: null,
      isPackageCurrentForProposalContent: false,
    },
  }
}
