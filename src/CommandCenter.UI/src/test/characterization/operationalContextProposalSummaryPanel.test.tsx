import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { OperationalContextProposalSummaryPanel } from '../../features/operational-context/OperationalContextProposalSummaryPanel'
import type { OperationalContextProjection, OperationalContextProposalSummary } from '../../types'

afterEach(() => {
  cleanup()
})

function createOperationalContext(
  overrides: Partial<OperationalContextProjection> = {},
): OperationalContextProjection {
  return {
    exists: true,
    currentRelativePath: '.agents/operational_context.md',
    revisionCount: 7,
    currentRevisionNumber: 6,
    lastUpdatedAt: null,
    lastPromotionAt: null,
    currentUnderstandingSummary: [],
    architecture: [],
    authorityBoundaries: [],
    constraints: [],
    stableDecisions: [],
    decisionRationale: [],
    openQuestions: [],
    activeRisks: [],
    recentUnderstandingChanges: [],
    pendingProposalSummary: createProposalSummary(),
    latestReviewState: null,
    continuityWarnings: [],
    ...overrides,
  }
}

function createProposalSummary(
  overrides: Partial<OperationalContextProposalSummary> = {},
): OperationalContextProposalSummary {
  return {
    pendingProposalExists: true,
    latestProposalId: 'proposal-42',
    generatedAt: '2026-01-02T03:04:05Z',
    status: 'Edited',
    sourceInputCount: 5,
    contentByteCount: 2048,
    contentCharacterCount: 1999,
    lastPromotedAt: '2026-01-03T04:05:06Z',
    lastArchivedRelativePath: '.agents/operational_context.0001.md',
    ...overrides,
  }
}

function renderPanel(
  operationalContext = createOperationalContext(),
  proposalSummary = createProposalSummary(),
) {
  render(
    <OperationalContextProposalSummaryPanel
      operationalContext={operationalContext}
      proposalSummary={proposalSummary}
    />,
  )
}

describe('operational context proposal summary panel rendering characterization', () => {
  it('renders existing proposal summary labels in order', () => {
    renderPanel()

    expect(screen.getByText('Latest: proposal-42')).toBeInTheDocument()
    expect(screen.getByText('Status: Edited')).toBeInTheDocument()
    expect(screen.getByText('Generated: 1/1/2026, 7:04:05 PM')).toBeInTheDocument()
    expect(screen.getByText('Inputs: 5')).toBeInTheDocument()
    expect(screen.getByText('Size: 2048 bytes')).toBeInTheDocument()
    expect(screen.getByText('Current revisions: 7')).toBeInTheDocument()
    expect(screen.getByText('Last promoted: 1/2/2026, 8:05:06 PM')).toBeInTheDocument()
    expect(screen.getByText('Archived prior: .agents/operational_context.0001.md')).toBeInTheDocument()
  })

  it('preserves unknown and none fallbacks when summary fields are missing', () => {
    renderPanel(
      createOperationalContext({ revisionCount: 0 }),
      createProposalSummary({
        status: null,
        generatedAt: null,
        lastPromotedAt: null,
        lastArchivedRelativePath: null,
      }),
    )

    expect(screen.getByText('Status: Unknown')).toBeInTheDocument()
    expect(screen.getByText('Generated: Not recorded')).toBeInTheDocument()
    expect(screen.getByText('Last promoted: Not recorded')).toBeInTheDocument()
    expect(screen.getByText('Archived prior: None')).toBeInTheDocument()
  })

  it('preserves the empty proposal fallback', () => {
    renderPanel(createOperationalContext(), createProposalSummary({ latestProposalId: null }))

    expect(screen.getByText('No operational-context proposal has been generated.')).toBeInTheDocument()
  })
})
