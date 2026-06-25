import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DecisionRefinementPanel } from '../../features/decisions/DecisionRefinementPanel'
import type { DecisionProposal, DecisionProposalLineage, DecisionReviewWorkspace } from '../../types'

const refineMock = vi.hoisted(() => vi.fn())
const analyzeMock = vi.hoisted(() => vi.fn())
const regenerateMock = vi.hoisted(() => vi.fn())
const useDecisionProposalRefinementMock = vi.hoisted(() => vi.fn())

vi.mock('../../hooks', () => ({
  useDecisionProposalRefinement: useDecisionProposalRefinementMock,
}))

afterEach(() => {
  cleanup()
  refineMock.mockReset()
  analyzeMock.mockReset()
  regenerateMock.mockReset()
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
      analyze: analyzeMock,
      regenerate: regenerateMock,
      isSubmitting: false,
      error: null,
    })

    render(
      <DecisionRefinementPanel
        repositoryId="repo-alpha"
        workspace={workspace}
        lineage={createLineage(workspace)}
        eligibility={createEligibility(workspace)}
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
    const interactionSummary = screen.getByLabelText('Refinement interaction summary')
    expect(within(interactionSummary).getByText('Submit refinement')).toBeInTheDocument()
    expect(within(interactionSummary).getByText('Proposal PROP-0001 is NeedsRefinement.')).toBeInTheDocument()
    expect(within(interactionSummary).getByText('Refinement command succeeded for PROP-0001.')).toBeInTheDocument()
    expect(screen.getByText('Refinement submitted for PROP-0001.')).toBeInTheDocument()
  })

  it('analyzes directive guidance and regenerates a package with visible comparison output', async () => {
    const workspace = createWorkspace('NeedsRefinement')
    const plan = {
      repositoryId: 'repo-alpha',
      proposalId: 'PROP-0001',
      analyzedAt: '2026-06-22T17:01:00.000Z',
      baseProposalFingerprint: 'fingerprint-PROP-0001',
      directives: [
        {
          id: 'DIR-0001',
          type: 'AddConstraint' as const,
          summary: 'Add or tighten a review constraint.',
          targetOptionId: null,
          targetField: 'Constraints',
          instruction: 'Must reduce risk and recommend again.',
          sources: [],
        },
        {
          id: 'DIR-0002',
          type: 'ReevaluateRecommendation' as const,
          summary: 'Reevaluate the recommendation.',
          targetOptionId: null,
          targetField: 'Recommendation',
          instruction: 'Must reduce risk and recommend again.',
          sources: [],
        },
      ],
      regenerateOptions: false,
      reevaluateTradeoffs: true,
      reevaluateRecommendation: true,
      fullRegeneration: false,
      appliedConstraints: ['Must reduce risk and recommend again.'],
      diagnostics: ['Analyzed reviewer guidance.'],
    }
    const result = {
      repositoryId: 'repo-alpha',
      proposalId: 'PROP-0001',
      plan,
      basePackageVersion: createPackageVersion(workspace, 'PKG-0001', 'package-fingerprint-current', 'Current rationale.'),
      regeneratedPackageVersion: createPackageVersion(
        workspace,
        'PKG-0002',
        'package-fingerprint-regenerated',
        'Regenerated rationale from directives.',
      ),
      comparison: {
        proposalId: 'PROP-0001',
        leftPackageId: 'PKG-0001',
        rightPackageId: 'PKG-0002',
        repositoryId: 'repo-alpha',
        leftPackageFingerprint: 'package-fingerprint-current',
        rightPackageFingerprint: 'package-fingerprint-regenerated',
        recommendationChanged: true,
        optionsChanged: false,
        evidenceChanged: true,
        risksChanged: true,
        contextFingerprintChanged: false,
        fieldComparisons: [],
        addedOptions: [],
        removedOptions: [],
        modifiedOptions: [],
        addedEvidence: ['Must reduce risk and recommend again.'],
        removedEvidence: [],
        addedRisks: ['Reevaluated risk.'],
        removedRisks: [],
        diagnostics: [],
      },
      humanAuthoringBurden: 'MajorRefinement' as const,
      diagnostics: [],
      refinementArtifact: null,
    }
    analyzeMock.mockResolvedValue(plan)
    regenerateMock.mockResolvedValue(result)
    useDecisionProposalRefinementMock.mockReturnValue({
      refine: refineMock,
      analyze: analyzeMock,
      regenerate: regenerateMock,
      isSubmitting: false,
      error: null,
    })

    render(
      <DecisionRefinementPanel
        repositoryId="repo-alpha"
        workspace={workspace}
        lineage={createLineage(workspace)}
        eligibility={createEligibility(workspace)}
        isLoading={false}
        onRefined={vi.fn()}
      />,
    )

    fireEvent.change(screen.getByLabelText('Requested by'), {
      target: { value: 'reviewer' },
    })
    fireEvent.change(screen.getByLabelText('Reviewer guidance'), {
      target: { value: 'Must reduce risk and recommend again.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Analyze Guidance' }))

    await waitFor(() => {
      expect(analyzeMock).toHaveBeenCalledWith({
        guidance: 'Must reduce risk and recommend again.',
        requestedBy: 'reviewer',
        baseProposalFingerprint: 'fingerprint-PROP-0001',
      })
    })
    expect(await screen.findByLabelText('Refinement plan')).toHaveTextContent('Tradeoffs, Recommendation')

    fireEvent.click(screen.getByRole('button', { name: 'Regenerate Package' }))

    await waitFor(() => {
      expect(regenerateMock).toHaveBeenCalledWith({
        plan,
        basePackageId: 'PKG-0001',
        basePackageFingerprint: 'package-fingerprint-current',
        requestedBy: 'reviewer',
      })
    })
    expect(await screen.findByLabelText('Regenerated package comparison')).toHaveTextContent(
      'MajorRefinement',
    )
    expect(screen.getByLabelText('Refinement interaction summary')).toHaveTextContent(
      'Human authoring burden',
    )
    expect(screen.getByLabelText('Recommendation diff')).toHaveTextContent('Current rationale.')
    expect(screen.getByLabelText('Recommendation diff')).toHaveTextContent(
      'Regenerated rationale from directives.',
    )
  })

  it('disables submission until backend proposal state is needs refinement', () => {
    useDecisionProposalRefinementMock.mockReturnValue({
      refine: refineMock,
      analyze: analyzeMock,
      regenerate: regenerateMock,
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
      analyze: analyzeMock,
      regenerate: regenerateMock,
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

function createEligibility(workspace: DecisionReviewWorkspace) {
  return {
    entityKind: 'Proposal',
    entityId: workspace.proposal.id,
    currentState: workspace.proposal.state,
    allowedActions: [
      {
        commandName: 'refine_decision_proposal',
        displayName: 'Submit refinement',
        targetState: 'Refined',
        isAllowed: workspace.proposal.state === 'NeedsRefinement',
        requiredInputs: ['reason'],
        reason: null,
        governingRule: 'proposal-refinement',
      },
    ],
    blockedActions: [],
    allowedNextStates: ['Refined'],
    blockedNextStates: [],
    diagnostics: ['Refinement eligibility projected by backend lifecycle rules.'],
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
    authority: {
      proposalFingerprint: `fingerprint-${proposal.id}`,
      packageId: 'PKG-0001',
      packageFingerprint: 'package-fingerprint-current',
      packageVersionCreatedAt: '2026-06-22T17:00:30.000Z',
      packageSourceProposalFingerprint: `fingerprint-${proposal.id}`,
      isPackageCurrentForProposalContent: true,
    },
  }
}

function createPackageVersion(
  workspace: DecisionReviewWorkspace,
  packageId: string,
  packageFingerprint: string,
  rationale: string,
) {
  return {
    id: packageId,
    repositoryId: workspace.proposal.repositoryId,
    proposalId: workspace.proposal.id,
    candidateId: workspace.proposal.candidateId,
    createdAt: '2026-06-22T17:02:00.000Z',
    packageFingerprint,
    package: {
      id: packageId,
      repositoryId: workspace.proposal.repositoryId,
      proposalId: workspace.proposal.id,
      candidateId: workspace.proposal.candidateId,
      title: workspace.proposal.title,
      decisionSummary: workspace.proposal.context,
      options: workspace.proposal.options,
      tradeoffs: workspace.proposal.tradeoffs,
      recommendation: {
        optionId: 'OPT-A',
        rationale,
        evidence: [],
      },
      assumptions: workspace.proposal.assumptions,
      openConcerns: [],
      evidence: workspace.proposal.evidence,
      metadata: {
        contextFingerprint: 'context-fingerprint',
        proposalFingerprint: `fingerprint-${workspace.proposal.id}`,
        generatorVersion: 'test',
        schemaVersion: '1',
        diagnostics: [],
      },
      generatedAt: '2026-06-22T17:02:00.000Z',
    },
  }
}
