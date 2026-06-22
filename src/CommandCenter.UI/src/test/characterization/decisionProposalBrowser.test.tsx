import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DecisionProposalBrowser } from '../../features/decisions/DecisionProposalBrowser'
import type { DecisionProposalBrowserItem, DecisionProposalState } from '../../types'

afterEach(() => {
  cleanup()
})

describe('DecisionProposalBrowser', () => {
  it('renders proposal browser rows from backend browser items', () => {
    render(
      <DecisionProposalBrowser
        proposals={createProposals()}
        selectedStates={[]}
        isLoading={false}
        onSelectedStatesChange={vi.fn()}
      />,
    )

    const rows = screen.getByRole('list', { name: 'Proposal browser rows' })
    expect(within(rows).getByText('Use backend-owned review read models')).toBeInTheDocument()
    expect(within(rows).getByText('Keep proposal selection in React state')).toBeInTheDocument()
    expect(screen.getByText('PROP-0001')).toBeInTheDocument()
  })

  it('requests backend-driven state filters', () => {
    const onSelectedStatesChange = vi.fn()

    render(
      <DecisionProposalBrowser
        proposals={createProposals()}
        selectedStates={['Viewed']}
        isLoading={false}
        onSelectedStatesChange={onSelectedStatesChange}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Ready for resolution' }))
    expect(onSelectedStatesChange).toHaveBeenCalledWith(['Viewed', 'ReadyForResolution'])

    fireEvent.click(screen.getByRole('button', { name: 'All' }))
    expect(onSelectedStatesChange).toHaveBeenCalledWith([])
  })

  it('keeps selected proposal as local presentation state', () => {
    render(
      <DecisionProposalBrowser
        proposals={createProposals()}
        selectedStates={[]}
        isLoading={false}
        onSelectedStatesChange={vi.fn()}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: /Keep proposal selection in React state/ }))

    const selectedPanel = screen.getByLabelText('Selected proposal')
    expect(within(selectedPanel).getByText('PROP-0002')).toBeInTheDocument()
    expect(within(selectedPanel).getAllByText('Viewed')).toHaveLength(2)
  })
})

function createProposals(): DecisionProposalBrowserItem[] {
  return [
    createProposal('PROP-0001', 'Generated', 'Use backend-owned review read models'),
    createProposal('PROP-0002', 'Viewed', 'Keep proposal selection in React state'),
    createProposal('PROP-0003', 'ReadyForResolution', 'Defer mutation controls'),
  ]
}

function createProposal(
  proposalId: string,
  state: DecisionProposalState,
  title: string,
): DecisionProposalBrowserItem {
  return {
    proposalId,
    candidateId: 'CAND-0001',
    state,
    title,
    classification: 'Architectural',
    priority: 'High',
    createdAt: '2026-06-22T17:00:00.000Z',
    updatedAt: '2026-06-22T17:01:00.000Z',
    reviewState: state === 'Viewed' ? 'Viewed' : 'NotStarted',
    reviewUpdatedAt: '2026-06-22T17:01:00.000Z',
    isResolved: state === 'Resolved',
  }
}
