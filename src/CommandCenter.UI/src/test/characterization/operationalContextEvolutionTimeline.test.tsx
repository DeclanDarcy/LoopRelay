import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { OperationalContextEvolutionTimeline } from '../../features/operational-context/OperationalContextEvolutionTimeline'
import type { OperationalContextSemanticChange } from '../../types'

afterEach(() => {
  cleanup()
})

function renderTimeline(semanticChanges: OperationalContextSemanticChange[]) {
  render(<OperationalContextEvolutionTimeline semanticChanges={semanticChanges} />)
}

describe('operational context evolution timeline rendering characterization', () => {
  it('renders the timeline heading and empty fallback', () => {
    renderTimeline([])

    expect(screen.getByRole('heading', { name: 'Operational Evolution Timeline' })).toBeInTheDocument()
    expect(screen.getByText('No operational evolution events detected.')).toBeInTheDocument()
  })

  it('groups backend semantic events into lifecycle lanes without parsing proposal markdown', () => {
    renderTimeline([
      createChange({
        type: 'ConstraintAdded',
        section: 'Constraints',
        description: 'Added the release gate constraint.',
      }),
      createChange({
        type: 'ModifiedConstraint',
        section: 'Constraints',
        description: 'Updated the review threshold.',
      }),
      createChange({
        type: 'RiskRetired',
        section: 'Active Risks',
        description: 'Resolved the flaky build risk.',
      }),
    ])

    expect(screen.getByRole('heading', { name: 'Added' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Modified' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Resolved' })).toBeInTheDocument()
    expect(screen.getByText('Added the release gate constraint.')).toBeInTheDocument()
    expect(screen.getByText('Updated the review threshold.')).toBeInTheDocument()
    expect(screen.getByText('Resolved the flaky build risk.')).toBeInTheDocument()
  })

  it('renders backend-provided state, reason, identity basis, and evidence for modifications', () => {
    renderTimeline([
      createChange({
        type: 'ModifiedArchitecture',
        section: 'Architecture',
        description: 'Refined the sidecar ownership boundary.',
        itemId: 'architecture-1',
        previousState: 'React owns sidecar launch sequencing.',
        currentState: 'Shell owns sidecar launch sequencing.',
        modificationReason: 'Backend projection records shell lifecycle ownership.',
        identityBasis: 'kind-source-lineage',
        supportingEvidence: ['.agents/operational_context.md#architecture'],
      }),
    ])

    const item = screen
      .getByText('Refined the sidecar ownership boundary.')
      .closest('.operational-evolution-item')
    if (!(item instanceof HTMLElement)) {
      throw new Error('Expected operational evolution item to render.')
    }
    expect(within(item).getByText('ModifiedArchitecture')).toBeInTheDocument()
    expect(within(item).getByText('Architecture')).toBeInTheDocument()
    expect(within(item).getByText('Previous state')).toBeInTheDocument()
    expect(within(item).getByText('React owns sidecar launch sequencing.')).toBeInTheDocument()
    expect(within(item).getByText('Current state')).toBeInTheDocument()
    expect(within(item).getByText('Shell owns sidecar launch sequencing.')).toBeInTheDocument()
    expect(within(item).getByText('Reason')).toBeInTheDocument()
    expect(within(item).getByText('Backend projection records shell lifecycle ownership.')).toBeInTheDocument()
    expect(within(item).getByText('Identity basis')).toBeInTheDocument()
    expect(within(item).getByText('kind-source-lineage')).toBeInTheDocument()
    expect(within(item).getByText('.agents/operational_context.md#architecture')).toBeInTheDocument()
  })
})

function createChange(
  overrides: Partial<OperationalContextSemanticChange> = {},
): OperationalContextSemanticChange {
  return {
    type: 'ItemAdded',
    section: 'Current Mental Model',
    description: 'Added an operational context item.',
    itemId: null,
    previousState: null,
    currentState: null,
    modificationReason: null,
    identityBasis: null,
    supportingEvidence: [],
    ...overrides,
  }
}
