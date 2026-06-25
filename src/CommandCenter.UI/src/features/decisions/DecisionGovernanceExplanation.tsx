import { DiagnosticList } from '../../components/explainability'
import { decisionGovernanceFindingsToDiagnostics } from '../../lib/explainability'
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
  return (
    <article className="decision-governance-finding">
      <DiagnosticList
        diagnostics={decisionGovernanceFindingsToDiagnostics([finding])}
        title={finding.id}
      />
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
