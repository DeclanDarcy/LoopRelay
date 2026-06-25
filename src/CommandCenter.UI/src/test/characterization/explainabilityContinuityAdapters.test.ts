import { describe, expect, it } from 'vitest'
import {
  continuityCompressionTrendToDiagnostics,
  continuityDiagnosticGroupsToDiagnostics,
  continuityRepeatedSignalsToDiagnostics,
  continuityReportToEvidence,
  continuityTrendToEvidence,
  continuityWarningsToDiagnostics,
  operationalContextCompressionOutcomeToConstraints,
  operationalContextCompressionOutcomeToEvidence,
  operationalContextCompressionRevisionsToEvidence,
  operationalContextCompressionSummaryToDiagnostics,
  operationalContextSemanticChangeToEvidence,
  operationalEvolutionTimelineEntryToEvidence,
} from '../../lib/explainability'
import type {
  ContinuityDiagnostics,
  ContinuityReport,
  OperationalContextCompressionOutcome,
  OperationalContextCompressionSummary,
  OperationalContextSemanticChange,
  OperationalEvolutionTimelineEntry,
} from '../../types'

const compressionOutcome: OperationalContextCompressionOutcome = {
  outcome: 'Retained',
  itemKind: 'Constraint',
  itemText: 'Backend continuity services own compression.',
  rule: 'normalized-text-retention',
  threshold: 'Normalized text is compared across current and proposed context.',
  rationale: 'Item remains present after compression.',
  evidence: ['Normalized text: backend continuity services own compression.'],
}

const compressionSummary: OperationalContextCompressionSummary = {
  preservedItemCount: 1,
  addedItemCount: 2,
  modifiedItemCount: 3,
  removedItemCount: 4,
  compressedItemCount: 5,
  permanentUnderstandingItemCount: 6,
  activeUnderstandingItemCount: 7,
  historicalUnderstandingItemCount: 8,
  historicalNoiseItemCount: 9,
  resolvedQuestionCount: 10,
  retiredRiskCount: 11,
  warningCount: 12,
  warnings: ['Compression warning'],
  revisionSummary: ['Revision one'],
  noiseRemovedIndicators: ['Noise removed'],
  stableUnderstandingRetentionWarnings: ['Retention warning'],
  itemOutcomes: [compressionOutcome],
}

const semanticChange: OperationalContextSemanticChange = {
  type: 'ModifiedConstraint',
  section: 'Constraints',
  description: 'Updated the deployment constraint.',
  itemId: 'constraint-1',
  previousState: 'Deployments are manual.',
  currentState: 'Deployments are automated after review.',
  modificationReason: 'Current context records the automation boundary.',
  identityBasis: 'normalized-kind-and-source',
  supportingEvidence: ['.agents/operational_context.md#constraints'],
}

const timelineEntry: OperationalEvolutionTimelineEntry = {
  outcome: 'Modified',
  semanticEventType: 'ModifiedConstraint',
  section: 'Constraints',
  description: 'Updated the deployment constraint.',
  itemId: 'constraint-1',
  previousState: 'Deployments are manual.',
  currentState: 'Deployments are automated after review.',
  reason: 'Current context records the automation boundary.',
  identityBasis: 'normalized-kind-and-source',
  previousRevisionNumber: 2,
  currentRevisionNumber: 3,
  supportingEvidence: ['Revision evidence'],
}

const diagnostics: ContinuityDiagnostics = {
  repositoryId: 'repo-1',
  generatedAt: '2026-01-02T03:04:05Z',
  revisionCount: 3,
  currentContextByteCount: 1200,
  currentContextCharacterCount: 1100,
  contextByteGrowth: 200,
  averageBytesPerRevision: 66.6,
  operationalEvolution: {
    addedCount: 1,
    modifiedCount: 2,
    removedCount: 3,
    preservedCount: 4,
    lostCount: 5,
    resolvedCount: 6,
    semanticChanges: [semanticChange],
    timelineEntries: [timelineEntry],
    diagnosticGroups: [],
  },
  architectureTrend: { addedCount: 1, modifiedCount: 2, removedCount: 3, resolvedCount: 4, lostCount: 5 },
  constraintTrend: { addedCount: 0, modifiedCount: 0, removedCount: 0, resolvedCount: 0, lostCount: 0 },
  decisionTrend: { addedCount: 0, modifiedCount: 0, removedCount: 0, resolvedCount: 0, lostCount: 0 },
  rationaleTrend: { addedCount: 0, modifiedCount: 0, removedCount: 0, resolvedCount: 0, lostCount: 0 },
  openQuestionTrend: { addedCount: 0, modifiedCount: 0, removedCount: 0, resolvedCount: 0, lostCount: 0 },
  activeRiskTrend: { addedCount: 0, modifiedCount: 0, removedCount: 0, resolvedCount: 0, lostCount: 0 },
  compressionTrend: {
    proposalCount: 2,
    compressedItemCount: 3,
    removedItemCount: 4,
    resolvedQuestionCount: 5,
    retiredRiskCount: 6,
    warningCount: 7,
    warnings: ['Compression warning'],
    noiseRemovedIndicators: ['Noise removed'],
  },
  repeatedInvestigationIndicators: ['Investigation repeated'],
  repeatedQuestionIndicators: ['Question repeated'],
  decisionReworkIndicators: ['Decision reworked'],
  continuityWarnings: ['Continuity warning'],
  diagnosticGroups: [
    {
      category: 'diff',
      title: 'Semantic diff',
      diagnostics: ['ModifiedConstraint in Constraints.'],
    },
  ],
}

describe('continuity explainability adapters', () => {
  it('preserves compression summary diagnostics and revision evidence without deriving eligibility', () => {
    expect(operationalContextCompressionSummaryToDiagnostics(compressionSummary)).toEqual([
      { label: 'Compression warning', detail: 'Compression warning', tone: 'warning' },
      { label: 'Retention warning', detail: 'Retention warning', tone: 'warning' },
      { label: 'Compressed understanding', detail: 'Noise removed', tone: 'info' },
    ])

    expect(operationalContextCompressionRevisionsToEvidence(compressionSummary)).toEqual([
      { label: 'Revision summary', detail: 'Revision one' },
    ])
  })

  it('preserves item outcome rules, thresholds, rationale, and evidence as presentation constraints', () => {
    expect(operationalContextCompressionOutcomeToConstraints(compressionOutcome)).toEqual([
      { label: 'Rule', detail: 'normalized-text-retention' },
      { label: 'Threshold', detail: 'Normalized text is compared across current and proposed context.' },
      { label: 'Rationale', detail: 'Item remains present after compression.' },
    ])

    expect(operationalContextCompressionOutcomeToEvidence(compressionOutcome)).toEqual([
      {
        label: 'Compression evidence',
        detail: 'Normalized text: backend continuity services own compression.',
      },
    ])
  })

  it('preserves semantic identity and lifecycle evidence without interpreting the change outcome', () => {
    expect(operationalContextSemanticChangeToEvidence(semanticChange)).toEqual([
      { label: 'Identity basis', detail: 'normalized-kind-and-source' },
      { label: 'Reason', detail: 'Current context records the automation boundary.' },
      { label: 'Previous', detail: 'Deployments are manual.' },
      { label: 'Current', detail: 'Deployments are automated after review.' },
      { label: 'Supporting evidence', detail: '.agents/operational_context.md#constraints' },
    ])

    expect(operationalEvolutionTimelineEntryToEvidence(timelineEntry)).toEqual([
      { label: 'Item id', detail: 'constraint-1' },
      { label: 'Identity basis', detail: 'normalized-kind-and-source' },
      { label: 'Reason', detail: 'Current context records the automation boundary.' },
      { label: 'Previous state', detail: 'Deployments are manual.' },
      { label: 'Current state', detail: 'Deployments are automated after review.' },
      { label: 'Previous revision', detail: '2' },
      { label: 'Current revision', detail: '3' },
      { label: 'Evolution evidence', detail: 'Revision evidence' },
    ])
  })

  it('preserves diagnostics, repeated signals, warnings, trends, and report facts', () => {
    const report: ContinuityReport = {
      reportId: 'continuity.20260102',
      repositoryId: 'repo-1',
      generatedAt: '2026-01-02T04:05:06Z',
      relativePath: '.agents/continuity/continuity.20260102.json',
      diagnostics,
    }

    expect(continuityDiagnosticGroupsToDiagnostics(diagnostics.diagnosticGroups)).toEqual([
      {
        label: 'Semantic diff',
        detail: 'ModifiedConstraint in Constraints.',
        tone: 'info',
        evidence: [{ label: 'Diagnostic category', detail: 'diff' }],
      },
    ])
    expect(continuityCompressionTrendToDiagnostics(diagnostics.compressionTrend)).toEqual([
      { label: 'Compression warning', detail: 'Compression warning', tone: 'warning' },
      { label: 'Noise removed', detail: 'Noise removed', tone: 'info' },
    ])
    expect(continuityRepeatedSignalsToDiagnostics(diagnostics)).toEqual([
      { label: 'Repeated investigation', detail: 'Investigation repeated', tone: 'info' },
      { label: 'Repeated question', detail: 'Question repeated', tone: 'info' },
      { label: 'Decision rework', detail: 'Decision reworked', tone: 'info' },
    ])
    expect(continuityWarningsToDiagnostics(diagnostics.continuityWarnings)).toEqual([
      { label: 'Continuity warning', detail: 'Continuity warning', tone: 'warning' },
    ])
    expect(continuityTrendToEvidence('Architecture', diagnostics.architectureTrend)).toEqual([
      { label: 'Architecture added', detail: '1' },
      { label: 'Architecture modified', detail: '2' },
      { label: 'Architecture removed', detail: '3' },
      { label: 'Architecture resolved', detail: '4' },
      { label: 'Architecture lost', detail: '5' },
    ])
    expect(continuityReportToEvidence(report)).toEqual([
      {
        id: 'continuity.20260102',
        label: 'Latest report',
        detail: 'continuity.20260102',
        source: '.agents/continuity/continuity.20260102.json',
      },
      { label: 'Diagnostics revisions', detail: '3' },
    ])
  })
})
