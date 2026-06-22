import { useMemo } from 'react'
import { EmptyState } from '../../components/design'
import type {
  DecisionCertificationEvidence,
  DecisionCertificationReport,
  DecisionGovernanceFinding,
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

          {currentReport.diagnostics.length > 0 ? (
            <div className="decision-warning-list" aria-label="Certification diagnostics">
              {currentReport.diagnostics.map((diagnostic) => (
                <span key={diagnostic}>{diagnostic}</span>
              ))}
            </div>
          ) : null}

          <div className="decision-inspection-list" aria-label="Certification evidence">
            <h6>Evidence</h6>
            {failedEvidence.length > 0 ? (
              <div className="decision-certification-group">
                <h6>Failed</h6>
                {failedEvidence.map((evidence) => (
                  <CertificationEvidenceCard evidence={evidence} key={evidence.id} />
                ))}
              </div>
            ) : null}
            {passedEvidence.length > 0 ? (
              <div className="decision-certification-group">
                <h6>Passed</h6>
                {passedEvidence.map((evidence) => (
                  <CertificationEvidenceCard evidence={evidence} key={evidence.id} />
                ))}
              </div>
            ) : (
              <EmptyState className="empty-state">No certification evidence is available.</EmptyState>
            )}
          </div>

          {currentReport.findings.length > 0 ? (
            <div className="decision-inspection-list" aria-label="Certification governance findings">
              <h6>Governance Findings</h6>
              {currentReport.findings.map((finding) => (
                <CertificationFindingCard finding={finding} key={finding.id} />
              ))}
            </div>
          ) : (
            <div className="decision-success-list">
              <span>No governance findings were reported by certification.</span>
            </div>
          )}

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

function CertificationEvidenceCard({ evidence }: { evidence: DecisionCertificationEvidence }) {
  const related = [
    ...evidence.relatedDecisionIds.map((id) => `Decision ${id}`),
    ...evidence.relatedCandidateIds.map((id) => `Candidate ${id}`),
    ...evidence.relatedProposalIds.map((id) => `Proposal ${id}`),
  ]

  return (
    <article className="decision-certification-evidence">
      <div>
        <span>{evidence.area}</span>
        <strong>{evidence.id}</strong>
      </div>
      <p>{evidence.detail}</p>
      <div className="decision-badge-row">
        <span>{evidence.passed ? 'Passed' : 'Failed'}</span>
        {related.map((item) => (
          <span key={item}>{item}</span>
        ))}
      </div>
      {evidence.sources.length > 0 ? (
        <ul className="decision-source-list">
          {evidence.sources.map((source, index) => (
            <li key={`${evidence.id}-${source.sourceKind}-${source.relativePath ?? 'none'}-${index}`}>
              <strong>{source.sourceKind}</strong>
              {source.relativePath ? <span>{source.relativePath}</span> : null}
              {source.section ? <span>{source.section}</span> : null}
              {source.excerpt ? <p>{source.excerpt}</p> : null}
            </li>
          ))}
        </ul>
      ) : null}
    </article>
  )
}

function CertificationFindingCard({ finding }: { finding: DecisionGovernanceFinding }) {
  return (
    <article className="decision-governance-finding">
      <div>
        <span>
          {finding.severity} / {finding.category}
        </span>
        <strong>{finding.title}</strong>
      </div>
      <p>{finding.detail}</p>
      <div className="decision-badge-row">
        <span>{finding.blocksExecutionProjection ? 'Blocks execution projection' : 'Advisory'}</span>
        {finding.relatedDecisionIds.map((id) => (
          <span key={`decision-${id}`}>Decision {id}</span>
        ))}
        {finding.relatedCandidateIds.map((id) => (
          <span key={`candidate-${id}`}>Candidate {id}</span>
        ))}
        {finding.relatedProposalIds.map((id) => (
          <span key={`proposal-${id}`}>Proposal {id}</span>
        ))}
      </div>
    </article>
  )
}

function formatDate(value: string) {
  return new Date(value).toLocaleString()
}
