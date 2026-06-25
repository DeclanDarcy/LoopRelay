import { DiagnosticList, EvidenceList } from '../../components/explainability'
import {
  operationalContextCompressionRevisionsToEvidence,
  operationalContextCompressionSummaryToDiagnostics,
} from '../../lib/explainability'
import type { OperationalContextCompressionSummary } from '../../types'

type OperationalContextCompressionSummaryPanelProps = {
  compressionSummary: OperationalContextCompressionSummary
  onOpenContinuityCompression?: () => void
  onOpenContinuityDecisionRetention?: () => void
}

export function OperationalContextCompressionSummaryPanel({
  compressionSummary,
  onOpenContinuityCompression,
  onOpenContinuityDecisionRetention,
}: OperationalContextCompressionSummaryPanelProps) {
  const compressionDiagnostics = operationalContextCompressionSummaryToDiagnostics(compressionSummary)
  const revisionEvidence = operationalContextCompressionRevisionsToEvidence(compressionSummary)
  const compressionWarningDiagnostics = compressionDiagnostics.filter(
    (diagnostic) => diagnostic.label === 'Compression warning',
  )
  const retentionWarningDiagnostics = compressionDiagnostics.filter(
    (diagnostic) => diagnostic.label === 'Retention warning',
  )
  const compressedUnderstandingDiagnostics = compressionDiagnostics.filter(
    (diagnostic) => diagnostic.label === 'Compressed understanding',
  )

  return (
    <>
      <h5>Compression Summary</h5>
      <div className="context-summary-grid">
        <span>Preserved: {compressionSummary.preservedItemCount}</span>
        <span>Added: {compressionSummary.addedItemCount}</span>
        <span>Modified: {compressionSummary.modifiedItemCount}</span>
        <span>Removed: {compressionSummary.removedItemCount}</span>
        <span>Compressed: {compressionSummary.compressedItemCount}</span>
        <span>Permanent: {compressionSummary.permanentUnderstandingItemCount}</span>
        <span>Active: {compressionSummary.activeUnderstandingItemCount}</span>
        <span>Historical: {compressionSummary.historicalUnderstandingItemCount}</span>
        <span>Noise: {compressionSummary.historicalNoiseItemCount}</span>
        <span>Resolved: {compressionSummary.resolvedQuestionCount}</span>
        <span>Retired: {compressionSummary.retiredRiskCount}</span>
        <span>Warnings: {compressionSummary.warningCount}</span>
      </div>
      {compressionSummary.warnings.length > 0 ? (
        <div className="proposal-warning-list">
          <DiagnosticList
            diagnostics={compressionWarningDiagnostics}
            title="Compression Warnings"
          />
          {onOpenContinuityCompression ? (
            <button
              type="button"
              className="workspace-cross-link inline-cross-link warning-link"
              onClick={onOpenContinuityCompression}
            >
              Open compression diagnostics
            </button>
          ) : null}
        </div>
      ) : null}
      {compressionSummary.revisionSummary.length > 0 ? (
        <div className="proposal-warning-list proposal-revision-summary">
          <EvidenceList evidence={revisionEvidence} title="Revision Summary" />
        </div>
      ) : null}
      {compressionSummary.stableUnderstandingRetentionWarnings.length > 0 ? (
        <div className="proposal-warning-list">
          <DiagnosticList
            diagnostics={retentionWarningDiagnostics}
            title="Retention Warnings"
          />
          {onOpenContinuityDecisionRetention ? (
            <button
              type="button"
              className="workspace-cross-link inline-cross-link warning-link"
              onClick={onOpenContinuityDecisionRetention}
            >
              Open decision retention
            </button>
          ) : null}
        </div>
      ) : null}
      {compressionSummary.noiseRemovedIndicators.length > 0 ? (
        <div className="proposal-warning-list">
          <DiagnosticList
            diagnostics={compressedUnderstandingDiagnostics}
            title="Compressed Understanding"
          />
        </div>
      ) : null}
    </>
  )
}
