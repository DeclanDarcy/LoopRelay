import { StatusBadge, Table } from '../../components/design'
import { formatDateTime } from '../../lib'
import { continuityWarningStatus } from '../../lib/status'
import type { ContinuityDiagnostics, ContinuityReport, ContinuityTrend } from '../../types'

type ContinuityDiagnosticsPanelProps = {
  diagnostics: ContinuityDiagnostics
  reports?: ContinuityReport[]
  onOpenOperationalContextSection?: (sectionId: string) => void
  onOpenReport?: (relativePath: string) => void
}

export function ContinuityDiagnosticsPanel({
  diagnostics,
  reports = [],
  onOpenOperationalContextSection,
  onOpenReport,
}: ContinuityDiagnosticsPanelProps) {
  const repeatedSignalCount =
    diagnostics.repeatedInvestigationIndicators.length +
    diagnostics.repeatedQuestionIndicators.length +
    diagnostics.decisionReworkIndicators.length
  const latestReport = reports[0] ?? null

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

      <div id="continuity-evolution">
        <h5>Understanding Evolution</h5>
        <Table aria-label="Understanding evolution">
          <thead>
            <tr>
              <th>Section</th>
              <th>Added</th>
              <th>Removed</th>
              <th>Resolved</th>
              <th>Lost</th>
            </tr>
          </thead>
          <tbody>
            {[
              {
                label: 'Architecture',
                trend: diagnostics.architectureTrend,
                sectionId: 'operational-architecture',
              },
              {
                label: 'Constraints',
                trend: diagnostics.constraintTrend,
                sectionId: 'operational-constraints',
              },
              {
                label: 'Stable decisions',
                trend: diagnostics.decisionTrend,
                sectionId: 'operational-stable-decisions',
              },
              {
                label: 'Rationale',
                trend: diagnostics.rationaleTrend,
                sectionId: 'operational-decision-rationale',
              },
              {
                label: 'Open questions',
                trend: diagnostics.openQuestionTrend,
                sectionId: 'operational-open-questions',
              },
              {
                label: 'Active risks',
                trend: diagnostics.activeRiskTrend,
                sectionId: 'operational-active-risks',
              },
            ].map((row) => (
              <ContinuityEvolutionRow
                key={row.label}
                label={row.label}
                trend={row.trend}
                sectionId={row.sectionId}
                onOpenOperationalContextSection={onOpenOperationalContextSection}
              />
            ))}
          </tbody>
        </Table>
      </div>

      <div className="context-columns">
        <div id="continuity-decision-retention">
          <h5>Preservation</h5>
          <ul>
            <li>Architecture lost: {diagnostics.architectureTrend.lostCount}</li>
            <li>Constraints lost: {diagnostics.constraintTrend.lostCount}</li>
            <li>Decisions added: {diagnostics.decisionTrend.addedCount}</li>
            <li>Decisions removed: {diagnostics.decisionTrend.removedCount}</li>
          </ul>
        </div>
        <div id="continuity-compression">
          <h5>Compression</h5>
          <ul>
            <li>Proposals observed: {diagnostics.compressionTrend.proposalCount}</li>
            <li>Items compressed: {diagnostics.compressionTrend.compressedItemCount}</li>
            <li>Items removed: {diagnostics.compressionTrend.removedItemCount}</li>
            <li>Questions resolved: {diagnostics.compressionTrend.resolvedQuestionCount}</li>
            <li>Risks retired: {diagnostics.compressionTrend.retiredRiskCount}</li>
            <li>Warnings: {diagnostics.compressionTrend.warningCount}</li>
          </ul>
          {diagnostics.compressionTrend.warnings.length > 0 ? (
            <ul aria-label="Compression warnings">
              {diagnostics.compressionTrend.warnings.map((warning) => (
                <li key={warning}>{warning}</li>
              ))}
            </ul>
          ) : null}
          {diagnostics.compressionTrend.noiseRemovedIndicators.length > 0 ? (
            <ul aria-label="Noise removed indicators">
              {diagnostics.compressionTrend.noiseRemovedIndicators.map((indicator) => (
                <li key={indicator}>{indicator}</li>
              ))}
            </ul>
          ) : null}
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
        <div id="continuity-warnings">
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
        <div id="continuity-question-risk-lifecycle">
          <h5>Question and Risk Lifecycle</h5>
          <ul>
            <li>Questions added: {diagnostics.openQuestionTrend.addedCount}</li>
            <li>Questions resolved: {diagnostics.openQuestionTrend.resolvedCount}</li>
            <li>Questions lost: {diagnostics.openQuestionTrend.lostCount}</li>
            <li>Risks added: {diagnostics.activeRiskTrend.addedCount}</li>
            <li>Risks retired: {diagnostics.activeRiskTrend.resolvedCount}</li>
            <li>Risks lost: {diagnostics.activeRiskTrend.lostCount}</li>
          </ul>
        </div>
        <div id="continuity-reports">
          <h5>Reports</h5>
          {latestReport ? (
            <ul>
              <li>Latest report: {latestReport.reportId}</li>
              <li>Generated: {formatDateTime(latestReport.generatedAt)}</li>
              <li>
                Path:{' '}
                {onOpenReport ? (
                  <button type="button" className="link-button" onClick={() => onOpenReport(latestReport.relativePath)}>
                    {latestReport.relativePath}
                  </button>
                ) : (
                  latestReport.relativePath
                )}
              </li>
              <li>Report history: {reports.length}</li>
              <li>Diagnostics revisions: {latestReport.diagnostics.revisionCount}</li>
            </ul>
          ) : (
            <p>No continuity reports recorded.</p>
          )}
        </div>
      </div>
    </div>
  )
}

type ContinuityEvolutionRowProps = {
  label: string
  trend: ContinuityTrend
  sectionId: string
  onOpenOperationalContextSection?: (sectionId: string) => void
}

function ContinuityEvolutionRow({
  label,
  trend,
  sectionId,
  onOpenOperationalContextSection,
}: ContinuityEvolutionRowProps) {
  return (
    <tr>
      <th scope="row">
        {onOpenOperationalContextSection ? (
          <button
            type="button"
            className="link-button"
            onClick={() => onOpenOperationalContextSection(sectionId)}
          >
            {label}
          </button>
        ) : (
          label
        )}
      </th>
      <td>{trend.addedCount}</td>
      <td>{trend.removedCount}</td>
      <td>{trend.resolvedCount}</td>
      <td>{trend.lostCount}</td>
    </tr>
  )
}
