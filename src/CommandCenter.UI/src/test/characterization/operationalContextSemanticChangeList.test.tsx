import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { OperationalContextSemanticChangeList } from '../../features/operational-context/OperationalContextSemanticChangeList'
import type { OperationalContextSemanticChange } from '../../types'

afterEach(() => {
  cleanup()
})

function renderList(semanticChanges: OperationalContextSemanticChange[]) {
  render(<OperationalContextSemanticChangeList semanticChanges={semanticChanges} />)
}

describe('operational context semantic change list rendering characterization', () => {
  it('preserves the heading and empty fallback', () => {
    renderList([])

    expect(screen.getByRole('heading', { name: 'Semantic Changes' })).toBeInTheDocument()
    expect(screen.getByText('No coarse semantic changes detected.')).toBeInTheDocument()
  })

  it('renders backend-provided semantic changes in order with existing labels', () => {
    renderList([
      {
        type: 'StableUnderstandingPreserved',
        section: 'Stable Decisions',
        description: 'Preserved the deployment ownership decision.',
        itemId: 'decision-1',
      },
      {
        type: 'OpenQuestionResolved',
        section: 'Open Questions',
        description: 'Resolved the packaging question.',
        itemId: null,
      },
      {
        type: 'RiskRetired',
        section: 'Active Risks',
        description: 'Retired the flaky certification risk.',
        itemId: 'risk-1',
      },
    ])

    expect(screen.getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'StableUnderstandingPreserved: Preserved the deployment ownership decision.',
      'OpenQuestionResolved: Resolved the packaging question.',
      'RiskRetired: Retired the flaky certification risk.',
    ])
  })
})
