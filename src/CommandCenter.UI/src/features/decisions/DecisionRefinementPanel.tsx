import { useEffect, useMemo, useState } from 'react'
import { EmptyState } from '../../components/design'
import { useDecisionProposalRefinement } from '../../hooks'
import type { FormEvent } from 'react'
import type {
  DecisionProposal,
  DecisionProposalLineage,
  DecisionRefinementRequest,
  DecisionReviewWorkspace,
} from '../../types'

type DecisionRefinementPanelProps = {
  repositoryId: string | null
  workspace: DecisionReviewWorkspace | null
  lineage: DecisionProposalLineage | null
  isLoading: boolean
  onRefined: (proposal: DecisionProposal) => Promise<void> | void
}

const refinableStates = new Set(['NeedsRefinement'])

export function DecisionRefinementPanel({
  repositoryId,
  workspace,
  lineage,
  isLoading,
  onRefined,
}: DecisionRefinementPanelProps) {
  const proposalId = workspace?.proposal.id ?? null
  const { refine, isSubmitting, error } = useDecisionProposalRefinement(repositoryId, proposalId)
  const [reason, setReason] = useState('')
  const [requestedBy, setRequestedBy] = useState('')
  const [context, setContext] = useState('')
  const [recommendationRationale, setRecommendationRationale] = useState('')
  const [rejectedChanges, setRejectedChanges] = useState('')
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  useEffect(() => {
    setReason('')
    setRequestedBy('')
    setContext('')
    setRecommendationRationale('')
    setRejectedChanges('')
    setSuccessMessage(null)
  }, [proposalId])

  const proposal = workspace?.proposal ?? null
  const canSubmitState = proposal ? refinableStates.has(proposal.state) : false
  const hasChange = Boolean(context.trim() || recommendationRationale.trim() || rejectedChanges.trim())
  const canSubmit = Boolean(proposal && canSubmitState && reason.trim() && hasChange && !isSubmitting)
  const baseProposalFingerprint = lineage?.currentProposalFingerprint ?? null
  const authorityText = useMemo(() => {
    if (!proposal) {
      return null
    }

    if (!canSubmitState) {
      return `Backend state ${proposal.state} is not open for refinement.`
    }

    return 'Refinement requests update backend proposal state and reload current proposal lineage.'
  }, [canSubmitState, proposal])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSuccessMessage(null)

    if (!proposal || !canSubmit) {
      return
    }

    const trimmedContext = context.trim()
    const trimmedRationale = recommendationRationale.trim()
    const request: DecisionRefinementRequest = {
      reason: reason.trim(),
      requestedBy: requestedBy.trim() || null,
      baseProposalFingerprint,
      context: trimmedContext || null,
      recommendation:
        trimmedRationale && proposal.recommendation
          ? { ...proposal.recommendation, rationale: trimmedRationale }
          : null,
      rejectedChanges: splitLines(rejectedChanges),
    }

    const refinedProposal = await refine(request)
    if (!refinedProposal) {
      return
    }

    setSuccessMessage(`Refinement submitted for ${refinedProposal.id}.`)
    setReason('')
    setContext('')
    setRecommendationRationale('')
    setRejectedChanges('')
    await onRefined(refinedProposal)
  }

  if (!workspace) {
    return (
      <section className="decision-lifecycle-panel decision-refinement-panel" aria-label="Refinement request">
        <h5>Refinement Request</h5>
        <EmptyState className="empty-state">
          {isLoading ? 'Loading refinement workspace...' : 'Select a proposal to request refinement.'}
        </EmptyState>
      </section>
    )
  }

  return (
    <section className="decision-lifecycle-panel decision-refinement-panel" aria-label="Refinement request">
      <div className="decision-panel-heading">
        <h5>Refinement Request</h5>
        <span>{workspace.proposal.id}</span>
      </div>

      <article className="decision-authority-callout" aria-label="Refinement authority">
        <div>
          <span>Backend Mutation</span>
          <strong>{workspace.proposal.state}</strong>
        </div>
        <p>{authorityText}</p>
        {baseProposalFingerprint ? <small>{baseProposalFingerprint}</small> : null}
      </article>

      <form className="decision-refinement-form" onSubmit={handleSubmit}>
        <label>
          <span>Reason</span>
          <textarea
            className="artifact-editor"
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            disabled={isSubmitting || !canSubmitState}
            spellCheck={false}
          />
        </label>

        <label>
          <span>Requested by</span>
          <input
            className="artifact-editor"
            value={requestedBy}
            onChange={(event) => setRequestedBy(event.target.value)}
            disabled={isSubmitting || !canSubmitState}
          />
        </label>

        <label>
          <span>Replacement context</span>
          <textarea
            className="artifact-editor"
            value={context}
            onChange={(event) => setContext(event.target.value)}
            disabled={isSubmitting || !canSubmitState}
            spellCheck={false}
          />
        </label>

        <label>
          <span>Recommendation rationale</span>
          <textarea
            className="artifact-editor"
            value={recommendationRationale}
            onChange={(event) => setRecommendationRationale(event.target.value)}
            disabled={isSubmitting || !canSubmitState || !workspace.proposal.recommendation}
            spellCheck={false}
          />
        </label>

        <label>
          <span>Rejected changes</span>
          <textarea
            className="artifact-editor"
            value={rejectedChanges}
            onChange={(event) => setRejectedChanges(event.target.value)}
            disabled={isSubmitting || !canSubmitState}
            spellCheck={false}
          />
        </label>

        <div className="decision-form-actions">
          <button type="submit" className="primary-action" disabled={!canSubmit}>
            {isSubmitting ? 'Submitting...' : 'Submit Refinement'}
          </button>
        </div>
      </form>

      {error ? (
        <div className="decision-warning-list" role="alert" aria-label="Refinement error">
          <span>{error}</span>
        </div>
      ) : null}

      {successMessage ? (
        <div className="decision-success-list" aria-label="Refinement success">
          <span>{successMessage}</span>
        </div>
      ) : null}
    </section>
  )
}

function splitLines(value: string) {
  const lines = value
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)

  return lines.length > 0 ? lines : null
}
