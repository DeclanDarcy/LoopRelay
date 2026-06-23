import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ReasoningTrajectoryTab } from '../../features/reasoning/ReasoningTrajectoryTab'
import type {
  ManualReasoningCaptureTemplate,
  ReasoningEvent,
  ReasoningGraph,
  ReasoningRelationship,
  ReasoningTrace,
  ReasoningThread,
} from '../../types'

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

const graph: ReasoningGraph = {
  repositoryId: 'repo-alpha',
  generatedAt: '2026-06-22T16:07:00.0000000Z',
  nodes: [
    {
      id: 'ReasoningEvent:EVT-0001',
      kind: 'ReasoningEvent',
      referenceId: 'EVT-0001',
      label: 'Event substrate can stay narrow',
      resolved: true,
      reference: {
        kind: 'ReasoningEvent',
        id: 'EVT-0001',
        relativePath: null,
        section: null,
        excerpt: 'Reasoning should begin as immutable events with provenance.',
      },
    },
    {
      id: 'ReasoningThread:THR-0001',
      kind: 'ReasoningThread',
      referenceId: 'THR-0001',
      label: 'Milestone 1 ontology boundary',
      resolved: true,
      reference: {
        kind: 'ReasoningThread',
        id: 'THR-0001',
        relativePath: null,
        section: 'DecisionEvolution',
        excerpt: 'Tracks why the event substrate remains explanatory.',
      },
    },
  ],
  relationships: [
    {
      id: 'ThreadMembership:EVT-0001:THR-0001',
      type: 'BelongsTo',
      sourceNodeId: 'ReasoningEvent:EVT-0001',
      targetNodeId: 'ReasoningThread:THR-0001',
      label: 'Event belongs to thread',
      provenance: 'ReasoningEvent.ThreadIds',
      relationshipId: null,
    },
  ],
  diagnostics: ['Artifact reference could not be resolved: .agents/missing.md'],
}

const backwardTrace: ReasoningTrace = {
  repositoryId: 'repo-alpha',
  direction: 'Backward',
  target: {
    kind: 'ReasoningThread',
    id: 'THR-0001',
    relativePath: null,
    section: null,
    excerpt: null,
  },
  nodes: graph.nodes,
  relationships: graph.relationships,
  diagnostics: [],
}

const templates: ManualReasoningCaptureTemplate[] = [
  {
    kind: 'ContradictionResolved',
    family: 'Contradiction',
    type: 'ContradictionResolved',
    suggestedThreadTheme: 'Conflict',
    provenanceSourceKind: 'UserSupplied',
    suggestedReferenceKinds: ['Artifact'],
  },
  {
    kind: 'AlternativeRejected',
    family: 'Alternative',
    type: 'AlternativeRejected',
    suggestedThreadTheme: 'PathConsidered',
    provenanceSourceKind: 'UserSupplied',
    suggestedReferenceKinds: ['Artifact'],
  },
]

function renderTab(overrides: Partial<Parameters<typeof ReasoningTrajectoryTab>[0]> = {}) {
  const props = {
    events,
    threads,
    relationships,
    graph,
    backwardTrace: null,
    forwardTrace: null,
    templates,
    hasSelectedRepository: true,
    isLoading: false,
    isTracingGraph: false,
    error: null,
    onRefresh: vi.fn(),
    onTraceGraphNode: vi.fn(),
    onCaptureManualReasoning: vi.fn().mockResolvedValue(undefined),
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

    const threadPanel = screen.getByRole('region', { name: 'Reasoning threads' })
    fireEvent.click(within(threadPanel).getByRole('button', { name: /Milestone 1 ontology boundary/ }))

    const feed = screen.getByRole('region', { name: 'Reasoning event feed' })
    expect(within(feed).getByText('Event substrate can stay narrow')).toBeInTheDocument()
    expect(within(feed).queryByText('Specialized entity storage deferred')).toBeNull()
  })

  it('filters event families without implying materialized derived entities', () => {
    renderTab()

    fireEvent.click(screen.getByRole('button', { name: 'Alternative Events' }))

    const feed = screen.getByRole('region', { name: 'Reasoning event feed' })
    expect(within(feed).getByText('Specialized entity storage deferred')).toBeInTheDocument()
    expect(within(feed).queryByText('Event substrate can stay narrow')).toBeNull()
    expect(screen.queryByRole('button', { name: 'Alternatives' })).toBeNull()
  })

  it('shows graph navigation without creating graph authority', () => {
    const onTraceGraphNode = vi.fn()
    renderTab({ backwardTrace, onTraceGraphNode })

    const graphRegion = screen.getByRole('region', { name: 'Reasoning graph' })
    expect(within(graphRegion).getByLabelText('Reasoning graph authority')).toHaveTextContent(
      'Derived graph',
    )
    expect(within(graphRegion).getByLabelText('Reasoning graph nodes')).toHaveTextContent(
      'Event substrate can stay narrow',
    )
    expect(within(graphRegion).getByText('Artifact reference could not be resolved: .agents/missing.md')).toBeInTheDocument()

    fireEvent.click(within(graphRegion).getByRole('button', { name: 'Trace Node' }))
    expect(onTraceGraphNode).toHaveBeenCalledWith(graph.nodes[0])
    expect(within(graphRegion).getByLabelText('Backward Trace')).toHaveTextContent(
      'ReasoningEvent:EVT-0001',
    )
  })

  it('submits manual capture from backend-approved templates', async () => {
    const onCaptureManualReasoning = vi.fn().mockResolvedValue(undefined)
    renderTab({ onCaptureManualReasoning })

    fireEvent.change(screen.getByLabelText('Capture template'), {
      target: { value: 'AlternativeRejected' },
    })
    fireEvent.change(screen.getByLabelText('Title'), {
      target: { value: 'Alternative rejected after comparison' },
    })
    fireEvent.change(screen.getByLabelText('Summary'), {
      target: { value: 'The simpler event substrate covered the needed explanation.' },
    })
    fireEvent.change(screen.getByLabelText('Details'), {
      target: { value: 'No first-class alternative entity is required yet.' },
    })
    fireEvent.change(screen.getByLabelText('Source path'), {
      target: { value: '.agents/decisions/decisions.md' },
    })
    fireEvent.change(screen.getByLabelText('Source section'), {
      target: { value: 'Newly Authorized' },
    })
    fireEvent.change(screen.getByLabelText('Excerpt'), {
      target: { value: 'Keep derived concepts as event classifications.' },
    })
    fireEvent.change(screen.getByLabelText('Tags'), {
      target: { value: 'milestone-2, manual' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Record Event' }))

    await waitFor(() => expect(onCaptureManualReasoning).toHaveBeenCalledTimes(1))
    expect(onCaptureManualReasoning).toHaveBeenCalledWith({
      kind: 'AlternativeRejected',
      title: 'Alternative rejected after comparison',
      narrative: {
        summary: 'The simpler event substrate covered the needed explanation.',
        details: 'No first-class alternative entity is required yet.',
      },
      references: [],
      provenance: {
        sourceKind: 'UserSupplied',
        capturedBy: 'user',
        relativePath: '.agents/decisions/decisions.md',
        section: 'Newly Authorized',
        excerpt: 'Keep derived concepts as event classifications.',
        fingerprint: null,
      },
      threadIds: [],
      tags: ['milestone-2', 'manual'],
    })
  })
})
