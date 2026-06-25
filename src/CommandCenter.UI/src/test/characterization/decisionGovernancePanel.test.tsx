import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DecisionGovernancePanel } from '../../features/decisions/DecisionGovernancePanel'
import type {
  Decision,
  DecisionGovernanceReport,
  DecisionLifecycleEntityEligibility,
  DecisionReviewWorkspace,
} from '../../types'

afterEach(() => {
  cleanup()
})

describe('DecisionGovernancePanel', () => {
  it('groups advisory findings by severity and category without repair actions', () => {
    const onGenerateReport = vi.fn()
    const onSelectProposal = vi.fn()

    render(
      <DecisionGovernancePanel
        currentReport={createReport()}
        reports={[createReport('governance.202606221800000000001')]}
        isLoading={false}
        isGenerating={false}
        error={null}
        onGenerateReport={onGenerateReport}
        onSelectProposal={onSelectProposal}
      />,
    )

    expect(screen.getByText('Blocking')).toBeInTheDocument()
    expect(screen.getByText('Warning')).toBeInTheDocument()
    expect(screen.getByText('ExecutionProjectionReadiness')).toBeInTheDocument()
    expect(screen.getByText('DecisionCoverage')).toBeInTheDocument()
    expect(screen.getByText('Blocks execution projection')).toBeInTheDocument()
    expect(screen.getByText(/\.agents\/decisions\/proposals\/PROP-0001\/proposal\.json/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /repair|fix|resolve|correct/i })).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'View PROP-0001' }))

    expect(onSelectProposal).toHaveBeenCalledWith('PROP-0001')
  })

  it('generates a persistent report only through the report action', () => {
    const onGenerateReport = vi.fn()

    render(
      <DecisionGovernancePanel
        currentReport={createReport()}
        reports={[]}
        isLoading={false}
        isGenerating={false}
        error={null}
        onGenerateReport={onGenerateReport}
        onSelectProposal={vi.fn()}
      />,
    )

    fireEvent.click(within(screen.getByLabelText('Decision governance')).getByRole('button', {
      name: 'Generate Report',
    }))

    expect(onGenerateReport).toHaveBeenCalledTimes(1)
  })

  it('renders review authority, recommendation divergence, and lifecycle transition facts from backend projections', () => {
    render(
      <DecisionGovernancePanel
        currentReport={createReport()}
        reports={[]}
        selectedProposalWorkspace={createWorkspace()}
        selectedProposalEligibility={createEligibility('Proposal', 'ReadyForResolution')}
        selectedDecisionEligibility={createEligibility('Decision', 'Resolved')}
        resolvedDecision={createDecision()}
        isLoading={false}
        isGenerating={false}
        error={null}
        onGenerateReport={vi.fn()}
        onSelectProposal={vi.fn()}
      />,
    )

    const authority = screen.getByLabelText('Governance authority and lifecycle')
    expect(within(authority).getByText('Package authority: Stale')).toBeInTheDocument()
    expect(within(authority).getByText('Reviewed package content does not match the current proposal.')).toBeInTheDocument()
    expect(within(authority).getByText('Proposal lifecycle state: ReadyForResolution')).toBeInTheDocument()
    const proposalLifecycle = screen.getByLabelText('Proposal governance lifecycle eligibility')
    expect(within(proposalLifecycle).getByText('Allowed actions: Resolve')).toBeInTheDocument()
    expect(within(proposalLifecycle).getByText('Archived decisions cannot transition.')).toBeInTheDocument()
    expect(within(authority).getByText('Recommendation divergence: Yes')).toBeInTheDocument()
    expect(within(authority).getByText('Source proposal: PROP-0001')).toBeInTheDocument()
  })
})

function createReport(id = 'governance.current'): DecisionGovernanceReport {
  return {
    id,
    repositoryId: 'repo-alpha',
    generatedAt: '2026-06-22T18:00:00.000Z',
    inputFingerprint: 'governance-fingerprint',
    health: 'Blocked',
    summary: {
      decisionCount: 1,
      resolvedDecisionCount: 1,
      activeCandidateCount: 1,
      activeProposalCount: 1,
      assimilationRecommendationCount: 0,
      findingCount: 2,
      blockingFindingCount: 1,
    },
    diagnostics: ['Current governance is advisory.'],
    findings: [
      {
        id: 'GOV-0001',
        category: 'ExecutionProjectionReadiness',
        severity: 'Blocking',
        blocksExecutionProjection: true,
        title: 'Resolved decision is not projection-ready',
        detail: 'A blocking finding prevents projection until execution decides how to act.',
        sources: [
          {
            sourceKind: 'DecisionProposal',
            relativePath: '.agents/decisions/proposals/PROP-0001/proposal.json',
            section: null,
            itemId: null,
            decisionId: null,
            proposalId: 'PROP-0001',
            candidateId: 'CAND-0001',
            excerpt: 'Proposal evidence remains source-linked.',
          },
        ],
        relatedDecisionIds: ['DEC-0001'],
        relatedCandidateIds: ['CAND-0001'],
        relatedProposalIds: ['PROP-0001'],
      },
      {
        id: 'GOV-0002',
        category: 'DecisionCoverage',
        severity: 'Warning',
        blocksExecutionProjection: false,
        title: 'Candidate coverage gap',
        detail: 'A promoted candidate still needs a resolved decision.',
        sources: [],
        relatedDecisionIds: [],
        relatedCandidateIds: ['CAND-0002'],
        relatedProposalIds: [],
      },
    ],
  }
}

function createWorkspace(): DecisionReviewWorkspace {
  return {
    proposal: {
      id: 'PROP-0001',
      repositoryId: 'repo-alpha',
      candidateId: 'CAND-0001',
      state: 'ReadyForResolution',
      title: 'Adopt backend-owned governance transparency',
      context: 'Decision governance should compose authoritative projections.',
      options: [
        {
          id: 'OPT-1',
          title: 'Use backend authority',
          description: 'Render the existing review workspace authority.',
          evidence: [],
        },
        {
          id: 'OPT-2',
          title: 'Override recommendation',
          description: 'Record divergence through resolution authority.',
          evidence: [],
        },
      ],
      tradeoffs: [],
      recommendation: {
        optionId: 'OPT-1',
        rationale: 'Backend projection is authoritative.',
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
      updatedAt: '2026-06-22T18:00:00.000Z',
      reason: 'Ready for governance inspection.',
      sources: [],
    },
    notes: [],
    revisions: [],
    diagnostics: {
      hasRecommendation: true,
      hasEvidence: true,
      optionCount: 2,
      tradeoffCount: 0,
      assumptionCount: 0,
      noteCount: 0,
      warnings: [],
    },
    authority: {
      proposalFingerprint: 'proposal-fingerprint-current',
      packageId: 'package.v1',
      packageFingerprint: 'package-fingerprint-stale',
      packageVersionCreatedAt: '2026-06-22T17:00:00.000Z',
      packageSourceProposalFingerprint: 'proposal-fingerprint-old',
      isPackageCurrentForProposalContent: false,
    },
  }
}

function createEligibility(
  entityKind: string,
  currentState: string,
): DecisionLifecycleEntityEligibility {
  return {
    entityKind,
    entityId: entityKind === 'Proposal' ? 'PROP-0001' : 'DEC-0001',
    currentState,
    allowedActions: [
      {
        commandName: 'resolve_decision_proposal',
        displayName: 'Resolve',
        targetState: 'Resolved',
        isAllowed: true,
        requiredInputs: ['resolver', 'rationale'],
        reason: null,
        governingRule: 'DecisionLifecycleRules',
      },
    ],
    blockedActions: [
      {
        commandName: 'archive_decision',
        displayName: 'Archive',
        targetState: 'Archived',
        isAllowed: false,
        requiredInputs: [],
        reason: 'Archived decisions cannot transition.',
        governingRule: 'DecisionLifecycleRules',
      },
    ],
    allowedNextStates: ['Resolved'],
    blockedNextStates: [
      {
        state: 'Archived',
        reason: 'Archived decisions cannot transition.',
        governingRule: 'DecisionLifecycleRules',
      },
    ],
    diagnostics: ['Lifecycle eligibility loaded from backend rules.'],
  }
}

function createDecision(): Decision {
  return {
    id: 'DEC-0001',
    state: 'Resolved',
    classification: 'Architectural',
    title: 'Governance transparency source',
    context: 'Resolution authority records divergence.',
    metadata: {
      repositoryId: 'repo-alpha',
      createdAt: '2026-06-22T18:10:00.000Z',
      updatedAt: '2026-06-22T18:10:00.000Z',
      schemaVersion: '1',
    },
    resolution: {
      outcome: 'Accepted',
      selectedOptionId: 'OPT-2',
      rationale: 'Override the recommendation with recorded authority.',
      resolvedBy: 'reviewer',
      recommendationDiverged: true,
      resolvedAt: '2026-06-22T18:10:00.000Z',
      sources: [],
      sourceProposalSnapshot: {
        proposalId: 'PROP-0001',
        candidateId: 'CAND-0001',
        proposalFingerprint: 'proposal-fingerprint-current',
        proposalState: 'ReadyForResolution',
        title: 'Adopt backend-owned governance transparency',
        context: 'Decision governance should compose authoritative projections.',
        options: [],
        tradeoffs: [],
        recommendation: null,
        assumptions: [],
        evidence: [],
        history: [],
        revisions: [],
      },
    },
    relationships: [],
    evidence: [],
    history: [],
  }
}
