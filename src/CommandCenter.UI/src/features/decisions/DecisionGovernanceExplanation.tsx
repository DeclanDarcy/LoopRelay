import type {
  DecisionGovernanceCategory,
  DecisionGovernanceFinding,
  DecisionGovernanceReport,
  DecisionGovernanceSeverity,
} from '../../types'

const severities: DecisionGovernanceSeverity[] = ['Blocking', 'Warning', 'Info']

export function DecisionGovernanceExplanation({
  report,
  onSelectProposal,
}: {
  report: DecisionGovernanceReport
  onSelectProposal: (proposalId: string) => void
}) {
  const groupedFindings = groupFindings(report.findings)

  if (report.findings.length === 0) {
    return (
      <div className="decision-success-list" aria-label="Governance findings">
        <span>No governance findings in the current inspection.</span>
      </div>
    )
  }

  return (
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
                  <GovernanceFindingCard finding={finding} key={finding.id} onSelectProposal={onSelectProposal} />
                ))}
              </div>
            ))}
          </div>
        )
      })}
    </div>
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
