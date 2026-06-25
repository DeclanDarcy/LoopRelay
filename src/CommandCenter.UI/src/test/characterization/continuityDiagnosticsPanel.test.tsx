import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ContinuityDiagnosticsPanel } from '../../features/continuity/ContinuityDiagnosticsPanel'
import type { ContinuityDiagnostics, ContinuityReport } from '../../types'

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
      modifiedCount: 2,
      removedCount: 0,
      resolvedCount: 0,
      lostCount: 1,
    },
    constraintTrend: {
      addedCount: 0,
      modifiedCount: 0,
      removedCount: 0,
      resolvedCount: 0,
      lostCount: 2,
    },
    decisionTrend: {
      addedCount: 3,
      modifiedCount: 19,
      removedCount: 4,
      resolvedCount: 0,
      lostCount: 5,
    },
    rationaleTrend: {
      addedCount: 0,
      modifiedCount: 0,
      removedCount: 0,
      resolvedCount: 0,
      lostCount: 6,
    },
    openQuestionTrend: {
      addedCount: 15,
      modifiedCount: 20,
      removedCount: 0,
      resolvedCount: 7,
      lostCount: 8,
    },
    activeRiskTrend: {
      addedCount: 16,
      modifiedCount: 0,
      removedCount: 0,
      resolvedCount: 9,
      lostCount: 10,
    },
    operationalEvolution: {
      addedCount: 21,
      modifiedCount: 22,
      removedCount: 23,
      preservedCount: 24,
      lostCount: 25,
      resolvedCount: 26,
      semanticChanges: [],
      diagnosticGroups: [],
    },
    compressionTrend: {
      proposalCount: 11,
      compressedItemCount: 12,
      removedItemCount: 13,
      resolvedQuestionCount: 17,
      retiredRiskCount: 18,
      warningCount: 14,
      warnings: ['Compression warning'],
      noiseRemovedIndicators: ['Noise removed'],
    },
    repeatedInvestigationIndicators: ['Investigation repeated'],
    repeatedQuestionIndicators: ['Question repeated'],
    decisionReworkIndicators: ['Decision reworked'],
    continuityWarnings: ['Decision rationale may be lost'],
    diagnosticGroups: [],
    ...overrides,
  }
}

function createReport(overrides: Partial<ContinuityReport> = {}): ContinuityReport {
  const diagnostics = createDiagnostics()

  return {
    reportId: 'continuity.20260102',
    repositoryId: diagnostics.repositoryId,
    generatedAt: '2026-01-02T04:05:06Z',
    relativePath: '.agents/continuity/continuity.20260102.json',
    diagnostics,
    ...overrides,
  }
}

describe('continuity diagnostics panel rendering characterization', () => {
  it('renders the existing continuity summary labels and rounded average', () => {
    render(<ContinuityDiagnosticsPanel diagnostics={createDiagnostics()} />)

    const summary = document.querySelector('.context-summary')

    expect(summary).not.toBeNull()
    expect(within(summary as HTMLElement).getByText('Revisions: 4')).toBeInTheDocument()
    expect(within(summary as HTMLElement).getByText('Current size: 1200 bytes')).toBeInTheDocument()
    expect(within(summary as HTMLElement).getByText('Growth: 320 bytes')).toBeInTheDocument()
    expect(within(summary as HTMLElement).getByText('Average: 81 bytes/revision')).toBeInTheDocument()
    expect(within(summary as HTMLElement).getByText('Questions resolved: 7')).toBeInTheDocument()
    expect(within(summary as HTMLElement).getByText('Questions lost: 8')).toBeInTheDocument()
    expect(within(summary as HTMLElement).getByText('Risks retired: 9')).toBeInTheDocument()
    expect(within(summary as HTMLElement).getByText('Risks lost: 10')).toBeInTheDocument()
    expect(within(summary as HTMLElement).getByText('Decisions lost: 5')).toBeInTheDocument()
    expect(within(summary as HTMLElement).getByText('Rationale lost: 6')).toBeInTheDocument()
    expect(within(summary as HTMLElement).getByText('Modified: 22')).toBeInTheDocument()
    expect(within(summary as HTMLElement).getByText('Preserved: 24')).toBeInTheDocument()
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
    expect(within(compression as HTMLElement).getByText('Questions resolved: 17')).toBeInTheDocument()
    expect(within(compression as HTMLElement).getByText('Risks retired: 18')).toBeInTheDocument()
    expect(within(compression as HTMLElement).getByText('Warnings: 14')).toBeInTheDocument()
    expect(within(compression as HTMLElement).getByText('Compression warning')).toBeInTheDocument()
    expect(within(compression as HTMLElement).getByText('Noise removed')).toBeInTheDocument()
  })

  it('renders understanding evolution from projected diagnostic trends', () => {
    render(<ContinuityDiagnosticsPanel diagnostics={createDiagnostics()} />)

    const table = screen.getByRole('table', { name: 'Understanding evolution' })

    expect(within(table).getByRole('columnheader', { name: 'Section' })).toBeInTheDocument()
    expect(within(table).getByRole('columnheader', { name: 'Modified' })).toBeInTheDocument()
    expect(within(table).getByRole('row', { name: 'Architecture 0 2 0 0 1' })).toBeInTheDocument()
    expect(within(table).getByRole('row', { name: 'Stable decisions 3 19 4 0 5' })).toBeInTheDocument()
    expect(within(table).getByRole('row', { name: 'Open questions 15 20 0 7 8' })).toBeInTheDocument()
    expect(within(table).getByRole('row', { name: 'Active risks 16 0 0 9 10' })).toBeInTheDocument()
  })

  it('renders backend-owned operational evolution counts and modification evidence', () => {
    render(
      <ContinuityDiagnosticsPanel
        diagnostics={createDiagnostics({
          operationalEvolution: {
            addedCount: 1,
            modifiedCount: 2,
            removedCount: 3,
            resolvedCount: 4,
            lostCount: 5,
            preservedCount: 6,
            semanticChanges: [
              {
                type: 'ItemChanged',
                section: 'Architecture',
                description: 'Updated the service ownership statement.',
                itemId: 'architecture-1',
                previousState: 'UI owns service state.',
                currentState: 'Backend owns service state.',
                modificationReason: 'Matched authoritative ownership.',
                identityBasis: 'kind+source',
                supportingEvidence: ['.agents/operational_context.md#architecture'],
              },
            ],
            diagnosticGroups: [],
          },
        })}
      />,
    )

    const evolution = screen.getByRole('heading', { name: 'Operational Evolution' }).closest('div')

    expect(evolution).not.toBeNull()
    expect(within(evolution as HTMLElement).getByText('Added: 1')).toBeInTheDocument()
    expect(within(evolution as HTMLElement).getByText('Modified: 2')).toBeInTheDocument()
    expect(within(evolution as HTMLElement).getByText('Removed: 3')).toBeInTheDocument()
    expect(within(evolution as HTMLElement).getByText('Resolved: 4')).toBeInTheDocument()
    expect(within(evolution as HTMLElement).getByText('Lost: 5')).toBeInTheDocument()
    expect(within(evolution as HTMLElement).getByText('Preserved: 6')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Operational Evolution Changes' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Modified' })).toBeInTheDocument()
    expect(screen.getByText('ItemChanged: Updated the service ownership statement.')).toBeInTheDocument()
    expect(screen.getByText('Identity basis')).toBeInTheDocument()
    expect(screen.getByText('kind+source')).toBeInTheDocument()
    expect(screen.getByText('Previous')).toBeInTheDocument()
    expect(screen.getByText('UI owns service state.')).toBeInTheDocument()
    expect(screen.getByText('Current')).toBeInTheDocument()
    expect(screen.getByText('Backend owns service state.')).toBeInTheDocument()
    expect(screen.getByText('Reason')).toBeInTheDocument()
    expect(screen.getByText('Matched authoritative ownership.')).toBeInTheDocument()
    expect(screen.getByText('.agents/operational_context.md#architecture')).toBeInTheDocument()
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

  it('renders question, risk, and report visibility without deriving scores', () => {
    render(<ContinuityDiagnosticsPanel diagnostics={createDiagnostics()} reports={[createReport()]} />)

    const lifecycle = screen.getByRole('heading', { name: 'Question and Risk Lifecycle' }).closest('div')
    const reports = screen.getByRole('heading', { name: 'Reports' }).closest('div')

    expect(lifecycle).not.toBeNull()
    expect(reports).not.toBeNull()
    expect(within(lifecycle as HTMLElement).getByText('Questions added: 15')).toBeInTheDocument()
    expect(within(lifecycle as HTMLElement).getByText('Questions resolved: 7')).toBeInTheDocument()
    expect(within(lifecycle as HTMLElement).getByText('Risks retired: 9')).toBeInTheDocument()
    expect(within(reports as HTMLElement).getByText('Latest report: continuity.20260102')).toBeInTheDocument()
    expect(within(reports as HTMLElement).getByText('Report history: 1')).toBeInTheDocument()
    expect(within(reports as HTMLElement).getByText('Diagnostics revisions: 4')).toBeInTheDocument()
    expect(screen.queryByText(/score/i)).not.toBeInTheDocument()
  })

  it('uses navigation-only callbacks for evolution rows and report paths', () => {
    const onOpenOperationalContextSection = vi.fn()
    const onOpenReport = vi.fn()

    render(
      <ContinuityDiagnosticsPanel
        diagnostics={createDiagnostics()}
        reports={[createReport()]}
        onOpenOperationalContextSection={onOpenOperationalContextSection}
        onOpenReport={onOpenReport}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Open questions' }))
    fireEvent.click(screen.getByRole('button', { name: '.agents/continuity/continuity.20260102.json' }))

    expect(onOpenOperationalContextSection).toHaveBeenCalledWith('operational-open-questions')
    expect(onOpenReport).toHaveBeenCalledWith('.agents/continuity/continuity.20260102.json')
  })
})
