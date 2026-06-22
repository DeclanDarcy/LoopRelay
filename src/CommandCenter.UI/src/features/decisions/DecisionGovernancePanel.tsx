import { useMemo } from 'react'
import { EmptyState } from '../../components/design'
import type {
  DecisionGovernanceCategory,
  DecisionGovernanceFinding,
  DecisionGovernanceReport,
  DecisionGovernanceSeverity,
} from '../../types'

type DecisionGovernancePanelProps = {
  currentReport: DecisionGovernanceReport | null
  reports: DecisionGovernanceReport[]
  isLoading: boolean
  isGenerating: boolean
  error: string | null
  onGenerateReport: () => void
  onSelectProposal: (proposalId: string) => void
}

const severities: DecisionGovernanceSeverity[] = ['Blocking', 'Warning', 'Info']

export function DecisionGovernancePanel({
  currentReport,
  reports,
  isLoading,
  isGenerating,
  error,
  onGenerateReport,
  onSelectProposal,
}: DecisionGovernancePanelProps) {
  const groupedFindings = useMemo(
    () => groupFindings(currentReport?.findings ?? []),
    [currentReport?.findings],
  )

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

          {currentReport.findings.length > 0 ? (
            <div className="decision-governance-findings" aria-label="Governance findings">
              {severities.map((severity) => {
                const byCategory = groupedFindings.get(severity)
                if (!byCategory || byCategory.size === 0) {
                  return null
                }

                return (
                  <div className="decision-governance-group" key={severity}>
                    <h6>{severity}</h6>
                    {[...byCategory.entries()].map(([category, findings]) => (
                      <div className="decision-inspection-card" key={`${severity}-${category}`}>
                        <div>
                          <span>{category}</span>
                          <strong>{findings.length} finding(s)</strong>
                        </div>
                        {findings.map((finding) => (
                          <GovernanceFindingCard
                            finding={finding}
                            key={finding.id}
                            onSelectProposal={onSelectProposal}
                          />
                        ))}
                      </div>
                    ))}
                  </div>
                )
              })}
            </div>
          ) : (
            <div className="decision-success-list">
              <span>No governance findings in the current inspection.</span>
            </div>
          )}

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

function GovernanceFindingCard({
  finding,
  onSelectProposal,
}: {
  finding: DecisionGovernanceFinding
  onSelectProposal: (proposalId: string) => void
}) {
  const related = [
    ...finding.relatedDecisionIds.map((id) => `Decision ${id}`),
    ...finding.relatedCandidateIds.map((id) => `Candidate ${id}`),
    ...finding.relatedProposalIds.map((id) => `Proposal ${id}`),
  ]

  return (
    <article className="decision-governance-finding">
      <div>
        <span>{finding.id}</span>
        <strong>{finding.title}</strong>
      </div>
      <p>{finding.detail}</p>
      <div className="decision-badge-row">
        <span>{finding.blocksExecutionProjection ? 'Blocks execution projection' : 'Advisory'}</span>
        {related.map((item) => (
          <span key={item}>{item}</span>
        ))}
      </div>
      {finding.relatedProposalIds.length > 0 ? (
        <div className="decision-governance-actions" aria-label="Finding navigation">
          {finding.relatedProposalIds.map((proposalId) => (
            <button
              type="button"
              className="secondary-action"
              onClick={() => onSelectProposal(proposalId)}
              key={proposalId}
            >
              View {proposalId}
            </button>
          ))}
        </div>
      ) : null}
      {finding.sources.length > 0 ? (
        <ul className="decision-source-list">
          {finding.sources.map((source, index) => (
            <li key={`${finding.id}-${source.sourceKind}-${source.relativePath ?? 'none'}-${index}`}>
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

function groupFindings(findings: DecisionGovernanceFinding[]) {
  const grouped = new Map<
    DecisionGovernanceSeverity,
    Map<DecisionGovernanceCategory, DecisionGovernanceFinding[]>
  >()

  for (const finding of findings) {
    const byCategory = grouped.get(finding.severity) ?? new Map()
    const categoryFindings = byCategory.get(finding.category) ?? []
    categoryFindings.push(finding)
    byCategory.set(finding.category, categoryFindings)
    grouped.set(finding.severity, byCategory)
  }

  return grouped
}

function formatDate(value: string) {
  return new Date(value).toLocaleString()
}
