import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ContinuityDiagnosticsPanel } from '../../features/continuity/ContinuityDiagnosticsPanel'
import type { ContinuityDiagnostics } from '../../types'

afterEach(() => {
  cleanup()
})

function createDiagnostics(
  overrides: Partial<ContinuityDiagnostics> = {},
): ContinuityDiagnostics {
  return {
    repositoryId: 'repo-1',
    generatedAt: '2026-01-02T03:04:05Z',
    revisionCount: 4,
    currentContextByteCount: 1200,
    currentContextCharacterCount: 1100,
    contextByteGrowth: 320,
    averageBytesPerRevision: 80.6,
    architectureTrend: {
      addedCount: 0,
      removedCount: 0,
      resolvedCount: 0,
      lostCount: 1,
    },
    constraintTrend: {
      addedCount: 0,
      removedCount: 0,
      resolvedCount: 0,
      lostCount: 2,
    },
    decisionTrend: {
      addedCount: 3,
      removedCount: 4,
      resolvedCount: 0,
      lostCount: 5,
    },
    rationaleTrend: {
      addedCount: 0,
      removedCount: 0,
      resolvedCount: 0,
      lostCount: 6,
    },
    openQuestionTrend: {
      addedCount: 0,
      removedCount: 0,
      resolvedCount: 7,
      lostCount: 8,
    },
    activeRiskTrend: {
      addedCount: 0,
      removedCount: 0,
      resolvedCount: 9,
      lostCount: 10,
    },
    compressionTrend: {
      proposalCount: 11,
      compressedItemCount: 12,
      removedItemCount: 13,
      resolvedQuestionCount: 0,
      retiredRiskCount: 0,
      warningCount: 14,
      warnings: [],
      noiseRemovedIndicators: [],
    },
    repeatedInvestigationIndicators: ['Investigation repeated'],
    repeatedQuestionIndicators: ['Question repeated'],
    decisionReworkIndicators: ['Decision reworked'],
    continuityWarnings: ['Decision rationale may be lost'],
    ...overrides,
  }
}

describe('continuity diagnostics panel rendering characterization', () => {
  it('renders the existing continuity summary labels and rounded average', () => {
    render(<ContinuityDiagnosticsPanel diagnostics={createDiagnostics()} />)

    expect(screen.getByText('Revisions: 4')).toBeInTheDocument()
    expect(screen.getByText('Current size: 1200 bytes')).toBeInTheDocument()
    expect(screen.getByText('Growth: 320 bytes')).toBeInTheDocument()
    expect(screen.getByText('Average: 81 bytes/revision')).toBeInTheDocument()
    expect(screen.getByText('Questions resolved: 7')).toBeInTheDocument()
    expect(screen.getByText('Questions lost: 8')).toBeInTheDocument()
    expect(screen.getByText('Risks retired: 9')).toBeInTheDocument()
    expect(screen.getByText('Risks lost: 10')).toBeInTheDocument()
    expect(screen.getByText('Decisions lost: 5')).toBeInTheDocument()
    expect(screen.getByText('Rationale lost: 6')).toBeInTheDocument()
  })

  it('renders preservation and compression rows with existing labels', () => {
    render(<ContinuityDiagnosticsPanel diagnostics={createDiagnostics()} />)

    const preservation = screen.getByRole('heading', { name: 'Preservation' }).closest('div')
    const compression = screen.getByRole('heading', { name: 'Compression' }).closest('div')

    expect(preservation).not.toBeNull()
    expect(compression).not.toBeNull()
    expect(within(preservation as HTMLElement).getByText('Architecture lost: 1')).toBeInTheDocument()
    expect(within(preservation as HTMLElement).getByText('Constraints lost: 2')).toBeInTheDocument()
    expect(within(preservation as HTMLElement).getByText('Decisions added: 3')).toBeInTheDocument()
    expect(within(preservation as HTMLElement).getByText('Decisions removed: 4')).toBeInTheDocument()
    expect(within(compression as HTMLElement).getByText('Proposals observed: 11')).toBeInTheDocument()
    expect(within(compression as HTMLElement).getByText('Items compressed: 12')).toBeInTheDocument()
    expect(within(compression as HTMLElement).getByText('Items removed: 13')).toBeInTheDocument()
    expect(within(compression as HTMLElement).getByText('Warnings: 14')).toBeInTheDocument()
  })

  it('preserves repeated-signal ordering across indicator groups', () => {
    render(<ContinuityDiagnosticsPanel diagnostics={createDiagnostics()} />)

    const repeatedSignals = screen.getByRole('heading', { name: 'Repeated Signals' }).closest('div')

    expect(repeatedSignals).not.toBeNull()
    expect(within(repeatedSignals as HTMLElement).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'Investigation repeated',
      'Question repeated',
      'Decision reworked',
    ])
  })

  it('preserves empty fallbacks for repeated signals and warnings', () => {
    render(
      <ContinuityDiagnosticsPanel
        diagnostics={createDiagnostics({
          repeatedInvestigationIndicators: [],
          repeatedQuestionIndicators: [],
          decisionReworkIndicators: [],
          continuityWarnings: [],
        })}
      />,
    )

    expect(screen.getByText('No repeated indicators recorded.')).toBeInTheDocument()
    expect(screen.getByText('No continuity warnings recorded.')).toBeInTheDocument()
  })
})
