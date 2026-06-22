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
          <h5>Compression Warnings</h5>
          <ul>
            {compressionSummary.warnings.map((warning) => (
              <li key={warning}>
                {onOpenContinuityCompression ? (
                  <button
                    type="button"
                    className="workspace-cross-link inline-cross-link warning-link"
                    onClick={onOpenContinuityCompression}
                  >
                    {warning}
                  </button>
                ) : (
                  warning
                )}
              </li>
            ))}
          </ul>
        </div>
      ) : null}
      {compressionSummary.revisionSummary.length > 0 ? (
        <div className="proposal-warning-list proposal-revision-summary">
          <h5>Revision Summary</h5>
          <ul>
            {compressionSummary.revisionSummary.map((summary) => (
              <li key={summary}>{summary}</li>
            ))}
          </ul>
        </div>
      ) : null}
      {compressionSummary.stableUnderstandingRetentionWarnings.length > 0 ? (
        <div className="proposal-warning-list">
          <h5>Retention Warnings</h5>
          <ul>
            {compressionSummary.stableUnderstandingRetentionWarnings.map((warning) => (
              <li key={warning}>
                {onOpenContinuityDecisionRetention ? (
                  <button
                    type="button"
                    className="workspace-cross-link inline-cross-link warning-link"
                    onClick={onOpenContinuityDecisionRetention}
                  >
                    {warning}
                  </button>
                ) : (
                  warning
                )}
              </li>
            ))}
          </ul>
        </div>
      ) : null}
      {compressionSummary.noiseRemovedIndicators.length > 0 ? (
        <div className="proposal-warning-list">
          <h5>Compressed Understanding</h5>
          <ul>
            {compressionSummary.noiseRemovedIndicators.map((indicator) => (
              <li key={indicator}>{indicator}</li>
            ))}
          </ul>
        </div>
      ) : null}
    </>
  )
}
