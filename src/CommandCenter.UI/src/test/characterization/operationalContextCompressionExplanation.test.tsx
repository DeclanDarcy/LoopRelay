import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { OperationalContextCompressionExplanation } from '../../features/operational-context/OperationalContextCompressionExplanation'
import type { OperationalContextCompressionOutcome } from '../../types'

afterEach(() => {
  cleanup()
})

const outcomes: OperationalContextCompressionOutcome[] = [
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
    outcome: 'ResolvedQuestion',
    itemKind: 'OpenQuestion',
    itemText: 'Should diagnostics include growth trends?',
    rule: 'explicit-question-resolution',
    threshold: 'Open questions are removed only when recent understanding contains explicit resolution evidence.',
    rationale: 'Open question was explicitly resolved by proposal evidence.',
    evidence: ['Resolved question: Should diagnostics include growth trends?'],
  },
  {
    outcome: 'RetiredRisk',
    itemKind: 'ActiveRisk',
    itemText: 'Context growth can hide important constraints.',
    rule: 'explicit-risk-retirement',
    threshold: 'Active risks are removed only when recent understanding contains explicit retirement evidence.',
    rationale: 'Active risk was explicitly retired by proposal evidence.',
    evidence: ['Retired risk: Context growth can hide important constraints.'],
  },
]

describe('operational context compression explanation rendering characterization', () => {
  it('renders backend-authored item outcomes without synthetic classifications', () => {
    render(<OperationalContextCompressionExplanation itemOutcomes={outcomes} />)

    const explanation = screen.getByRole('heading', { name: 'Compression Explanation' }).closest('div')

    expect(explanation).not.toBeNull()
    expect(within(explanation as HTMLElement).getByText('Retained')).toBeInTheDocument()
    expect(within(explanation as HTMLElement).getByText('Constraint')).toBeInTheDocument()
    expect(within(explanation as HTMLElement).getByText('Backend continuity services own compression.')).toBeInTheDocument()
    expect(within(explanation as HTMLElement).getByText('normalized-text-retention')).toBeInTheDocument()
    expect(
      within(explanation as HTMLElement).getByText(
        'Normalized item text is compared across current and compressed proposed operational context.',
      ),
    ).toBeInTheDocument()
    expect(within(explanation as HTMLElement).getByText('Item remains present after compression.')).toBeInTheDocument()
    expect(
      within(explanation as HTMLElement).getByText('Normalized text: backend continuity services own compression.'),
    ).toBeInTheDocument()
    expect(within(explanation as HTMLElement).getByText('ResolvedQuestion')).toBeInTheDocument()
    expect(within(explanation as HTMLElement).getByText('explicit-question-resolution')).toBeInTheDocument()
    expect(within(explanation as HTMLElement).getByText('RetiredRisk')).toBeInTheDocument()
    expect(within(explanation as HTMLElement).getByText('explicit-risk-retirement')).toBeInTheDocument()
    expect(within(explanation as HTMLElement).queryByText('Critical')).not.toBeInTheDocument()
    expect(within(explanation as HTMLElement).queryByText('High')).not.toBeInTheDocument()
  })

  it('does not render when there are no backend outcomes', () => {
    render(<OperationalContextCompressionExplanation itemOutcomes={[]} />)

    expect(screen.queryByRole('heading', { name: 'Compression Explanation' })).not.toBeInTheDocument()
  })
})
