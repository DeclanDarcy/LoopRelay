import { useEffect, useMemo, useState } from 'react'
import { Badge, EmptyState } from '../../components/design'
import type { DecisionProposalBrowserItem, DecisionProposalState } from '../../types'

const proposalStateFilters: DecisionProposalState[] = [
  'Generated',
  'Viewed',
  'NeedsRefinement',
  'Refined',
  'ReadyForResolution',
  'Resolved',
  'Expired',
  'Discarded',
]

const proposalStateLabels: Record<DecisionProposalState, string> = {
  Draft: 'Draft',
  Generated: 'Generated',
  Viewed: 'Viewed',
  NeedsRefinement: 'Needs refinement',
  ReadyForResolution: 'Ready for resolution',
  Refined: 'Refined',
  Resolved: 'Resolved',
  Expired: 'Expired',
  Discarded: 'Discarded',
}

type DecisionProposalBrowserProps = {
  proposals: DecisionProposalBrowserItem[]
  selectedStates: DecisionProposalState[]
  isLoading: boolean
  onSelectedStatesChange: (states: DecisionProposalState[]) => void
  onSelectedProposalChange?: (proposalId: string | null) => void
}

export function DecisionProposalBrowser({
  proposals,
  selectedStates,
  isLoading,
  onSelectedStatesChange,
  onSelectedProposalChange,
}: DecisionProposalBrowserProps) {
  const [selectedProposalId, setSelectedProposalId] = useState<string | null>(null)
  const selectedStateSet = useMemo(() => new Set(selectedStates), [selectedStates])
  const selectedProposal =
    proposals.find((proposal) => proposal.proposalId === selectedProposalId) ?? proposals[0] ?? null

  useEffect(() => {
    if (!selectedProposalId || proposals.some((proposal) => proposal.proposalId === selectedProposalId)) {
      return
    }

    setSelectedProposalId(proposals[0]?.proposalId ?? null)
  }, [proposals, selectedProposalId])

  useEffect(() => {
    onSelectedProposalChange?.(selectedProposal?.proposalId ?? null)
  }, [onSelectedProposalChange, selectedProposal?.proposalId])

  const toggleState = (state: DecisionProposalState) => {
    if (selectedStateSet.has(state)) {
      onSelectedStatesChange(selectedStates.filter((selectedState) => selectedState !== state))
      return
    }

    onSelectedStatesChange([...selectedStates, state])
  }

  return (
    <section className="decision-lifecycle-panel decision-proposal-browser" aria-label="Decision proposals">
      <div className="decision-panel-heading">
        <h5>Proposals</h5>
        <span>{proposals.length} shown</span>
      </div>

      <div className="decision-filter-bar" aria-label="Proposal state filters">
        <button
          type="button"
          className={`decision-filter${selectedStates.length === 0 ? ' selected' : ''}`}
          aria-pressed={selectedStates.length === 0}
          onClick={() => onSelectedStatesChange([])}
        >
          All
        </button>
        {proposalStateFilters.map((state) => (
          <button
            type="button"
            className={`decision-filter${selectedStateSet.has(state) ? ' selected' : ''}`}
            aria-pressed={selectedStateSet.has(state)}
            onClick={() => toggleState(state)}
            key={state}
          >
            {proposalStateLabels[state]}
          </button>
        ))}
      </div>

      {proposals.length > 0 ? (
        <div className="decision-proposal-browser-grid">
          <div className="decision-row-list" role="list" aria-label="Proposal browser rows">
            {proposals.map((proposal) => {
              const isSelected = proposal.proposalId === selectedProposal?.proposalId
              return (
                <button
                  type="button"
                  className={`decision-row decision-row-button${isSelected ? ' selected' : ''}`}
                  onClick={() => setSelectedProposalId(proposal.proposalId)}
                  aria-pressed={isSelected}
                  key={proposal.proposalId}
                >
                  <strong>{proposal.title}</strong>
                  <span>
                    {proposal.proposalId} | {proposalStateLabels[proposal.state]} | Review{' '}
                    {proposal.reviewState}
                  </span>
                  <p>{proposal.classification} / {proposal.priority}</p>
                </button>
              )
            })}
          </div>

          {selectedProposal ? (
            <aside className="decision-selection-panel" aria-label="Selected proposal">
              <div>
                <span>Selected proposal</span>
                <strong>{selectedProposal.proposalId}</strong>
              </div>
              <p>{selectedProposal.title}</p>
              <div className="decision-badge-row">
                <Badge tone={selectedProposal.isResolved ? 'done' : 'info'}>
                  {proposalStateLabels[selectedProposal.state]}
                </Badge>
                <Badge>{selectedProposal.reviewState}</Badge>
              </div>
              <dl>
                <div>
                  <dt>Candidate</dt>
                  <dd>{selectedProposal.candidateId}</dd>
                </div>
                <div>
                  <dt>Priority</dt>
                  <dd>{selectedProposal.priority}</dd>
                </div>
                <div>
                  <dt>Updated</dt>
                  <dd>{formatProposalDate(selectedProposal.updatedAt)}</dd>
                </div>
              </dl>
            </aside>
          ) : null}
        </div>
      ) : (
        <EmptyState className="empty-state">
          {isLoading ? 'Loading decision proposals...' : 'No decision proposals match these filters.'}
        </EmptyState>
      )}
    </section>
  )
}

function formatProposalDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  return date.toLocaleString()
}
