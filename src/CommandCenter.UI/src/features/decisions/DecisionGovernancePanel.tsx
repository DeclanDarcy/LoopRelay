import { EmptyState } from '../../components/design'
import { DecisionGovernanceExplanation } from './DecisionGovernanceExplanation'
import type {
  Decision,
  DecisionGovernanceReport,
  DecisionLifecycleEntityEligibility,
  DecisionReviewWorkspace,
} from '../../types'

type DecisionGovernancePanelProps = {
  currentReport: DecisionGovernanceReport | null
  reports: DecisionGovernanceReport[]
  selectedProposalWorkspace?: DecisionReviewWorkspace | null
  selectedProposalEligibility?: DecisionLifecycleEntityEligibility | null
  selectedDecisionEligibility?: DecisionLifecycleEntityEligibility | null
  resolvedDecision?: Decision | null
  isLoading: boolean
  isGenerating: boolean
  error: string | null
  onGenerateReport: () => void
  onSelectProposal: (proposalId: string) => void
}

export function DecisionGovernancePanel({
  currentReport,
  reports,
  selectedProposalWorkspace = null,
  selectedProposalEligibility = null,
  selectedDecisionEligibility = null,
  resolvedDecision = null,
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

          <DecisionGovernanceAuthority
            workspace={selectedProposalWorkspace}
            proposalEligibility={selectedProposalEligibility}
            decisionEligibility={selectedDecisionEligibility}
            resolvedDecision={resolvedDecision}
          />

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

function DecisionGovernanceAuthority({
  workspace,
  proposalEligibility,
  decisionEligibility,
  resolvedDecision,
}: {
  workspace: DecisionReviewWorkspace | null
  proposalEligibility: DecisionLifecycleEntityEligibility | null
  decisionEligibility: DecisionLifecycleEntityEligibility | null
  resolvedDecision: Decision | null
}) {
  const authority = workspace?.authority ?? null
  const resolution = resolvedDecision?.resolution ?? null

  return (
    <div className="decision-inspection-list" aria-label="Governance authority and lifecycle">
      <h6>Authority and Lifecycle</h6>
      {workspace ? (
        <article className="decision-inspection-card" aria-label="Proposal governance authority">
          <div>
            <span>Resolution authority</span>
            <strong>{workspace.proposal.id}</strong>
          </div>
          <div className="decision-diagnostics-grid">
            <span>Proposal lifecycle state: {proposalEligibility?.currentState ?? workspace.proposal.state}</span>
            <span>Review state: {workspace.review.state}</span>
            <span>Recommended option: {workspace.proposal.recommendation?.optionId ?? 'No recommendation'}</span>
            <span>
              Package authority:{' '}
              {authority?.packageId
                ? authority.isPackageCurrentForProposalContent
                  ? 'Current'
                  : 'Stale'
                : 'No package version'}
            </span>
            <span>Proposal fingerprint: {shortFingerprint(authority?.proposalFingerprint ?? null)}</span>
            <span>Package fingerprint: {shortFingerprint(authority?.packageFingerprint ?? null)}</span>
          </div>
          {authority?.packageId && !authority.isPackageCurrentForProposalContent ? (
            <div className="decision-warning-list" aria-label="Stale authority warning">
              <span>Reviewed package content does not match the current proposal.</span>
            </div>
          ) : null}
        </article>
      ) : (
        <EmptyState className="empty-state">Select a proposal to inspect governance authority.</EmptyState>
      )}

      {proposalEligibility ? (
        <LifecycleAuthorityCard label="Proposal" eligibility={proposalEligibility} />
      ) : null}

      {decisionEligibility ? (
        <LifecycleAuthorityCard label="Decision" eligibility={decisionEligibility} />
      ) : null}

      {resolvedDecision ? (
        <article className="decision-inspection-card" aria-label="Resolved decision governance authority">
          <div>
            <span>Resolved decision</span>
            <strong>{resolvedDecision.id}</strong>
          </div>
          <div className="decision-diagnostics-grid">
            <span>Decision state: {resolvedDecision.state}</span>
            <span>Outcome: {resolution?.outcome ?? 'No resolution'}</span>
            <span>Selected option: {resolution?.selectedOptionId ?? 'No selected option'}</span>
            <span>Resolved by: {resolution?.resolvedBy ?? 'No resolver'}</span>
            <span>Recommendation divergence: {resolution?.recommendationDiverged ? 'Yes' : 'No'}</span>
            <span>Source proposal: {resolution?.sourceProposalSnapshot?.proposalId ?? 'No source proposal'}</span>
          </div>
        </article>
      ) : (
        <EmptyState className="empty-state">No resolved decision authority returned for the selected proposal.</EmptyState>
      )}
    </div>
  )
}

function LifecycleAuthorityCard({
  label,
  eligibility,
}: {
  label: string
  eligibility: DecisionLifecycleEntityEligibility
}) {
  return (
    <article className="decision-inspection-card" aria-label={`${label} governance lifecycle eligibility`}>
      <div>
        <span>{label} lifecycle state</span>
        <strong>{eligibility.currentState}</strong>
      </div>
      <div className="decision-diagnostics-grid">
        <span>Allowed actions: {eligibility.allowedActions.map((action) => action.displayName).join(', ') || 'None'}</span>
        <span>Allowed transitions: {eligibility.allowedNextStates.join(', ') || 'None'}</span>
      </div>
      {eligibility.blockedActions.length > 0 ? (
        <ul className="decision-lifecycle-reasons" aria-label={`${label} governance blocked transitions`}>
          {eligibility.blockedActions.map((action) => (
            <li key={action.commandName}>
              <strong>{action.displayName}</strong>
              <span>{action.reason ?? 'Blocked by backend lifecycle rules.'}</span>
              <small>{action.governingRule}</small>
            </li>
          ))}
        </ul>
      ) : null}
      {eligibility.diagnostics.length > 0 ? (
        <ul className="decision-lifecycle-reasons" aria-label={`${label} governance transition diagnostics`}>
          {eligibility.diagnostics.map((diagnostic) => (
            <li key={diagnostic}>{diagnostic}</li>
          ))}
        </ul>
      ) : null}
    </article>
  )
}

function formatDate(value: string) {
  return new Date(value).toLocaleString()
}

function shortFingerprint(value: string | null) {
  if (!value) {
    return 'None'
  }

  return value.length > 12 ? value.slice(0, 12) : value
}
