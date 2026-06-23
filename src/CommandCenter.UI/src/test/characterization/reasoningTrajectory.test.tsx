import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ReasoningTrajectoryTab } from '../../features/reasoning/ReasoningTrajectoryTab'
import type { ReasoningEvent, ReasoningRelationship, ReasoningThread } from '../../types'

const events: ReasoningEvent[] = [
  {
    id: 'EVT-0001',
    repositoryId: 'repo-alpha',
    createdAt: '2026-06-22T16:00:00.0000000Z',
    family: 'Hypothesis',
    type: 'HypothesisRaised',
    title: 'Event substrate can stay narrow',
    narrative: {
      summary: 'Reasoning should begin as immutable events with provenance.',
      details: 'Specialized concepts remain reconstructed from traces.',
    },
    references: [],
    provenance: {
      sourceKind: 'ManualCapture',
      capturedBy: 'codex',
      relativePath: '.agents/plan.md',
      section: 'Milestone 1',
      excerpt: 'Preserve events first.',
      fingerprint: 'fingerprint-1',
    },
    threadIds: ['THR-0001'],
    tags: ['milestone-1'],
  },
  {
    id: 'EVT-0002',
    repositoryId: 'repo-alpha',
    createdAt: '2026-06-22T16:05:00.0000000Z',
    family: 'Alternative',
    type: 'AlternativeRejected',
    title: 'Specialized entity storage deferred',
    narrative: {
      summary: 'Specialized storage stays behind the materialization gate.',
      details: '',
    },
    references: [],
    provenance: {
      sourceKind: 'ManualCapture',
      capturedBy: 'codex',
      relativePath: '.agents/decisions/decisions.md',
      section: 'Newly Authorized',
      excerpt: 'Do not add specialized endpoints.',
      fingerprint: 'fingerprint-2',
    },
    threadIds: [],
    tags: ['derived-only'],
  },
]

const threads: ReasoningThread[] = [
  {
    id: 'THR-0001',
    repositoryId: 'repo-alpha',
    title: 'Milestone 1 ontology boundary',
    theme: 'DecisionEvolution',
    createdAt: '2026-06-22T16:00:00.0000000Z',
    updatedAt: '2026-06-22T16:05:00.0000000Z',
    summary: 'Tracks why the event substrate remains explanatory.',
    eventIds: ['EVT-0001'],
    tags: ['milestone-1'],
  },
]

const relationships: ReasoningRelationship[] = [
  {
    id: 'REL-0001',
    repositoryId: 'repo-alpha',
    createdAt: '2026-06-22T16:06:00.0000000Z',
    type: 'Supports',
    source: {
      kind: 'ReasoningEvent',
      id: 'EVT-0001',
      relativePath: null,
      section: null,
      excerpt: null,
    },
    target: {
      kind: 'ReasoningEvent',
      id: 'EVT-0002',
      relativePath: null,
      section: null,
      excerpt: null,
    },
    narrative: {
      summary: 'A narrow substrate supports deferring specialized storage.',
      details: '',
    },
    provenance: {
      sourceKind: 'ManualCapture',
      capturedBy: 'codex',
      relativePath: '.agents/handoffs/handoff.md',
      section: 'Current Gaps',
      excerpt: 'Derived status remains display-only.',
      fingerprint: 'fingerprint-3',
    },
  },
]

function renderTab(overrides: Partial<Parameters<typeof ReasoningTrajectoryTab>[0]> = {}) {
  const props = {
    events,
    threads,
    relationships,
    hasSelectedRepository: true,
    isLoading: false,
    error: null,
    onRefresh: vi.fn(),
    ...overrides,
  }

  render(<ReasoningTrajectoryTab {...props} />)

  return props
}

describe('reasoning trajectory tab', () => {
  afterEach(() => cleanup())

  it('shows event feed entries with provenance', () => {
    renderTab()

    expect(screen.getByRole('heading', { name: 'Trajectory' })).toBeInTheDocument()
    const feed = screen.getByRole('region', { name: 'Reasoning event feed' })
    expect(within(feed).getByText('Event substrate can stay narrow')).toBeInTheDocument()
    expect(within(feed).getAllByText('ManualCapture by codex')).toHaveLength(2)
    expect(within(feed).getByText('.agents/plan.md')).toBeInTheDocument()
    expect(screen.getByLabelText('Derived reasoning status')).toHaveTextContent(
      'Derived display only',
    )
  })

  it('shows empty states for repositories with no reasoning records', () => {
    renderTab({
      events: [],
      threads: [],
      relationships: [],
    })

    expect(screen.getByText('No reasoning events recorded.')).toBeInTheDocument()
    expect(screen.getByText('No reasoning threads recorded.')).toBeInTheDocument()
    expect(screen.getByText('No reasoning relationships recorded.')).toBeInTheDocument()
  })

  it('filters the event feed when a thread is selected', () => {
    renderTab()

    fireEvent.click(screen.getByRole('button', { name: /Milestone 1 ontology boundary/ }))

    const feed = screen.getByRole('region', { name: 'Reasoning event feed' })
    expect(within(feed).getByText('Event substrate can stay narrow')).toBeInTheDocument()
    expect(within(feed).queryByText('Specialized entity storage deferred')).toBeNull()
  })
})
