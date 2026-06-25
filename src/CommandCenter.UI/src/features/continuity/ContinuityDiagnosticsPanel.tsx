import { StatusBadge, Table } from '../../components/design'
import { DiagnosticList, EvidenceList } from '../../components/explainability'
import { formatDateTime } from '../../lib'
import {
  continuityCompressionTrendToDiagnostics,
  continuityDiagnosticGroupsToDiagnostics,
  continuityRepeatedSignalsToDiagnostics,
  continuityReportToEvidence,
  continuityWarningsToDiagnostics,
} from '../../lib/explainability'
import { continuityWarningStatus } from '../../lib/status'
import type { ContinuityDiagnosticGroup, ContinuityDiagnostics, ContinuityReport, ContinuityTrend } from '../../types'
import { OperationalContextEvolutionTimeline } from '../operational-context/OperationalContextEvolutionTimeline'
import { OperationalContextSemanticChangeList } from '../operational-context/OperationalContextSemanticChangeList'

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
  const latestReport = reports[0] ?? null
  const compressionDiagnostics = continuityCompressionTrendToDiagnostics(diagnostics.compressionTrend)
  const repeatedSignalDiagnostics = continuityRepeatedSignalsToDiagnostics(diagnostics)
  const warningDiagnostics = continuityWarningsToDiagnostics(diagnostics.continuityWarnings)

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
        <span>Modified: {diagnostics.operationalEvolution.modifiedCount}</span>
        <span>Preserved: {diagnostics.operationalEvolution.preservedCount}</span>
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
              <th>Modified</th>
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
        <div id="continuity-operational-evolution">
          <h5>Operational Evolution</h5>
          <ul>
            <li>Added: {diagnostics.operationalEvolution.addedCount}</li>
            <li>Modified: {diagnostics.operationalEvolution.modifiedCount}</li>
            <li>Removed: {diagnostics.operationalEvolution.removedCount}</li>
            <li>Resolved: {diagnostics.operationalEvolution.resolvedCount}</li>
            <li>Lost: {diagnostics.operationalEvolution.lostCount}</li>
            <li>Preserved: {diagnostics.operationalEvolution.preservedCount}</li>
          </ul>
        </div>
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
          <DiagnosticList
            diagnostics={compressionDiagnostics}
            title="Compression Observations"
            emptyLabel="No compression warnings or removed noise indicators recorded."
          />
        </div>
        <div>
          <h5>Repeated Signals</h5>
          <DiagnosticList
            diagnostics={repeatedSignalDiagnostics}
            title="Repeated Signal Diagnostics"
            emptyLabel="No repeated indicators recorded."
          />
        </div>
        <div id="continuity-warnings">
          <h5>Warnings</h5>
          <DiagnosticList
            diagnostics={warningDiagnostics}
            title="Continuity Warning Diagnostics"
            emptyLabel="No continuity warnings recorded."
          />
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
            <>
              <ul>
                <li>Latest report: {latestReport.reportId}</li>
                <li>Generated: {formatDateTime(latestReport.generatedAt)}</li>
                <li>
                  Path:{' '}
                  {onOpenReport ? (
                    <button
                      type="button"
                      className="link-button"
                      onClick={() => onOpenReport(latestReport.relativePath)}
                    >
                      {latestReport.relativePath}
                    </button>
                  ) : (
                    latestReport.relativePath
                  )}
                </li>
                <li>Report history: {reports.length}</li>
                <li>Diagnostics revisions: {latestReport.diagnostics.revisionCount}</li>
              </ul>
              <EvidenceList evidence={continuityReportToEvidence(latestReport)} title="Report Evidence" />
            </>
          ) : (
            <p>No continuity reports recorded.</p>
          )}
        </div>
      </div>
      <ContinuityDiagnosticsGroupedPanel groups={diagnostics.diagnosticGroups} />
      <OperationalContextEvolutionTimeline timelineEntries={diagnostics.operationalEvolution.timelineEntries} />
      <OperationalContextSemanticChangeList
        semanticChanges={diagnostics.operationalEvolution.semanticChanges}
        grouping="outcome"
        title="Operational Evolution Changes"
        emptyText="No operational evolution changes recorded."
      />
    </div>
  )
}

type ContinuityDiagnosticsGroupedPanelProps = {
  groups: ContinuityDiagnosticGroup[]
}

function ContinuityDiagnosticsGroupedPanel({ groups }: ContinuityDiagnosticsGroupedPanelProps) {
  const diagnostics = continuityDiagnosticGroupsToDiagnostics(groups)

  return (
    <section className="continuity-diagnostic-groups" aria-label="Grouped continuity diagnostics">
      <DiagnosticList
        diagnostics={diagnostics}
        title="Grouped Diagnostics"
        emptyLabel="No grouped continuity diagnostics recorded."
      />
    </section>
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
      <td>{trend.modifiedCount}</td>
      <td>{trend.removedCount}</td>
      <td>{trend.resolvedCount}</td>
      <td>{trend.lostCount}</td>
    </tr>
  )
}
