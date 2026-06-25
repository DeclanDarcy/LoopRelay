import { describe, expect, it } from 'vitest'
import {
  continuityCompressionTrendToDiagnostics,
  continuityDiagnosticGroupsToDiagnostics,
  continuityRepeatedSignalsToDiagnostics,
  continuityDecisionConsequenceToEvidence,
  continuityDecisionContradictionToDiagnostics,
  continuityDecisionContradictionToEvidence,
  decisionAssimilationLimitToConstraints,
  decisionAssimilationLimitToEvidence,
  decisionAssimilationRecordToConstraints,
  decisionAssimilationRecordToDiagnostics,
  decisionAssimilationRecordToEvidence,
  decisionAssimilationRecordToUncertainty,
  decisionTaxonomyBasisToDiagnostics,
  decisionTaxonomyBasisToEvidence,
  continuityReportToEvidence,
  continuityTrendToEvidence,
  continuityWarningsToDiagnostics,
  operationalContextProjectionToEvidence,
  operationalContextProposalLifecycleToDiagnostics,
  operationalContextProposalLifecycleToEvidence,
  operationalContextProposalSummaryToEvidence,
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
  DecisionAssimilationLimit,
  DecisionAssimilationRecord,
  OperationalContextProjection,
  OperationalContextCompressionOutcome,
  OperationalContextCompressionSummary,
  OperationalContextProposal,
  OperationalContextProposalSummary,
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

const proposalSummary: OperationalContextProposalSummary = {
  pendingProposalExists: true,
  latestProposalId: 'proposal-42',
  generatedAt: '2026-01-02T03:04:05Z',
  status: 'Accepted',
  sourceInputCount: 4,
  contentByteCount: 1200,
  contentCharacterCount: 1100,
  lastPromotedAt: '2026-01-03T04:05:06Z',
  lastArchivedRelativePath: '.agents/operational_context.0001.md',
}

const operationalContext: OperationalContextProjection = {
  exists: true,
  currentRelativePath: '.agents/operational_context.md',
  revisionCount: 8,
  currentRevisionNumber: 8,
  lastUpdatedAt: '2026-01-03T04:05:06Z',
  lastPromotionAt: '2026-01-03T04:05:06Z',
  currentUnderstandingSummary: ['Backend continuity services own promotion.'],
  architecture: [],
  authorityBoundaries: [],
  constraints: [],
  stableDecisions: [],
  decisionRationale: [],
  openQuestions: [{ id: 'q1', kind: 'OpenQuestion', text: 'What remains?', rationale: null, sourceRelativePath: null }],
  activeRisks: [{ id: 'r1', kind: 'ActiveRisk', text: 'Risk remains.', rationale: null, sourceRelativePath: null }],
  recentUnderstandingChanges: [],
  pendingProposalSummary: proposalSummary,
  latestReviewState: 'Accepted',
  continuityWarnings: ['Context is growing quickly.'],
}

const proposal: OperationalContextProposal = {
  proposalId: 'proposal-42',
  repositoryId: 'repo-1',
  generatedAt: '2026-01-02T03:04:05Z',
  status: 'Accepted',
  generatedContentRelativePath: '.agents/proposals/proposal-42.md',
  generatedContentHash: 'generated-hash',
  editedContentRelativePath: '.agents/proposals/proposal-42.edited.md',
  semanticChanges: [],
  decisionAssimilation: {
    decisions: [],
    consequences: [],
    contradictions: [],
    limit: {
      limit: 8,
      reason: 'Keep the proposal reviewable.',
      totalAnalyzedItemCount: 9,
      totalQualifyingItemCount: 8,
      assimilatedItemCount: 7,
      omittedItemCount: 1,
    },
  },
  compressionSummary,
  review: {
    proposalId: 'proposal-42',
    reviewState: 'Stale',
    baselineCurrentContextHash: 'baseline-hash',
    reviewedContentHash: 'reviewed-hash',
    reviewedAt: '2026-01-03T04:05:06Z',
    reviewNote: 'Reviewer note',
    staleReason: 'Baseline context changed.',
  },
  promotion: {
    proposalId: 'proposal-42',
    promotedAt: null,
    promotedContentHash: 'promoted-hash',
    promotedContentSourceRelativePath: '.agents/proposals/proposal-42.edited.md',
    revisionNumber: 8,
    archivedRelativePath: '.agents/operational_context.0001.md',
    archiveFailureReason: 'Archive path exists.',
    writeFailureReason: 'Context is locked.',
  },
  generatedContent: null,
  editedContent: null,
}

const assimilationDecision: DecisionAssimilationRecord = {
  decisionId: 'DEC-0001',
  sourceRelativePath: '.agents/decisions/decisions.md',
  statement: 'Backend continuity services own operational context promotion.',
  taxonomy: 'ArchitecturalDecision',
  taxonomyBasis: {
    taxonomy: 'ArchitecturalDecision',
    matchedRules: ['architectural-continuity-keywords'],
    matchedEvidence: ['Matched architectural keyword: Backend continuity services own'],
    isHeuristicFallback: true,
    fallbackReason: 'No stronger taxonomy rule matched.',
    diagnostics: ['Taxonomy was ambiguous.'],
  },
  status: 'OmittedByLimit',
  isDurable: true,
  qualifiesForAssimilation: true,
  isAssimilated: false,
  isOmittedByLimit: true,
  exclusionReason: 'Excluded by backend policy.',
  omissionReason: 'Limit reached.',
  operationalStatement: 'Decision: Backend continuity services own operational context promotion.',
  rationale: 'Promotion is repository artifact mutation.',
  constraintsIntroduced: ['Promotion must write repository artifacts.'],
  consequencesIntroduced: ['Review panels must show backend promotion status.'],
  openQuestions: ['Should stale proposals be regenerated automatically?'],
  sourceEvidence: ['Decision statement: Backend continuity services own operational context promotion.'],
}

const assimilationLimit: DecisionAssimilationLimit = {
  limit: 8,
  reason: 'Keep the proposal reviewable.',
  totalAnalyzedItemCount: 11,
  totalQualifyingItemCount: 9,
  assimilatedItemCount: 8,
  omittedItemCount: 1,
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

  it('preserves operational-context lifecycle and summary facts without deriving review state', () => {
    expect(operationalContextProposalSummaryToEvidence(proposalSummary, 8)).toEqual([
      { label: 'Latest proposal', detail: 'proposal-42' },
      { label: 'Proposal status', detail: 'Accepted' },
      { label: 'Source inputs', detail: '4' },
      { label: 'Content bytes', detail: '1200' },
      { label: 'Content characters', detail: '1100' },
      { label: 'Current revisions', detail: '8' },
      {
        label: 'Archived prior context',
        detail: '.agents/operational_context.0001.md',
        source: '.agents/operational_context.0001.md',
      },
    ])

    expect(
      operationalContextProjectionToEvidence(
        operationalContext,
        'Included (1200 bytes)',
        'Accepted',
        'Accepted',
      ),
    ).toContainEqual({ label: 'Review state', detail: 'Accepted' })

    expect(operationalContextProposalLifecycleToDiagnostics(proposal)).toEqual([
      { label: 'Review blocked', detail: 'Baseline context changed.', tone: 'warning' },
      { label: 'Promotion archive failed', detail: 'Archive path exists.', tone: 'danger' },
      { label: 'Promotion write failed', detail: 'Context is locked.', tone: 'danger' },
    ])
    expect(operationalContextProposalLifecycleToEvidence(proposal)).toContainEqual({
      label: 'Review state',
      detail: 'Stale',
    })
  })

  it('preserves decision assimilation status, limits, taxonomy basis, and open questions', () => {
    expect(decisionAssimilationRecordToEvidence(assimilationDecision)).toEqual([
      {
        label: 'Decision',
        detail: 'DEC-0001',
        source: '.agents/decisions/decisions.md',
      },
      {
        label: 'Source',
        detail: '.agents/decisions/decisions.md',
        source: '.agents/decisions/decisions.md',
      },
      { label: 'Status', detail: 'OmittedByLimit' },
      { label: 'Taxonomy', detail: 'ArchitecturalDecision' },
      { label: 'Durability', detail: 'Durable' },
      { label: 'Qualifies', detail: 'Yes' },
      { label: 'Assimilated', detail: 'No' },
      { label: 'Omitted by limit', detail: 'Yes' },
      {
        label: 'Operational statement',
        detail: 'Decision: Backend continuity services own operational context promotion.',
      },
      { label: 'Rationale', detail: 'Promotion is repository artifact mutation.' },
      {
        label: 'Source evidence',
        detail: 'Decision statement: Backend continuity services own operational context promotion.',
        source: '.agents/decisions/decisions.md',
      },
    ])
    expect(decisionAssimilationRecordToConstraints(assimilationDecision)).toEqual([
      {
        label: 'Introduced constraint',
        detail: 'Promotion must write repository artifacts.',
        evidence: [
          {
            label: 'Decision',
            detail: 'DEC-0001',
            source: '.agents/decisions/decisions.md',
          },
        ],
      },
    ])
    expect(decisionAssimilationRecordToDiagnostics(assimilationDecision)).toHaveLength(2)
    expect(decisionAssimilationRecordToUncertainty(assimilationDecision)).toEqual([
      {
        label: 'Open question',
        detail: 'Should stale proposals be regenerated automatically?',
        severity: 'info',
        missingEvidence: [
          {
            label: 'Decision',
            detail: 'DEC-0001',
            source: '.agents/decisions/decisions.md',
          },
        ],
      },
    ])
    expect(decisionTaxonomyBasisToEvidence('DEC-0001', assimilationDecision.taxonomyBasis)).toContainEqual({
      label: 'Matched rule',
      detail: 'architectural-continuity-keywords',
    })
    expect(decisionTaxonomyBasisToDiagnostics(assimilationDecision.taxonomyBasis)).toEqual([
      {
        label: 'Heuristic fallback',
        detail: 'No stronger taxonomy rule matched.',
        tone: 'warning',
      },
      {
        label: 'Taxonomy diagnostic',
        detail: 'Taxonomy was ambiguous.',
        tone: 'info',
        evidence: [{ label: 'Taxonomy', detail: 'ArchitecturalDecision' }],
      },
    ])
    expect(decisionAssimilationLimitToEvidence(assimilationLimit)).toEqual([
      { label: 'Analyzed', detail: '11' },
      { label: 'Qualifying', detail: '9' },
      { label: 'Assimilated', detail: '8' },
      { label: 'Omitted', detail: '1' },
      { label: 'Limit', detail: '8' },
    ])
    expect(decisionAssimilationLimitToConstraints(assimilationLimit)).toEqual([
      {
        label: 'Assimilation limit',
        detail: 'Keep the proposal reviewable.',
        evidence: decisionAssimilationLimitToEvidence(assimilationLimit),
      },
    ])
  })

  it('preserves consequence and contradiction evidence without deriving conflict meaning', () => {
    const consequence = {
      consequenceId: 'consequence-1',
      originatingDecision: {
        decisionId: 'DEC-0001',
        sourceRelativePath: '.agents/decisions/decisions.md',
        statement: 'Backend continuity services own operational context promotion.',
        taxonomy: 'ArchitecturalDecision',
      },
      operationalStatement: 'Review panels must show backend promotion status.',
      affectedArea: 'Operational context review',
      supportingEvidence: ['Consequence statement: Review panels must show backend promotion status.'],
      operationalImpact: 'Review panels need backend promotion status.',
    }
    const contradiction = {
      contradictionId: 'contradiction-1',
      firstDecision: consequence.originatingDecision,
      secondDecision: {
        decisionId: 'DEC-0002',
        sourceRelativePath: '.agents/decisions/decisions.0001.md',
        statement: 'React owns operational context promotion.',
        taxonomy: 'ArchitecturalDecision',
      },
      conflictType: 'DirectNegation',
      severity: 'Critical',
      evidence: ['DEC-0001 and DEC-0002 assign different authorities.'],
      resolutionGuidance: 'Resolve authority before assimilating.',
    }

    expect(continuityDecisionConsequenceToEvidence(consequence)).toContainEqual({
      label: 'Operational impact',
      detail: 'Review panels need backend promotion status.',
    })
    expect(continuityDecisionContradictionToEvidence(contradiction)).toContainEqual({
      label: 'Conflict type',
      detail: 'DirectNegation',
    })
    expect(continuityDecisionContradictionToDiagnostics(contradiction)).toEqual([
      {
        label: 'DirectNegation',
        detail: 'Resolve authority before assimilating.',
        tone: 'danger',
        evidence: [
          {
            label: 'First decision',
            detail: 'DEC-0001: Backend continuity services own operational context promotion.',
            source: '.agents/decisions/decisions.md',
          },
          {
            label: 'Second decision',
            detail: 'DEC-0002: React owns operational context promotion.',
            source: '.agents/decisions/decisions.0001.md',
          },
          {
            label: 'Contradiction evidence',
            detail: 'DEC-0001 and DEC-0002 assign different authorities.',
          },
        ],
      },
    ])
  })
})
