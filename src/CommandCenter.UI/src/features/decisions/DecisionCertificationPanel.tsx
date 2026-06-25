import { EmptyState } from '../../components/design'
import { CertificationFindingsView, DiagnosticList } from '../../components/explainability'
import {
  decisionCertificationEvidenceToFindings,
  decisionDiagnosticsToExplanation,
  decisionGovernanceFindingsToCertificationFindings,
} from '../../lib/explainability'
import type {
  DecisionCertificationReport,
} from '../../types'

type DecisionCertificationPanelProps = {
  currentReport: DecisionCertificationReport | null
  reports: DecisionCertificationReport[]
  isLoading: boolean
  isRunning: boolean
  error: string | null
  onRunCertification: () => void
}

export function DecisionCertificationPanel({
  currentReport,
  reports,
  isLoading,
  isRunning,
  error,
  onRunCertification,
}: DecisionCertificationPanelProps) {
  return (
    <section
      className="decision-lifecycle-panel decision-certification-panel"
      aria-label="Decision certification"
    >
      <div className="decision-panel-heading">
        <div>
          <h5>Certification</h5>
          <span>Read-only lifecycle evidence</span>
        </div>
        <button
          type="button"
          className="secondary-action"
          onClick={onRunCertification}
          disabled={!currentReport || isLoading || isRunning}
        >
          {isRunning ? 'Running...' : 'Run Certification'}
        </button>
      </div>

      {error ? <p className="notice error">{error}</p> : null}

      {currentReport ? (
        <>
          <div className="decision-lineage-summary" aria-label="Decision certification summary">
            <span>Result: {currentReport.result.kind}</span>
            <span>{currentReport.result.passedEvidenceCount} passed evidence</span>
            <span>{currentReport.result.failedEvidenceCount} failed evidence</span>
            <span>Health: {currentReport.health}</span>
          </div>

          <div className="decision-governance-meta" aria-label="Certification report metadata">
            <span>Current inspection: {currentReport.id}</span>
            <span>Generated: {formatDate(currentReport.generatedAt)}</span>
            <span>Fingerprint: {currentReport.inputFingerprint}</span>
          </div>

          <DiagnosticList
            diagnostics={decisionDiagnosticsToExplanation(currentReport.diagnostics, 'Certification')}
            emptyLabel="No certification diagnostics projected."
            title="Certification Diagnostics"
          />

          <CertificationFindingsView
            findings={decisionCertificationEvidenceToFindings(currentReport.evidence)}
            emptyLabel="No certification evidence is available."
            title="Evidence"
          />

          <CertificationFindingsView
            findings={decisionGovernanceFindingsToCertificationFindings(currentReport.findings)}
            emptyLabel="No governance findings were reported by certification."
            title="Governance Findings"
          />

          <div className="decision-inspection-list" aria-label="Generated certification report history">
            <h6>Generated Reports</h6>
            {reports.length > 0 ? (
              <div className="decision-row-list">
                {reports.map((report) => (
                  <div className="decision-row" key={report.id}>
                    <strong>{report.id}</strong>
                    <span>{formatDate(report.generatedAt)}</span>
                    <p>
                      {report.result.kind} | {report.result.passedEvidenceCount} passed,{' '}
                      {report.result.failedEvidenceCount} failed
                    </p>
                  </div>
                ))}
              </div>
            ) : (
              <EmptyState className="empty-state">No generated certification reports yet.</EmptyState>
            )}
          </div>
        </>
      ) : (
        <EmptyState className="empty-state">
          {isLoading ? 'Loading certification...' : 'No certification report is available.'}
        </EmptyState>
      )}
    </section>
  )
}

function formatDate(value: string) {
  return new Date(value).toLocaleString()
}
