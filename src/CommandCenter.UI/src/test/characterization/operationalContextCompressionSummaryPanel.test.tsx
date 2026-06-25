import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { OperationalContextCompressionSummaryPanel } from '../../features/operational-context/OperationalContextCompressionSummaryPanel'
import type { OperationalContextCompressionSummary } from '../../types'

afterEach(() => {
  cleanup()
})

function createCompressionSummary(
  overrides: Partial<OperationalContextCompressionSummary> = {},
): OperationalContextCompressionSummary {
  return {
    preservedItemCount: 10,
    addedItemCount: 2,
    modifiedItemCount: 3,
    removedItemCount: 4,
    compressedItemCount: 5,
    permanentUnderstandingItemCount: 6,
    activeUnderstandingItemCount: 7,
    historicalUnderstandingItemCount: 8,
    historicalNoiseItemCount: 9,
    resolvedQuestionCount: 1,
    retiredRiskCount: 2,
    warningCount: 3,
    warnings: ['General warning'],
    revisionSummary: ['Revision one', 'Revision two'],
    noiseRemovedIndicators: ['Noise one', 'Noise two'],
    stableUnderstandingRetentionWarnings: ['Retention one', 'Retention two'],
    itemOutcomes: [
      {
        outcome: 'Retained',
        itemKind: 'Constraint',
        itemText: 'Backend continuity services own compression.',
        rule: 'normalized-text-retention',
        threshold: 'Normalized item text is compared across current and compressed proposed operational context.',
        rationale: 'Item remains present after compression.',
        evidence: ['Normalized text: backend continuity services own compression.'],
      },
      {
        outcome: 'TransientRemoved',
        itemKind: 'RecentChange',
        itemText: 'Recent execution for milestone is recorded with state Completed.',
        rule: 'transient-execution-noise-removal',
        threshold: 'Transient execution detail is removed after 6 retained recent-change item(s).',
        rationale: 'Transient execution status is historical noise after enough recent context is retained.',
        evidence: ['Retained recent-change count before removal: 6'],
      },
    ],
    ...overrides,
  }
}

function renderPanel(compressionSummary = createCompressionSummary()) {
  render(<OperationalContextCompressionSummaryPanel compressionSummary={compressionSummary} />)
}

describe('operational context compression summary panel rendering characterization', () => {
  it('renders the existing count labels', () => {
    renderPanel()

    expect(screen.getByRole('heading', { name: 'Compression Summary' })).toBeInTheDocument()
    expect(screen.getByText('Preserved: 10')).toBeInTheDocument()
    expect(screen.getByText('Added: 2')).toBeInTheDocument()
    expect(screen.getByText('Modified: 3')).toBeInTheDocument()
    expect(screen.getByText('Removed: 4')).toBeInTheDocument()
    expect(screen.getByText('Compressed: 5')).toBeInTheDocument()
    expect(screen.getByText('Permanent: 6')).toBeInTheDocument()
    expect(screen.getByText('Active: 7')).toBeInTheDocument()
    expect(screen.getByText('Historical: 8')).toBeInTheDocument()
    expect(screen.getByText('Noise: 9')).toBeInTheDocument()
    expect(screen.getByText('Resolved: 1')).toBeInTheDocument()
    expect(screen.getByText('Retired: 2')).toBeInTheDocument()
    expect(screen.getByText('Warnings: 3')).toBeInTheDocument()
  })

  it('preserves optional detail headings, ordering, and item text', () => {
    renderPanel()

    const headings = screen.getAllByRole('heading', { level: 5 }).map((heading) => heading.textContent)

    expect(headings).toEqual([
      'Compression Summary',
      'Compression Warnings',
      'Revision Summary',
      'Retention Warnings',
      'Compressed Understanding',
    ])

    const compressionWarnings = screen.getByRole('heading', { name: 'Compression Warnings' }).closest('div')
    const revisionSummary = screen.getByRole('heading', { name: 'Revision Summary' }).closest('div')
    const retentionWarnings = screen.getByRole('heading', { name: 'Retention Warnings' }).closest('div')
    const compressedUnderstanding = screen.getByRole('heading', { name: 'Compressed Understanding' }).closest('div')

    expect(compressionWarnings).not.toBeNull()
    expect(revisionSummary).not.toBeNull()
    expect(retentionWarnings).not.toBeNull()
    expect(compressedUnderstanding).not.toBeNull()
    expect(within(compressionWarnings as HTMLElement).getAllByRole('listitem').map((li) => li.textContent)).toEqual([
      'General warning',
    ])
    expect(within(revisionSummary as HTMLElement).getAllByRole('listitem').map((li) => li.textContent)).toEqual([
      'Revision one',
      'Revision two',
    ])
    expect(within(retentionWarnings as HTMLElement).getAllByRole('listitem').map((li) => li.textContent)).toEqual([
      'Retention one',
      'Retention two',
    ])
    expect(within(compressedUnderstanding as HTMLElement).getAllByRole('listitem').map((li) => li.textContent)).toEqual([
      'Noise one',
      'Noise two',
    ])
  })

  it('omits optional detail sections when backend lists are empty', () => {
    renderPanel(
      createCompressionSummary({
        revisionSummary: [],
        warnings: [],
        stableUnderstandingRetentionWarnings: [],
        noiseRemovedIndicators: [],
        itemOutcomes: [],
      }),
    )

    expect(screen.queryByRole('heading', { name: 'Compression Warnings' })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Revision Summary' })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Retention Warnings' })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Compressed Understanding' })).not.toBeInTheDocument()
  })
})
