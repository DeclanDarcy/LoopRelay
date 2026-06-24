import { useState } from 'react'
import { EmptyState, Panel, SectionHeader } from '../../components/design'
import type {
  DecisionCandidate,
  DecisionContextSnapshot,
  DecisionLifecycleActionEligibility,
  DecisionLifecycleEligibilityProjection,
  DecisionLifecycleEntityEligibility,
  DecisionProposalBrowserItem,
  DecisionProposalState,
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
import { DecisionOptionComparison } from './DecisionOptionComparison'
import { DecisionProposalBrowser } from './DecisionProposalBrowser'
import { DecisionProposalViewer } from './DecisionProposalViewer'
import { DecisionQualityPanel } from './DecisionQualityPanel'
import { DecisionRefinementPanel } from './DecisionRefinementPanel'
import { DecisionResolutionPanel } from './DecisionResolutionPanel'
import { DecisionRevisionHistory } from './DecisionRevisionHistory'

type DecisionLifecycleTabProps = {
  context: DecisionContextSnapshot | null
  candidates: DecisionCandidate[]
  proposals: DecisionProposalBrowserItem[]
  selectedProposalStates: DecisionProposalState[]
  hasSelectedRepository: boolean
  isLoading: boolean
  actionsEnabled?: boolean
  lifecycleEligibility?: DecisionLifecycleEligibilityProjection | null
  isLifecycleEligibilityLoading?: boolean
  lifecycleEligibilityError?: string | null
  repositoryId: string | null
  onSelectedProposalStatesChange: (states: DecisionProposalState[]) => void
  onRefresh: () => void
  onDiscover?: () => Promise<void> | void
  onPromoteCandidate?: (candidateId: string) => Promise<void> | void
  onDismissCandidate?: (candidateId: string) => Promise<void> | void
  onExpireCandidate?: (candidateId: string) => Promise<void> | void
  onMarkCandidateDuplicate?: (candidateId: string, duplicateOfCandidateId: string) => Promise<void> | void
  onGenerateProposal?: (candidateId: string) => Promise<string | null> | string | null
  onExpireProposal?: (proposalId: string) => Promise<void> | void
  onDiscardProposal?: (proposalId: string) => Promise<void> | void
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
}: DecisionLifecycleTabProps) {
  const [selectedProposalId, setSelectedProposalId] = useState<string | null>(null)
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
              const proposalId = await onGenerateProposal?.(candidateId)
              if (proposalId) {
                setSelectedProposalId(proposalId)
              }
            }}
          />

          <DecisionProposalBrowser
            proposals={proposals}
            selectedStates={selectedProposalStates}
            isLoading={isLoading}
            onSelectedStatesChange={onSelectedProposalStatesChange}
            onSelectedProposalChange={setSelectedProposalId}
          />

          <DecisionProposalViewer
            workspace={proposalReviewWorkspace}
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
                <LifecycleEligibilityDetails
                  eligibility={selectedProposalEligibility}
                  label="Proposal"
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
            isLoading={isProposalReviewLoading}
            onResolved={async () => {
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

function LifecycleEligibilityDetails({
  eligibility,
  label,
}: {
  eligibility: DecisionLifecycleEntityEligibility
  label: string
}) {
  return (
    <div className="decision-lifecycle-eligibility" aria-label={`${label} lifecycle eligibility`}>
      <div>
        <span>Allowed actions</span>
        <strong>{eligibility.allowedActions.map((action) => action.displayName).join(', ') || 'None'}</strong>
      </div>
      <div>
        <span>Allowed next states</span>
        <strong>{eligibility.allowedNextStates.join(', ') || 'None'}</strong>
      </div>
      {eligibility.blockedActions.length > 0 ? (
        <ul className="decision-lifecycle-reasons" aria-label={`${label} blocked action reasons`}>
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
        <ul className="decision-lifecycle-reasons" aria-label={`${label} eligibility diagnostics`}>
          {eligibility.diagnostics.map((diagnostic) => (
            <li key={diagnostic}>{diagnostic}</li>
          ))}
        </ul>
      ) : null}
    </div>
  )
}
