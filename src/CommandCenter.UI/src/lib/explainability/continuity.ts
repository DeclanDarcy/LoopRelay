import type {
  CompressionTrend,
  ContinuityDiagnosticGroup,
  ContinuityDiagnostics,
  ContinuityReport,
  ContinuityTrend,
  ContinuityDecisionConsequence,
  ContinuityDecisionContradiction,
  DecisionAssimilationLimit,
  DecisionAssimilationRecord,
  DecisionTaxonomyBasis,
  OperationalContextCompressionOutcome,
  OperationalContextCompressionSummary,
  OperationalContextProjection,
  OperationalContextProposal,
  OperationalContextProposalSummary,
  OperationalContextSemanticChange,
  OperationalEvolutionTimelineEntry,
} from '../../types'
import type {
  ExplanationConstraint,
  ExplanationDiagnostic,
  ExplanationEvidence,
  ExplanationUncertainty,
  ExplanationTone,
} from '../../types/explainability'

export function continuityDiagnosticGroupsToDiagnostics(
  groups: ContinuityDiagnosticGroup[],
): ExplanationDiagnostic[] {
  return groups.flatMap((group) =>
    group.diagnostics.map((diagnostic) => ({
      label: group.title || group.category,
      detail: diagnostic,
      tone: continuityToneFromCategory(group.category),
      evidence: [
        {
          label: 'Diagnostic category',
          detail: group.category,
        },
      ],
    })),
  )
}

export function continuityWarningsToDiagnostics(
  warnings: string[],
  label = 'Continuity warning',
): ExplanationDiagnostic[] {
  return warnings.map((warning) => ({
    label,
    detail: warning,
    tone: 'warning',
  }))
}

export function continuityRepeatedSignalsToDiagnostics(
  diagnostics: ContinuityDiagnostics,
): ExplanationDiagnostic[] {
  return [
    ...diagnostics.repeatedInvestigationIndicators.map((indicator) => ({
      label: 'Repeated investigation',
      detail: indicator,
      tone: 'info' as const,
    })),
    ...diagnostics.repeatedQuestionIndicators.map((indicator) => ({
      label: 'Repeated question',
      detail: indicator,
      tone: 'info' as const,
    })),
    ...diagnostics.decisionReworkIndicators.map((indicator) => ({
      label: 'Decision rework',
      detail: indicator,
      tone: 'info' as const,
    })),
  ]
}

export function continuityCompressionTrendToDiagnostics(
  trend: CompressionTrend,
): ExplanationDiagnostic[] {
  return [
    ...trend.warnings.map((warning) => ({
      label: 'Compression warning',
      detail: warning,
      tone: 'warning' as const,
    })),
    ...trend.noiseRemovedIndicators.map((indicator) => ({
      label: 'Noise removed',
      detail: indicator,
      tone: 'info' as const,
    })),
  ]
}

export function operationalContextCompressionSummaryToDiagnostics(
  summary: OperationalContextCompressionSummary,
): ExplanationDiagnostic[] {
  return [
    ...summary.warnings.map((warning) => ({
      label: 'Compression warning',
      detail: warning,
      tone: 'warning' as const,
    })),
    ...summary.stableUnderstandingRetentionWarnings.map((warning) => ({
      label: 'Retention warning',
      detail: warning,
      tone: 'warning' as const,
    })),
    ...summary.noiseRemovedIndicators.map((indicator) => ({
      label: 'Compressed understanding',
      detail: indicator,
      tone: 'info' as const,
    })),
  ]
}

export function operationalContextCompressionRevisionsToEvidence(
  summary: OperationalContextCompressionSummary,
): ExplanationEvidence[] {
  return summary.revisionSummary.map((item) => ({
    label: 'Revision summary',
    detail: item,
  }))
}

export function operationalContextCompressionOutcomeToConstraints(
  outcome: OperationalContextCompressionOutcome,
): ExplanationConstraint[] {
  return [
    {
      label: 'Rule',
      detail: outcome.rule,
    },
    {
      label: 'Threshold',
      detail: outcome.threshold,
    },
    {
      label: 'Rationale',
      detail: outcome.rationale,
    },
  ]
}

export function operationalContextCompressionOutcomeToEvidence(
  outcome: OperationalContextCompressionOutcome,
  label = 'Compression evidence',
): ExplanationEvidence[] {
  return outcome.evidence.map((evidence) => ({
    label,
    detail: evidence,
  }))
}

export function operationalContextSemanticChangeToEvidence(
  change: OperationalContextSemanticChange,
): ExplanationEvidence[] {
  return [
    ...(change.identityBasis ? [{ label: 'Identity basis', detail: change.identityBasis }] : []),
    ...(change.modificationReason ? [{ label: 'Reason', detail: change.modificationReason }] : []),
    ...(change.previousState ? [{ label: 'Previous', detail: change.previousState }] : []),
    ...(change.currentState ? [{ label: 'Current', detail: change.currentState }] : []),
    ...change.supportingEvidence.map((evidence) => ({
      label: 'Supporting evidence',
      detail: evidence,
    })),
  ]
}

export function operationalContextSemanticChangeSupportingEvidenceToEvidence(
  change: OperationalContextSemanticChange,
): ExplanationEvidence[] {
  return change.supportingEvidence.map((evidence) => ({
    label: 'Supporting evidence',
    detail: evidence,
  }))
}

export function operationalEvolutionTimelineEntryToEvidence(
  entry: OperationalEvolutionTimelineEntry,
): ExplanationEvidence[] {
  return [
    ...(entry.itemId ? [{ label: 'Item id', detail: entry.itemId }] : []),
    ...(entry.identityBasis ? [{ label: 'Identity basis', detail: entry.identityBasis }] : []),
    ...(entry.reason ? [{ label: 'Reason', detail: entry.reason }] : []),
    ...(entry.previousState ? [{ label: 'Previous state', detail: entry.previousState }] : []),
    ...(entry.currentState ? [{ label: 'Current state', detail: entry.currentState }] : []),
    ...(entry.previousRevisionNumber
      ? [{ label: 'Previous revision', detail: String(entry.previousRevisionNumber) }]
      : []),
    ...(entry.currentRevisionNumber
      ? [{ label: 'Current revision', detail: String(entry.currentRevisionNumber) }]
      : []),
    ...entry.supportingEvidence.map((evidence) => ({
      label: 'Evolution evidence',
      detail: evidence,
    })),
  ]
}

export function continuityReportToEvidence(report: ContinuityReport): ExplanationEvidence[] {
  return [
    {
      id: report.reportId,
      label: 'Latest report',
      detail: report.reportId,
      source: report.relativePath,
    },
    {
      label: 'Diagnostics revisions',
      detail: String(report.diagnostics.revisionCount),
    },
  ]
}

export function continuityTrendToEvidence(
  label: string,
  trend: ContinuityTrend,
): ExplanationEvidence[] {
  return [
    { label: `${label} added`, detail: String(trend.addedCount) },
    { label: `${label} modified`, detail: String(trend.modifiedCount) },
    { label: `${label} removed`, detail: String(trend.removedCount) },
    { label: `${label} resolved`, detail: String(trend.resolvedCount) },
    { label: `${label} lost`, detail: String(trend.lostCount) },
  ]
}

export function operationalContextProposalLifecycleToEvidence(
  proposal: OperationalContextProposal,
): ExplanationEvidence[] {
  return [
    { label: 'Proposal id', detail: proposal.proposalId },
    { label: 'Proposal status', detail: proposal.status },
    { label: 'Generated content hash', detail: proposal.generatedContentHash },
    {
      label: 'Generated content path',
      detail: proposal.generatedContentRelativePath,
      source: proposal.generatedContentRelativePath,
    },
    ...(proposal.editedContentRelativePath
      ? [
          {
            label: 'Edited content path',
            detail: proposal.editedContentRelativePath,
            source: proposal.editedContentRelativePath,
          },
        ]
      : []),
    { label: 'Review state', detail: proposal.review.reviewState },
    { label: 'Baseline context hash', detail: proposal.review.baselineCurrentContextHash },
    ...(proposal.review.reviewedContentHash
      ? [{ label: 'Reviewed content hash', detail: proposal.review.reviewedContentHash }]
      : []),
    ...(proposal.review.reviewNote
      ? [{ label: 'Review note', detail: proposal.review.reviewNote }]
      : []),
    ...(proposal.promotion.promotedContentHash
      ? [{ label: 'Promoted content hash', detail: proposal.promotion.promotedContentHash }]
      : []),
    ...(proposal.promotion.promotedContentSourceRelativePath
      ? [
          {
            label: 'Promotion source',
            detail: proposal.promotion.promotedContentSourceRelativePath,
            source: proposal.promotion.promotedContentSourceRelativePath,
          },
        ]
      : []),
    ...(proposal.promotion.revisionNumber
      ? [{ label: 'Promotion revision', detail: String(proposal.promotion.revisionNumber) }]
      : []),
    ...(proposal.promotion.archivedRelativePath
      ? [
          {
            label: 'Archived prior context',
            detail: proposal.promotion.archivedRelativePath,
            source: proposal.promotion.archivedRelativePath,
          },
        ]
      : []),
  ]
}

export function operationalContextProposalLifecycleToDiagnostics(
  proposal: OperationalContextProposal,
): ExplanationDiagnostic[] {
  return [
    ...(proposal.review.staleReason
      ? [
          {
            label: 'Review blocked',
            detail: proposal.review.staleReason,
            tone: 'warning' as const,
          },
        ]
      : []),
    ...(proposal.promotion.archiveFailureReason
      ? [
          {
            label: 'Promotion archive failed',
            detail: proposal.promotion.archiveFailureReason,
            tone: 'danger' as const,
          },
        ]
      : []),
    ...(proposal.promotion.writeFailureReason
      ? [
          {
            label: 'Promotion write failed',
            detail: proposal.promotion.writeFailureReason,
            tone: 'danger' as const,
          },
        ]
      : []),
  ]
}

export function operationalContextProposalSummaryToEvidence(
  summary: OperationalContextProposalSummary,
  revisionCount: number,
): ExplanationEvidence[] {
  if (!summary.latestProposalId) {
    return []
  }

  return [
    { label: 'Latest proposal', detail: summary.latestProposalId },
    { label: 'Proposal status', detail: summary.status ?? 'Unknown' },
    { label: 'Source inputs', detail: String(summary.sourceInputCount) },
    { label: 'Content bytes', detail: String(summary.contentByteCount) },
    { label: 'Content characters', detail: String(summary.contentCharacterCount) },
    { label: 'Current revisions', detail: String(revisionCount) },
    ...(summary.lastArchivedRelativePath
      ? [
          {
            label: 'Archived prior context',
            detail: summary.lastArchivedRelativePath,
            source: summary.lastArchivedRelativePath,
          },
        ]
      : []),
  ]
}

export function operationalContextProjectionToEvidence(
  projection: OperationalContextProjection,
  executionStatus: string,
  reviewStatus: string,
  proposalStatus: string,
): ExplanationEvidence[] {
  return [
    ...(projection.currentRelativePath
      ? [
          {
            label: 'Current context path',
            detail: projection.currentRelativePath,
            source: projection.currentRelativePath,
          },
        ]
      : []),
    { label: 'Execution context', detail: executionStatus },
    { label: 'Review state', detail: reviewStatus },
    { label: 'Proposal status', detail: proposalStatus },
    { label: 'Revision count', detail: String(projection.revisionCount) },
    { label: 'Current revision', detail: String(projection.currentRevisionNumber) },
    { label: 'Questions', detail: String(projection.openQuestions.length) },
    { label: 'Risks', detail: String(projection.activeRisks.length) },
    { label: 'Current model items', detail: String(projection.currentUnderstandingSummary.length) },
  ]
}

export function decisionAssimilationRecordToEvidence(
  decision: DecisionAssimilationRecord,
): ExplanationEvidence[] {
  return [
    { label: 'Decision', detail: decision.decisionId, source: decision.sourceRelativePath },
    { label: 'Source', detail: decision.sourceRelativePath, source: decision.sourceRelativePath },
    { label: 'Status', detail: decision.status },
    { label: 'Taxonomy', detail: decision.taxonomy },
    { label: 'Durability', detail: decision.isDurable ? 'Durable' : 'Not durable' },
    { label: 'Qualifies', detail: decision.qualifiesForAssimilation ? 'Yes' : 'No' },
    { label: 'Assimilated', detail: decision.isAssimilated ? 'Yes' : 'No' },
    { label: 'Omitted by limit', detail: decision.isOmittedByLimit ? 'Yes' : 'No' },
    ...(decision.operationalStatement
      ? [{ label: 'Operational statement', detail: decision.operationalStatement }]
      : []),
    ...(decision.rationale ? [{ label: 'Rationale', detail: decision.rationale }] : []),
    ...decision.sourceEvidence.map((evidence) => ({
      label: 'Source evidence',
      detail: evidence,
      source: decision.sourceRelativePath,
    })),
  ]
}

export function decisionAssimilationRecordToConstraints(
  decision: DecisionAssimilationRecord,
): ExplanationConstraint[] {
  return decision.constraintsIntroduced.map((constraint) => ({
    label: 'Introduced constraint',
    detail: constraint,
    evidence: [{ label: 'Decision', detail: decision.decisionId, source: decision.sourceRelativePath }],
  }))
}

export function decisionAssimilationRecordToDiagnostics(
  decision: DecisionAssimilationRecord,
): ExplanationDiagnostic[] {
  return [
    ...(decision.exclusionReason
      ? [
          {
            label: 'Exclusion reason',
            detail: decision.exclusionReason,
            tone: 'warning' as const,
            evidence: [
              { label: 'Decision status', detail: decision.status },
              { label: 'Decision', detail: decision.decisionId, source: decision.sourceRelativePath },
            ],
          },
        ]
      : []),
    ...(decision.omissionReason
      ? [
          {
            label: 'Omission reason',
            detail: decision.omissionReason,
            tone: 'warning' as const,
            evidence: [
              { label: 'Decision status', detail: decision.status },
              { label: 'Decision', detail: decision.decisionId, source: decision.sourceRelativePath },
            ],
          },
        ]
      : []),
  ]
}

export function decisionAssimilationRecordToUncertainty(
  decision: DecisionAssimilationRecord,
): ExplanationUncertainty[] {
  return decision.openQuestions.map((question) => ({
    label: 'Open question',
    detail: question,
    severity: 'info',
    missingEvidence: [
      { label: 'Decision', detail: decision.decisionId, source: decision.sourceRelativePath },
    ],
  }))
}

export function decisionAssimilationConsequencesToEvidence(
  decision: DecisionAssimilationRecord,
): ExplanationEvidence[] {
  return decision.consequencesIntroduced.map((consequence) => ({
    label: 'Introduced consequence',
    detail: consequence,
    source: decision.sourceRelativePath,
  }))
}

export function decisionTaxonomyBasisToEvidence(
  decisionId: string,
  basis: DecisionTaxonomyBasis,
): ExplanationEvidence[] {
  return [
    { label: 'Taxonomy', detail: basis.taxonomy },
    ...basis.matchedRules.map((rule) => ({ label: 'Matched rule', detail: rule })),
    ...basis.matchedEvidence.map((evidence) => ({ label: 'Matched evidence', detail: evidence })),
    ...(basis.fallbackReason
      ? [{ label: 'Fallback reason', detail: basis.fallbackReason }]
      : []),
    { label: 'Decision', detail: decisionId },
  ]
}

export function decisionTaxonomyBasisToDiagnostics(
  basis: DecisionTaxonomyBasis,
): ExplanationDiagnostic[] {
  return [
    ...(basis.isHeuristicFallback
      ? [
          {
            label: 'Heuristic fallback',
            detail: basis.fallbackReason ?? 'Taxonomy basis used heuristic fallback.',
            tone: 'warning' as const,
          },
        ]
      : []),
    ...basis.diagnostics.map((diagnostic) => ({
      label: 'Taxonomy diagnostic',
      detail: diagnostic,
      tone: 'info' as const,
      evidence: [{ label: 'Taxonomy', detail: basis.taxonomy }],
    })),
  ]
}

export function decisionAssimilationLimitToEvidence(
  limit: DecisionAssimilationLimit,
): ExplanationEvidence[] {
  return [
    { label: 'Analyzed', detail: String(limit.totalAnalyzedItemCount) },
    { label: 'Qualifying', detail: String(limit.totalQualifyingItemCount) },
    { label: 'Assimilated', detail: String(limit.assimilatedItemCount) },
    { label: 'Omitted', detail: String(limit.omittedItemCount) },
    { label: 'Limit', detail: String(limit.limit) },
  ]
}

export function decisionAssimilationLimitToConstraints(
  limit: DecisionAssimilationLimit,
): ExplanationConstraint[] {
  return [
    {
      label: 'Assimilation limit',
      detail: limit.reason,
      evidence: decisionAssimilationLimitToEvidence(limit),
    },
  ]
}

export function continuityDecisionConsequenceToEvidence(
  consequence: ContinuityDecisionConsequence,
): ExplanationEvidence[] {
  return [
    { label: 'Consequence', detail: consequence.consequenceId },
    { label: 'Affected area', detail: consequence.affectedArea },
    {
      label: 'Originating decision',
      detail: consequence.originatingDecision.statement,
      source: consequence.originatingDecision.sourceRelativePath,
    },
    { label: 'Decision id', detail: consequence.originatingDecision.decisionId },
    { label: 'Decision taxonomy', detail: consequence.originatingDecision.taxonomy },
    { label: 'Operational impact', detail: consequence.operationalImpact },
    ...consequence.supportingEvidence.map((evidence) => ({
      label: 'Consequence evidence',
      detail: evidence,
      source: consequence.originatingDecision.sourceRelativePath,
    })),
  ]
}

export function continuityDecisionContradictionToDiagnostics(
  contradiction: ContinuityDecisionContradiction,
): ExplanationDiagnostic[] {
  return [
    {
      label: contradiction.conflictType,
      detail: contradiction.resolutionGuidance,
      tone: contradiction.severity.toLowerCase() === 'critical' ? 'danger' : 'warning',
      evidence: [
        {
          label: 'First decision',
          detail: `${contradiction.firstDecision.decisionId}: ${contradiction.firstDecision.statement}`,
          source: contradiction.firstDecision.sourceRelativePath,
        },
        {
          label: 'Second decision',
          detail: `${contradiction.secondDecision.decisionId}: ${contradiction.secondDecision.statement}`,
          source: contradiction.secondDecision.sourceRelativePath,
        },
        ...contradiction.evidence.map((evidence) => ({
          label: 'Contradiction evidence',
          detail: evidence,
        })),
      ],
    },
  ]
}

export function continuityDecisionContradictionToEvidence(
  contradiction: ContinuityDecisionContradiction,
): ExplanationEvidence[] {
  return [
    { label: 'Contradiction', detail: contradiction.contradictionId },
    { label: 'Severity', detail: contradiction.severity },
    { label: 'Conflict type', detail: contradiction.conflictType },
    {
      label: 'First source',
      detail: contradiction.firstDecision.sourceRelativePath,
      source: contradiction.firstDecision.sourceRelativePath,
    },
    {
      label: 'Second source',
      detail: contradiction.secondDecision.sourceRelativePath,
      source: contradiction.secondDecision.sourceRelativePath,
    },
  ]
}

function continuityToneFromCategory(category: string): ExplanationTone {
  const normalized = category.toLowerCase()
  if (normalized.includes('warning') || normalized.includes('risk') || normalized.includes('lost')) {
    return 'warning'
  }
  if (normalized.includes('compression') || normalized.includes('diff') || normalized.includes('evolution')) {
    return 'info'
  }
  return 'neutral'
}
