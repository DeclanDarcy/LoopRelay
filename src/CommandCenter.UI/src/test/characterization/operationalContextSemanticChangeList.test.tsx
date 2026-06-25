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
        previousState: null,
        currentState: null,
        modificationReason: null,
        identityBasis: null,
        supportingEvidence: [],
      },
      {
        type: 'OpenQuestionResolved',
        section: 'Open Questions',
        description: 'Resolved the packaging question.',
        itemId: null,
        previousState: null,
        currentState: null,
        modificationReason: null,
        identityBasis: null,
        supportingEvidence: [],
      },
      {
        type: 'RiskRetired',
        section: 'Active Risks',
        description: 'Retired the flaky certification risk.',
        itemId: 'risk-1',
        previousState: null,
        currentState: null,
        modificationReason: null,
        identityBasis: null,
        supportingEvidence: [],
      },
    ])

    expect(screen.getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'StableUnderstandingPreserved: Preserved the deployment ownership decision.',
      'OpenQuestionResolved: Resolved the packaging question.',
      'RiskRetired: Retired the flaky certification risk.',
    ])
  })

  it('renders identity-aware modification facts without interpreting them', () => {
    renderList([
      {
        type: 'ModifiedConstraint',
        section: 'Constraints',
        description: 'Updated the deployment constraint.',
        itemId: 'constraint-1',
        previousState: 'Deployments are manual.',
        currentState: 'Deployments are automated after review.',
        modificationReason: 'Current context records the automation boundary.',
        identityBasis: 'normalized-kind-and-source',
        supportingEvidence: ['.agents/operational_context.md#constraints'],
      },
    ])

    expect(screen.getByRole('heading', { name: 'Constraints' })).toBeInTheDocument()
    expect(screen.getByText('ModifiedConstraint: Updated the deployment constraint.')).toBeInTheDocument()
    expect(screen.getByText('Identity basis')).toBeInTheDocument()
    expect(screen.getByText('normalized-kind-and-source')).toBeInTheDocument()
    expect(screen.getByText('Previous')).toBeInTheDocument()
    expect(screen.getByText('Deployments are manual.')).toBeInTheDocument()
    expect(screen.getByText('Current')).toBeInTheDocument()
    expect(screen.getByText('Deployments are automated after review.')).toBeInTheDocument()
    expect(screen.getByText('Reason')).toBeInTheDocument()
    expect(screen.getByText('Current context records the automation boundary.')).toBeInTheDocument()
    expect(screen.getByText('.agents/operational_context.md#constraints')).toBeInTheDocument()
  })
})
