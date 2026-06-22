import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { DecisionOptionComparison } from '../../features/decisions/DecisionOptionComparison'
import type { DecisionOptionComparison as DecisionOptionComparisonModel } from '../../types'

afterEach(() => {
  cleanup()
})

describe('DecisionOptionComparison', () => {
  it('renders backend option rows with tradeoffs and recommendation state', () => {
    render(<DecisionOptionComparison comparison={createComparison()} isLoading={false} />)

    const rows = screen.getByLabelText('Option comparison rows')
    expect(within(rows).getByText('Render a read-only backend workspace')).toBeInTheDocument()
    expect(within(rows).getByText('Delay full inspection')).toBeInTheDocument()
    expect(within(rows).getByText('Reviewers can inspect the proposal before mutation.')).toBeInTheDocument()
    expect(within(rows).getByText('Evidence and review state remain hidden.')).toBeInTheDocument()
    expect(within(rows).getByText('Recommended')).toBeInTheDocument()
  })

  it('keeps comparison evidence adjacent to the option row', () => {
    render(<DecisionOptionComparison comparison={createComparison()} isLoading={false} />)

    const recommendedOption = screen.getByText('Render a read-only backend workspace').closest('article')
    expect(recommendedOption).not.toBeNull()
    expect(
      within(recommendedOption as HTMLElement).getByText('M4 exit criteria require full proposal inspection.'),
    ).toBeInTheDocument()
  })

  it('does not expose lifecycle mutation controls', () => {
    render(<DecisionOptionComparison comparison={createComparison()} isLoading={false} />)

    expect(screen.queryByRole('button', { name: /resolve/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /refine/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /discard/i })).not.toBeInTheDocument()
  })
})

function createComparison(): DecisionOptionComparisonModel {
  return {
    proposalId: 'PROP-0001',
    recommendedOptionId: 'OPT-A',
    options: [
      {
        optionId: 'OPT-A',
        title: 'Render a read-only backend workspace',
        description: 'Display the option comparison read model without recomputing tradeoffs.',
        isRecommended: true,
        benefits: ['Reviewers can inspect the proposal before mutation.'],
        costs: ['The UI needs a larger read-only surface.'],
        evidence: [
          {
            summary: 'M4 exit criteria require full proposal inspection.',
            sources: [],
          },
        ],
      },
      {
        optionId: 'OPT-B',
        title: 'Delay full inspection',
        description: 'Keep only the proposal browser until mutation controls are ready.',
        isRecommended: false,
        benefits: ['Smaller UI change.'],
        costs: ['Evidence and review state remain hidden.'],
        evidence: [],
      },
    ],
  }
}
