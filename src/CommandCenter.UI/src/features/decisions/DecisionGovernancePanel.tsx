import { EmptyState } from '../../components/design'
import { DecisionGovernanceExplanation } from './DecisionGovernanceExplanation'
import type { DecisionGovernanceReport } from '../../types'

type DecisionGovernancePanelProps = {
  currentReport: DecisionGovernanceReport | null
  reports: DecisionGovernanceReport[]
  isLoading: boolean
  isGenerating: boolean
  error: string | null
  onGenerateReport: () => void
  onSelectProposal: (proposalId: string) => void
}

export function DecisionGovernancePanel({
  currentReport,
  reports,
  isLoading,
  isGenerating,
  error,
  onGenerateReport,
  onSelectProposal,
}: DecisionGovernancePanelProps) {
  return (
    <section className="decision-lifecycle-panel decision-governance-panel" aria-label="Decision governance">
      <div className="decision-panel-heading">
        <div>
          <h5>Governance</h5>
          <span>Advisory report surface</span>
        </div>
        <button
          type="button"
          className="secondary-action"
          onClick={onGenerateReport}
          disabled={!currentReport || isLoading || isGenerating}
        >
          {isGenerating ? 'Generating...' : 'Generate Report'}
        </button>
      </div>

      {error ? <p className="notice error">{error}</p> : null}

      {currentReport ? (
        <>
          <div className="decision-lineage-summary" aria-label="Decision governance summary">
            <span>Health: {currentReport.health}</span>
            <span>{currentReport.summary.findingCount} findings</span>
            <span>{currentReport.summary.blockingFindingCount} blocking</span>
            <span>{currentReport.summary.resolvedDecisionCount} resolved decisions</span>
          </div>

          <div className="decision-governance-meta" aria-label="Governance report metadata">
            <span>Current inspection: {currentReport.id}</span>
            <span>Generated: {formatDate(currentReport.generatedAt)}</span>
            <span>Fingerprint: {currentReport.inputFingerprint}</span>
          </div>

          {currentReport.diagnostics.length > 0 ? (
            <div className="decision-warning-list" aria-label="Governance diagnostics">
              {currentReport.diagnostics.map((diagnostic) => (
                <span key={diagnostic}>{diagnostic}</span>
              ))}
            </div>
          ) : null}

          <DecisionGovernanceExplanation report={currentReport} onSelectProposal={onSelectProposal} />

          <div className="decision-inspection-list" aria-label="Generated governance report history">
            <h6>Generated Reports</h6>
            {reports.length > 0 ? (
              <div className="decision-row-list">
                {reports.map((report) => (
                  <div className="decision-row" key={report.id}>
                    <strong>{report.id}</strong>
                    <span>{formatDate(report.generatedAt)}</span>
                    <p>
                      {report.health} | {report.summary.findingCount} finding(s),{' '}
                      {report.summary.blockingFindingCount} blocking
                    </p>
                  </div>
                ))}
              </div>
            ) : (
              <EmptyState className="empty-state">No generated governance reports yet.</EmptyState>
            )}
          </div>
        </>
      ) : (
        <EmptyState className="empty-state">
          {isLoading ? 'Loading governance...' : 'No governance report is available.'}
        </EmptyState>
      )}
    </section>
  )
}

function formatDate(value: string) {
  return new Date(value).toLocaleString()
}
