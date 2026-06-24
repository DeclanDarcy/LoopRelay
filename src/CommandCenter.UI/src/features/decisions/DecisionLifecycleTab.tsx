import { useState } from 'react'
import { EmptyState, Panel, SectionHeader } from '../../components/design'
import type {
  DecisionCandidate,
  DecisionContextSnapshot,
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
              <div className="context-controls">
                <button
                  type="button"
                  className="secondary-action"
                  onClick={async () => {
                    await markViewed()
                    await Promise.all([refreshProposalLineage(), refreshOptionComparison()])
                    onRefresh()
                  }}
                  disabled={!selectedProposalId || isProposalReviewMutating}
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
                  disabled={!selectedProposalId || isProposalReviewMutating}
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
                  disabled={!selectedProposalId || isProposalReviewMutating}
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
                  disabled={!selectedProposalId || isProposalReviewMutating || !onExpireProposal}
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
                  disabled={!selectedProposalId || isProposalReviewMutating || !onDiscardProposal}
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
