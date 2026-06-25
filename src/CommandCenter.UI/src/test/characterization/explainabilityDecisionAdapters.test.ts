import { describe, expect, it } from 'vitest'
import {
  decisionCertificationEvidenceToFindings,
  decisionDiagnosticsToExplanation,
  decisionEvidenceInspectionItemsToEvidence,
  decisionGenerationCertificationFindingsToExplanation,
  decisionGenerationDiagnosticsToRejectedOptionDiagnostics,
  decisionGovernanceFindingsToCertificationFindings,
  decisionGovernanceFindingsToDiagnostics,
  decisionInfluenceStatementAdherenceToDiagnostics,
  decisionInfluenceStatementsToEvidence,
  decisionLifecycleEligibilityToActions,
  decisionOptionsToAlternatives,
  decisionProjectionDiagnosticsToExplanation,
  decisionQualityAssessmentToExplanation,
  decisionRecommendationToExplanation,
  decisionSourceAttributionsToEvidence,
  decisionSourceReferencesToEvidence,
  humanAuthoringBurdenExplanationToDiagnostics,
  refinementPlanToConstraints,
  refinementPlanToDiagnostics,
} from '../../lib/explainability'
import type {
  DecisionCertificationEvidence,
  DecisionGenerationDiagnostics,
  DecisionEvidenceInspectionItem,
  DecisionGenerationCertificationFinding,
  DecisionGovernanceFinding,
  DecisionInfluenceStatement,
  DecisionLifecycleEntityEligibility,
  DecisionProjectionDecisionDiagnostic,
  DecisionQualityAssessment,
  DecisionRecommendation,
  DecisionSourceAttribution,
  DecisionSourceReference,
  HumanAuthoringBurdenExplanation,
  RefinementPlan,
} from '../../types'

describe('decision explainability adapters', () => {
  it('preserves decision source references as evidence without deriving source authority', () => {
    expect(decisionSourceReferencesToEvidence([createSourceReference()])).toEqual([
      {
        id: 'DecisionProposal-.agents/decisions/proposals/PROP-0001/proposal.json-source-1',
        label: 'DecisionProposal',
        detail: 'Recommendation | Proposal evidence remains source-linked. | Item source-1 | Proposal PROP-0001 | Candidate CAND-0001',
        source: '.agents/decisions/proposals/PROP-0001/proposal.json',
      },
    ])
  })

  it('preserves certification evidence pass/fail state and related ids', () => {
    const evidence: DecisionCertificationEvidence = {
      id: 'authority-boundaries',
      area: 'Authority',
      passed: false,
      detail: 'A resolved decision claimed system authority.',
      sources: [createSourceReference()],
      relatedDecisionIds: ['DEC-0001'],
      relatedCandidateIds: ['CAND-0001'],
      relatedProposalIds: ['PROP-0001'],
    }

    expect(decisionCertificationEvidenceToFindings([evidence])).toEqual([
      {
        id: 'authority-boundaries',
        title: 'Authority: authority-boundaries',
        category: 'Authority',
        passed: false,
        detail: 'A resolved decision claimed system authority.',
        evidence: [
          {
            id: 'DecisionProposal-.agents/decisions/proposals/PROP-0001/proposal.json-source-1',
            label: 'DecisionProposal',
            detail: 'Recommendation | Proposal evidence remains source-linked. | Item source-1 | Proposal PROP-0001 | Candidate CAND-0001',
            source: '.agents/decisions/proposals/PROP-0001/proposal.json',
          },
          { label: 'Decision DEC-0001', detail: 'Related decision' },
          { label: 'Candidate CAND-0001', detail: 'Related candidate' },
          { label: 'Proposal PROP-0001', detail: 'Related proposal' },
        ],
        diagnostics: [],
      },
    ])
  })

  it('preserves governance findings as diagnostics and certification-style findings without computing report health', () => {
    const finding = createGovernanceFinding()

    expect(decisionGovernanceFindingsToDiagnostics([finding])).toEqual([
      {
        label: 'Blocking: ExecutionProjectionReadiness',
        detail: 'Resolved decision is not projection-ready: A blocking finding prevents projection.',
        tone: 'danger',
        evidence: [
          {
            id: 'DecisionProposal-.agents/decisions/proposals/PROP-0001/proposal.json-source-1',
            label: 'DecisionProposal',
            detail: 'Recommendation | Proposal evidence remains source-linked. | Item source-1 | Proposal PROP-0001 | Candidate CAND-0001',
            source: '.agents/decisions/proposals/PROP-0001/proposal.json',
          },
          { label: 'Decision DEC-0001', detail: 'Related decision' },
          { label: 'Candidate CAND-0001', detail: 'Related candidate' },
          { label: 'Proposal PROP-0001', detail: 'Related proposal' },
          { label: 'Blocks execution projection', detail: 'Governance projection impact' },
        ],
      },
    ])
    expect(decisionGovernanceFindingsToCertificationFindings([finding])[0]).toMatchObject({
      id: 'GOV-0001',
      title: 'Resolved decision is not projection-ready',
      category: 'Blocking / ExecutionProjectionReadiness',
      passed: false,
      detail: 'A blocking finding prevents projection.',
    })
  })

  it('preserves generation certification finding pass/fail state and sources', () => {
    const finding: DecisionGenerationCertificationFinding = {
      id: 'workflow-replacement',
      category: 'WorkflowReplacement',
      passed: false,
      summary: 'Workflow replacement not certified',
      detail: 'Quality evidence and execution influence are incomplete.',
      sources: [createSourceReference()],
      relatedDecisionIds: ['DEC-0001'],
      relatedCandidateIds: [],
      relatedProposalIds: [],
    }

    expect(decisionGenerationCertificationFindingsToExplanation([finding])).toEqual([
      {
        id: 'workflow-replacement',
        title: 'Workflow replacement not certified',
        category: 'WorkflowReplacement',
        passed: false,
        detail: 'Quality evidence and execution influence are incomplete.',
        evidence: [
          {
            id: 'DecisionProposal-.agents/decisions/proposals/PROP-0001/proposal.json-source-1',
            label: 'DecisionProposal',
            detail: 'Recommendation | Proposal evidence remains source-linked. | Item source-1 | Proposal PROP-0001 | Candidate CAND-0001',
            source: '.agents/decisions/proposals/PROP-0001/proposal.json',
          },
          { label: 'Decision DEC-0001', detail: 'Related decision' },
        ],
        diagnostics: [],
      },
    ])
  })

  it('preserves lifecycle eligibility command, rule, inputs, and blocked reason', () => {
    const eligibility: DecisionLifecycleEntityEligibility = {
      entityKind: 'Proposal',
      entityId: 'PROP-0001',
      currentState: 'ReadyForResolution',
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
      blockedNextStates: [],
      diagnostics: ['Lifecycle eligibility loaded from backend rules.'],
    }

    expect(decisionLifecycleEligibilityToActions(eligibility)).toEqual([
      {
        label: 'Resolve',
        detail: 'Proposal PROP-0001: ReadyForResolution -> Resolved.',
        eligible: true,
        reason: null,
        command: 'resolve_decision_proposal',
        constraints: [
          {
            label: 'DecisionLifecycleRules',
            detail: 'Allowed by backend lifecycle rules.',
            satisfied: true,
          },
          {
            label: 'Required input: resolver',
            detail: 'resolver must be supplied by the command caller.',
            satisfied: true,
          },
          {
            label: 'Required input: rationale',
            detail: 'rationale must be supplied by the command caller.',
            satisfied: true,
          },
        ],
      },
      {
        label: 'Archive',
        detail: 'Proposal PROP-0001: ReadyForResolution -> Archived.',
        eligible: false,
        reason: 'Archived decisions cannot transition.',
        command: 'archive_decision',
        constraints: [
          {
            label: 'DecisionLifecycleRules',
            detail: 'Archived decisions cannot transition.',
            satisfied: false,
          },
        ],
      },
    ])
  })

  it('preserves inspection summaries, source attributions, and diagnostics as presentation facts', () => {
    const attribution: DecisionSourceAttribution = {
      appliesToKind: 'Recommendation',
      itemId: 'OPT-A',
      sourceKind: 'DecisionProposal',
      relativePath: '.agents/decisions/proposals/PROP-0001/proposal.json',
      section: 'Recommendation',
      excerpt: 'Recommendation comes from backend proposal read model.',
      source: createSourceReference(),
    }
    const item: DecisionEvidenceInspectionItem = {
      appliesToKind: 'Recommendation',
      itemId: 'OPT-A',
      summary: 'Human authority should see evidence before mutation.',
      sources: [attribution],
    }

    expect(decisionEvidenceInspectionItemsToEvidence([item])).toEqual([
      {
        id: 'Recommendation-OPT-A-0',
        label: 'Human authority should see evidence before mutation.',
        detail: 'Recommendation | OPT-A',
      },
    ])
    expect(decisionSourceAttributionsToEvidence([attribution])).toEqual([
      {
        id: 'Recommendation-.agents/decisions/proposals/PROP-0001/proposal.json-OPT-A',
        label: 'DecisionProposal',
        detail: 'Recommendation | OPT-A | Recommendation | Recommendation comes from backend proposal read model.',
        source: '.agents/decisions/proposals/PROP-0001/proposal.json',
      },
    ])
    expect(decisionDiagnosticsToExplanation(['Current governance is advisory.'], 'Governance')).toEqual([
      { label: 'Governance', detail: 'Current governance is advisory.' },
    ])
  })

  it('preserves recommendation evidence, concerns, assumptions, and alternatives without choosing a winner', () => {
    const recommendation: DecisionRecommendation = {
      optionId: 'OPT-A',
      rationale: 'Backend selected OPT-A.',
      evidence: [{ summary: 'Primary evidence', sources: [createSourceReference()] }],
      summary: 'Prefer OPT-A',
      supportingFactors: ['Lower risk'],
      concerns: ['Migration cost remains open.'],
      assumptions: ['Backend authority is available.'],
      alternativeExplanations: ['OPT-B remains viable.'],
      mode: 'PreferredPlusAlternative',
      recommendationEvidence: [
        {
          type: 'Risk',
          optionId: 'OPT-A',
          summary: 'Risk is lower.',
          evidence: [{ summary: 'Risk evidence', sources: [] }],
        },
      ],
    }

    const explanation = decisionRecommendationToExplanation(recommendation)

    expect(explanation.title).toBe('OPT-A')
    expect(explanation.why).toBe('Backend selected OPT-A.')
    expect(explanation.evidence).toEqual([
      { id: 'Recommendation Evidence-0', label: 'Recommendation Evidence', detail: 'Primary evidence' },
      {
        id: 'DecisionProposal-.agents/decisions/proposals/PROP-0001/proposal.json-source-1',
        label: 'DecisionProposal',
        detail: 'Recommendation | Proposal evidence remains source-linked. | Item source-1 | Proposal PROP-0001 | Candidate CAND-0001',
        source: '.agents/decisions/proposals/PROP-0001/proposal.json',
      },
      { id: 'Risk-OPT-A-Risk is lower.', label: 'Risk: OPT-A', detail: 'Risk is lower.' },
      { id: 'Risk Evidence-0', label: 'Risk Evidence', detail: 'Risk evidence' },
    ])
    expect(explanation.constraints).toEqual([
      { label: 'Concern', detail: 'Migration cost remains open.', satisfied: null },
    ])
    expect(explanation.assumptions).toEqual([
      { label: 'Recommendation assumption', detail: 'Backend authority is available.' },
    ])
    expect(explanation.alternatives).toEqual([
      { label: 'Alternative explanation', detail: 'OPT-B remains viable.' },
    ])
    expect(explanation.diagnostics).toEqual([
      { label: 'Recommendation mode', detail: 'PreferredPlusAlternative' },
      { label: 'Supporting factor', detail: 'Lower risk' },
    ])
  })

  it('preserves quality score contributions and burden selection facts without recomputing rating or burden', () => {
    const assessment = createQualityAssessment()
    const quality = decisionQualityAssessmentToExplanation(assessment)

    expect(quality.title).toBe('DEC-0001: Good')
    expect(quality.summary).toBe('Score 82')
    expect(quality.constraints).toEqual([
      { label: 'Threshold: Good', detail: '65-84', satisfied: true },
      { label: 'Base score', detail: '50', satisfied: true },
      { label: 'Raw score', detail: '82', satisfied: true },
      { label: 'Clamped score', detail: '82', satisfied: true },
    ])
    expect(quality.diagnostics).toContainEqual({
      label: '+12 score | QS-RecommendationStability',
      detail: 'Recommendation remained stable.',
      tone: 'info',
      evidence: [
        {
          label: 'RecommendationStability / Positive / Info',
          detail: 'Quality signal contribution',
        },
      ],
    })

    const burden: HumanAuthoringBurdenExplanation = {
      decisionId: 'DEC-0001',
      selectionRule: 'Select the highest-weight human-authoring burden signal.',
      effectiveBurden: 'ReviewOnly',
      winningSignal: assessment.humanAuthoringBurdenSignals[0],
      isUnknown: false,
      isInferred: false,
      diagnostics: ['Signal HAB-0001 selected effective burden ReviewOnly.'],
    }

    expect(humanAuthoringBurdenExplanationToDiagnostics(burden)).toMatchObject([
      { label: 'Selection rule', detail: 'Select the highest-weight human-authoring burden signal.' },
      { label: 'Effective burden', detail: 'ReviewOnly | Known | Signal-backed' },
      { label: 'Burden Diagnostic', detail: 'Signal HAB-0001 selected effective burden ReviewOnly.' },
    ])
  })

  it('preserves refinement constraints, rejected-option diagnostics, and rejected alternatives', () => {
    const plan: RefinementPlan = {
      repositoryId: 'repo-alpha',
      proposalId: 'PROP-0001',
      analyzedAt: '2026-06-23T19:00:00.000Z',
      baseProposalFingerprint: 'fingerprint-PROP-0001',
      directives: [
        {
          id: 'DIR-0001',
          type: 'ReevaluateRecommendation',
          summary: 'Reevaluate recommendation.',
          targetOptionId: null,
          targetField: 'Recommendation',
          instruction: 'Consider evidence gap.',
          sources: [createSourceReference()],
        },
      ],
      regenerateOptions: false,
      reevaluateTradeoffs: true,
      reevaluateRecommendation: true,
      fullRegeneration: false,
      appliedConstraints: ['Consider evidence gap.'],
      diagnostics: ['Analyzed reviewer guidance.'],
    }
    const diagnostics: DecisionGenerationDiagnostics = {
      generatedOptionCount: 1,
      acceptedOptionCount: 0,
      rejectedOptionCount: 1,
      deduplicatedOptionCount: 0,
      fallbackOptionCount: 0,
      optionValidationResults: [
        {
          optionId: 'OPT-B',
          isValid: false,
          issues: [{ type: 'MissingEvidence', message: 'No evidence.' }],
        },
      ],
      diagnostics: [],
      rejectedOptions: [
        {
          id: 'OPT-B',
          title: 'Use local derivation',
          description: 'Derive backend state in UI.',
          evidence: [{ summary: 'Rejected rationale', sources: [] }],
        },
      ],
    }

    expect(refinementPlanToConstraints(plan)[0]).toEqual({
      label: 'Applied constraint',
      detail: 'Consider evidence gap.',
      satisfied: true,
    })
    expect(refinementPlanToDiagnostics(plan)[0]).toEqual({
      label: 'Plan scope',
      detail: 'Tradeoffs, Recommendation',
    })
    expect(decisionGenerationDiagnosticsToRejectedOptionDiagnostics(diagnostics)).toEqual([
      {
        label: 'Invalid option OPT-B',
        detail: 'MissingEvidence: No evidence.',
        tone: 'warning',
      },
    ])
    expect(decisionOptionsToAlternatives(diagnostics.rejectedOptions!, 'Rejected options')).toEqual([
      {
        label: 'Rejected options: OPT-B',
        detail: 'Use local derivation: Derive backend state in UI.',
        evidence: [
          { id: 'OPT-B Evidence-0', label: 'OPT-B Evidence', detail: 'Rejected rationale' },
        ],
      },
    ])
  })

  it('preserves influence statements, adherence observations, and projection category diagnostics', () => {
    const statement: DecisionInfluenceStatement = {
      statementId: 'STMT-0001',
      decisionId: 'DEC-0001',
      title: 'Use persisted influence',
      statement: 'Load influence from persisted execution traces.',
      classification: 'Tactical',
      projectionKind: 'WorkflowPolicy',
      statementType: 'Directive',
      promptSection: 'Decision Directives',
      priorityRank: 1,
      sources: [createSourceReference()],
      adherenceObservations: [
        {
          observedAt: '2026-06-23T19:00:00.000Z',
          observer: 'ExecutionMonitor',
          observation: 'Prompt contained directive.',
        },
      ],
    }
    const diagnostic: DecisionProjectionDecisionDiagnostic = {
      decisionId: 'DEC-0001',
      title: 'Use persisted influence',
      state: 'Resolved',
      outcome: 'Accepted',
      classification: 'Tactical',
      reason: 'Included because it is accepted and resolved.',
      projectedStatementIds: ['STMT-0001'],
    }

    expect(decisionInfluenceStatementsToEvidence([statement], 'Projected Directives')).toEqual([
      {
        id: 'STMT-0001',
        label: 'Projected Directives: DEC-0001',
        detail: 'Load influence from persisted execution traces.',
      },
      {
        id: 'STMT-0001-metadata',
        label: 'Use persisted influence',
        detail: 'Tactical | WorkflowPolicy | Decision Directives',
      },
      { id: 'STMT-0001-rank', label: 'Rank 1', detail: 'Priority rank' },
      {
        id: 'DecisionProposal-.agents/decisions/proposals/PROP-0001/proposal.json-source-1',
        label: 'DecisionProposal',
        detail: 'Recommendation | Proposal evidence remains source-linked. | Item source-1 | Proposal PROP-0001 | Candidate CAND-0001',
        source: '.agents/decisions/proposals/PROP-0001/proposal.json',
      },
    ])
    expect(decisionInfluenceStatementAdherenceToDiagnostics([statement])).toEqual([
      {
        label: 'Adherence: STMT-0001',
        detail: 'ExecutionMonitor at 2026-06-23T19:00:00.000Z: Prompt contained directive.',
        evidence: [{ label: 'DEC-0001', detail: 'Use persisted influence' }],
      },
    ])
    expect(decisionProjectionDiagnosticsToExplanation([diagnostic], 'Included Decisions')).toEqual([
      {
        label: 'Included Decisions: DEC-0001',
        detail: 'Included because it is accepted and resolved.',
        evidence: [
          { label: 'Use persisted influence', detail: 'Resolved | Accepted | Tactical' },
          { label: 'Projected statement count', detail: '1' },
          { label: 'Projected statement', detail: 'STMT-0001' },
        ],
      },
    ])
  })
})

function createQualityAssessment(): DecisionQualityAssessment {
  return {
    id: 'assessment.1',
    repositoryId: 'repo-alpha',
    decisionId: 'DEC-0001',
    assessedAt: '2026-06-23T19:00:00.000Z',
    rating: 'Good',
    score: 82,
    signals: [],
    humanAuthoringBurdenSignals: [
      {
        id: 'HAB-0001',
        repositoryId: 'repo-alpha',
        decisionId: 'DEC-0001',
        burden: 'ReviewOnly',
        sourceKind: 'ResolutionSnapshot',
        summary: 'Human reviewed generated content only.',
        sources: [createSourceReference()],
      },
    ],
    diagnostics: [],
    qualityExplanation: {
      baseScore: 50,
      rawScore: 82,
      clampedScore: 82,
      threshold: {
        rating: 'Good',
        minimumScore: 65,
        maximumScore: 84,
        reason: 'Good threshold crossed by backend score contribution.',
      },
      overrideReason: null,
      signalContributions: [
        {
          signalId: 'QS-RecommendationStability',
          category: 'RecommendationStability',
          direction: 'Positive',
          severity: 'Info',
          scoreContribution: 12,
          summary: 'Recommendation remained stable.',
        },
      ],
      diagnostics: ['Quality explanation is backend-owned.'],
    },
  }
}

function createGovernanceFinding(): DecisionGovernanceFinding {
  return {
    id: 'GOV-0001',
    category: 'ExecutionProjectionReadiness',
    severity: 'Blocking',
    blocksExecutionProjection: true,
    title: 'Resolved decision is not projection-ready',
    detail: 'A blocking finding prevents projection.',
    sources: [createSourceReference()],
    relatedDecisionIds: ['DEC-0001'],
    relatedCandidateIds: ['CAND-0001'],
    relatedProposalIds: ['PROP-0001'],
  }
}

function createSourceReference(): DecisionSourceReference {
  return {
    sourceKind: 'DecisionProposal',
    relativePath: '.agents/decisions/proposals/PROP-0001/proposal.json',
    section: 'Recommendation',
    itemId: 'source-1',
    decisionId: null,
    proposalId: 'PROP-0001',
    candidateId: 'CAND-0001',
    excerpt: 'Proposal evidence remains source-linked.',
  }
}
