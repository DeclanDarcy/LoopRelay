import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ReasoningTrajectoryTab } from '../../features/reasoning/ReasoningTrajectoryTab'
import type {
  ManualReasoningCaptureTemplate,
  ReasoningEvent,
  ReasoningGraph,
  ReasoningMaterializationReviewReport,
  ReasoningQueryResult,
  ReasoningReconstruction,
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

const reconstruction: ReasoningReconstruction = {
  repositoryId: 'repo-alpha',
  generatedAt: '2026-06-22T16:08:00.0000000Z',
  query: {
    category: 'Decision',
    question: 'Why did this decision change?',
    target: {
      kind: 'ReasoningEvent',
      id: 'EVT-0001',
      relativePath: null,
      section: null,
      excerpt: null,
    },
    direction: 'Backward',
  },
  narrative: {
    summary: 'The decision question is reconstructed from one event and one relationship.',
    details:
      'Question: Why did this decision change?\nTarget: ReasoningEvent EVT-0001\nTrace direction: Backward\nScale diagnostics: 2 evidence item(s), 1 event(s), 1 relationship edge(s), 0 external reference(s), 1 thread(s).\nEvidence summary: 1 event(s), 1 relationship edge(s), 0 external reference(s), 1 thread(s).\nEvents:\n- Event EVT-0001: HypothesisRaised: Event substrate can stay narrow - Reasoning should begin as immutable events with provenance.\nRelationships:\n- GraphRelationship ThreadMembership:EVT-0001:THR-0001: BelongsTo - Event belongs to thread\nExternal References:\n- None\nThreads:\n- Thread THR-0001: Milestone 1 ontology boundary - Tracks why the event substrate remains explanatory.',
  },
  confidence: 'High',
  trace: backwardTrace,
  evidence: [
    {
      kind: 'Event',
      id: 'EVT-0001',
      title: 'HypothesisRaised: Event substrate can stay narrow',
      summary: 'Reasoning should begin as immutable events with provenance.',
      reference: {
        kind: 'ReasoningEvent',
        id: 'EVT-0001',
        relativePath: null,
        section: null,
        excerpt: null,
      },
      provenance: events[0].provenance,
    },
    {
      kind: 'GraphRelationship',
      id: 'ThreadMembership:EVT-0001:THR-0001',
      title: 'BelongsTo',
      summary: 'Event belongs to thread',
      reference: null,
      provenance: null,
    },
  ],
  diagnostics: [],
}

const queryResult: ReasoningQueryResult = {
  repositoryId: 'repo-alpha',
  generatedAt: reconstruction.generatedAt,
  query: reconstruction.query,
  reconstruction,
  diagnostics: [],
}

const materializationReview: ReasoningMaterializationReviewReport = {
  repositoryId: 'repo-alpha',
  generatedAt: '2026-06-22T16:09:00.0000000Z',
  concepts: [
    {
      concept: 'Hypothesis',
      recommendation: 'RemainDerived',
      summary: 'Hypothesis remains reconstructable from reasoning events and trace evidence.',
      evidence: ['1 hypothesis event', '0 failed reconstruction scenarios'],
      risks: ['Promoting hypotheses would imply lifecycle authority.'],
    },
    {
      concept: 'Direction',
      recommendation: 'RemainDerived',
      summary: 'Direction remains derived because direction events alone do not justify stronger persistence.',
      evidence: ['0 direction events'],
      risks: ['Direction persistence could imply strategic authority.'],
    },
    {
      concept: 'Thread',
      recommendation: 'RemainDerived',
      summary: 'Thread identity remains a grouping aid and not an authoritative artifact family.',
      evidence: ['1 thread'],
      risks: ['Thread persistence must stay subject to future materialization review.'],
    },
  ],
  taxonomyFindings: [
    {
      family: 'Hypothesis',
      eventTypeCount: 1,
      lifecycleRisk: false,
      summary: 'Hypothesis remains classification vocabulary.',
      evidence: ['1 event types observed'],
    },
  ],
  diagnostics: [],
}

function renderTab(overrides: Partial<Parameters<typeof ReasoningTrajectoryTab>[0]> = {}) {
  const props = {
    events,
    threads,
    relationships,
    graph,
    backwardTrace: null,
    forwardTrace: null,
    queryResult: null,
    reconstruction: null,
    materializationReview,
    templates,
    hasSelectedRepository: true,
    isLoading: false,
    isTracingGraph: false,
    isQuerying: false,
    isReconstructing: false,
    isLoadingMaterializationReview: false,
    isRunningMaterializationReview: false,
    error: null,
    queryError: null,
    reconstructionError: null,
    materializationReviewError: null,
    onRefresh: vi.fn(),
    onTraceGraphNode: vi.fn(),
    onRunQuery: vi.fn().mockResolvedValue(undefined),
    onRunMaterializationReview: vi.fn(),
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

  it('runs derived queries and exposes reconstruction evidence', async () => {
    const onRunQuery = vi.fn().mockResolvedValue(undefined)
    renderTab({ queryResult, reconstruction, onRunQuery })

    const queryRegion = screen.getByRole('region', { name: 'Reasoning query' })
    expect(within(queryRegion).getByLabelText('Reasoning query authority')).toHaveTextContent(
      'Derived query',
    )
    expect(within(queryRegion).getByLabelText('Reasoning query result')).toHaveTextContent(
      '2 evidence items',
    )

    const reconstructionRegion = screen.getByRole('region', { name: 'Reasoning reconstruction' })
    expect(
      within(reconstructionRegion).getByLabelText('Reasoning reconstruction authority'),
    ).toHaveTextContent('Non-authoritative')
    expect(within(reconstructionRegion).getByLabelText('Reconstruction evidence')).toHaveTextContent(
      'HypothesisRaised: Event substrate can stay narrow',
    )
    expect(within(reconstructionRegion).getByLabelText('Project narrative reconstruction')).toHaveTextContent(
      'Project reconstruction uses 1 event evidence item(s)',
    )
    fireEvent.change(within(reconstructionRegion).getByLabelText('Horizon'), {
      target: { value: 'Multi-year' },
    })
    expect(within(reconstructionRegion).getByLabelText('Project narrative reconstruction')).toHaveTextContent(
      'Multi-year reconstruction uses 1 event evidence item(s)',
    )
    expect(reconstructionRegion).toHaveTextContent('Scale diagnostics')
    const groupedDetails = within(reconstructionRegion).getByLabelText('Grouped reconstruction details')
    expect(within(groupedDetails).getByText('Events')).toBeInTheDocument()
    expect(within(groupedDetails).getByText('Relationships')).toBeInTheDocument()
    expect(within(groupedDetails).getByText('External References')).toBeInTheDocument()
    expect(within(groupedDetails).getByText('Threads')).toBeInTheDocument()
    expect(groupedDetails).toHaveTextContent('Event substrate can stay narrow')
    expect(groupedDetails).toHaveTextContent('Milestone 1 ontology boundary')

    fireEvent.change(within(queryRegion).getByLabelText('Question'), {
      target: { value: 'Why did the event substrate remain narrow?' },
    })
    fireEvent.click(within(queryRegion).getByRole('button', { name: 'Run Query' }))

    await waitFor(() => expect(onRunQuery).toHaveBeenCalledTimes(1))
    expect(onRunQuery).toHaveBeenCalledWith({
      category: 'Decision',
      question: 'Why did the event substrate remain narrow?',
      target: {
        kind: 'ReasoningEvent',
        id: 'EVT-0001',
        relativePath: null,
        section: null,
        excerpt: null,
      },
      direction: 'Backward',
    })
  })

  it('shows materialization review findings as advisory architecture review', () => {
    const onRunMaterializationReview = vi.fn()
    renderTab({ onRunMaterializationReview })

    const reviewRegion = screen.getByRole('region', { name: 'Reasoning materialization review' })
    expect(within(reviewRegion).getByLabelText('Reasoning materialization authority')).toHaveTextContent(
      'Architecture review',
    )
    expect(within(reviewRegion).getByText('Hypothesis')).toBeInTheDocument()
    expect(within(reviewRegion).getAllByText('Derived remains sufficient')).toHaveLength(3)
    expect(within(reviewRegion).getByLabelText('Materialization taxonomy findings')).toHaveTextContent(
      'Hypothesis remains classification vocabulary.',
    )
    expect(within(reviewRegion).queryByText('Approved')).toBeNull()
    expect(within(reviewRegion).queryByText('Rejected')).toBeNull()

    fireEvent.click(within(reviewRegion).getByRole('button', { name: 'Run Review' }))
    expect(onRunMaterializationReview).toHaveBeenCalledTimes(1)
  })
})
