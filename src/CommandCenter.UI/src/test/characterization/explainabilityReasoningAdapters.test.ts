import { describe, expect, it } from 'vitest'
import {
  reasoningCertificationEvidenceToFindings,
  reasoningDiagnosticGroupsToExplanation,
  reasoningMaterializationConceptToConstraints,
  reasoningMaterializationConceptToDiagnostics,
  reasoningMaterializationConceptToEvidence,
  reasoningReconstructionConfidenceToDiagnostics,
  reasoningReconstructionConfidenceToUncertainty,
  reasoningReconstructionEvidenceToEvidence,
  reasoningReconstructionScopeToEvidence,
  reasoningReferenceToEvidence,
  reasoningTaxonomyFindingsToDiagnostics,
} from '../../lib/explainability'
import type {
  ReasoningCertificationEvidence,
  ReasoningConceptMaterializationReview,
  ReasoningDiagnosticGroup,
  ReasoningReconstruction,
  ReasoningReconstructionConfidence,
  ReasoningReconstructionEvidence,
  ReasoningReference,
  ReasoningTaxonomyMaterializationFinding,
} from '../../types'

const reference: ReasoningReference = {
  kind: 'ReasoningEvent',
  id: 'EVT-0001',
  relativePath: '.agents/decisions/decisions.md',
  section: 'Newly Authorized',
  excerpt: 'Keep reasoning as event-sourced evidence.',
  fingerprint: 'fingerprint-1',
}

const reconstructionEvidence: ReasoningReconstructionEvidence = {
  kind: 'Event',
  id: 'EVT-0001',
  title: 'HypothesisRaised: Event substrate can stay narrow',
  summary: 'Reasoning should begin as immutable events with provenance.',
  reference,
  provenance: {
    sourceKind: 'ManualCapture',
    capturedBy: 'codex',
    relativePath: '.agents/plan.md',
    section: 'Milestone 8',
    excerpt: 'Reasoning migration preserves provenance.',
    fingerprint: 'fingerprint-2',
  },
}

const confidence: ReasoningReconstructionConfidence = {
  level: 'Limited',
  rationale: 'Only event evidence was reachable and trace diagnostics were present.',
  eventEvidencePresent: true,
  relationshipEvidencePresent: false,
  traceDiagnosticsPresent: true,
  missingEvidence: ['No relationship evidence was reachable for this query.'],
  whyNotHigher: ['Historical cutoff excluded later evidence.'],
}

const reconstruction = {
  scope: {
    direction: 'Forward',
    target: reference,
    source: null,
    historicalCutoff: '2026-06-22T16:03:00.0000000Z',
    reachableEvidence: [reconstructionEvidence],
    unreachableEvidence: [
      {
        ...reconstructionEvidence,
        id: 'EVT-0002',
        title: 'AlternativeRejected: Specialized entity storage deferred',
      },
    ],
  },
} as ReasoningReconstruction

describe('reasoning explainability adapters', () => {
  it('preserves reasoning references and reconstruction evidence metadata', () => {
    expect(reasoningReferenceToEvidence(reference)).toEqual({
      id: 'ReasoningEvent:EVT-0001',
      label: 'ReasoningEvent',
      detail: 'ReasoningEvent EVT-0001 (.agents/decisions/decisions.md - Newly Authorized)',
      source: '.agents/decisions/decisions.md',
      fingerprint: 'fingerprint-1',
    })

    expect(reasoningReconstructionEvidenceToEvidence([reconstructionEvidence])).toEqual([
      {
        id: 'Event:EVT-0001',
        label: 'Event EVT-0001: HypothesisRaised: Event substrate can stay narrow',
        detail: 'Reasoning should begin as immutable events with provenance.',
        source: '.agents/decisions/decisions.md',
        fingerprint: 'fingerprint-1',
      },
    ])
  })

  it('preserves confidence rationale, missing evidence, and reachability without recomputing confidence', () => {
    expect(reasoningReconstructionConfidenceToDiagnostics(confidence)).toEqual([
      {
        label: 'Limited confidence',
        detail: 'Only event evidence was reachable and trace diagnostics were present.',
        tone: 'warning',
      },
      {
        label: 'Event evidence',
        detail: 'Present',
        tone: 'success',
      },
      {
        label: 'Relationship evidence',
        detail: 'Not present',
        tone: 'warning',
      },
      {
        label: 'Trace diagnostics',
        detail: 'Present',
        tone: 'warning',
      },
    ])

    expect(reasoningReconstructionConfidenceToUncertainty(confidence)).toEqual([
      {
        label: 'Missing evidence',
        detail: 'No relationship evidence was reachable for this query.',
        severity: 'warning',
      },
      {
        label: 'Why confidence was not higher',
        detail: 'Historical cutoff excluded later evidence.',
        severity: 'info',
      },
    ])

    expect(reasoningReconstructionScopeToEvidence(reconstruction)).toEqual([
      {
        id: 'ReasoningEvent:EVT-0001',
        label: 'Target',
        detail: 'ReasoningEvent EVT-0001 (.agents/decisions/decisions.md - Newly Authorized)',
        source: '.agents/decisions/decisions.md',
        fingerprint: 'fingerprint-1',
      },
      {
        label: 'Source',
        detail: 'Not reported',
      },
      {
        label: 'Historical cutoff',
        detail: '2026-06-22T16:03:00.0000000Z',
      },
      {
        label: 'Reachable evidence',
        detail: '1 item(s)',
      },
      {
        label: 'Known unreachable evidence',
        detail: '1 item(s)',
      },
    ])
  })

  it('preserves grouped diagnostics with diagnostic category evidence', () => {
    const groups: ReasoningDiagnosticGroup[] = [
      {
        category: 'confidence',
        title: 'Confidence rationale',
        diagnostics: ['Trace diagnostics were present.'],
      },
    ]

    expect(reasoningDiagnosticGroupsToExplanation(groups)).toEqual([
      {
        label: 'Confidence rationale',
        detail: 'Trace diagnostics were present.',
        tone: 'info',
        evidence: [
          {
            label: 'Diagnostic category',
            detail: 'confidence',
          },
        ],
      },
    ])
  })

  it('preserves materialization branch facts without approving materialization in React', () => {
    const concept: ReasoningConceptMaterializationReview = {
      concept: 'Direction',
      recommendation: 'RemainDerived',
      summary: 'Direction remains derived because direction events alone do not justify stronger persistence.',
      failedScenarioCount: 0,
      repeatedWorkflowCount: 0,
      failedScenarioThreshold: 2,
      repeatedWorkflowThreshold: 3,
      branchReason: 'No threshold was met.',
      elevatedRiskSignals: ['Direction materialization can imply strategic authority.'],
      evidence: ['0 direction events'],
      risks: ['Direction persistence could imply strategic authority.'],
    }

    expect(reasoningMaterializationConceptToEvidence(concept)).toEqual([
      {
        label: 'Direction branch reason',
        detail: 'No threshold was met.',
      },
      {
        label: 'Direction evidence',
        detail: '0 direction events',
      },
      {
        label: 'Direction elevated risk signal',
        detail: 'Direction materialization can imply strategic authority.',
      },
    ])
    expect(reasoningMaterializationConceptToConstraints(concept)).toEqual([
      {
        label: 'Failed scenario threshold',
        detail: '0/2',
        satisfied: false,
      },
      {
        label: 'Repeated workflow threshold',
        detail: '0/3',
        satisfied: false,
      },
      {
        label: 'Recommended outcome',
        detail: 'RemainDerived',
        satisfied: true,
      },
    ])
    expect(reasoningMaterializationConceptToDiagnostics(concept)).toEqual([
      {
        label: 'Direction recommendation',
        detail: 'Direction remains derived because direction events alone do not justify stronger persistence.',
        tone: 'success',
      },
      {
        label: 'Direction risk',
        detail: 'Direction persistence could imply strategic authority.',
        tone: 'warning',
      },
    ])
  })

  it('preserves taxonomy and certification references as shared findings', () => {
    const taxonomy: ReasoningTaxonomyMaterializationFinding = {
      family: 'Hypothesis',
      eventTypeCount: 1,
      eventTypeThreshold: 4,
      lifecycleRisk: false,
      terminalEventTypePresent: false,
      terminalEventTypes: [],
      riskReason: 'Lifecycle risk remains below threshold.',
      summary: 'Hypothesis remains classification vocabulary.',
      evidence: ['1 event types observed'],
    }
    const certification: ReasoningCertificationEvidence = {
      id: 'CERT-010',
      scenario: 'Provenance completeness',
      passed: false,
      summary: 'One or more reasoning events lack provenance.',
      details: ['EVT-9999 is missing provenance.'],
      references: [reference],
    }

    expect(reasoningTaxonomyFindingsToDiagnostics([taxonomy])).toEqual([
      {
        label: 'Hypothesis taxonomy finding',
        detail:
          'Hypothesis remains classification vocabulary. (1/4 event types; terminal event types absent). Lifecycle risk remains below threshold.',
        tone: 'neutral',
        evidence: [
          {
            label: 'Hypothesis taxonomy evidence',
            detail: '1 event types observed',
          },
        ],
      },
    ])
    expect(reasoningCertificationEvidenceToFindings([certification])).toEqual([
      {
        id: 'CERT-010',
        title: 'Provenance completeness',
        category: 'Failed',
        passed: false,
        detail: 'One or more reasoning events lack provenance.',
        evidence: [
          {
            id: 'ReasoningEvent:EVT-0001',
            label: 'ReasoningEvent',
            detail: 'ReasoningEvent EVT-0001 (.agents/decisions/decisions.md - Newly Authorized)',
            source: '.agents/decisions/decisions.md',
            fingerprint: 'fingerprint-1',
          },
        ],
        diagnostics: [
          {
            label: 'Certification failure detail',
            detail: 'EVT-9999 is missing provenance.',
            tone: 'danger',
          },
        ],
      },
    ])
  })
})
