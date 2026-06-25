import type {
  CompressionTrend,
  ContinuityDiagnosticGroup,
  ContinuityDiagnostics,
  ContinuityReport,
  ContinuityTrend,
  OperationalContextCompressionOutcome,
  OperationalContextCompressionSummary,
  OperationalContextSemanticChange,
  OperationalEvolutionTimelineEntry,
} from '../../types'
import type {
  ExplanationConstraint,
  ExplanationDiagnostic,
  ExplanationEvidence,
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
