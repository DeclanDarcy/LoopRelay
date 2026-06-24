import { useEffect, useMemo, useState } from 'react'
import { Badge, EmptyState } from '../../components/design'
import type {
  DecisionCandidate,
  DecisionCandidateState,
  DecisionLifecycleActionEligibility,
  DecisionLifecycleEntityEligibility,
} from '../../types'

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
  eligibility?: DecisionLifecycleEntityEligibility[]
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
  eligibility,
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
  const selectedDuplicateStatus = selectedCandidate ? getDuplicateStatus(selectedCandidate) : null
  const selectedEligibility =
    eligibility?.find((candidateEligibility) => candidateEligibility.entityId === selectedCandidate?.id) ?? null
  const duplicateTargets = candidates.filter((candidate) => candidate.id !== selectedCandidate?.id)
  const promoteAction = getAction(selectedEligibility, 'promote_decision_candidate')
  const dismissAction = getAction(selectedEligibility, 'dismiss_decision_candidate')
  const expireAction = getAction(selectedEligibility, 'expire_decision_candidate')
  const duplicateAction = getAction(selectedEligibility, 'mark_decision_candidate_duplicate')
  const generateAction = getAction(selectedEligibility, 'generate_decision_proposal')

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
              const duplicateStatus = getDuplicateStatus(candidate)
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
                  {duplicateStatus ? <span>Duplicates {duplicateStatus.duplicateOfCandidateId}</span> : null}
                  <p>{candidate.summary}</p>
                </button>
              )
            })}
          </div>

          {selectedCandidate ? (
            <aside className="decision-selection-panel" aria-label="Selected candidate">
              {selectedDuplicateStatus ? (
                <div className="decision-lifecycle-notice" aria-label="Candidate duplicate status">
                  <strong>Duplicates {selectedDuplicateStatus.duplicateOfCandidateId}</strong>
                  <span>{selectedDuplicateStatus.reason}</span>
                </div>
              ) : null}
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
              {selectedEligibility ? (
                <LifecycleEligibilityDetails eligibility={selectedEligibility} />
              ) : actionsEnabled ? (
                <div className="decision-lifecycle-notice" role="status">
                  Lifecycle eligibility has not loaded for this candidate.
                </div>
              ) : null}
              {actionsEnabled ? (
                <>
                  <div className="context-controls" aria-label="Candidate actions">
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={() => void onPromote?.(selectedCandidate.id)}
                      disabled={isLoading || !onPromote || !promoteAction?.isAllowed}
                      title={actionTitle(promoteAction)}
                    >
                      Promote Candidate
                    </button>
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={() => void onGenerateProposal?.(selectedCandidate.id)}
                      disabled={isLoading || !onGenerateProposal || !generateAction?.isAllowed}
                      title={actionTitle(generateAction)}
                    >
                      Generate Decision Proposal
                    </button>
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={() => void onDismiss?.(selectedCandidate.id)}
                      disabled={isLoading || !onDismiss || !dismissAction?.isAllowed}
                      title={actionTitle(dismissAction)}
                    >
                      Dismiss
                    </button>
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={() => void onExpire?.(selectedCandidate.id)}
                      disabled={isLoading || !onExpire || !expireAction?.isAllowed}
                      title={actionTitle(expireAction)}
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
                      disabled={
                        isLoading ||
                        !onMarkDuplicate ||
                        !duplicateOfCandidateId ||
                        !duplicateAction?.isAllowed
                      }
                      title={actionTitle(duplicateAction)}
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

function getDuplicateStatus(candidate: DecisionCandidate) {
  if (candidate.state !== 'Duplicate') {
    return null
  }

  const duplicateHistory = [...candidate.history]
    .reverse()
    .find((entry) => entry.toState === 'Duplicate' || entry.action === 'MarkedDuplicate')
  const duplicateSource = duplicateHistory?.sources.find((source) => source.candidateId)

  return duplicateSource?.candidateId
    ? {
        duplicateOfCandidateId: duplicateSource.candidateId,
        reason: duplicateHistory?.reason ?? `Candidate duplicates ${duplicateSource.candidateId}.`,
      }
    : null
}

function getAction(
  eligibility: DecisionLifecycleEntityEligibility | null,
  commandName: string,
): DecisionLifecycleActionEligibility | null {
  return eligibility?.allowedActions.find((action) => action.commandName === commandName) ??
    eligibility?.blockedActions.find((action) => action.commandName === commandName) ??
    null
}

function actionTitle(action: DecisionLifecycleActionEligibility | null) {
  if (!action) {
    return 'Lifecycle eligibility has not loaded.'
  }

  return action.isAllowed
    ? `${action.displayName} allowed by ${action.governingRule}.`
    : action.reason ?? `${action.displayName} is blocked by ${action.governingRule}.`
}

function LifecycleEligibilityDetails({ eligibility }: { eligibility: DecisionLifecycleEntityEligibility }) {
  return (
    <div className="decision-lifecycle-eligibility" aria-label="Candidate lifecycle eligibility">
      <div>
        <span>Allowed actions</span>
        <strong>{eligibility.allowedActions.map((action) => action.displayName).join(', ') || 'None'}</strong>
      </div>
      {eligibility.blockedActions.length > 0 ? (
        <ul className="decision-lifecycle-reasons" aria-label="Candidate blocked action reasons">
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
        <ul className="decision-lifecycle-reasons" aria-label="Candidate eligibility diagnostics">
          {eligibility.diagnostics.map((diagnostic) => (
            <li key={diagnostic}>{diagnostic}</li>
          ))}
        </ul>
      ) : null}
    </div>
  )
}
