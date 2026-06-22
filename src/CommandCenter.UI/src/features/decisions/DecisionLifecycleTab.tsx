import { EmptyState, Panel, SectionHeader } from '../../components/design'
import type {
  DecisionCandidate,
  DecisionContextSnapshot,
  DecisionProposalBrowserItem,
  DecisionProposalState,
} from '../../types'
import { DecisionProposalBrowser } from './DecisionProposalBrowser'

type DecisionLifecycleTabProps = {
  context: DecisionContextSnapshot | null
  candidates: DecisionCandidate[]
  proposals: DecisionProposalBrowserItem[]
  selectedProposalStates: DecisionProposalState[]
  hasSelectedRepository: boolean
  isLoading: boolean
  onSelectedProposalStatesChange: (states: DecisionProposalState[]) => void
  onRefresh: () => void
}

export function DecisionLifecycleTab({
  context,
  candidates,
  proposals,
  selectedProposalStates,
  hasSelectedRepository,
  isLoading,
  onSelectedProposalStatesChange,
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

          <DecisionProposalBrowser
            proposals={proposals}
            selectedStates={selectedProposalStates}
            isLoading={isLoading}
            onSelectedStatesChange={onSelectedProposalStatesChange}
          />
        </div>
      ) : (
        <EmptyState className="empty-state">Select or add a repository.</EmptyState>
      )}
    </Panel>
  )
}
