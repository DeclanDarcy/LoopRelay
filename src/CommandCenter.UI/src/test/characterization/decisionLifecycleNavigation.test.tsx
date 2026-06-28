import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DecisionLifecycleTab } from '../../features/decisions/DecisionLifecycleTab'
import type {
  DecisionCandidate,
  DecisionContext,
  DecisionLifecycleEligibilityProjection,
  DecisionLifecycleEntityEligibility,
  DecisionProposal,
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
const useDecisionGenerationCertificationMock = vi.hoisted(() => vi.fn())
const useDecisionQualityMock = vi.hoisted(() => vi.fn())

vi.mock('../../hooks', () => ({
  useDecisionCertification: useDecisionCertificationMock,
  useDecisionEvidenceInspection: useDecisionEvidenceInspectionMock,
  useDecisionGenerationCertification: useDecisionGenerationCertificationMock,
  useDecisionGovernance: useDecisionGovernanceMock,
  useDecisionOptionComparison: useDecisionOptionComparisonMock,
  useDecisionProposalLineage: useDecisionProposalLineageMock,
  useDecisionProposalReview: useDecisionProposalReviewMock,
  useDecisionProposalRefinement: useDecisionProposalRefinementMock,
  useDecisionQuality: useDecisionQualityMock,
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
  useDecisionGenerationCertificationMock.mockReset()
  useDecisionQualityMock.mockReset()
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
    useDecisionGenerationCertificationMock.mockReturnValue({
      currentReport: null,
      reports: [],
      isLoading: false,
      isRunning: false,
      error: null,
      refresh: vi.fn(),
      runCertification: vi.fn(),
    })
    useDecisionQualityMock.mockReturnValue({
      assessments: [],
      currentReport: null,
      reports: [],
      currentTrend: null,
      trends: [],
      isLoading: false,
      isAssessing: false,
      isGeneratingReport: false,
      isGeneratingTrend: false,
      error: null,
      refresh: vi.fn(),
      assessProposal: vi.fn(),
      generateReport: vi.fn(),
      generateTrend: vi.fn(),
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

    // The live DecisionContext exposes items at the top level; the summary must
    // render its count without crashing on the (removed) snapshot wrapper.
    expect(screen.getByText('1 context items')).toBeInTheDocument()

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

  it('renders backend proposal lifecycle eligibility and disables blocked review transitions', async () => {
    const markViewed = vi.fn()
    const markNeedsRefinement = vi.fn()
    useDecisionProposalReviewMock.mockImplementation((repositoryId: string | null, proposalId: string | null) => ({
      data: repositoryId && proposalId ? createWorkspace(repositoryId, proposalId) : null,
      isLoading: false,
      isMutating: false,
      refresh: vi.fn(),
      markViewed,
      markNeedsRefinement,
      markReadyForResolution: vi.fn(),
    }))
    useDecisionProposalLineageMock.mockReturnValue({ data: null, isLoading: false, refresh: vi.fn() })
    useDecisionOptionComparisonMock.mockReturnValue({ data: null, isLoading: false, refresh: vi.fn() })
    useDecisionEvidenceInspectionMock.mockReturnValue({ data: null, isLoading: false, refresh: vi.fn() })
    useDecisionSourceAttributionsMock.mockReturnValue({ data: [], isLoading: false, refresh: vi.fn() })
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
    useDecisionGenerationCertificationMock.mockReturnValue({
      currentReport: null,
      reports: [],
      isLoading: false,
      isRunning: false,
      error: null,
      refresh: vi.fn(),
      runCertification: vi.fn(),
    })
    useDecisionQualityMock.mockReturnValue({
      assessments: [],
      currentReport: null,
      reports: [],
      currentTrend: null,
      trends: [],
      isLoading: false,
      isAssessing: false,
      isGeneratingReport: false,
      isGeneratingTrend: false,
      error: null,
      refresh: vi.fn(),
      assessProposal: vi.fn(),
      generateReport: vi.fn(),
      generateTrend: vi.fn(),
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
        lifecycleEligibility={createLifecycleEligibility()}
        onSelectedProposalStatesChange={vi.fn()}
        onRefresh={vi.fn()}
      />,
    )

    await waitFor(() => {
      expect(screen.getByLabelText('Proposal interaction summary')).toHaveTextContent('Mark viewed')
    })

    const interaction = screen.getByLabelText('Proposal interaction summary')
    expect(within(interaction).getByText('Action subject')).toBeInTheDocument()
    expect(within(interaction).getByText('Proposal PROP-0001: Generated')).toBeInTheDocument()
    expect(within(interaction).getByText('Result')).toBeInTheDocument()
    expect(within(interaction).getByText('No proposal lifecycle command result recorded.')).toBeInTheDocument()
    expect(within(interaction).getByText('Action Eligibility')).toBeInTheDocument()
    expect(within(interaction).getByText('Interaction Evidence')).toBeInTheDocument()
    expect(within(interaction).getByText('Current state')).toBeInTheDocument()
    expect(within(interaction).getByText('Proposal PROP-0001 is Generated.')).toBeInTheDocument()
    expect(within(interaction).getByText('Interaction Diagnostics')).toBeInTheDocument()
    expect(
      within(screen.getByLabelText('Proposal lifecycle actions')).getByText(
        'Generated proposals must be viewed before refinement.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Mark Viewed' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Needs Refinement' })).toBeDisabled()
  })

  it('renders generated proposal output and selects the generated proposal', async () => {
    const generatedProposal = createGeneratedProposal()
    const onGenerateProposal = vi.fn().mockResolvedValue(generatedProposal)
    useDecisionProposalReviewMock.mockImplementation((repositoryId: string | null, proposalId: string | null) => ({
      data: repositoryId && proposalId ? createWorkspace(repositoryId, proposalId) : null,
      isLoading: false,
      isMutating: false,
      refresh: vi.fn(),
      markViewed: vi.fn(),
      markNeedsRefinement: vi.fn(),
      markReadyForResolution: vi.fn(),
    }))
    useDecisionProposalLineageMock.mockImplementation((repositoryId: string | null, proposalId: string | null) => ({
      data: repositoryId && proposalId ? createLineage(repositoryId, proposalId) : null,
      isLoading: false,
      refresh: vi.fn(),
    }))
    useDecisionOptionComparisonMock.mockReturnValue({ data: null, isLoading: false, refresh: vi.fn() })
    useDecisionEvidenceInspectionMock.mockReturnValue({ data: null, isLoading: false, refresh: vi.fn() })
    useDecisionSourceAttributionsMock.mockReturnValue({ data: [], isLoading: false, refresh: vi.fn() })
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
    useDecisionGenerationCertificationMock.mockReturnValue({
      currentReport: null,
      reports: [],
      isLoading: false,
      isRunning: false,
      error: null,
      refresh: vi.fn(),
      runCertification: vi.fn(),
    })
    useDecisionQualityMock.mockReturnValue({
      assessments: [],
      currentReport: null,
      reports: [],
      currentTrend: null,
      trends: [],
      isLoading: false,
      isAssessing: false,
      isGeneratingReport: false,
      isGeneratingTrend: false,
      error: null,
      refresh: vi.fn(),
      assessProposal: vi.fn(),
      generateReport: vi.fn(),
      generateTrend: vi.fn(),
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
        lifecycleEligibility={createLifecycleEligibilityWithGeneration()}
        onSelectedProposalStatesChange={vi.fn()}
        onRefresh={vi.fn()}
        onGenerateProposal={onGenerateProposal}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Generate Decision Proposal' }))

    await waitFor(() => {
      expect(onGenerateProposal).toHaveBeenCalledWith('CAND-0001')
      expect(screen.getByLabelText('Generated proposal summary')).toHaveTextContent('Generated proposal PROP-0099')
      expect(screen.getByLabelText('Generated proposal summary')).toHaveTextContent('Generation mode PreferredOption')
      expect(screen.getByLabelText('Generated proposal summary')).toHaveTextContent('2 accepted options')
      expect(screen.getByLabelText('Generated proposal summary')).toHaveTextContent('1 rejected options')
      expect(screen.getByLabelText('Generated proposal summary')).toHaveTextContent('1 deduplicated options')
      expect(screen.getByLabelText('Generation validation diagnostics')).toHaveTextContent('OPT-B: Option lacks evidence.')
      expect(screen.getByLabelText('Generation command diagnostics')).toHaveTextContent('Generated from promoted candidate evidence.')
      expect(useDecisionProposalReviewMock).toHaveBeenLastCalledWith('repo-alpha', 'PROP-0099')
    })
  })

  it('renders decision supersede and archive eligibility and submits required fields', async () => {
    const onSupersedeDecision = vi.fn()
    const onArchiveDecision = vi.fn()
    const onRefreshExecutionProjection = vi.fn()
    useDecisionProposalReviewMock.mockReturnValue({
      data: null,
      isLoading: false,
      isMutating: false,
      refresh: vi.fn(),
      markViewed: vi.fn(),
      markNeedsRefinement: vi.fn(),
      markReadyForResolution: vi.fn(),
    })
    useDecisionProposalLineageMock.mockReturnValue({ data: null, isLoading: false, refresh: vi.fn() })
    useDecisionOptionComparisonMock.mockReturnValue({ data: null, isLoading: false, refresh: vi.fn() })
    useDecisionEvidenceInspectionMock.mockReturnValue({ data: null, isLoading: false, refresh: vi.fn() })
    useDecisionSourceAttributionsMock.mockReturnValue({ data: [], isLoading: false, refresh: vi.fn() })
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
    useDecisionGenerationCertificationMock.mockReturnValue({
      currentReport: null,
      reports: [],
      isLoading: false,
      isRunning: false,
      error: null,
      refresh: vi.fn(),
      runCertification: vi.fn(),
    })
    useDecisionQualityMock.mockReturnValue({
      assessments: [],
      currentReport: null,
      reports: [],
      currentTrend: null,
      trends: [],
      isLoading: false,
      isAssessing: false,
      isGeneratingReport: false,
      isGeneratingTrend: false,
      error: null,
      refresh: vi.fn(),
      assessProposal: vi.fn(),
      generateReport: vi.fn(),
      generateTrend: vi.fn(),
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
        lifecycleEligibility={createLifecycleEligibilityWithDecisions()}
        onSelectedProposalStatesChange={vi.fn()}
        onRefresh={vi.fn()}
        onSupersedeDecision={onSupersedeDecision}
        onArchiveDecision={onArchiveDecision}
        onRefreshExecutionProjection={onRefreshExecutionProjection}
      />,
    )

    const decisionSelection = screen.getByLabelText('Resolved decision selection')
    const decisionSelects = decisionSelection.querySelectorAll('select')

    const interaction = screen.getByLabelText('Decision interaction summary')
    expect(within(interaction).getByText('Action subject')).toBeInTheDocument()
    expect(within(interaction).getByText('Decision DEC-0001: Resolved')).toBeInTheDocument()
    expect(within(interaction).getByText('Result')).toBeInTheDocument()
    expect(within(interaction).getByText('No decision lifecycle command result recorded.')).toBeInTheDocument()
    expect(within(interaction).getByText('Action Eligibility')).toBeInTheDocument()
    expect(within(interaction).getByText('Supersede')).toBeInTheDocument()
    expect(within(interaction).getByText('Archive')).toBeInTheDocument()
    expect(within(interaction).getByText('Interaction Evidence')).toBeInTheDocument()
    expect(within(interaction).getByText('Current state')).toBeInTheDocument()
    expect(within(interaction).getByText('Decision DEC-0001 is Resolved.')).toBeInTheDocument()
    expect(within(interaction).getByText('Interaction Diagnostics')).toBeInTheDocument()
    fireEvent.change(decisionSelects[0], {
      target: { value: 'DEC-0003' },
    })
    expect(screen.getByLabelText('Decision interaction summary')).toHaveTextContent('Decision DEC-0003: Archived')
    expect(screen.getByText('Archived decisions cannot transition.')).toBeInTheDocument()
    fireEvent.change(decisionSelects[0], {
      target: { value: 'DEC-0001' },
    })

    fireEvent.change(decisionSelects[1], {
      target: { value: 'DEC-0002' },
    })
    expect(screen.getByLabelText('Decision interaction summary')).toHaveTextContent('Selected replacement decision')
    expect(screen.getByLabelText('Decision interaction summary')).toHaveTextContent('DEC-0002')
    fireEvent.change(screen.getByLabelText('Rationale'), {
      target: { value: 'DEC-0002 replaces the earlier architecture choice.' },
    })
    fireEvent.change(screen.getByLabelText('Resolver'), {
      target: { value: 'reviewer' },
    })

    fireEvent.click(screen.getByRole('button', { name: 'Supersede' }))

    await waitFor(() => {
      expect(onSupersedeDecision).toHaveBeenCalledWith(
        'DEC-0001',
        'DEC-0002',
        'DEC-0002 replaces the earlier architecture choice.',
        'reviewer',
      )
      expect(onRefreshExecutionProjection).toHaveBeenCalledTimes(1)
    })

    fireEvent.change(screen.getByLabelText('Rationale'), {
      target: { value: 'Terminal decision no longer participates in execution.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Archive' }))

    await waitFor(() => {
      expect(onArchiveDecision).toHaveBeenCalledWith(
        'DEC-0001',
        'Terminal decision no longer participates in execution.',
        'reviewer',
      )
      expect(onRefreshExecutionProjection).toHaveBeenCalledTimes(2)
    })
  })
})

// The GET /decisions/context endpoint returns a live DecisionContext (bare, no
// snapshot wrapper); POST returns the persisted DecisionContextSnapshot. The tab
// consumes the live context, so the fixture must match that shape.
function createContext(): DecisionContext {
  return {
    repositoryId: 'repo-alpha',
    fingerprint: 'context-fingerprint',
    items: [
      {
        id: 'context-goal',
        kind: 'CurrentDecisionMarkdown',
        title: 'Current decisions',
        content: 'Decision lifecycle UI requires backend-owned state.',
        required: false,
        fingerprint: 'context-goal-fingerprint',
        sources: [],
      },
    ],
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

function createLifecycleEligibility(): DecisionLifecycleEligibilityProjection {
  return {
    repositoryId: 'repo-alpha',
    candidates: [],
    proposals: [
      createProposalEligibility('PROP-0001', [
        createAction('mark_decision_proposal_viewed', 'Mark viewed', 'Viewed', true, null),
      ], [
        createAction(
          'mark_decision_proposal_needs_refinement',
          'Needs refinement',
          'NeedsRefinement',
          false,
          'Generated proposals must be viewed before refinement.',
        ),
      ]),
    ],
    decisions: [],
    diagnostics: [],
  }
}

function createLifecycleEligibilityWithGeneration(): DecisionLifecycleEligibilityProjection {
  return {
    ...createLifecycleEligibility(),
    candidates: [
      {
        entityKind: 'Candidate',
        entityId: 'CAND-0001',
        currentState: 'Promoted',
        allowedActions: [
          createAction('generate_decision_proposal', 'Generate proposal', 'Generated', true, null),
        ],
        blockedActions: [],
        allowedNextStates: ['Generated'],
        blockedNextStates: [],
        diagnostics: [],
      },
    ],
  }
}

function createGeneratedProposal(): DecisionProposal {
  return {
    id: 'PROP-0099',
    repositoryId: 'repo-alpha',
    candidateId: 'CAND-0001',
    state: 'Generated',
    title: 'Generated proposal from candidate evidence',
    context: 'Generated context.',
    options: [],
    tradeoffs: [],
    recommendation: {
      optionId: 'OPT-A',
      rationale: 'Generated recommendation.',
      evidence: [],
      mode: 'PreferredOption',
    },
    assumptions: [],
    evidence: [],
    history: [],
    generationDiagnostics: {
      generatedOptionCount: 4,
      acceptedOptionCount: 2,
      rejectedOptionCount: 1,
      deduplicatedOptionCount: 1,
      fallbackOptionCount: 0,
      optionValidationResults: [
        { optionId: 'OPT-A', isValid: true, issues: [] },
        {
          optionId: 'OPT-B',
          isValid: false,
          issues: [{ type: 'MissingEvidence', message: 'Option lacks evidence.' }],
        },
      ],
      diagnostics: ['Generated from promoted candidate evidence.'],
    },
  }
}

function createLifecycleEligibilityWithDecisions(): DecisionLifecycleEligibilityProjection {
  return {
    ...createLifecycleEligibility(),
    decisions: [
      createDecisionEligibility('DEC-0001', 'Resolved', [
        createAction('supersede_decision', 'Supersede', 'Superseded', true, null),
        createAction('archive_decision', 'Archive', 'Archived', true, null),
      ], []),
      createDecisionEligibility('DEC-0002', 'Resolved', [
        createAction('archive_decision', 'Archive', 'Archived', true, null),
      ], [
        createAction(
          'supersede_decision',
          'Supersede',
          'Superseded',
          false,
          'Replacement decision must be different from the source decision.',
        ),
      ]),
      createDecisionEligibility('DEC-0003', 'Archived', [], [
        createAction(
          'archive_decision',
          'Archive',
          'Archived',
          false,
          'Archived decisions cannot transition.',
        ),
      ]),
    ],
  }
}

function createProposalEligibility(
  entityId: string,
  allowedActions: DecisionLifecycleEntityEligibility['allowedActions'],
  blockedActions: DecisionLifecycleEntityEligibility['blockedActions'],
): DecisionLifecycleEntityEligibility {
  return {
    entityKind: 'Proposal',
    entityId,
    currentState: 'Generated',
    allowedActions,
    blockedActions,
    allowedNextStates: allowedActions.map((action) => action.targetState),
    blockedNextStates: [],
    diagnostics: [],
  }
}

function createDecisionEligibility(
  entityId: string,
  currentState: string,
  allowedActions: DecisionLifecycleEntityEligibility['allowedActions'],
  blockedActions: DecisionLifecycleEntityEligibility['blockedActions'],
): DecisionLifecycleEntityEligibility {
  return {
    entityKind: 'Decision',
    entityId,
    currentState,
    allowedActions,
    blockedActions,
    allowedNextStates: allowedActions.map((action) => action.targetState),
    blockedNextStates: [],
    diagnostics: [],
  }
}

function createAction(
  commandName: string,
  displayName: string,
  targetState: string,
  isAllowed: boolean,
  reason: string | null,
): DecisionLifecycleEntityEligibility['allowedActions'][number] {
  return {
    commandName,
    displayName,
    targetState,
    isAllowed,
    requiredInputs: ['reason'],
    reason,
    governingRule: 'DecisionLifecycleRules.ValidateProposalTransition',
  }
}
