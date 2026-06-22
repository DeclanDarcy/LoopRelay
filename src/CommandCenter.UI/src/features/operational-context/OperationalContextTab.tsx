import { EmptyState, Panel, SectionHeader } from '../../components/design'
import { getOperationalContextSectionItems } from '../../lib'
import type {
  OperationalContextCompressionSummary,
  OperationalContextProposal,
  RepositoryWorkspaceProjection,
} from '../../types'
import { OperationalContextCompressionSummaryPanel } from './OperationalContextCompressionSummaryPanel'
import { OperationalContextCurrentPanel } from './OperationalContextCurrentPanel'
import { OperationalContextProposalComparison } from './OperationalContextProposalComparison'
import { OperationalContextProposalStatusPanel } from './OperationalContextProposalStatusPanel'
import { OperationalContextProposalSummaryPanel } from './OperationalContextProposalSummaryPanel'
import { OperationalContextSemanticChangeList } from './OperationalContextSemanticChangeList'

type OperationalContextTabProps = {
  workspace: RepositoryWorkspaceProjection | null
  proposal: OperationalContextProposal | null
  currentContent: string
  proposalDraft: string
  reviewNote: string
  executionStatus: string
  reviewStatus: string
  isProposalLoading: boolean
  isProposalSaving: boolean
  isReviewBlocked: boolean
  canPromoteProposal: boolean
  hasProposalDraftChanges: boolean
  hasSelectedRepository: boolean
  onLoadLatestProposal: () => void
  onGenerateProposal: () => void
  onSaveProposalEdit: () => void
  onAcceptProposal: () => void
  onRejectProposal: () => void
  onPromoteProposal: () => void
  onProposalDraftChange: (draft: string) => void
  onReviewNoteChange: (note: string) => void
  onOpenOperationalContextSection: (sectionId: string) => void
  onOpenWorkspaceExecutionContext: () => void
  onOpenContinuityWarnings: () => void
  onOpenContinuityCompression: () => void
  onOpenContinuityDecisionRetention: () => void
  onOpenArtifact: (relativePath: string) => void
}

const decisionSemanticChangeTypes = new Set([
  'ImportantDecisionIntroduced',
  'DecisionRetired',
  'DecisionAdded',
  'DecisionRemoved',
  'RationaleChanged',
  'RationaleLostWarning',
  'OpenDecisionPreserved',
  'OpenDecisionResolved',
])

function getDecisionContinuityWarnings(summary: OperationalContextCompressionSummary) {
  return summary.warnings
    .concat(summary.stableUnderstandingRetentionWarnings)
    .filter((warning, index, warnings) =>
      warning.toLowerCase().includes('decision') && warnings.indexOf(warning) === index,
    )
}

export function OperationalContextTab({
  workspace,
  proposal,
  currentContent,
  proposalDraft,
  reviewNote,
  executionStatus,
  reviewStatus,
  isProposalLoading,
  isProposalSaving,
  isReviewBlocked,
  canPromoteProposal,
  hasProposalDraftChanges,
  hasSelectedRepository,
  onLoadLatestProposal,
  onGenerateProposal,
  onSaveProposalEdit,
  onAcceptProposal,
  onRejectProposal,
  onPromoteProposal,
  onProposalDraftChange,
  onReviewNoteChange,
  onOpenOperationalContextSection,
  onOpenWorkspaceExecutionContext,
  onOpenContinuityWarnings,
  onOpenContinuityCompression,
  onOpenContinuityDecisionRetention,
  onOpenArtifact,
}: OperationalContextTabProps) {
  const proposedStableDecisions = getOperationalContextSectionItems(
    proposalDraft,
    'Stable Decisions',
  )
  const proposedOpenDecisions = getOperationalContextSectionItems(
    proposalDraft,
    'Open Questions',
  ).filter((item) => item.toLowerCase().startsWith('open decision:'))
  const proposedDecisionRationale = getOperationalContextSectionItems(
    proposalDraft,
    'Decision Rationale',
  )
  const decisionSemanticChanges =
    proposal?.semanticChanges.filter((change) => decisionSemanticChangeTypes.has(change.type)) ?? []
  const decisionContinuityWarnings = proposal
    ? getDecisionContinuityWarnings(proposal.compressionSummary)
    : []
  const artifactPaths = new Set(
    workspace
      ? [
          workspace.artifactInventory.plan,
          workspace.artifactInventory.operationalContext,
          workspace.artifactInventory.currentHandoff,
          workspace.artifactInventory.currentDecisions,
          ...workspace.artifactInventory.historicalOperationalContexts,
          ...workspace.artifactInventory.milestones,
          ...workspace.artifactInventory.historicalHandoffs,
          ...workspace.artifactInventory.historicalDecisions,
        ]
          .filter((artifact) => artifact !== null)
          .map((artifact) => artifact.relativePath)
      : [],
  )
  const isArtifactPathAvailable = (relativePath: string | null) =>
    relativePath !== null && artifactPaths.has(relativePath)

  return (
    <>
      <Panel
        id="operational-current"
        className="execution-context-panel tab-panel tab-operational-context"
        aria-label="Current understanding"
      >
        <SectionHeader
          className="context-toolbar"
          eyebrow="Operational Context"
          title="Current Understanding"
          headingLevel={4}
        />

        {workspace ? (
          <OperationalContextCurrentPanel
            operationalContext={workspace.operationalContext}
            proposalSummary={workspace.operationalContextProposalSummary}
            executionStatus={executionStatus}
            reviewStatus={reviewStatus}
            onOpenSection={onOpenOperationalContextSection}
            onOpenExecutionContext={onOpenWorkspaceExecutionContext}
            onOpenProposalReview={() => onOpenOperationalContextSection('proposal-review')}
            onOpenContinuityWarnings={onOpenContinuityWarnings}
          />
        ) : (
          <EmptyState className="empty-state">No repository workspace selected.</EmptyState>
        )}
      </Panel>

      <Panel
        className="execution-context-panel tab-panel tab-operational-context"
        id="proposal-review"
        aria-label="Operational context proposals"
      >
        <SectionHeader
          className="context-toolbar"
          eyebrow="Operational Context"
          title="Proposal Review"
          headingLevel={4}
          actions={
            <div className="context-controls">
              <button
                type="button"
                className="secondary-action"
                onClick={onLoadLatestProposal}
                disabled={
                  !workspace?.operationalContextProposalSummary.latestProposalId ||
                  isProposalLoading
                }
              >
                Load Latest
              </button>
              <button
                type="button"
                onClick={onGenerateProposal}
                disabled={!hasSelectedRepository || isProposalLoading}
              >
                {isProposalLoading ? 'Working...' : 'Generate Proposal'}
              </button>
            </div>
          }
        />

        {workspace ? (
          <OperationalContextProposalSummaryPanel
            operationalContext={workspace.operationalContext}
            proposalSummary={workspace.operationalContextProposalSummary}
          />
        ) : null}

        {proposal ? (
          <div className="context-artifact-previews">
            <OperationalContextProposalStatusPanel
              proposal={proposal}
              isArtifactPathAvailable={isArtifactPathAvailable}
              onOpenArtifact={onOpenArtifact}
            />
            <div className="proposal-review-toolbar">
              <button
                type="button"
                className="secondary-action"
                onClick={onSaveProposalEdit}
                disabled={isReviewBlocked || !hasProposalDraftChanges || isProposalSaving}
              >
                {isProposalSaving ? 'Saving...' : 'Save Edits'}
              </button>
              <button
                type="button"
                className="primary-action"
                onClick={onAcceptProposal}
                disabled={isReviewBlocked || isProposalSaving}
              >
                Accept
              </button>
              <button
                type="button"
                className="secondary-action"
                onClick={onRejectProposal}
                disabled={isReviewBlocked || isProposalSaving}
              >
                Reject
              </button>
              <button
                type="button"
                className="primary-action"
                onClick={onPromoteProposal}
                disabled={!canPromoteProposal || isProposalSaving}
              >
                Promote
              </button>
            </div>
            <label className="commit-message-editor">
              <span>Review note</span>
              <textarea
                value={reviewNote}
                onChange={(event) => onReviewNoteChange(event.target.value)}
                spellCheck={false}
              />
            </label>
            <label className="proposal-editor">
              <span>Proposed markdown</span>
              <textarea
                value={proposalDraft}
                onChange={(event) => onProposalDraftChange(event.target.value)}
                disabled={isReviewBlocked}
                spellCheck={false}
              />
            </label>
            <DecisionContinuityReview
              stableDecisions={proposedStableDecisions}
              openDecisions={proposedOpenDecisions}
              decisionRationale={proposedDecisionRationale}
              semanticChanges={decisionSemanticChanges}
              warnings={decisionContinuityWarnings}
              onOpenContinuityDecisionRetention={onOpenContinuityDecisionRetention}
            />
            <OperationalContextSemanticChangeList semanticChanges={proposal.semanticChanges} />
            <OperationalContextCompressionSummaryPanel
              compressionSummary={proposal.compressionSummary}
              onOpenContinuityCompression={onOpenContinuityCompression}
              onOpenContinuityDecisionRetention={onOpenContinuityDecisionRetention}
            />
            <OperationalContextProposalComparison
              currentContent={currentContent}
              proposedContent={proposalDraft}
            />
          </div>
        ) : null}
      </Panel>
    </>
  )
}

type DecisionContinuityReviewProps = {
  stableDecisions: string[]
  openDecisions: string[]
  decisionRationale: string[]
  semanticChanges: OperationalContextProposal['semanticChanges']
  warnings: string[]
  onOpenContinuityDecisionRetention: () => void
}

function DecisionContinuityReview({
  stableDecisions,
  openDecisions,
  decisionRationale,
  semanticChanges,
  warnings,
  onOpenContinuityDecisionRetention,
}: DecisionContinuityReviewProps) {
  return (
    <div className="proposal-warning-list proposal-decision-review">
      <h5>Decision Continuity Review</h5>
      <p>
        Confirm important decisions, unresolved decisions, and rationale remain present before accepting.
      </p>
      <div className="proposal-decision-grid">
        <DecisionContinuityList
          title="Stable Decisions"
          emptyLabel="No stable decisions in the proposal."
          items={stableDecisions}
        />
        <DecisionContinuityList
          title="Open Decisions"
          emptyLabel="No open decisions in the proposal."
          items={openDecisions}
        />
        <DecisionContinuityList
          title="Decision Rationale"
          emptyLabel="No decision rationale in the proposal."
          items={decisionRationale}
        />
        <div>
          <h6>Decision Changes</h6>
          {semanticChanges.length > 0 ? (
            <ul>
              {semanticChanges.map((change, index) => (
                <li key={`${change.type}-${change.itemId ?? index}`}>
                  {change.type}: {change.description}
                </li>
              ))}
            </ul>
          ) : (
            <p>No decision-specific semantic changes detected.</p>
          )}
        </div>
      </div>
      {warnings.length > 0 ? (
        <>
          <h6>Decision Warnings</h6>
          <ul>
            {warnings.map((warning) => (
              <li key={warning}>
                <button
                  type="button"
                  className="workspace-cross-link inline-cross-link warning-link"
                  onClick={onOpenContinuityDecisionRetention}
                >
                  {warning}
                </button>
              </li>
            ))}
          </ul>
        </>
      ) : null}
    </div>
  )
}

type DecisionContinuityListProps = {
  title: string
  emptyLabel: string
  items: string[]
}

function DecisionContinuityList({
  title,
  emptyLabel,
  items,
}: DecisionContinuityListProps) {
  return (
    <div>
      <h6>{title}</h6>
      {items.length > 0 ? (
        <ul>
          {items.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      ) : (
        <p>{emptyLabel}</p>
      )}
    </div>
  )
}
