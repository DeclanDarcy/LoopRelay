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
    expect(within(recommendation).getAllByText('.agents/plan.md').length).toBeGreaterThan(0)

    const optionEvidence = screen.getByLabelText('Option Evidence')
    expect(within(optionEvidence).getByText('The current slice authorizes inspection-only UI.')).toBeInTheDocument()
    expect(within(optionEvidence).getByText(/Milestone 4/)).toBeInTheDocument()
  })

  it('renders recommendation mode, supporting context, evidence categories, and option evaluations from the backend', () => {
    render(<DecisionProposalViewer workspace={createWorkspace()} isLoading={false} />)

    const recommendation = screen.getByLabelText('Decision recommendation')
    expect(within(recommendation).getByText('Mode PreferredOption')).toBeInTheDocument()
    expect(within(recommendation).getAllByText('Backend read models are already available.').length).toBeGreaterThan(0)
    expect(within(recommendation).getByText('UI must not infer recommendation confidence.')).toBeInTheDocument()
    expect(within(recommendation).getByText('Assume review workspace remains authoritative.')).toBeInTheDocument()
    expect(within(recommendation).getByText('Alternative manual review flow remains possible.')).toBeInTheDocument()

    const recommendationExplanation = screen.getByLabelText('Decision Recommendation explanation')
    expect(within(recommendationExplanation).getByText('Benefit: OPT-A')).toBeInTheDocument()
    expect(within(recommendationExplanation).getByText('Keeps recommendation tied to proposal evidence.')).toBeInTheDocument()

    const evaluations = screen.getByLabelText('Decision option evaluations')
    expect(within(evaluations).getByText('Rank 1 / Score 92')).toBeInTheDocument()
    expect(within(evaluations).getByText('Score comes from backend recommendation analysis.')).toBeInTheDocument()
    expect(within(evaluations).getByText('Avoids duplicate client scoring.')).toBeInTheDocument()
    expect(within(evaluations).getByText('Requires a larger read-only panel.')).toBeInTheDocument()
  })

  it('renders analyzed option details, constraints, invalid results, rejected options, and deduplicated options', () => {
    render(<DecisionProposalViewer workspace={createWorkspace()} isLoading={false} />)

    const optionTransparency = screen.getByLabelText('Option transparency for OPT-A')
    expect(within(optionTransparency).getByText('Type Adopt')).toBeInTheDocument()
    expect(within(optionTransparency).getByText('Valid option')).toBeInTheDocument()
    expect(within(optionTransparency).getByText('1 disqualifying constraints')).toBeInTheDocument()
    expect(within(optionTransparency).getByText('High: Preserves backend authority.')).toBeInTheDocument()
    expect(within(optionTransparency).getByText('Medium: Extra UI surface area.')).toBeInTheDocument()
    expect(within(optionTransparency).getByText('Blocking: Do not compute scores in React.')).toBeInTheDocument()

    const hiddenOptions = screen.getByLabelText('Rejected and hidden proposal options')
    expect(within(hiddenOptions).getByText(/Rejected generated option/)).toBeInTheDocument()
    expect(within(hiddenOptions).getByText(/Rejected because it moved authority into React./)).toBeInTheDocument()
    expect(within(hiddenOptions).getByText(/Duplicate generated option/)).toBeInTheDocument()
    expect(within(hiddenOptions).getByText(/Duplicate: Same behavior as OPT-A./)).toBeInTheDocument()
    expect(within(hiddenOptions).getByText('EvidenceMissing: Requires reviewer-supplied evidence.')).toBeInTheDocument()
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
      within(unavailableReasons).getAllByText('DecisionLifecycleRules.ValidateProposalTransition').length,
    ).toBeGreaterThan(0)
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
          type: 'Adopt',
          assumptions: ['Assume proposal review routes remain current.'],
          dependencies: ['DecisionReviewWorkspace endpoint'],
          diagnostics: ['Option diagnostics are backend-authored.'],
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
        summary: 'Render backend recommendation data without local scoring.',
        mode: 'PreferredOption',
        supportingFactors: ['Backend read models are already available.'],
        concerns: ['UI must not infer recommendation confidence.'],
        assumptions: ['Assume review workspace remains authoritative.'],
        alternativeExplanations: ['Alternative manual review flow remains possible.'],
        recommendationEvidence: [
          {
            type: 'Benefit',
            optionId: 'OPT-A',
            summary: 'Keeps recommendation tied to proposal evidence.',
            evidence: [
              {
                summary: 'Recommendation evidence is generated by the backend.',
                sources: [source],
              },
            ],
          },
        ],
        optionEvaluations: [
          {
            optionId: 'OPT-A',
            strengths: ['Avoids duplicate client scoring.'],
            weaknesses: ['Requires a larger read-only panel.'],
            risks: ['Reviewers may need to scan more generated facts.'],
            constraints: ['Must remain render-only.'],
            summary: 'Best fit for proposal transparency.',
            score: 92,
            rank: 1,
            scoreExplanation: 'Score comes from backend recommendation analysis.',
            evidence: [
              {
                type: 'RepositoryState',
                optionId: 'OPT-A',
                summary: 'Workspace projection already carries the data.',
                evidence: [
                  {
                    summary: 'Review workspace includes current proposal data.',
                    sources: [source],
                  },
                ],
              },
            ],
          },
        ],
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
      analyzedOptions: [
        {
          optionId: 'OPT-A',
          benefits: [
            {
              statement: 'Preserves backend authority.',
              impact: 'High',
              evidence: [],
            },
          ],
          costs: [
            {
              statement: 'Extra UI surface area.',
              impact: 'Medium',
              evidence: [],
            },
          ],
          risks: [
            {
              statement: 'Do not compute scores in React.',
              severity: 'Blocking',
              isUnknown: false,
              evidence: [],
            },
          ],
          dependencies: [
            {
              statement: 'Decision review workspace projection.',
              evidence: [],
            },
          ],
          consequences: [
            {
              statement: 'Reviewers see more backend rationale.',
              impact: 'High',
              evidence: [],
            },
          ],
          diagnostics: ['Analyzed option diagnostics remain backend-owned.'],
          evidence: [
            {
              summary: 'Analysis evidence is projected with the proposal.',
              sources: [source],
            },
          ],
        },
      ],
      tradeoffComparisons: [
        {
          optionId: 'OPT-A',
          relativeStrengths: ['Strongest authority preservation.'],
          relativeWeaknesses: ['More dense review UI.'],
          uniqueAdvantages: ['Keeps proposal explanations local to decisions.'],
          uniqueRisks: ['Can overwhelm if not grouped.'],
          disqualifyingConstraints: ['Must not add React scoring.'],
          evidence: [
            {
              summary: 'Tradeoff comparison came from backend analysis.',
              sources: [source],
            },
          ],
        },
      ],
      generationDiagnostics: {
        generatedOptionCount: 4,
        acceptedOptionCount: 1,
        rejectedOptionCount: 1,
        deduplicatedOptionCount: 1,
        fallbackOptionCount: 0,
        optionValidationResults: [
          {
            optionId: 'OPT-A',
            isValid: true,
            issues: [],
          },
          {
            optionId: 'OPT-X',
            isValid: false,
            issues: [
              {
                type: 'EvidenceMissing',
                message: 'Requires reviewer-supplied evidence.',
              },
            ],
          },
        ],
        diagnostics: ['Generation diagnostics are preserved.'],
        rejectedOptions: [
          {
            id: 'OPT-R',
            title: 'Rejected generated option',
            description: 'Rejected because it moved authority into React.',
            diagnostics: ['AuthorityBoundary: React cannot own recommendation scoring.'],
            evidence: [
              {
                summary: 'Rejected option evidence remains visible.',
                sources: [source],
              },
            ],
          },
        ],
        deduplicatedOptions: [
          {
            id: 'OPT-D',
            title: 'Duplicate generated option',
            description: 'Same behavior as OPT-A.',
            diagnostics: ['Duplicate: Same behavior as OPT-A.'],
            evidence: [],
          },
        ],
      },
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
