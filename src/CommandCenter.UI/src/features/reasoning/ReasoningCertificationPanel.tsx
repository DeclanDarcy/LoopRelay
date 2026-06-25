import { useMemo } from 'react'
import { CertificationFindingsView, DiagnosticList } from '../../components/explainability'
import { EmptyState } from '../../components/design'
import {
  reasoningCertificationEvidenceToFindings,
  reasoningDiagnosticsToExplanation,
} from '../../lib/explainability'
import type { ReasoningCertificationReport } from '../../types'

type ReasoningCertificationPanelProps = {
  currentReport: ReasoningCertificationReport | null
  reports: ReasoningCertificationReport[]
  isLoading: boolean
  isRunning: boolean
  error: string | null
  onRunCertification: () => void
}

export function ReasoningCertificationPanel({
  currentReport,
  reports,
  isLoading,
  isRunning,
  error,
  onRunCertification,
}: ReasoningCertificationPanelProps) {
  const failedEvidence = useMemo(
    () => currentReport?.evidence.filter((item) => !item.passed) ?? [],
    [currentReport?.evidence],
  )
  const passedEvidence = useMemo(
    () => currentReport?.evidence.filter((item) => item.passed) ?? [],
    [currentReport?.evidence],
  )

  return (
    <section
      className="reasoning-panel reasoning-certification-panel"
      id="reasoning-certification"
      aria-label="Reasoning certification"
    >
      <div className="decision-panel-heading">
        <div>
          <h5>Certification</h5>
          <span>{currentReport ? `Generated ${formatTimestamp(currentReport.generatedAt)}` : 'Answerability evidence'}</span>
        </div>
        <button
          type="button"
          className="secondary-action"
          onClick={onRunCertification}
          disabled={isLoading || isRunning}
        >
          {isRunning ? 'Certifying...' : 'Run Certification'}
        </button>
      </div>

      <div className="reasoning-derived-status" aria-label="Reasoning certification authority">
        <strong>Evidence report</strong>
        <span>Non-authoritative</span>
        <span>Reconstructability only</span>
      </div>

      {error ? <p className="notice error">{error}</p> : null}

      {currentReport ? (
        <>
          <div className="reasoning-certification-summary" aria-label="Reasoning certification summary">
            <span>Result: {formatResult(currentReport.result.kind)}</span>
            <span>{passedEvidence.length} passed evidence</span>
            <span>{failedEvidence.length} failed evidence</span>
            <span>{currentReport.result.summary}</span>
          </div>

          {currentReport.diagnostics.length > 0 ? (
            <div aria-label="Reasoning certification diagnostics">
              <DiagnosticList
                title="Reasoning Certification Diagnostics"
                diagnostics={reasoningDiagnosticsToExplanation(
                  currentReport.diagnostics,
                  'Certification diagnostic',
                  'warning',
                )}
              />
            </div>
          ) : null}

          <div className="reasoning-certification-evidence" aria-label="Reasoning certification evidence">
            <CertificationFindingsView
              title="Reasoning Certification Findings"
              findings={reasoningCertificationEvidenceToFindings([
                ...failedEvidence,
                ...passedEvidence,
              ])}
              emptyLabel="No certification evidence is available."
            />
          </div>

          <div className="reasoning-certification-history" aria-label="Reasoning certification report history">
            <h6>Generated Reports</h6>
            {reports.length > 0 ? (
              <div className="decision-row-list">
                {reports.map((report) => (
                  <div className="decision-row" key={report.id}>
                    <strong>{report.id}</strong>
                    <span>{formatTimestamp(report.generatedAt)}</span>
                    <p>
                      {formatResult(report.result.kind)} | {report.evidence.filter((item) => item.passed).length}{' '}
                      passed, {report.evidence.filter((item) => !item.passed).length} failed
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

function formatResult(value: string) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function formatTimestamp(value: string) {
  return new Date(value).toLocaleString()
}
