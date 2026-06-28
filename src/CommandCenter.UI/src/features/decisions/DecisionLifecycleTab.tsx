import { useState } from 'react'
import { EmptyState, Panel, SectionHeader } from '../../components/design'
import { DiagnosticList, InteractionPatternView } from '../../components/explainability'
import type {
  Decision,
  DecisionCandidate,
  DecisionContext,
  DecisionGenerationDiagnostics,
  DecisionLifecycleActionEligibility,
  DecisionLifecycleEligibilityProjection,
  DecisionLifecycleEntityEligibility,
  DecisionInfluenceTrace,
  DecisionProposal,
  DecisionProposalBrowserItem,
  DecisionProposalState,
  DecisionReviewWorkspace,
} from '../../types'
import {
  useDecisionCertification,
  useDecisionEvidenceInspection,
  useDecisionGenerationCertification,
  useDecisionGovernance,
  useDecisionOptionComparison,
  useDecisionProposalLineage,
  useDecisionProposalReview,
  useDecisionQuality,
  useDecisionSourceAttributions,
} from '../../hooks'
import { DecisionCandidateBrowser } from './DecisionCandidateBrowser'
import { DecisionCertificationPanel } from './DecisionCertificationPanel'
import { DecisionEvidenceSourcePanel } from './DecisionEvidenceSourcePanel'
import { DecisionGenerationCertificationPanel } from './DecisionGenerationCertificationPanel'
import { DecisionGovernancePanel } from './DecisionGovernancePanel'
import { DecisionInfluenceTracePanel } from './DecisionInfluenceTracePanel'
import { DecisionOptionComparison } from './DecisionOptionComparison'
import { DecisionProposalBrowser } from './DecisionProposalBrowser'
import { DecisionProposalViewer } from './DecisionProposalViewer'
import { DecisionQualityPanel } from './DecisionQualityPanel'
import { DecisionRefinementPanel } from './DecisionRefinementPanel'
import { DecisionResolutionPanel } from './DecisionResolutionPanel'
import { DecisionRevisionHistory } from './DecisionRevisionHistory'
import {
  decisionDiagnosticsToExplanation,
  decisionLifecycleEligibilityToActions,
} from '../../lib/explainability'

type DecisionLifecycleTabProps = {
  context: DecisionContext | null
  candidates: DecisionCandidate[]
  proposals: DecisionProposalBrowserItem[]
  selectedProposalStates: DecisionProposalState[]
  hasSelectedRepository: boolean
  isLoading: boolean
  actionsEnabled?: boolean
  lifecycleEligibility?: DecisionLifecycleEligibilityProjection | null
  isLifecycleEligibilityLoading?: boolean
  lifecycleEligibilityError?: string | null
  decisionInfluenceTrace?: DecisionInfluenceTrace | null
  isDecisionInfluenceLoading?: boolean
  decisionInfluenceError?: string | null
  repositoryId: string | null
  onSelectedProposalStatesChange: (states: DecisionProposalState[]) => void
  onRefresh: () => void
  onDiscover?: () => Promise<void> | void
  onPromoteCandidate?: (candidateId: string) => Promise<void> | void
  onDismissCandidate?: (candidateId: string) => Promise<void> | void
  onExpireCandidate?: (candidateId: string) => Promise<void> | void
  onMarkCandidateDuplicate?: (candidateId: string, duplicateOfCandidateId: string) => Promise<void> | void
  onGenerateProposal?: (candidateId: string) => Promise<DecisionProposal | null> | DecisionProposal | null
  onExpireProposal?: (proposalId: string) => Promise<void> | void
  onDiscardProposal?: (proposalId: string) => Promise<void> | void
  onSupersedeDecision?: (
    decisionId: string,
    replacementDecisionId: string,
    rationale: string,
    resolver: string,
  ) => Promise<void> | void
  onArchiveDecision?: (decisionId: string, rationale: string, resolver: string) => Promise<void> | void
  onRefreshExecutionProjection?: () => Promise<void> | void
}

export function DecisionLifecycleTab({
  context,
  candidates,
  proposals,
  selectedProposalStates,
  hasSelectedRepository,
  isLoading,
  actionsEnabled = true,
  lifecycleEligibility,
  isLifecycleEligibilityLoading = false,
  lifecycleEligibilityError,
  decisionInfluenceTrace = null,
  isDecisionInfluenceLoading = false,
  decisionInfluenceError = null,
  repositoryId,
  onSelectedProposalStatesChange,
  onRefresh,
  onDiscover,
  onPromoteCandidate,
  onDismissCandidate,
  onExpireCandidate,
  onMarkCandidateDuplicate,
  onGenerateProposal,
  onExpireProposal,
  onDiscardProposal,
  onSupersedeDecision,
  onArchiveDecision,
  onRefreshExecutionProjection,
}: DecisionLifecycleTabProps) {
  const [selectedProposalId, setSelectedProposalId] = useState<string | null>(null)
  const [selectedDecisionId, setSelectedDecisionId] = useState<string>('')
  const [replacementDecisionId, setReplacementDecisionId] = useState<string>('')
  const [decisionRationale, setDecisionRationale] = useState<string>('')
  const [decisionResolver, setDecisionResolver] = useState<string>('')
  const [lastGeneratedProposal, setLastGeneratedProposal] = useState<DecisionProposal | null>(null)
  const [lastResolvedDecision, setLastResolvedDecision] = useState<Decision | null>(null)
  const {
    data: proposalReviewWorkspace,
    isLoading: isProposalReviewLoading,
    isMutating: isProposalReviewMutating,
    refresh: refreshProposalReview,
    markViewed,
    markNeedsRefinement,
    markReadyForResolution,
  } = useDecisionProposalReview(repositoryId, selectedProposalId)
  const {
    data: proposalLineage,
    isLoading: isProposalLineageLoading,
    refresh: refreshProposalLineage,
  } = useDecisionProposalLineage(repositoryId, selectedProposalId)
  const {
    data: optionComparison,
    isLoading: isOptionComparisonLoading,
    refresh: refreshOptionComparison,
  } = useDecisionOptionComparison(repositoryId, selectedProposalId)
  const {
    data: evidenceInspection,
    isLoading: isEvidenceInspectionLoading,
    refresh: refreshEvidenceInspection,
  } = useDecisionEvidenceInspection(repositoryId, selectedProposalId)
  const {
    data: sourceAttributions,
    isLoading: isSourceAttributionsLoading,
    refresh: refreshSourceAttributions,
  } = useDecisionSourceAttributions(repositoryId, selectedProposalId)
  const {
    currentReport: governanceReport,
    reports: governanceReports,
    isLoading: isGovernanceLoading,
    isGenerating: isGovernanceGenerating,
    error: governanceError,
    refresh: refreshGovernance,
    generateReport: generateGovernanceReport,
  } = useDecisionGovernance(repositoryId)
  const {
    currentReport: certificationReport,
    reports: certificationReports,
    isLoading: isCertificationLoading,
    isRunning: isCertificationRunning,
    error: certificationError,
    refresh: refreshCertification,
    runCertification,
  } = useDecisionCertification(repositoryId)
  const {
    currentReport: generationCertificationReport,
    reports: generationCertificationReports,
    isLoading: isGenerationCertificationLoading,
    isRunning: isGenerationCertificationRunning,
    error: generationCertificationError,
    refresh: refreshGenerationCertification,
    runCertification: runGenerationCertification,
  } = useDecisionGenerationCertification(repositoryId)
  const {
    assessments: qualityAssessments,
    currentReport: qualityReport,
    reports: qualityReports,
    currentTrend: qualityTrend,
    trends: qualityTrends,
    isLoading: isQualityLoading,
    isAssessing: isQualityAssessing,
    isGeneratingReport: isQualityGeneratingReport,
    isGeneratingTrend: isQualityGeneratingTrend,
    error: qualityError,
    refresh: refreshQuality,
    assessProposal: assessProposalQuality,
    generateReport: generateQualityReport,
    generateTrend: generateQualityTrend,
  } = useDecisionQuality(repositoryId)
  const activeCandidateCount = candidates.filter((candidate) =>
    candidate.state === 'Discovered' || candidate.state === 'Promoted',
  ).length
  const reviewableProposalCount = proposals.filter((proposal) =>
    proposal.state !== 'Resolved' && proposal.state !== 'Expired' && proposal.state !== 'Discarded',
  ).length
  const selectedProposalEligibility =
    lifecycleEligibility?.proposals.find((proposal) => proposal.entityId === selectedProposalId) ?? null
  const proposalViewedAction = getAction(selectedProposalEligibility, 'mark_decision_proposal_viewed')
  const proposalNeedsRefinementAction = getAction(
    selectedProposalEligibility,
    'mark_decision_proposal_needs_refinement',
  )
  const proposalReadyForResolutionAction = getAction(
    selectedProposalEligibility,
    'mark_decision_proposal_ready_for_resolution',
  )
  const proposalExpireAction = getAction(selectedProposalEligibility, 'expire_decision_proposal')
  const proposalDiscardAction = getAction(selectedProposalEligibility, 'discard_decision_proposal')
  const decisionEligibilities = lifecycleEligibility?.decisions ?? []
  const selectedDecisionEligibility =
    decisionEligibilities.find((decision) => decision.entityId === selectedDecisionId) ??
    decisionEligibilities[0] ??
    null
  const activeDecisionId = selectedDecisionEligibility?.entityId ?? ''
  const selectedProposalResolvedDecision =
    lastResolvedDecision?.resolution?.sourceProposalSnapshot?.proposalId === selectedProposalId
      ? lastResolvedDecision
      : null
  const resolvedReplacementTargets = decisionEligibilities.filter((decision) =>
    decision.entityId !== activeDecisionId && decision.currentState === 'Resolved'
  )
  const supersedeAction = getAction(selectedDecisionEligibility, 'supersede_decision')
  const archiveAction = getAction(selectedDecisionEligibility, 'archive_decision')
  const canSubmitSupersede = Boolean(
    activeDecisionId &&
    replacementDecisionId &&
    decisionRationale.trim() &&
    decisionResolver.trim() &&
    onSupersedeDecision &&
    supersedeAction?.isAllowed,
  )
  const canSubmitArchive = Boolean(
    activeDecisionId &&
    decisionRationale.trim() &&
    decisionResolver.trim() &&
    onArchiveDecision &&
    archiveAction?.isAllowed,
  )

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
            <span>{context?.items?.length ?? 0} context items</span>
            <span>{activeCandidateCount} active candidates</span>
            <span>{proposals.length} proposals</span>
            <span>{reviewableProposalCount} reviewable proposals</span>
          </div>

          <DecisionCandidateBrowser
            candidates={candidates}
            isLoading={isLoading}
            actionsEnabled={actionsEnabled}
            eligibility={lifecycleEligibility?.candidates ?? []}
            onDiscover={onDiscover}
            onPromote={onPromoteCandidate}
            onDismiss={onDismissCandidate}
            onExpire={onExpireCandidate}
            onMarkDuplicate={onMarkCandidateDuplicate}
            onGenerateProposal={async (candidateId) => {
              const proposal = await onGenerateProposal?.(candidateId)
              setLastGeneratedProposal(proposal ?? null)
              if (proposal?.id) {
                setSelectedProposalId(proposal.id)
              }
            }}
          />

          <DecisionProposalGenerationResult proposal={lastGeneratedProposal} />

          <DecisionProposalBrowser
            proposals={proposals}
            selectedStates={selectedProposalStates}
            isLoading={isLoading}
            onSelectedStatesChange={onSelectedProposalStatesChange}
            onSelectedProposalChange={setSelectedProposalId}
          />

          <DecisionProposalViewer
            workspace={proposalReviewWorkspace}
            eligibility={selectedProposalEligibility}
            isLoading={isProposalReviewLoading || isProposalReviewMutating}
          />

          {actionsEnabled ? (
            <section className="decision-lifecycle-panel" aria-label="Proposal lifecycle actions">
              <div className="decision-panel-heading">
                <h5>Proposal Actions</h5>
                <span>{selectedProposalId ?? 'No proposal selected'}</span>
              </div>
              {lifecycleEligibilityError ? (
                <div className="decision-lifecycle-notice" role="alert">
                  {lifecycleEligibilityError}
                </div>
              ) : null}
              {selectedProposalEligibility ? (
                <ProposalInteractionSummary
                  eligibility={selectedProposalEligibility}
                  selectedProposalId={selectedProposalId}
                  workspace={proposalReviewWorkspace}
                />
              ) : selectedProposalId ? (
                <div className="decision-lifecycle-notice" role="status">
                  {isLifecycleEligibilityLoading
                    ? 'Loading lifecycle eligibility...'
                    : 'Lifecycle eligibility has not loaded for this proposal.'}
                </div>
              ) : null}
              <div className="context-controls">
                <button
                  type="button"
                  className="secondary-action"
                  onClick={async () => {
                    await markViewed()
                    await Promise.all([refreshProposalLineage(), refreshOptionComparison()])
                    onRefresh()
                  }}
                  disabled={
                    !selectedProposalId ||
                    isProposalReviewMutating ||
                    !proposalViewedAction?.isAllowed
                  }
                  title={actionTitle(proposalViewedAction)}
                >
                  Mark Viewed
                </button>
                <button
                  type="button"
                  className="secondary-action"
                  onClick={async () => {
                    await markNeedsRefinement()
                    await Promise.all([refreshProposalLineage(), refreshOptionComparison()])
                    onRefresh()
                  }}
                  disabled={
                    !selectedProposalId ||
                    isProposalReviewMutating ||
                    !proposalNeedsRefinementAction?.isAllowed
                  }
                  title={actionTitle(proposalNeedsRefinementAction)}
                >
                  Needs Refinement
                </button>
                <button
                  type="button"
                  className="secondary-action"
                  onClick={async () => {
                    await markReadyForResolution()
                    await Promise.all([refreshProposalLineage(), refreshOptionComparison()])
                    onRefresh()
                  }}
                  disabled={
                    !selectedProposalId ||
                    isProposalReviewMutating ||
                    !proposalReadyForResolutionAction?.isAllowed
                  }
                  title={actionTitle(proposalReadyForResolutionAction)}
                >
                  Ready For Resolution
                </button>
                <button
                  type="button"
                  className="secondary-action"
                  onClick={async () => {
                    if (!selectedProposalId) {
                      return
                    }

                    await onExpireProposal?.(selectedProposalId)
                    await refreshProposalReview()
                    onRefresh()
                  }}
                  disabled={
                    !selectedProposalId ||
                    isProposalReviewMutating ||
                    !onExpireProposal ||
                    !proposalExpireAction?.isAllowed
                  }
                  title={actionTitle(proposalExpireAction)}
                >
                  Expire
                </button>
                <button
                  type="button"
                  className="secondary-action"
                  onClick={async () => {
                    if (!selectedProposalId) {
                      return
                    }

                    await onDiscardProposal?.(selectedProposalId)
                    await refreshProposalReview()
                    onRefresh()
                  }}
                  disabled={
                    !selectedProposalId ||
                    isProposalReviewMutating ||
                    !onDiscardProposal ||
                    !proposalDiscardAction?.isAllowed
                  }
                  title={actionTitle(proposalDiscardAction)}
                >
                  Discard
                </button>
              </div>
            </section>
          ) : null}

          <DecisionRefinementPanel
            repositoryId={repositoryId}
            workspace={proposalReviewWorkspace}
            lineage={proposalLineage}
            eligibility={selectedProposalEligibility}
            isLoading={isProposalReviewLoading || isProposalLineageLoading}
            onRefined={async () => {
              await Promise.all([
                refreshProposalReview(),
                refreshProposalLineage(),
                refreshOptionComparison(),
                refreshEvidenceInspection(),
                refreshSourceAttributions(),
                refreshQuality(),
              ])
              onRefresh()
            }}
          />

          <DecisionResolutionPanel
            repositoryId={repositoryId}
            workspace={proposalReviewWorkspace}
            eligibility={selectedProposalEligibility}
            isLoading={isProposalReviewLoading}
            onResolved={async (decision) => {
              setLastResolvedDecision(decision)
              await Promise.all([
                refreshProposalReview(),
                refreshProposalLineage(),
                refreshOptionComparison(),
                refreshEvidenceInspection(),
                refreshSourceAttributions(),
                refreshQuality(),
              ])
              onRefresh()
            }}
          />

          {actionsEnabled ? (
            <section className="decision-lifecycle-panel" aria-label="Resolved decision lifecycle actions">
              <div className="decision-panel-heading">
                <h5>Decision Actions</h5>
                <span>{activeDecisionId || 'No decision selected'}</span>
              </div>
              {decisionEligibilities.length > 0 ? (
                <>
                  <div className="decision-filter-bar" aria-label="Resolved decision selection">
                    <select
                      value={activeDecisionId}
                      onChange={(event) => {
                        setSelectedDecisionId(event.target.value)
                        setReplacementDecisionId('')
                      }}
                    >
                      {decisionEligibilities.map((decision) => (
                        <option value={decision.entityId} key={decision.entityId}>
                          {decision.entityId} - {decision.currentState}
                        </option>
                      ))}
                    </select>
                    <select
                      value={replacementDecisionId}
                      onChange={(event) => setReplacementDecisionId(event.target.value)}
                      disabled={resolvedReplacementTargets.length === 0 || !supersedeAction?.isAllowed}
                    >
                      <option value="">Replacement decision</option>
                      {resolvedReplacementTargets.map((decision) => (
                        <option value={decision.entityId} key={decision.entityId}>
                          {decision.entityId}
                        </option>
                      ))}
                    </select>
                  </div>
                  {selectedDecisionEligibility ? (
                    <DecisionInteractionSummary
                      eligibility={selectedDecisionEligibility}
                      replacementDecisionId={replacementDecisionId}
                    />
                  ) : null}
                  <div className="decision-lifecycle-form">
                    <label>
                      <span>Rationale</span>
                      <textarea
                        value={decisionRationale}
                        onChange={(event) => setDecisionRationale(event.target.value)}
                        rows={3}
                      />
                    </label>
                    <label>
                      <span>Resolver</span>
                      <input
                        type="text"
                        value={decisionResolver}
                        onChange={(event) => setDecisionResolver(event.target.value)}
                      />
                    </label>
                  </div>
                  <div className="context-controls" aria-label="Resolved decision actions">
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={async () => {
                        if (!canSubmitSupersede) {
                          return
                        }

                        await onSupersedeDecision?.(
                          activeDecisionId,
                          replacementDecisionId,
                          decisionRationale.trim(),
                          decisionResolver.trim(),
                        )
                        await Promise.all([
                          refreshGovernance(),
                          refreshQuality(),
                          onRefreshExecutionProjection?.(),
                        ])
                        setDecisionRationale('')
                        setReplacementDecisionId('')
                      }}
                      disabled={!canSubmitSupersede}
                      title={actionTitle(supersedeAction)}
                    >
                      Supersede
                    </button>
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={async () => {
                        if (!canSubmitArchive) {
                          return
                        }

                        await onArchiveDecision?.(
                          activeDecisionId,
                          decisionRationale.trim(),
                          decisionResolver.trim(),
                        )
                        await Promise.all([
                          refreshGovernance(),
                          refreshQuality(),
                          onRefreshExecutionProjection?.(),
                        ])
                        setDecisionRationale('')
                      }}
                      disabled={!canSubmitArchive}
                      title={actionTitle(archiveAction)}
                    >
                      Archive
                    </button>
                  </div>
                </>
              ) : (
                <div className="decision-lifecycle-notice" role="status">
                  {isLifecycleEligibilityLoading
                    ? 'Loading decision lifecycle eligibility...'
                    : 'No resolved decisions are available for supersede or archive actions.'}
                </div>
              )}
            </section>
          ) : null}

          <DecisionRevisionHistory
            lineage={proposalLineage}
            isLoading={isProposalLineageLoading}
          />

          <DecisionOptionComparison
            comparison={optionComparison}
            isLoading={isOptionComparisonLoading}
          />

          <DecisionEvidenceSourcePanel
            inspection={evidenceInspection}
            attributions={sourceAttributions}
            isLoading={isEvidenceInspectionLoading || isSourceAttributionsLoading}
          />

          <DecisionGovernancePanel
            currentReport={governanceReport}
            reports={governanceReports}
            selectedProposalWorkspace={proposalReviewWorkspace}
            selectedProposalEligibility={selectedProposalEligibility}
            selectedDecisionEligibility={selectedDecisionEligibility}
            resolvedDecision={selectedProposalResolvedDecision}
            isLoading={isGovernanceLoading}
            isGenerating={isGovernanceGenerating}
            error={governanceError}
            onSelectProposal={setSelectedProposalId}
            onGenerateReport={async () => {
              await generateGovernanceReport()
              await refreshGovernance()
              onRefresh()
            }}
          />

          <DecisionQualityPanel
            assessments={qualityAssessments}
            currentReport={qualityReport}
            reports={qualityReports}
            currentTrend={qualityTrend}
            trends={qualityTrends}
            selectedProposalId={selectedProposalId}
            isLoading={isQualityLoading}
            isAssessing={isQualityAssessing}
            isGeneratingReport={isQualityGeneratingReport}
            isGeneratingTrend={isQualityGeneratingTrend}
            error={qualityError}
            onAssessProposal={async () => {
              await assessProposalQuality(selectedProposalId)
              await refreshQuality()
              onRefresh()
            }}
            onGenerateReport={async () => {
              await generateQualityReport()
              await refreshQuality()
              onRefresh()
            }}
            onGenerateTrend={async () => {
              await generateQualityTrend()
              await refreshQuality()
              onRefresh()
            }}
          />

          <DecisionInfluenceTracePanel
            trace={decisionInfluenceTrace}
            isLoading={isDecisionInfluenceLoading}
            error={decisionInfluenceError}
          />

          <DecisionCertificationPanel
            currentReport={certificationReport}
            reports={certificationReports}
            isLoading={isCertificationLoading}
            isRunning={isCertificationRunning}
            error={certificationError}
            onRunCertification={async () => {
              await runCertification()
              await refreshCertification()
              onRefresh()
            }}
          />

          <DecisionGenerationCertificationPanel
            currentReport={generationCertificationReport}
            reports={generationCertificationReports}
            isLoading={isGenerationCertificationLoading}
            isRunning={isGenerationCertificationRunning}
            error={generationCertificationError}
            onRunCertification={async () => {
              await runGenerationCertification()
              await refreshGenerationCertification()
              onRefresh()
            }}
          />
        </div>
      ) : (
        <EmptyState className="empty-state">Select or add a repository.</EmptyState>
      )}
    </Panel>
  )
}

function ProposalInteractionSummary({
  eligibility,
  selectedProposalId,
  workspace,
}: {
  eligibility: DecisionLifecycleEntityEligibility
  selectedProposalId: string | null
  workspace: DecisionReviewWorkspace | null
}) {
  const lastTransition = workspace ? getLastTransition(workspace.proposal.history) : null
  const subject = selectedProposalId
    ? `Proposal ${selectedProposalId}: ${eligibility.currentState}`
    : `Proposal: ${eligibility.currentState}`
  const result = lastTransition
    ? `${lastTransition.action}: ${lastTransition.fromState ?? 'None'} -> ${lastTransition.toState ?? 'None'}`
    : workspace?.review.reason ?? 'No proposal lifecycle command result recorded.'
  const evidence = [
    {
      label: 'Current state',
      detail: `${eligibility.entityKind} ${eligibility.entityId} is ${eligibility.currentState}.`,
    },
    ...eligibility.allowedNextStates.map((state) => ({
      label: 'Allowed next state',
      detail: state,
    })),
    ...eligibility.blockedNextStates.map((state) => ({
      label: 'Blocked next state',
      detail: `${state.state}: ${state.reason}`,
    })),
    ...(lastTransition
      ? [
          {
            label: 'Last transition reason',
            detail: lastTransition.reason ?? 'No transition reason recorded.',
          },
        ]
      : []),
    ...(workspace?.review.reason
      ? [
          {
            label: 'Review reason',
            detail: workspace.review.reason,
          },
        ]
      : []),
  ]

  return (
    <InteractionPatternView
      actions={decisionLifecycleEligibilityToActions(eligibility)}
      diagnostics={decisionDiagnosticsToExplanation(eligibility.diagnostics, 'Proposal lifecycle diagnostic')}
      evidence={evidence}
      result={result}
      subject={subject}
      title="Proposal interaction summary"
    />
  )
}

function DecisionInteractionSummary({
  eligibility,
  replacementDecisionId,
}: {
  eligibility: DecisionLifecycleEntityEligibility
  replacementDecisionId: string
}) {
  const subject = `Decision ${eligibility.entityId}: ${eligibility.currentState}`
  const evidence = [
    {
      label: 'Current state',
      detail: `${eligibility.entityKind} ${eligibility.entityId} is ${eligibility.currentState}.`,
    },
    ...eligibility.allowedNextStates.map((state) => ({
      label: 'Allowed next state',
      detail: state,
    })),
    ...eligibility.blockedNextStates.map((state) => ({
      label: 'Blocked next state',
      detail: `${state.state}: ${state.reason}`,
    })),
    ...(replacementDecisionId
      ? [
          {
            label: 'Selected replacement decision',
            detail: replacementDecisionId,
          },
        ]
      : []),
  ]

  return (
    <InteractionPatternView
      actions={decisionLifecycleEligibilityToActions(eligibility)}
      diagnostics={decisionDiagnosticsToExplanation(eligibility.diagnostics, 'Decision lifecycle diagnostic')}
      evidence={evidence}
      result="No decision lifecycle command result recorded."
      subject={subject}
      title="Decision interaction summary"
    />
  )
}

function getLastTransition(history: DecisionProposal['history'][number][]) {
  return [...history]
    .reverse()
    .find((entry) => entry.fromState !== entry.toState && (entry.fromState || entry.toState))
}

function DecisionProposalGenerationResult({ proposal }: { proposal: DecisionProposal | null }) {
  if (!proposal) {
    return (
      <section className="decision-lifecycle-panel" aria-label="Proposal generation result">
        <div className="decision-panel-heading">
          <h5>Generation Result</h5>
          <span>No proposal generated</span>
        </div>
        <EmptyState className="empty-state">Generate a promoted candidate to inspect proposal output.</EmptyState>
      </section>
    )
  }

  const diagnostics = proposal.generationDiagnostics
  const recommendationMode = proposal.recommendation?.mode ?? 'NoRecommendation'

  return (
    <section className="decision-lifecycle-panel" aria-label="Proposal generation result">
      <div className="decision-panel-heading">
        <h5>Generation Result</h5>
        <span>{proposal.id}</span>
      </div>
      <div className="decision-diagnostics-grid" aria-label="Generated proposal summary">
        <span>Generated proposal {proposal.id}</span>
        <span>Generation mode {recommendationMode}</span>
        <span>Candidate {proposal.candidateId}</span>
        <span>{diagnostics?.acceptedOptionCount ?? 0} accepted options</span>
        <span>{diagnostics?.rejectedOptionCount ?? 0} rejected options</span>
        <span>{diagnostics?.deduplicatedOptionCount ?? 0} deduplicated options</span>
      </div>
      {diagnostics?.optionValidationResults.length ? (
        <div aria-label="Generation validation diagnostics">
          <DiagnosticList
            diagnostics={decisionGenerationOptionValidationToDiagnostics(diagnostics)}
            title="Generation Validation Diagnostics"
          />
        </div>
      ) : null}
      {diagnostics?.diagnostics.length ? (
        <div aria-label="Generation command diagnostics">
          <DiagnosticList
            diagnostics={decisionDiagnosticsToExplanation(diagnostics.diagnostics, 'Generation command diagnostic')}
            title="Generation Command Diagnostics"
          />
        </div>
      ) : null}
    </section>
  )
}

function decisionGenerationOptionValidationToDiagnostics(diagnostics: DecisionGenerationDiagnostics) {
  return diagnostics.optionValidationResults.map((result) => ({
    label: `Option ${result.optionId}`,
    detail: result.isValid
      ? `${result.optionId}: valid`
      : `${result.optionId}: ${result.issues.map((issue) => issue.message).join('; ')}`,
    tone: result.isValid ? 'info' as const : 'warning' as const,
  }))
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
