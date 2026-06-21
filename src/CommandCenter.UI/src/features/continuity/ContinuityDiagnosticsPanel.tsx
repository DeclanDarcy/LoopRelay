import { StatusBadge } from '../../components/design'
import { continuityWarningStatus } from '../../lib/status'
import type { ContinuityDiagnostics } from '../../types'

type ContinuityDiagnosticsPanelProps = {
  diagnostics: ContinuityDiagnostics
}

export function ContinuityDiagnosticsPanel({
  diagnostics,
}: ContinuityDiagnosticsPanelProps) {
  const repeatedSignalCount =
    diagnostics.repeatedInvestigationIndicators.length +
    diagnostics.repeatedQuestionIndicators.length +
    diagnostics.decisionReworkIndicators.length

  return (
    <div className="context-artifact-previews">
      <div className="context-summary">
        <span>Revisions: {diagnostics.revisionCount}</span>
        <span>Current size: {diagnostics.currentContextByteCount} bytes</span>
        <span>Growth: {diagnostics.contextByteGrowth} bytes</span>
        <span>Average: {Math.round(diagnostics.averageBytesPerRevision)} bytes/revision</span>
        <span>Questions resolved: {diagnostics.openQuestionTrend.resolvedCount}</span>
        <span>Questions lost: {diagnostics.openQuestionTrend.lostCount}</span>
        <span>Risks retired: {diagnostics.activeRiskTrend.resolvedCount}</span>
        <span>Risks lost: {diagnostics.activeRiskTrend.lostCount}</span>
        <span>Decisions lost: {diagnostics.decisionTrend.lostCount}</span>
        <span>Rationale lost: {diagnostics.rationaleTrend.lostCount}</span>
        <span>
          Continuity: <StatusBadge status={continuityWarningStatus(diagnostics)} />
        </span>
      </div>

      <div className="context-columns">
        <div>
          <h5>Preservation</h5>
          <ul>
            <li>Architecture lost: {diagnostics.architectureTrend.lostCount}</li>
            <li>Constraints lost: {diagnostics.constraintTrend.lostCount}</li>
            <li>Decisions added: {diagnostics.decisionTrend.addedCount}</li>
            <li>Decisions removed: {diagnostics.decisionTrend.removedCount}</li>
          </ul>
        </div>
        <div>
          <h5>Compression</h5>
          <ul>
            <li>Proposals observed: {diagnostics.compressionTrend.proposalCount}</li>
            <li>Items compressed: {diagnostics.compressionTrend.compressedItemCount}</li>
            <li>Items removed: {diagnostics.compressionTrend.removedItemCount}</li>
            <li>Warnings: {diagnostics.compressionTrend.warningCount}</li>
          </ul>
        </div>
        <div>
          <h5>Repeated Signals</h5>
          {repeatedSignalCount > 0 ? (
            <ul>
              {diagnostics.repeatedInvestigationIndicators.map((indicator) => (
                <li key={`investigation-${indicator}`}>{indicator}</li>
              ))}
              {diagnostics.repeatedQuestionIndicators.map((indicator) => (
                <li key={`question-${indicator}`}>{indicator}</li>
              ))}
              {diagnostics.decisionReworkIndicators.map((indicator) => (
                <li key={`decision-${indicator}`}>{indicator}</li>
              ))}
            </ul>
          ) : (
            <p>No repeated indicators recorded.</p>
          )}
        </div>
        <div>
          <h5>Warnings</h5>
          {diagnostics.continuityWarnings.length > 0 ? (
            <ul>
              {diagnostics.continuityWarnings.map((warning) => (
                <li key={warning}>{warning}</li>
              ))}
            </ul>
          ) : (
            <p>No continuity warnings recorded.</p>
          )}
        </div>
      </div>
    </div>
  )
}
