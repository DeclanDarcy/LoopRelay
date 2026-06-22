import { useState } from 'react'
import { EmptyState, Panel, SectionHeader } from '../../components/design'
import type {
  DecisionCandidate,
  DecisionContextSnapshot,
  DecisionProposalBrowserItem,
  DecisionProposalState,
} from '../../types'
import {
  useDecisionEvidenceInspection,
  useDecisionOptionComparison,
  useDecisionProposalReview,
  useDecisionSourceAttributions,
} from '../../hooks'
import { DecisionCandidateBrowser } from './DecisionCandidateBrowser'
import { DecisionEvidenceSourcePanel } from './DecisionEvidenceSourcePanel'
import { DecisionOptionComparison } from './DecisionOptionComparison'
import { DecisionProposalBrowser } from './DecisionProposalBrowser'
import { DecisionProposalViewer } from './DecisionProposalViewer'

type DecisionLifecycleTabProps = {
  context: DecisionContextSnapshot | null
  candidates: DecisionCandidate[]
  proposals: DecisionProposalBrowserItem[]
  selectedProposalStates: DecisionProposalState[]
  hasSelectedRepository: boolean
  isLoading: boolean
  repositoryId: string | null
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
  repositoryId,
  onSelectedProposalStatesChange,
  onRefresh,
}: DecisionLifecycleTabProps) {
  const [selectedProposalId, setSelectedProposalId] = useState<string | null>(null)
  const {
    data: proposalReviewWorkspace,
    isLoading: isProposalReviewLoading,
  } = useDecisionProposalReview(repositoryId, selectedProposalId)
  const {
    data: optionComparison,
    isLoading: isOptionComparisonLoading,
  } = useDecisionOptionComparison(repositoryId, selectedProposalId)
  const {
    data: evidenceInspection,
    isLoading: isEvidenceInspectionLoading,
  } = useDecisionEvidenceInspection(repositoryId, selectedProposalId)
  const {
    data: sourceAttributions,
    isLoading: isSourceAttributionsLoading,
  } = useDecisionSourceAttributions(repositoryId, selectedProposalId)
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

          <DecisionCandidateBrowser candidates={candidates} isLoading={isLoading} />

          <DecisionProposalBrowser
            proposals={proposals}
            selectedStates={selectedProposalStates}
            isLoading={isLoading}
            onSelectedStatesChange={onSelectedProposalStatesChange}
            onSelectedProposalChange={setSelectedProposalId}
          />

          <DecisionProposalViewer
            workspace={proposalReviewWorkspace}
            isLoading={isProposalReviewLoading}
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
        </div>
      ) : (
        <EmptyState className="empty-state">Select or add a repository.</EmptyState>
      )}
    </Panel>
  )
}
