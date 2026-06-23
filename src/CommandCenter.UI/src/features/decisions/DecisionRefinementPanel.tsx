import { useEffect, useMemo, useState } from 'react'
import { EmptyState } from '../../components/design'
import { useDecisionProposalRefinement } from '../../hooks'
import type { FormEvent } from 'react'
import type {
  DecisionPackageRegenerationResult,
  DecisionProposal,
  DecisionProposalLineage,
  DecisionRefinementRequest,
  DecisionReviewWorkspace,
  RefinementPlan,
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
  const { refine, analyze, regenerate, isSubmitting, error } = useDecisionProposalRefinement(
    repositoryId,
    proposalId,
  )
  const [reason, setReason] = useState('')
  const [requestedBy, setRequestedBy] = useState('')
  const [guidance, setGuidance] = useState('')
  const [plan, setPlan] = useState<RefinementPlan | null>(null)
  const [regenerationResult, setRegenerationResult] =
    useState<DecisionPackageRegenerationResult | null>(null)
  const [context, setContext] = useState('')
  const [recommendationRationale, setRecommendationRationale] = useState('')
  const [rejectedChanges, setRejectedChanges] = useState('')
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  useEffect(() => {
    setReason('')
    setRequestedBy('')
    setGuidance('')
    setPlan(null)
    setRegenerationResult(null)
    setContext('')
    setRecommendationRationale('')
    setRejectedChanges('')
    setSuccessMessage(null)
  }, [proposalId])

  const proposal = workspace?.proposal ?? null
  const canSubmitState = proposal ? refinableStates.has(proposal.state) : false
  const authority = workspace?.authority ?? null
  const hasChange = Boolean(context.trim() || recommendationRationale.trim() || rejectedChanges.trim())
  const canSubmit = Boolean(proposal && canSubmitState && reason.trim() && hasChange && !isSubmitting)
  const canAnalyze = Boolean(proposal && canSubmitState && guidance.trim() && !isSubmitting)
  const canRegenerate = Boolean(
    plan &&
      canSubmitState &&
      authority?.packageId &&
      authority.packageFingerprint &&
      !isSubmitting,
  )
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

  async function handleAnalyze() {
    setSuccessMessage(null)
    setRegenerationResult(null)

    if (!proposal || !canAnalyze) {
      return
    }

    const analyzedPlan = await analyze?.({
      guidance: guidance.trim(),
      requestedBy: requestedBy.trim() || null,
      baseProposalFingerprint,
    })
    if (!analyzedPlan) {
      return
    }

    setPlan(analyzedPlan)
  }

  async function handleRegenerate() {
    setSuccessMessage(null)

    if (!plan || !authority?.packageId || !authority.packageFingerprint || !canRegenerate) {
      return
    }

    const result = await regenerate?.({
      plan,
      basePackageId: authority.packageId,
      basePackageFingerprint: authority.packageFingerprint,
      requestedBy: requestedBy.trim() || null,
    })
    if (!result) {
      return
    }

    setRegenerationResult(result)
    setSuccessMessage(`Regenerated package ${result.regeneratedPackageVersion.id}.`)
  }

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

      <section className="decision-directive-refinement" aria-label="Directive-driven refinement">
        <div className="decision-panel-heading">
          <h6>Directive Regeneration</h6>
          {authority?.packageId ? <span>{authority.packageId}</span> : <span>No package</span>}
        </div>

        <div className="decision-refinement-form">
          <label>
            <span>Reviewer guidance</span>
            <textarea
              className="artifact-editor"
              value={guidance}
              onChange={(event) => {
                setGuidance(event.target.value)
                setPlan(null)
                setRegenerationResult(null)
              }}
              disabled={isSubmitting || !canSubmitState}
              spellCheck={false}
            />
          </label>

          <div className="decision-form-actions decision-form-actions-split">
            <button type="button" className="secondary-action" disabled={!canAnalyze} onClick={handleAnalyze}>
              {isSubmitting ? 'Analyzing...' : 'Analyze Guidance'}
            </button>
            <button
              type="button"
              className="primary-action"
              disabled={!canRegenerate}
              onClick={handleRegenerate}
            >
              {isSubmitting ? 'Regenerating...' : 'Regenerate Package'}
            </button>
          </div>
        </div>

        {plan ? <RefinementPlanSummary plan={plan} /> : null}
        {regenerationResult ? <RegenerationResultSummary result={regenerationResult} /> : null}
      </section>

      <form className="decision-refinement-form" onSubmit={handleSubmit}>
        <div className="decision-panel-heading">
          <h6>Compatibility Revision</h6>
          <span>Direct proposal edit</span>
        </div>

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

function RefinementPlanSummary({ plan }: { plan: RefinementPlan }) {
  const scope = [
    plan.regenerateOptions ? 'Options' : null,
    plan.reevaluateTradeoffs ? 'Tradeoffs' : null,
    plan.reevaluateRecommendation ? 'Recommendation' : null,
    plan.fullRegeneration ? 'Full regeneration' : null,
  ].filter(Boolean)

  return (
    <article className="decision-inspection-card" aria-label="Refinement plan">
      <div>
        <span>Plan Scope</span>
        <strong>{scope.length > 0 ? scope.join(', ') : 'No mutation scope detected'}</strong>
      </div>
      <div className="decision-directive-grid" aria-label="Detected directives">
        {plan.directives.map((directive) => (
          <span key={directive.id}>
            {directive.type}: {directive.summary}
          </span>
        ))}
      </div>
      {plan.appliedConstraints.length > 0 ? (
        <div className="decision-warning-list" aria-label="Applied constraints">
          {plan.appliedConstraints.map((constraint) => (
            <span key={constraint}>{constraint}</span>
          ))}
        </div>
      ) : null}
      {plan.diagnostics.length > 0 ? (
        <div className="decision-warning-list" aria-label="Refinement diagnostics">
          {plan.diagnostics.map((diagnostic) => (
            <span key={diagnostic}>{diagnostic}</span>
          ))}
        </div>
      ) : null}
    </article>
  )
}

function RegenerationResultSummary({ result }: { result: DecisionPackageRegenerationResult }) {
  const previousRecommendation = result.basePackageVersion.package.recommendation
  const regeneratedRecommendation = result.regeneratedPackageVersion.package.recommendation
  const comparisonFlags = [
    result.comparison.recommendationChanged ? 'Recommendation changed' : null,
    result.comparison.optionsChanged ? 'Options changed' : null,
    result.comparison.evidenceChanged ? 'Evidence changed' : null,
    result.comparison.risksChanged ? 'Risks changed' : null,
    result.comparison.contextFingerprintChanged ? 'Context changed' : null,
  ].filter(Boolean)

  return (
    <article className="decision-inspection-card" aria-label="Regenerated package comparison">
      <div>
        <span>Human Authoring Burden</span>
        <strong>{result.humanAuthoringBurden}</strong>
      </div>
      <div className="decision-diagnostics-grid" aria-label="Package comparison flags">
        {(comparisonFlags.length > 0 ? comparisonFlags : ['No comparison changes']).map((flag) => (
          <span key={flag}>{flag}</span>
        ))}
      </div>
      <div className="decision-recommendation-diff" aria-label="Recommendation diff">
        <section>
          <span>Previous Recommendation</span>
          <strong>{previousRecommendation?.optionId ?? 'No recommendation'}</strong>
          <p>{previousRecommendation?.rationale ?? 'No previous rationale.'}</p>
        </section>
        <section>
          <span>Regenerated Recommendation</span>
          <strong>{regeneratedRecommendation?.optionId ?? 'No recommendation'}</strong>
          <p>{regeneratedRecommendation?.rationale ?? 'No regenerated rationale.'}</p>
        </section>
      </div>
      {result.comparison.addedRisks.length > 0 || result.comparison.addedEvidence.length > 0 ? (
        <div className="decision-warning-list" aria-label="Regeneration additions">
          {[...result.comparison.addedRisks, ...result.comparison.addedEvidence].map((item) => (
            <span key={item}>{item}</span>
          ))}
        </div>
      ) : null}
      {result.diagnostics.length > 0 ? (
        <div className="decision-warning-list" aria-label="Regeneration diagnostics">
          {result.diagnostics.map((diagnostic) => (
            <span key={diagnostic}>{diagnostic}</span>
          ))}
        </div>
      ) : null}
    </article>
  )
}

function splitLines(value: string) {
  const lines = value
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)

  return lines.length > 0 ? lines : null
}
