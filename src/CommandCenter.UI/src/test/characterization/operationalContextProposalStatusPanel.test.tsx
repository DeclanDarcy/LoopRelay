import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { OperationalContextProposalStatusPanel } from '../../features/operational-context/OperationalContextProposalStatusPanel'
import type { OperationalContextProposal } from '../../types'

afterEach(() => {
  cleanup()
})

function createProposal(overrides: Partial<OperationalContextProposal> = {}): OperationalContextProposal {
  return {
    proposalId: 'proposal-42',
    repositoryId: 'repo-1',
    generatedAt: '2026-01-02T03:04:05Z',
    status: 'Accepted',
    generatedContentRelativePath: '.agents/proposals/proposal-42.md',
    generatedContentHash: 'generated-hash',
    editedContentRelativePath: null,
    semanticChanges: [],
    decisionAssimilation: {
      decisions: [],
      consequences: [],
      contradictions: [],
      limit: {
        limit: 0,
        reason: '',
        totalAnalyzedItemCount: 0,
        totalQualifyingItemCount: 0,
        assimilatedItemCount: 0,
        omittedItemCount: 0,
      },
    },
    compressionSummary: {
      preservedItemCount: 0,
      addedItemCount: 0,
      modifiedItemCount: 0,
      removedItemCount: 0,
      compressedItemCount: 0,
      permanentUnderstandingItemCount: 0,
      activeUnderstandingItemCount: 0,
      historicalUnderstandingItemCount: 0,
      historicalNoiseItemCount: 0,
      resolvedQuestionCount: 0,
      retiredRiskCount: 0,
      warningCount: 0,
      warnings: [],
      revisionSummary: [],
      noiseRemovedIndicators: [],
      stableUnderstandingRetentionWarnings: [],
      itemOutcomes: [],
    },
    review: {
      proposalId: 'proposal-42',
      reviewState: 'Accepted',
      baselineCurrentContextHash: 'baseline-hash',
      reviewedContentHash: 'reviewed-hash',
      reviewedAt: '2026-01-03T04:05:06Z',
      reviewNote: null,
      staleReason: null,
    },
    promotion: {
      proposalId: 'proposal-42',
      promotedAt: '2026-01-04T05:06:07Z',
      promotedContentHash: 'promoted-hash',
      promotedContentSourceRelativePath: '.agents/operational_context.proposal.md',
      revisionNumber: 8,
      archivedRelativePath: '.agents/operational_context.0001.md',
      archiveFailureReason: null,
      writeFailureReason: null,
    },
    generatedContent: null,
    editedContent: null,
    ...overrides,
  }
}

function renderPanel(proposal = createProposal()) {
  render(<OperationalContextProposalStatusPanel proposal={proposal} />)
}

describe('operational context proposal status panel rendering characterization', () => {
  it('renders loaded proposal metadata labels in order', () => {
    renderPanel()

    expect(screen.getByText('Proposal: proposal-42')).toBeInTheDocument()
    const acceptedBadges = screen.getAllByText('Accepted')
    expect(acceptedBadges[0]).toHaveClass('cc-badge', 'cc-badge-done')
    expect(acceptedBadges[1]).toHaveClass('cc-badge', 'cc-badge-done')
    expect(screen.getByText('Reviewed: 1/2/2026, 8:05:06 PM')).toBeInTheDocument()
    expect(screen.getByText('Promoted: 1/3/2026, 9:06:07 PM')).toBeInTheDocument()
    expect(screen.getByText('Archived: .agents/operational_context.0001.md')).toBeInTheDocument()
  })

  it('preserves not-recorded and none fallbacks for missing review and promotion fields', () => {
    renderPanel(
      createProposal({
        review: {
          ...createProposal().review,
          reviewedAt: null,
        },
        promotion: {
          ...createProposal().promotion,
          promotedAt: null,
          archivedRelativePath: null,
        },
      }),
    )

    expect(screen.getByText('Reviewed: Not recorded')).toBeInTheDocument()
    expect(screen.getByText('Promoted: Not recorded')).toBeInTheDocument()
    expect(screen.getByText('Archived: None')).toBeInTheDocument()
  })

  it('renders stale review and promotion failure notices only when present', () => {
    const { rerender } = render(
      <OperationalContextProposalStatusPanel
        proposal={createProposal({
          review: {
            ...createProposal().review,
            staleReason: 'Baseline context changed.',
          },
          promotion: {
            ...createProposal().promotion,
            archiveFailureReason: 'Archive path already exists.',
            writeFailureReason: 'Current context is locked.',
          },
        })}
      />,
    )

    expect(screen.getByRole('heading', { name: 'Proposal Lifecycle Diagnostics' })).toBeInTheDocument()
    expect(screen.getByText('Review blocked')).toBeInTheDocument()
    expect(screen.getByText('Baseline context changed.')).toBeInTheDocument()
    expect(screen.getByText('Promotion archive failed')).toBeInTheDocument()
    expect(screen.getByText('Archive path already exists.')).toBeInTheDocument()
    expect(screen.getByText('Promotion write failed')).toBeInTheDocument()
    expect(screen.getByText('Current context is locked.')).toBeInTheDocument()

    rerender(<OperationalContextProposalStatusPanel proposal={createProposal()} />)

    expect(screen.queryByText('Review blocked')).not.toBeInTheDocument()
    expect(screen.queryByText('Promotion archive failed')).not.toBeInTheDocument()
    expect(screen.queryByText('Promotion write failed')).not.toBeInTheDocument()
  })
})
