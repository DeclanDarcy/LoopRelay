import { EmptyState, Panel, SectionHeader } from '../../components/design'
import type {
  DecisionCandidate,
  DecisionContextSnapshot,
  DecisionProposalBrowserItem,
} from '../../types'

type DecisionLifecycleTabProps = {
  context: DecisionContextSnapshot | null
  candidates: DecisionCandidate[]
  proposals: DecisionProposalBrowserItem[]
  hasSelectedRepository: boolean
  isLoading: boolean
  onRefresh: () => void
}

export function DecisionLifecycleTab({
  context,
  candidates,
  proposals,
  hasSelectedRepository,
  isLoading,
  onRefresh,
}: DecisionLifecycleTabProps) {
  const activeCandidateCount = candidates.filter((candidate) =>
    candidate.state === 'Discovered' || candidate.state === 'Promoted',
  ).length
  const reviewableProposalCount = proposals.filter((proposal) =>
    proposal.state !== 'Resolved' && proposal.state !== 'Expired' && proposal.state !== 'Discarded',
  ).length

  return (
    <Panel
      className="execution-context-panel decision-lifecycle-tab tab-panel tab-decisions"
      id="decision-lifecycle"
      aria-label="Decision lifecycle"
    >
      <SectionHeader
        className="context-toolbar"
        eyebrow="Decision Lifecycle"
        title="Review Workspace"
        headingLevel={4}
        actions={
          <div className="context-controls">
            <button
              type="button"
              className="secondary-action"
              onClick={onRefresh}
              disabled={!hasSelectedRepository || isLoading}
            >
              {isLoading ? 'Loading...' : 'Refresh Decisions'}
            </button>
          </div>
        }
      />

      {hasSelectedRepository ? (
        <div className="decision-lifecycle-grid">
          <div className="context-summary" aria-label="Decision lifecycle summary">
            <span>{context?.context.items.length ?? 0} context items</span>
            <span>{activeCandidateCount} active candidates</span>
            <span>{proposals.length} proposals</span>
            <span>{reviewableProposalCount} reviewable proposals</span>
          </div>

          <section className="decision-lifecycle-panel" aria-label="Decision candidates">
            <h5>Candidates</h5>
            {candidates.length > 0 ? (
              <div className="decision-row-list">
                {candidates.slice(0, 6).map((candidate) => (
                  <article className="decision-row" key={candidate.id}>
                    <strong>{candidate.title}</strong>
                    <span>{candidate.id} | {candidate.state} | {candidate.priority}</span>
                    <p>{candidate.summary}</p>
                  </article>
                ))}
              </div>
            ) : (
              <EmptyState className="empty-state">
                {isLoading ? 'Loading decision candidates...' : 'No decision candidates found.'}
              </EmptyState>
            )}
          </section>

          <section className="decision-lifecycle-panel" aria-label="Decision proposals">
            <h5>Proposals</h5>
            {proposals.length > 0 ? (
              <div className="decision-row-list">
                {proposals.slice(0, 8).map((proposal) => (
                  <article className="decision-row" key={proposal.proposalId}>
                    <strong>{proposal.title}</strong>
                    <span>
                      {proposal.proposalId} | {proposal.state} | Review {proposal.reviewState}
                    </span>
                    <p>{proposal.classification} / {proposal.priority}</p>
                  </article>
                ))}
              </div>
            ) : (
              <EmptyState className="empty-state">
                {isLoading ? 'Loading decision proposals...' : 'No decision proposals found.'}
              </EmptyState>
            )}
          </section>
        </div>
      ) : (
        <EmptyState className="empty-state">Select or add a repository.</EmptyState>
      )}
    </Panel>
  )
}
