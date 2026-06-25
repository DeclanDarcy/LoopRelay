import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { OperationalContextProposalComparison } from '../../features/operational-context/OperationalContextProposalComparison'
import type { OperationalContextSemanticChange } from '../../types'

afterEach(() => {
  cleanup()
})

function renderComparison(
  currentContent: string,
  proposedContent: string,
  semanticChanges: OperationalContextSemanticChange[] = [],
) {
  render(
    <OperationalContextProposalComparison
      currentContent={currentContent}
      proposedContent={proposedContent}
      semanticChanges={semanticChanges}
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

  it('renders backend-provided modification facts before the markdown comparison', () => {
    renderComparison(
      '## Constraints\n\n- Deployments are manual.',
      '## Constraints\n\n- Deployments are automated after review.',
      [
        {
          type: 'ModifiedConstraint',
          section: 'Constraints',
          description: 'Updated deployment ownership.',
          itemId: 'constraint-1',
          previousState: 'Deployments are manual.',
          currentState: 'Deployments are automated after review.',
          modificationReason: 'Identity-aware diff matched the deployment constraint.',
          identityBasis: 'normalized-kind-and-source',
          supportingEvidence: ['.agents/operational_context.md#constraints'],
        },
      ],
    )

    expect(screen.getByRole('heading', { name: 'Modification Review' })).toBeInTheDocument()
    expect(screen.getByText('ModifiedConstraint: Updated deployment ownership.')).toBeInTheDocument()
    expect(screen.getByText('Previous')).toBeInTheDocument()
    expect(screen.getAllByText('Deployments are manual.').length).toBeGreaterThan(0)
    expect(screen.getByText('Current')).toBeInTheDocument()
    expect(screen.getAllByText('Deployments are automated after review.').length).toBeGreaterThan(0)
    expect(screen.getByText('Reason')).toBeInTheDocument()
    expect(screen.getByText('Identity-aware diff matched the deployment constraint.')).toBeInTheDocument()
    expect(screen.getByText('Identity basis')).toBeInTheDocument()
    expect(screen.getByText('normalized-kind-and-source')).toBeInTheDocument()
    expect(screen.getByText('.agents/operational_context.md#constraints')).toBeInTheDocument()
  })
})
