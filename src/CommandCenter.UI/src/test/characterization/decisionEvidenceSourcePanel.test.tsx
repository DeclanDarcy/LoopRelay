import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { DecisionEvidenceSourcePanel } from '../../features/decisions/DecisionEvidenceSourcePanel'
import type { DecisionEvidenceInspection, DecisionSourceAttribution } from '../../types'

afterEach(() => {
  cleanup()
})

describe('DecisionEvidenceSourcePanel', () => {
  it('renders evidence rows and selected source attribution', () => {
    render(
      <DecisionEvidenceSourcePanel
        inspection={createInspection()}
        attributions={createAttributions()}
        isLoading={false}
      />,
    )

    expect(
      within(screen.getByLabelText('Evidence inspection rows')).getByText(
        'Proposal-level evidence remains visible.',
      ),
    ).toBeInTheDocument()
    const selectedSource = screen.getByLabelText('Selected evidence source')
    expect(within(selectedSource).getByText(/\.agents\/plan\.md/)).toBeInTheDocument()
    expect(within(selectedSource).getByText(/Milestone 4/)).toBeInTheDocument()
    expect(within(selectedSource).getByText(/Provide full proposal inspection\./)).toBeInTheDocument()
  })

  it('selects another evidence item using local presentation state', () => {
    render(
      <DecisionEvidenceSourcePanel
        inspection={createInspection()}
        attributions={createAttributions()}
        isLoading={false}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: /Human authority should see evidence/ }))

    const selectedSource = screen.getByLabelText('Selected evidence source')
    expect(within(selectedSource).getByText('Recommendation')).toBeInTheDocument()
    expect(within(selectedSource).getByText('OPT-A')).toBeInTheDocument()
    expect(within(selectedSource).getByText(/\.agents\/decisions\/proposals\/PROP-0001\/proposal\.json/)).toBeInTheDocument()
  })

  it('does not expose evidence mutation controls', () => {
    render(
      <DecisionEvidenceSourcePanel
        inspection={createInspection()}
        attributions={createAttributions()}
        isLoading={false}
      />,
    )

    expect(screen.queryByRole('button', { name: /edit/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /delete/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /accept/i })).not.toBeInTheDocument()
  })
})

function createInspection(): DecisionEvidenceInspection {
  const planSource = createAttribution(
    'Proposal',
    'PROP-0001',
    'Plan',
    '.agents/plan.md',
    'Milestone 4',
    'Provide full proposal inspection.',
  )
  const proposalSource = createAttribution(
    'Recommendation',
    'OPT-A',
    'DecisionProposal',
    '.agents/decisions/proposals/PROP-0001/proposal.json',
    null,
    'Recommendation comes from backend proposal read model.',
  )

  return {
    proposalId: 'PROP-0001',
    candidateId: 'CAND-0001',
    items: [
      {
        appliesToKind: 'Proposal',
        itemId: 'PROP-0001',
        summary: 'Proposal-level evidence remains visible.',
        sources: [planSource],
      },
      {
        appliesToKind: 'Recommendation',
        itemId: 'OPT-A',
        summary: 'Human authority should see evidence before mutation.',
        sources: [proposalSource],
      },
    ],
    diagnostics: {
      hasRecommendation: true,
      hasEvidence: true,
      optionCount: 2,
      tradeoffCount: 2,
      assumptionCount: 1,
      noteCount: 0,
      warnings: [],
    },
  }
}

function createAttributions(): DecisionSourceAttribution[] {
  return createInspection().items.flatMap((item) => item.sources)
}

function createAttribution(
  appliesToKind: string,
  itemId: string | null,
  sourceKind: string,
  relativePath: string,
  section: string | null,
  excerpt: string,
): DecisionSourceAttribution {
  return {
    appliesToKind,
    itemId,
    sourceKind,
    relativePath,
    section,
    excerpt,
    source: {
      sourceKind,
      relativePath,
      section,
      itemId: null,
      decisionId: null,
      proposalId: 'PROP-0001',
      candidateId: 'CAND-0001',
      excerpt,
    },
  }
}
