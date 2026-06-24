import { useEffect, useMemo, useState } from 'react'
import { Badge, EmptyState } from '../../components/design'
import type { DecisionCandidate, DecisionCandidateState } from '../../types'

const candidateStateFilters: DecisionCandidateState[] = [
  'Discovered',
  'Promoted',
  'Dismissed',
  'Expired',
  'Duplicate',
]

const candidateStateLabels: Record<DecisionCandidateState, string> = {
  Discovered: 'Discovered',
  Promoted: 'Promoted',
  Dismissed: 'Dismissed',
  Expired: 'Expired',
  Duplicate: 'Duplicate',
}

type DecisionCandidateBrowserProps = {
  candidates: DecisionCandidate[]
  isLoading: boolean
  actionsEnabled?: boolean
  onSelectedCandidateChange?: (candidateId: string | null) => void
  onDiscover?: () => void | Promise<void>
  onPromote?: (candidateId: string) => void | Promise<void>
  onDismiss?: (candidateId: string) => void | Promise<void>
  onExpire?: (candidateId: string) => void | Promise<void>
  onMarkDuplicate?: (candidateId: string, duplicateOfCandidateId: string) => void | Promise<void>
  onGenerateProposal?: (candidateId: string) => void | Promise<void>
}

export function DecisionCandidateBrowser({
  candidates,
  isLoading,
  actionsEnabled = true,
  onSelectedCandidateChange,
  onDiscover,
  onPromote,
  onDismiss,
  onExpire,
  onMarkDuplicate,
  onGenerateProposal,
}: DecisionCandidateBrowserProps) {
  const [selectedStates, setSelectedStates] = useState<DecisionCandidateState[]>([
    'Discovered',
    'Promoted',
  ])
  const [selectedCandidateId, setSelectedCandidateId] = useState<string | null>(null)
  const [duplicateOfCandidateId, setDuplicateOfCandidateId] = useState<string>('')
  const selectedStateSet = useMemo(() => new Set(selectedStates), [selectedStates])
  const filteredCandidates = useMemo(() => {
    if (selectedStates.length === 0) {
      return candidates
    }

    return candidates.filter((candidate) => selectedStateSet.has(candidate.state))
  }, [candidates, selectedStateSet, selectedStates.length])
  const selectedCandidate =
    filteredCandidates.find((candidate) => candidate.id === selectedCandidateId) ??
    filteredCandidates[0] ??
    null
  const duplicateTargets = candidates.filter((candidate) => candidate.id !== selectedCandidate?.id)

  useEffect(() => {
    if (!selectedCandidateId || filteredCandidates.some((candidate) => candidate.id === selectedCandidateId)) {
      return
    }

    setSelectedCandidateId(filteredCandidates[0]?.id ?? null)
  }, [filteredCandidates, selectedCandidateId])

  useEffect(() => {
    onSelectedCandidateChange?.(selectedCandidate?.id ?? null)
  }, [onSelectedCandidateChange, selectedCandidate?.id])

  const toggleState = (state: DecisionCandidateState) => {
    if (selectedStateSet.has(state)) {
      setSelectedStates(selectedStates.filter((selectedState) => selectedState !== state))
      return
    }

    setSelectedStates([...selectedStates, state])
  }

  return (
    <section className="decision-lifecycle-panel decision-candidate-browser" aria-label="Decision candidates">
      <div className="decision-panel-heading">
        <h5>Candidates</h5>
        <div className="context-controls">
          <span>{filteredCandidates.length} shown</span>
          {actionsEnabled ? (
            <button
              type="button"
              className="secondary-action"
              onClick={() => void onDiscover?.()}
              disabled={isLoading || !onDiscover}
            >
              Discover
            </button>
          ) : null}
        </div>
      </div>

      <div className="decision-filter-bar" aria-label="Candidate state filters">
        <button
          type="button"
          className={`decision-filter${selectedStates.length === 0 ? ' selected' : ''}`}
          aria-pressed={selectedStates.length === 0}
          onClick={() => setSelectedStates([])}
        >
          All
        </button>
        {candidateStateFilters.map((state) => (
          <button
            type="button"
            className={`decision-filter${selectedStateSet.has(state) ? ' selected' : ''}`}
            aria-pressed={selectedStateSet.has(state)}
            onClick={() => toggleState(state)}
            key={state}
          >
            {candidateStateLabels[state]}
          </button>
        ))}
      </div>

      {filteredCandidates.length > 0 ? (
        <div className="decision-candidate-browser-grid">
          <div className="decision-row-list" role="list" aria-label="Candidate browser rows">
            {filteredCandidates.map((candidate) => {
              const isSelected = candidate.id === selectedCandidate?.id
              return (
                <button
                  type="button"
                  className={`decision-row decision-row-button${isSelected ? ' selected' : ''}`}
                  onClick={() => setSelectedCandidateId(candidate.id)}
                  aria-pressed={isSelected}
                  key={candidate.id}
                >
                  <strong>{candidate.title}</strong>
                  <span>
                    {candidate.id} | {candidateStateLabels[candidate.state]} | {candidate.priority}
                  </span>
                  <p>{candidate.summary}</p>
                </button>
              )
            })}
          </div>

          {selectedCandidate ? (
            <aside className="decision-selection-panel" aria-label="Selected candidate">
              <div>
                <span>Selected candidate</span>
                <strong>{selectedCandidate.id}</strong>
              </div>
              <p>{selectedCandidate.title}</p>
              <div className="decision-badge-row">
                <Badge tone={selectedCandidate.state === 'Promoted' ? 'done' : 'info'}>
                  {candidateStateLabels[selectedCandidate.state]}
                </Badge>
                <Badge>{selectedCandidate.classification}</Badge>
              </div>
              <dl>
                <div>
                  <dt>Priority</dt>
                  <dd>{selectedCandidate.priority}</dd>
                </div>
                <div>
                  <dt>Signals</dt>
                  <dd>{selectedCandidate.signals.length}</dd>
                </div>
                <div>
                  <dt>Evidence</dt>
                  <dd>{selectedCandidate.evidence.length}</dd>
                </div>
              </dl>
              {actionsEnabled ? (
                <>
                  <div className="context-controls" aria-label="Candidate actions">
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={() => void onPromote?.(selectedCandidate.id)}
                      disabled={isLoading || !onPromote}
                    >
                      Promote Candidate
                    </button>
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={() => void onGenerateProposal?.(selectedCandidate.id)}
                      disabled={isLoading || !onGenerateProposal}
                    >
                      Generate Decision Proposal
                    </button>
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={() => void onDismiss?.(selectedCandidate.id)}
                      disabled={isLoading || !onDismiss}
                    >
                      Dismiss
                    </button>
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={() => void onExpire?.(selectedCandidate.id)}
                      disabled={isLoading || !onExpire}
                    >
                      Expire
                    </button>
                  </div>
                  <div className="decision-filter-bar" aria-label="Candidate duplicate action">
                    <select
                      value={duplicateOfCandidateId}
                      onChange={(event) => setDuplicateOfCandidateId(event.target.value)}
                      disabled={isLoading || duplicateTargets.length === 0}
                    >
                      <option value="">Duplicate target</option>
                      {duplicateTargets.map((candidate) => (
                        <option value={candidate.id} key={candidate.id}>
                          {candidate.id}
                        </option>
                      ))}
                    </select>
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={() => void onMarkDuplicate?.(selectedCandidate.id, duplicateOfCandidateId)}
                      disabled={isLoading || !onMarkDuplicate || !duplicateOfCandidateId}
                    >
                      Mark Duplicate
                    </button>
                  </div>
                </>
              ) : null}
            </aside>
          ) : null}
        </div>
      ) : (
        <EmptyState className="empty-state">
          {isLoading ? 'Loading decision candidates...' : 'No decision candidates match these filters.'}
        </EmptyState>
      )}
    </section>
  )
}
