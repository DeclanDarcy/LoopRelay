import type { OperationalContextCompressionSummary } from '../../types'

type OperationalContextCompressionSummaryPanelProps = {
  compressionSummary: OperationalContextCompressionSummary
}

export function OperationalContextCompressionSummaryPanel({
  compressionSummary,
}: OperationalContextCompressionSummaryPanelProps) {
  return (
    <>
      <h5>Compression Summary</h5>
      <div className="context-summary-grid">
        <span>Preserved: {compressionSummary.preservedItemCount}</span>
        <span>Added: {compressionSummary.addedItemCount}</span>
        <span>Removed: {compressionSummary.removedItemCount}</span>
        <span>Compressed: {compressionSummary.compressedItemCount}</span>
        <span>Permanent: {compressionSummary.permanentUnderstandingItemCount}</span>
        <span>Active: {compressionSummary.activeUnderstandingItemCount}</span>
        <span>Historical: {compressionSummary.historicalUnderstandingItemCount}</span>
        <span>Resolved: {compressionSummary.resolvedQuestionCount}</span>
        <span>Retired: {compressionSummary.retiredRiskCount}</span>
        <span>Warnings: {compressionSummary.warningCount}</span>
      </div>
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
              <li key={warning}>{warning}</li>
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
