import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { OperationalContextProposalComparison } from '../../features/operational-context/OperationalContextProposalComparison'

afterEach(() => {
  cleanup()
})

function renderComparison(currentContent: string, proposedContent: string) {
  render(
    <OperationalContextProposalComparison
      currentContent={currentContent}
      proposedContent={proposedContent}
    />,
  )
}

describe('operational context proposal comparison rendering characterization', () => {
  it('preserves comparison headings and empty fallbacks', () => {
    renderComparison('', '   ')

    expect(screen.getByRole('heading', { name: 'Current Understanding' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Review Candidate' })).toBeInTheDocument()
    expect(screen.getByText('No current operational context.')).toBeInTheDocument()
    expect(screen.getByText('Empty proposal.')).toBeInTheDocument()
  })

  it('renders current and proposed markdown with existing markdown behavior', () => {
    renderComparison(
      [
        '# Current Context',
        '',
        '- Preserve backend authority',
        '- Keep draft state local',
      ].join('\n'),
      [
        '## Proposed Context',
        '',
        'Updated summary.',
        '',
        '```',
        'literal block',
        '```',
      ].join('\n'),
    )

    expect(screen.getByRole('heading', { name: 'Current Context' })).toBeInTheDocument()
    expect(screen.getByText('Preserve backend authority')).toBeInTheDocument()
    expect(screen.getByText('Keep draft state local')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Proposed Context' })).toBeInTheDocument()
    expect(screen.getByText('Updated summary.')).toBeInTheDocument()
    expect(screen.getByText('literal block')).toBeInTheDocument()
  })
})
