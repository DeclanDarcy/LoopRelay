import { describe, expect, it } from 'vitest'
import {
  decisionCertificationEvidenceToFindings,
  decisionDiagnosticsToExplanation,
  decisionEvidenceInspectionItemsToEvidence,
  decisionGenerationCertificationFindingsToExplanation,
  decisionGovernanceFindingsToCertificationFindings,
  decisionGovernanceFindingsToDiagnostics,
  decisionLifecycleEligibilityToActions,
  decisionSourceAttributionsToEvidence,
  decisionSourceReferencesToEvidence,
} from '../../lib/explainability'
import type {
  DecisionCertificationEvidence,
  DecisionEvidenceInspectionItem,
  DecisionGenerationCertificationFinding,
  DecisionGovernanceFinding,
  DecisionLifecycleEntityEligibility,
  DecisionSourceAttribution,
  DecisionSourceReference,
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
})

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
