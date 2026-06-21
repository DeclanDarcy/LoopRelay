import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { OperationalContextCurrentPanel } from '../../features/operational-context/OperationalContextCurrentPanel'
import type { OperationalContextProjection, OperationalContextProposalSummary } from '../../types'

afterEach(() => {
  cleanup()
})

function item(id: string, text: string) {
  return {
    id,
    kind: 'Fact',
    text,
    rationale: null,
    sourceRelativePath: null,
  }
}

function createProposalSummary(
  overrides: Partial<OperationalContextProposalSummary> = {},
): OperationalContextProposalSummary {
  return {
    pendingProposalExists: false,
    latestProposalId: 'proposal-1',
    generatedAt: '2026-01-02T03:04:05Z',
    status: 'Edited',
    sourceInputCount: 3,
    contentByteCount: 1200,
    contentCharacterCount: 1100,
    lastPromotedAt: null,
    lastArchivedRelativePath: null,
    ...overrides,
  }
}

function createOperationalContext(
  overrides: Partial<OperationalContextProjection> = {},
): OperationalContextProjection {
  return {
    exists: true,
    currentRelativePath: '.agents/operational_context.md',
    revisionCount: 5,
    currentRevisionNumber: 4,
    lastUpdatedAt: '2026-01-02T03:04:05Z',
    lastPromotionAt: '2026-01-03T04:05:06Z',
    currentUnderstandingSummary: ['Current model A', 'Current model B'],
    architecture: [item('architecture-1', 'Architecture item')],
    authorityBoundaries: [item('authority-1', 'Authority boundary')],
    constraints: [item('constraint-1', 'Constraint item')],
    stableDecisions: [item('decision-1', 'Stable decision')],
    decisionRationale: [item('rationale-1', 'Decision rationale')],
    openQuestions: [item('question-1', 'Open question')],
    activeRisks: [item('risk-1', 'Active risk')],
    recentUnderstandingChanges: [item('change-1', 'Recent change')],
    pendingProposalSummary: createProposalSummary(),
    latestReviewState: 'Edited',
    continuityWarnings: ['Continuity warning'],
    ...overrides,
  }
}

function renderPanel(
  operationalContext = createOperationalContext(),
  proposalSummary = createProposalSummary(),
) {
  render(
    <OperationalContextCurrentPanel
      operationalContext={operationalContext}
      proposalSummary={proposalSummary}
      executionStatus="Included (120 bytes)"
      reviewStatus="Edited"
    />,
  )
}

describe('operational context current panel rendering characterization', () => {
  it('renders the existing summary labels and formatted timestamps', () => {
    renderPanel()

    expect(screen.getByText('Path: .agents/operational_context.md')).toBeInTheDocument()
    expect(screen.getByText('Execution context: Included (120 bytes)')).toBeInTheDocument()
    expect(screen.getByText('Revisions: 5')).toBeInTheDocument()
    expect(screen.getByText('Current revision: 4')).toBeInTheDocument()
    expect(screen.getByText('Updated: 1/1/2026, 7:04:05 PM')).toBeInTheDocument()
    expect(screen.getByText('Last promoted: 1/2/2026, 8:05:06 PM')).toBeInTheDocument()
    expect(screen.getByText('Questions: 1')).toBeInTheDocument()
    expect(screen.getByText('Risks: 1')).toBeInTheDocument()
    expect(screen.getByText('Review: Edited')).toBeInTheDocument()
    expect(screen.getByText('Proposal: Edited')).toBeInTheDocument()
  })

  it('preserves current section order and item text', () => {
    renderPanel()

    const headings = screen.getAllByRole('heading', { level: 5 }).map((heading) => heading.textContent)

    expect(headings).toEqual([
      'Current Model',
      'Stable Decisions',
      'Decision Rationale',
      'Architecture',
      'Authority Boundaries',
      'Constraints',
      'Open Questions',
      'Active Risks',
      'Recent Changes',
      'Continuity Warnings',
    ])

    const currentModel = screen.getByRole('heading', { name: 'Current Model' }).closest('div')
    const stableDecisions = screen.getByRole('heading', { name: 'Stable Decisions' }).closest('div')
    const continuityWarnings = screen.getByRole('heading', { name: 'Continuity Warnings' }).closest('div')

    expect(currentModel).not.toBeNull()
    expect(stableDecisions).not.toBeNull()
    expect(continuityWarnings).not.toBeNull()
    expect(within(currentModel as HTMLElement).getAllByRole('listitem').map((li) => li.textContent)).toEqual([
      'Current model A',
      'Current model B',
    ])
    expect(within(stableDecisions as HTMLElement).getByText('Stable decision')).toBeInTheDocument()
    expect(within(continuityWarnings as HTMLElement).getByText('Continuity warning')).toBeInTheDocument()
  })

  it('renders the existing empty section fallbacks', () => {
    renderPanel(
      createOperationalContext({
        currentUnderstandingSummary: [],
        architecture: [],
        authorityBoundaries: [],
        constraints: [],
        stableDecisions: [],
        decisionRationale: [],
        openQuestions: [],
        activeRisks: [],
        recentUnderstandingChanges: [],
        continuityWarnings: [],
      }),
    )

    expect(screen.getByText('No current model items recorded.')).toBeInTheDocument()
    expect(screen.getByText('No stable decisions recorded.')).toBeInTheDocument()
    expect(screen.getByText('No decision rationale recorded.')).toBeInTheDocument()
    expect(screen.getByText('No architecture items recorded.')).toBeInTheDocument()
    expect(screen.getByText('No authority boundaries recorded.')).toBeInTheDocument()
    expect(screen.getByText('No constraints recorded.')).toBeInTheDocument()
    expect(screen.getByText('No open questions recorded.')).toBeInTheDocument()
    expect(screen.getByText('No active risks recorded.')).toBeInTheDocument()
    expect(screen.getByText('No recent understanding changes recorded.')).toBeInTheDocument()
    expect(screen.getByText('No continuity warnings recorded.')).toBeInTheDocument()
  })

  it('preserves missing current context and missing proposal fallbacks', () => {
    renderPanel(
      createOperationalContext({ exists: false }),
      createProposalSummary({ latestProposalId: null, status: null }),
    )

    expect(screen.getByText('Execution context: Included (120 bytes)')).toBeInTheDocument()
    expect(screen.getByText('Review: Edited')).toBeInTheDocument()
    expect(screen.getByText('Proposal: None')).toBeInTheDocument()
    expect(screen.getByText('No current operational context exists.')).toBeInTheDocument()
  })

  it('preserves unknown proposal status when a proposal id exists without status', () => {
    renderPanel(createOperationalContext(), createProposalSummary({ status: null }))

    expect(screen.getByText('Proposal: Unknown')).toBeInTheDocument()
  })
})
