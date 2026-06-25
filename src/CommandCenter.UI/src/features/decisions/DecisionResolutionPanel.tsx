import { useEffect, useMemo, useState } from 'react'
import { Badge, EmptyState } from '../../components/design'
import { DiagnosticList, EvidenceList } from '../../components/explainability'
import { useDecisionResolution } from '../../hooks'
import {
  decisionDiagnosticsToExplanation,
  decisionEvidenceToEvidence,
  decisionSourceReferencesToEvidence,
} from '../../lib/explainability'
import type { FormEvent } from 'react'
import type {
  Decision,
  DecisionAssimilationRecommendation,
  DecisionOutcome,
  DecisionReviewWorkspace,
} from '../../types'

type DecisionResolutionPanelProps = {
  repositoryId: string | null
  workspace: DecisionReviewWorkspace | null
  isLoading: boolean
  onResolved: (decision: Decision) => Promise<void> | void
}

const resolvableStates = new Set(['ReadyForResolution'])

export function DecisionResolutionPanel({
  repositoryId,
  workspace,
  isLoading,
  onResolved,
}: DecisionResolutionPanelProps) {
  const proposalId = workspace?.proposal.id ?? null
  const {
    decision,
    assimilationRecommendation,
    isSubmitting,
    isAssimilationLoading,
    error,
    resolve,
    loadAssimilationRecommendation,
    proposeAssimilationRecommendation,
    reset,
  } = useDecisionResolution(repositoryId, proposalId)
  const [outcome, setOutcome] = useState<DecisionOutcome>('Accepted')
  const [selectedOptionId, setSelectedOptionId] = useState('')
  const [resolver, setResolver] = useState('')
  const [rationale, setRationale] = useState('')
  const [assimilationRequestedBy, setAssimilationRequestedBy] = useState('')
  const [assimilationNotes, setAssimilationNotes] = useState('')

  useEffect(() => {
    reset()
    setOutcome('Accepted')
    setSelectedOptionId(workspace?.proposal.recommendation?.optionId ?? '')
    setResolver('')
    setRationale('')
    setAssimilationRequestedBy('')
    setAssimilationNotes('')
  }, [proposalId, reset, workspace?.proposal.recommendation?.optionId])

  const proposal = workspace?.proposal ?? null
  const authority = workspace?.authority ?? null
  const canResolveState = proposal ? resolvableStates.has(proposal.state) : false
  const recommendedOptionId = proposal?.recommendation?.optionId ?? null
  const recommendationDiverges = Boolean(
    recommendedOptionId && selectedOptionId && selectedOptionId !== recommendedOptionId,
  )
  const packageMatchesProposal = authority?.isPackageCurrentForProposalContent ?? false
  const canResolve = Boolean(
    proposal &&
      canResolveState &&
      resolver.trim() &&
      rationale.trim() &&
      selectedOptionId.trim() &&
      !isSubmitting,
  )
  const canCreateAssimilation = Boolean(
    decision?.state === 'Resolved' && decision.resolution?.outcome === 'Accepted' && !isAssimilationLoading,
  )
  const authorityText = useMemo(() => {
    if (!proposal) {
      return null
    }

    if (!canResolveState) {
      return `Backend state ${proposal.state} is not ready for resolution.`
    }

    return 'Resolution submits an explicit backend command; the returned decision is the only authority rendered here.'
  }, [canResolveState, proposal])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!proposal || !canResolve) {
      return
    }

    const resolvedDecision = await resolve({
      outcome,
      resolver: resolver.trim(),
      rationale: rationale.trim(),
      selectedOptionId: selectedOptionId.trim() || null,
      expectedProposalFingerprint: authority?.proposalFingerprint ?? null,
      expectedPackageId: authority?.packageId ?? null,
      expectedPackageFingerprint: authority?.packageFingerprint ?? null,
    })

    if (resolvedDecision) {
      await onResolved(resolvedDecision)
    }
  }

  async function handleLoadAssimilation() {
    if (!decision) {
      return
    }

    await loadAssimilationRecommendation(decision.id)
  }

  async function handleCreateAssimilation() {
    if (!decision) {
      return
    }

    await proposeAssimilationRecommendation(decision.id, {
      requestedBy: assimilationRequestedBy.trim() || null,
      notes: assimilationNotes.trim() || null,
    })
  }

  if (!workspace) {
    return (
      <section className="decision-lifecycle-panel decision-resolution-panel" aria-label="Decision resolution">
        <h5>Decision Resolution</h5>
        <EmptyState className="empty-state">
          {isLoading ? 'Loading resolution workspace...' : 'Select a proposal to resolve.'}
        </EmptyState>
      </section>
    )
  }

  return (
    <section className="decision-lifecycle-panel decision-resolution-panel" aria-label="Decision resolution">
      <div className="decision-panel-heading">
        <h5>Decision Resolution</h5>
        <span>{workspace.proposal.id}</span>
      </div>

      <article className="decision-authority-callout" aria-label="Resolution authority">
        <div>
          <span>Human Resolution</span>
          <strong>{workspace.proposal.state}</strong>
        </div>
        <p>{authorityText}</p>
      </article>

      <div className="decision-resolution-summary" aria-label="Proposal resolution summary">
        <article className="decision-inspection-card">
          <div>
            <span>Proposal Snapshot</span>
            <strong>{workspace.proposal.title}</strong>
          </div>
          <p>{workspace.proposal.context}</p>
          <div className="decision-badge-row">
            <Badge tone="info">{workspace.proposal.state}</Badge>
            <Badge>{workspace.review.state}</Badge>
            {recommendedOptionId ? <Badge tone="done">Recommended {recommendedOptionId}</Badge> : null}
            {authority?.packageId ? <Badge tone="info">{authority.packageId}</Badge> : null}
          </div>
        </article>

        <article className="decision-inspection-card">
          <div>
            <span>Recommendation</span>
            <strong>{recommendedOptionId ?? 'No recommendation'}</strong>
          </div>
          <p>{workspace.proposal.recommendation?.rationale ?? 'No backend recommendation is attached.'}</p>
          {recommendationDiverges ? (
            <div className="decision-warning-list" aria-label="Recommendation override">
              <span>Selected option overrides the recommendation. This is allowed and will be recorded.</span>
            </div>
          ) : null}
        </article>
      </div>

      <article className="decision-inspection-card" aria-label="Reviewed package authority">
        <div>
          <span>Reviewed Package</span>
          <strong>{authority?.packageId ?? 'No package version'}</strong>
        </div>
        <div className="decision-diagnostics-grid">
          <span>Proposal {shortFingerprint(authority?.proposalFingerprint ?? null)}</span>
          <span>Package {shortFingerprint(authority?.packageFingerprint ?? null)}</span>
          <span>Source {shortFingerprint(authority?.packageSourceProposalFingerprint ?? null)}</span>
          <span>{authority?.packageVersionCreatedAt ? formatDate(authority.packageVersionCreatedAt) : 'No package timestamp'}</span>
        </div>
        {authority?.packageId && !packageMatchesProposal ? (
          <div className="decision-warning-list" aria-label="Package authority warning">
            <span>Reviewed package content does not match the current proposal.</span>
          </div>
        ) : null}
      </article>

      <form className="decision-refinement-form decision-resolution-form" onSubmit={handleSubmit}>
        <label>
          <span>Outcome</span>
          <select
            className="artifact-editor"
            value={outcome}
            onChange={(event) => setOutcome(event.target.value as DecisionOutcome)}
            disabled={isSubmitting || !canResolveState}
          >
            <option value="Accepted">Accepted</option>
            <option value="Rejected">Rejected</option>
            <option value="Deferred">Deferred</option>
          </select>
        </label>

        <label>
          <span>Selected option</span>
          <select
            className="artifact-editor"
            value={selectedOptionId}
            onChange={(event) => setSelectedOptionId(event.target.value)}
            disabled={isSubmitting || !canResolveState}
          >
            <option value="">No option selected</option>
            {workspace.proposal.options.map((option) => (
              <option value={option.id} key={option.id}>
                {option.id} - {option.title}
              </option>
            ))}
          </select>
        </label>

        <label>
          <span>Resolver</span>
          <input
            className="artifact-editor"
            value={resolver}
            onChange={(event) => setResolver(event.target.value)}
            disabled={isSubmitting || !canResolveState}
          />
        </label>

        <label>
          <span>Rationale</span>
          <textarea
            className="artifact-editor"
            value={rationale}
            onChange={(event) => setRationale(event.target.value)}
            disabled={isSubmitting || !canResolveState}
            spellCheck={false}
          />
        </label>

        <div className="decision-form-actions">
          <button type="submit" className="primary-action" disabled={!canResolve}>
            {isSubmitting ? 'Resolving...' : 'Resolve Proposal'}
          </button>
        </div>
      </form>

      {decision ? <ResolvedDecisionCard decision={decision} /> : null}

      <section className="decision-inspection-list" aria-label="Assimilation recommendation">
        <div className="decision-panel-heading">
          <h6>Assimilation Recommendation</h6>
          <div className="decision-badge-row">
            <button
              type="button"
              className="secondary-action"
              onClick={handleLoadAssimilation}
              disabled={!decision || isAssimilationLoading}
            >
              {isAssimilationLoading ? 'Loading...' : 'Load Package'}
            </button>
            <button
              type="button"
              className="secondary-action"
              onClick={handleCreateAssimilation}
              disabled={!canCreateAssimilation}
            >
              Create Package
            </button>
          </div>
        </div>

        {decision ? (
          <form className="decision-refinement-form decision-assimilation-form">
            <label>
              <span>Requested by</span>
              <input
                className="artifact-editor"
                value={assimilationRequestedBy}
                onChange={(event) => setAssimilationRequestedBy(event.target.value)}
                disabled={isAssimilationLoading}
              />
            </label>
            <label>
              <span>Notes</span>
              <textarea
                className="artifact-editor"
                value={assimilationNotes}
                onChange={(event) => setAssimilationNotes(event.target.value)}
                disabled={isAssimilationLoading}
                spellCheck={false}
              />
            </label>
          </form>
        ) : (
          <EmptyState className="empty-state">Resolve a proposal before creating an advisory package.</EmptyState>
        )}

        {assimilationRecommendation ? (
          <AssimilationRecommendationCard recommendation={assimilationRecommendation} />
        ) : null}
      </section>

      {error ? (
        <div className="decision-warning-list" role="alert" aria-label="Resolution error">
          <span>{error}</span>
        </div>
      ) : null}
    </section>
  )
}

function ResolvedDecisionCard({ decision }: { decision: Decision }) {
  return (
    <article className="decision-inspection-card" aria-label="Resolved decision">
      <div>
        <span>Resulting Decision</span>
        <strong>{decision.id}</strong>
      </div>
      <div className="decision-badge-row">
        <Badge tone={decision.state === 'Resolved' ? 'done' : 'info'}>{decision.state}</Badge>
        {decision.resolution ? <Badge>{decision.resolution.outcome}</Badge> : null}
        {decision.resolution?.recommendationDiverged ? <Badge tone="warning">Override recorded</Badge> : null}
      </div>
      <p>{decision.resolution?.rationale ?? decision.context}</p>
      {decision.resolution ? (
        <small>
          {decision.resolution.selectedOptionId} by {decision.resolution.resolvedBy} at{' '}
          {formatDate(decision.resolution.resolvedAt)}
        </small>
      ) : null}
    </article>
  )
}

function AssimilationRecommendationCard({
  recommendation,
}: {
  recommendation: DecisionAssimilationRecommendation
}) {
  return (
    <article className="decision-inspection-card" aria-label="Operational context assimilation package">
      <div>
        <span>Advisory Package</span>
        <strong>{recommendation.decisionId}</strong>
      </div>
      <p>{recommendation.projectedStableDecision}</p>
      <p>{recommendation.rationale}</p>
      <div className="decision-diagnostics-grid">
        <span>Decision {recommendation.decisionFingerprint}</span>
        <span>Context {recommendation.contextFingerprint}</span>
        <span>Snapshot {recommendation.contextSnapshotId}</span>
      </div>
      <EvidenceList
        title="Assimilation Evidence"
        evidence={[
          ...decisionEvidenceToEvidence(recommendation.evidence, 'Assimilation Evidence'),
          ...decisionSourceReferencesToEvidence(recommendation.sources),
        ]}
      />
      <DiagnosticList
        title="Assimilation Diagnostics"
        diagnostics={decisionDiagnosticsToExplanation(recommendation.diagnostics, 'Assimilation Diagnostic')}
      />
      <small>
        Advisory only. This package does not mutate operational context or promote continuity policy.
      </small>
    </article>
  )
}

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  return date.toLocaleString()
}

function shortFingerprint(value: string | null) {
  if (!value) {
    return 'None'
  }

  return value.length > 12 ? value.slice(0, 12) : value
}
