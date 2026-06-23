import { useMemo } from 'react'
import { EmptyState } from '../../components/design'
import type { ReasoningCertificationEvidence, ReasoningCertificationReport } from '../../types'

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
            <div className="reasoning-diagnostics" aria-label="Reasoning certification diagnostics">
              {currentReport.diagnostics.map((diagnostic) => (
                <p key={diagnostic}>{diagnostic}</p>
              ))}
            </div>
          ) : null}

          <div className="reasoning-certification-evidence" aria-label="Reasoning certification evidence">
            {failedEvidence.length > 0 ? (
              <EvidenceGroup title="Failed" evidence={failedEvidence} />
            ) : null}
            {passedEvidence.length > 0 ? (
              <EvidenceGroup title="Passed" evidence={passedEvidence} />
            ) : (
              <EmptyState className="empty-state">No certification evidence is available.</EmptyState>
            )}
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

function EvidenceGroup({
  title,
  evidence,
}: {
  title: string
  evidence: ReasoningCertificationEvidence[]
}) {
  return (
    <div className="reasoning-certification-group">
      <h6>{title}</h6>
      {evidence.map((item) => (
        <EvidenceCard evidence={item} key={item.id} />
      ))}
    </div>
  )
}

function EvidenceCard({ evidence }: { evidence: ReasoningCertificationEvidence }) {
  return (
    <article className="reasoning-certification-card">
      <div className="reasoning-event-heading">
        <strong>{evidence.scenario}</strong>
        <span>{evidence.passed ? 'Passed' : 'Failed'}</span>
      </div>
      <p>{evidence.summary}</p>
      {evidence.details.length > 0 ? (
        <ul>
          {evidence.details.map((detail) => (
            <li key={detail}>{detail}</li>
          ))}
        </ul>
      ) : null}
      {evidence.references.length > 0 ? (
        <dl className="reasoning-provenance" aria-label={`${evidence.id} references`}>
          {evidence.references.map((reference, index) => (
            <div key={`${evidence.id}-${reference.kind}-${reference.id}-${index}`}>
              <dt>{reference.kind}</dt>
              <dd>
                {reference.id}
                {reference.relativePath ? ` / ${reference.relativePath}` : ''}
                {reference.section ? ` / ${reference.section}` : ''}
              </dd>
            </div>
          ))}
        </dl>
      ) : null}
    </article>
  )
}

function formatResult(value: string) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function formatTimestamp(value: string) {
  return new Date(value).toLocaleString()
}
