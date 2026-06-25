import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ReasoningTrajectoryTab } from '../../features/reasoning/ReasoningTrajectoryTab'
import type {
  ManualReasoningCaptureTemplate,
  ReasoningCertificationReport,
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
    captureProvenance: {
      mode: 'Manual',
      sourceKind: 'ManualCapture',
      capturedBy: 'codex',
      captureReason: 'Preserve events first.',
      sourceTransition: null,
      sourceArtifact: '.agents/plan.md',
      sourceTimestamp: null,
      skipReason: null,
      duplicateSignal: 'Fingerprint fingerprint-1',
      existingEventReference: null,
      diagnosticGroups: [
        {
          category: 'capture',
          title: 'Manual capture',
          diagnostics: [
            'Capture mode: Manual.',
            'Source kind: ManualCapture.',
            'Captured by: codex.',
            'Capture reason: Preserve events first.',
            'Source artifact: .agents/plan.md.',
            'Duplicate signal: Fingerprint fingerprint-1.',
          ],
        },
      ],
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
      sourceKind: 'InferredOperationalContextPromotion',
      capturedBy: 'operational-context-lifecycle-service',
      relativePath: '.agents/decisions/decisions.md',
      section: 'Newly Authorized',
      excerpt: 'Do not add specialized endpoints.',
      fingerprint: 'fingerprint-2',
    },
    captureProvenance: {
      mode: 'Inferred',
      sourceKind: 'InferredOperationalContextPromotion',
      capturedBy: 'operational-context-lifecycle-service',
      captureReason: 'Do not add specialized endpoints.',
      sourceTransition: 'OperationalContextPromotionReasoningObserved',
      sourceArtifact: '.agents/decisions/decisions.md',
      sourceTimestamp: '2026-06-22T16:05:00.0000000Z',
      skipReason: null,
      duplicateSignal: 'Fingerprint fingerprint-2',
      existingEventReference: null,
      diagnosticGroups: [
        {
          category: 'capture',
          title: 'Inferred capture',
          diagnostics: [
            'Capture mode: Inferred.',
            'Source kind: InferredOperationalContextPromotion.',
            'Captured by: operational-context-lifecycle-service.',
            'Capture reason: Do not add specialized endpoints.',
            'Source transition: OperationalContextPromotionReasoningObserved.',
          ],
        },
      ],
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
  diagnosticGroups: [
    {
      category: 'validation',
      title: 'Graph validation',
      diagnostics: ['Artifact reference could not be resolved: .agents/missing.md'],
    },
  ],
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
  diagnosticGroups: [],
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
  confidenceRationale: {
    level: 'High',
    rationale: 'Event evidence and relationship evidence were both reachable, and the trace reported no diagnostics.',
    eventEvidencePresent: true,
    relationshipEvidencePresent: true,
    traceDiagnosticsPresent: false,
    missingEvidence: [],
    whyNotHigher: [],
  },
  scope: {
    direction: 'Backward',
    target: {
      kind: 'ReasoningEvent',
      id: 'EVT-0001',
      relativePath: null,
      section: null,
      excerpt: null,
    },
    source: {
      kind: 'ReasoningThread',
      id: 'THR-0001',
      relativePath: null,
      section: null,
      excerpt: null,
    },
    historicalCutoff: null,
    reachableEvidence: [
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
    ],
    unreachableEvidence: [],
  },
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
  diagnosticGroups: [
    {
      category: 'evidence',
      title: 'Reconstruction evidence',
      diagnostics: ['1 event evidence item(s) were reachable.', '1 relationship evidence item(s) were reachable.'],
    },
    {
      category: 'confidence',
      title: 'Confidence rationale',
      diagnostics: ['Event evidence and relationship evidence were both reachable, and the trace reported no diagnostics.'],
    },
    {
      category: 'reconstruction',
      title: 'Reconstruction scope',
      diagnostics: ['Trace direction: Backward.', 'Current graph was used; no historical cutoff was applied.'],
    },
  ],
}

const limitedReconstruction: ReasoningReconstruction = {
  ...reconstruction,
  query: {
    ...reconstruction.query,
    direction: 'Forward',
    historicalAt: '2026-06-22T16:03:00.0000000Z',
  },
  confidence: 'Limited',
  confidenceRationale: {
    level: 'Limited',
    rationale: 'Only event evidence was reachable and trace diagnostics were present.',
    eventEvidencePresent: true,
    relationshipEvidencePresent: false,
    traceDiagnosticsPresent: true,
    missingEvidence: ['No relationship evidence was reachable for this query.'],
    whyNotHigher: ['Trace diagnostics were present.', 'Historical cutoff excluded later evidence.'],
  },
  scope: {
    ...reconstruction.scope,
    direction: 'Forward',
    source: null,
    historicalCutoff: '2026-06-22T16:03:00.0000000Z',
    unreachableEvidence: [
      {
        kind: 'Event',
        id: 'EVT-0002',
        title: 'AlternativeRejected: Specialized entity storage deferred',
        summary: 'Specialized storage stays behind the materialization gate.',
        reference: {
          kind: 'ReasoningEvent',
          id: 'EVT-0002',
          relativePath: null,
          section: null,
          excerpt: null,
        },
        provenance: events[1].provenance,
      },
    ],
  },
  trace: {
    ...backwardTrace,
    direction: 'Forward',
    diagnostics: ['Historical cutoff excluded one future event.'],
    diagnosticGroups: [
      {
        category: 'validation',
        title: 'Trace validation',
        diagnostics: ['Historical cutoff excluded one future event.'],
      },
    ],
  },
  diagnostics: ['Historical cutoff excluded one future event.'],
  diagnosticGroups: [
    {
      category: 'evidence',
      title: 'Reconstruction evidence',
      diagnostics: ['No relationship evidence was reachable for this query.'],
    },
    {
      category: 'confidence',
      title: 'Confidence rationale',
      diagnostics: ['Trace diagnostics were present.', 'Historical cutoff excluded later evidence.'],
    },
    {
      category: 'reconstruction',
      title: 'Reconstruction scope',
      diagnostics: ['Historical cutoff excluded one future event.'],
    },
  ],
}

const queryResult: ReasoningQueryResult = {
  repositoryId: 'repo-alpha',
  generatedAt: reconstruction.generatedAt,
  query: reconstruction.query,
  reconstruction,
  diagnostics: [],
  diagnosticGroups: reconstruction.diagnosticGroups,
}

const limitedQueryResult: ReasoningQueryResult = {
  ...queryResult,
  query: limitedReconstruction.query,
  reconstruction: limitedReconstruction,
  diagnostics: limitedReconstruction.diagnostics,
}

const materializationReview: ReasoningMaterializationReviewReport = {
  repositoryId: 'repo-alpha',
  generatedAt: '2026-06-22T16:09:00.0000000Z',
  concepts: [
    {
      concept: 'Hypothesis',
      recommendation: 'RemainDerived',
      summary: 'Hypothesis remains reconstructable from reasoning events and trace evidence.',
      failedScenarioCount: 0,
      repeatedWorkflowCount: 0,
      failedScenarioThreshold: 2,
      repeatedWorkflowThreshold: 3,
      branchReason: 'No threshold was met: 0/2 failed scenarios and 0/3 repeated workflow signals.',
      elevatedRiskSignals: [],
      evidence: ['1 hypothesis event', '0 failed reconstruction scenarios'],
      risks: ['Promoting hypotheses would imply lifecycle authority.'],
    },
    {
      concept: 'Direction',
      recommendation: 'RemainDerived',
      summary: 'Direction remains derived because direction events alone do not justify stronger persistence.',
      failedScenarioCount: 0,
      repeatedWorkflowCount: 0,
      failedScenarioThreshold: 2,
      repeatedWorkflowThreshold: 3,
      branchReason: 'No threshold was met: 0/2 failed scenarios and 0/3 repeated workflow signals.',
      elevatedRiskSignals: ['Direction materialization can imply strategic authority.'],
      evidence: ['0 direction events'],
      risks: ['Direction persistence could imply strategic authority.'],
    },
    {
      concept: 'Thread',
      recommendation: 'RemainDerived',
      summary: 'Thread identity remains a grouping aid and not an authoritative artifact family.',
      failedScenarioCount: 0,
      repeatedWorkflowCount: 0,
      failedScenarioThreshold: 2,
      repeatedWorkflowThreshold: 3,
      branchReason: 'No threshold was met: 0/2 failed scenarios and 0/3 repeated workflow signals.',
      elevatedRiskSignals: [],
      evidence: ['1 thread'],
      risks: ['Thread persistence must stay subject to future materialization review.'],
    },
  ],
  taxonomyFindings: [
    {
      family: 'Hypothesis',
      eventTypeCount: 1,
      eventTypeThreshold: 4,
      lifecycleRisk: false,
      terminalEventTypePresent: false,
      terminalEventTypes: [],
      riskReason: 'Lifecycle risk is not flagged because the family has 1 event type(s) against threshold 4 and terminal event presence is false.',
      summary: 'Hypothesis remains classification vocabulary.',
      evidence: ['1 event types observed'],
    },
  ],
  diagnostics: [],
  diagnosticGroups: [
    {
      category: 'materialization',
      title: 'Materialization review',
      diagnostics: ['Hypothesis branch: no materialization threshold was met.'],
    },
    {
      category: 'authority boundary',
      title: 'Authority boundary',
      diagnostics: ['Direction materialization would cross strategic authority boundaries.'],
    },
    {
      category: 'lifecycle risk',
      title: 'Taxonomy lifecycle risk',
      diagnostics: ['Hypothesis lifecycle risk remains below threshold 4.'],
    },
  ],
}

const certificationReport: ReasoningCertificationReport = {
  id: 'certification.current',
  repositoryId: 'repo-alpha',
  generatedAt: '2026-06-22T16:10:00.0000000Z',
  result: {
    kind: 'Passed',
    summary: 'Reasoning remains reconstructable from repository artifacts.',
  },
  evidence: [
    {
      id: 'CERT-000',
      scenario: 'Reasoning baseline',
      passed: true,
      summary: 'Reasoning records can answer at least one outcome-oriented scenario.',
      details: ['Alternative rejection and thread reconstruction are answerable.'],
      references: [
        {
          kind: 'ReasoningEvent',
          id: 'EVT-0002',
          relativePath: null,
          section: 'AlternativeRejected',
          excerpt: 'Specialized storage stays behind the materialization gate.',
        },
      ],
    },
    {
      id: 'CERT-040',
      scenario: 'Thread reconstruction',
      passed: true,
      summary: 'At least one reasoning thread can be reconstructed from event membership.',
      details: ['1 thread is available for navigation.'],
      references: [
        {
          kind: 'ReasoningThread',
          id: 'THR-0001',
          relativePath: null,
          section: 'DecisionEvolution',
          excerpt: 'Tracks why the event substrate remains explanatory.',
        },
      ],
    },
  ],
  diagnostics: [],
}

const failedCertificationReport: ReasoningCertificationReport = {
  ...certificationReport,
  id: 'certification.failed',
  result: {
    kind: 'Failed',
    summary: '1 certification evidence item(s) failed.',
  },
  evidence: [
    ...certificationReport.evidence,
    {
      id: 'CERT-010',
      scenario: 'Provenance completeness',
      passed: false,
      summary: 'One or more reasoning events lack provenance.',
      details: ['EVT-9999 is missing provenance.'],
      references: [
        {
          kind: 'ReasoningEvent',
          id: 'EVT-9999',
          relativePath: null,
          section: 'EvidenceAdded',
          excerpt: 'Broken event',
        },
      ],
    },
  ],
  diagnostics: ['Unresolved external reference: .agents/missing.md'],
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
    certificationReport,
    certificationReports: [certificationReport],
    templates,
    hasSelectedRepository: true,
    isLoading: false,
    isTracingGraph: false,
    isQuerying: false,
    isReconstructing: false,
    isLoadingMaterializationReview: false,
    isRunningMaterializationReview: false,
    isLoadingCertification: false,
    isRunningCertification: false,
    error: null,
    queryError: null,
    reconstructionError: null,
    materializationReviewError: null,
    certificationError: null,
    boundaryViolations: [],
    onRefresh: vi.fn(),
    onTraceGraphNode: vi.fn(),
    onRunQuery: vi.fn().mockResolvedValue(undefined),
    onRunMaterializationReview: vi.fn(),
    onRunCertification: vi.fn(),
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
    expect(within(feed).getByText('ManualCapture by codex')).toBeInTheDocument()
    expect(within(feed).getByText('InferredOperationalContextPromotion by operational-context-lifecycle-service')).toBeInTheDocument()
    expect(within(feed).getByText('OperationalContextPromotionReasoningObserved')).toBeInTheDocument()
    expect(within(feed).getByText('.agents/plan.md')).toBeInTheDocument()
    expect(within(feed).getByText('Fingerprint fingerprint-1')).toBeInTheDocument()
    expect(within(feed).getByLabelText('EVT-0001 capture diagnostics')).toHaveTextContent(
      'Manual capture',
    )
    expect(within(feed).getByLabelText('EVT-0001 capture diagnostics')).toHaveTextContent(
      'Capture reason: Preserve events first.',
    )
    expect(within(feed).getByLabelText('EVT-0002 capture diagnostics')).toHaveTextContent(
      'Inferred capture',
    )
    expect(within(feed).getByLabelText('EVT-0002 capture diagnostics')).toHaveTextContent(
      'Source transition: OperationalContextPromotionReasoningObserved.',
    )
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

  it('renders structured authority-boundary notices verbatim', () => {
    renderTab({
      error: 'Reasoning event EVT-9999 was not found.',
      boundaryViolations: [
        {
          boundaryRule: 'Reasoning relationships may only target existing reasoning-owned artifacts.',
          owningDomain: 'ReasoningEvent',
          rejectedAssertion: 'ReasoningEvent:EVT-9999',
          allowedAlternative: 'Create or recover the reasoning event before linking it.',
          diagnosticDetail: 'ReasoningEvent authority could not resolve EVT-9999.',
          severity: 'Blocking',
        },
      ],
    })

    const notices = screen.getByRole('region', { name: 'Authority boundary notices' })
    const notice = within(notices).getByRole('article', { name: 'Authority boundary notice' })

    expect(within(notice).getByText('Boundary rule')).toBeInTheDocument()
    expect(within(notice).getByText('Reasoning relationships may only target existing reasoning-owned artifacts.')).toBeInTheDocument()
    expect(within(notice).getByText('Owning domain')).toBeInTheDocument()
    expect(within(notice).getByText('ReasoningEvent')).toBeInTheDocument()
    expect(within(notice).getByText('Rejected assertion')).toBeInTheDocument()
    expect(within(notice).getByText('ReasoningEvent:EVT-9999')).toBeInTheDocument()
    expect(within(notice).getByText('Allowed alternative')).toBeInTheDocument()
    expect(within(notice).getByText('Create or recover the reasoning event before linking it.')).toBeInTheDocument()
    expect(within(notice).getByText('Diagnostic detail')).toBeInTheDocument()
    expect(within(notice).getByText('ReasoningEvent authority could not resolve EVT-9999.')).toBeInTheDocument()
    expect(within(notice).getByText('Severity')).toBeInTheDocument()
    expect(within(notice).getByText('Blocking')).toBeInTheDocument()
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
    expect(within(graphRegion).getByLabelText('Grouped graph diagnostics')).toHaveTextContent('Graph validation')
    expect(within(graphRegion).getByLabelText('Grouped graph diagnostics')).toHaveTextContent('validation')

    fireEvent.click(within(graphRegion).getByRole('button', { name: 'Trace Node' }))
    expect(onTraceGraphNode).toHaveBeenCalledWith(graph.nodes[0])
    expect(within(graphRegion).getByLabelText('Backward Trace')).toHaveTextContent(
      'ReasoningEvent:EVT-0001',
    )
    expect(within(graphRegion).getByLabelText('Backward Trace nodes')).toHaveTextContent(
      'Event substrate can stay narrow',
    )
    expect(within(graphRegion).getByLabelText('Backward Trace nodes')).toHaveTextContent(
      'Resolved',
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
    expect(within(queryRegion).getByLabelText('Executed reasoning query')).toHaveTextContent(
      'Why did this decision change?',
    )
    expect(within(queryRegion).getByLabelText('Executed reasoning query')).toHaveTextContent(
      'Decision',
    )
    expect(within(queryRegion).getByLabelText('Executed reasoning query')).toHaveTextContent(
      'Backward',
    )
    expect(within(queryRegion).getByLabelText('Executed reasoning query')).toHaveTextContent(
      'ReasoningEvent EVT-0001',
    )
    expect(within(queryRegion).getByLabelText('Executed reasoning query')).toHaveTextContent(
      'Current graph',
    )
    expect(within(queryRegion).getByLabelText('Reasoning query transparency')).toHaveTextContent(
      'Event evidence and relationship evidence were both reachable',
    )
    expect(within(queryRegion).getByLabelText('Reasoning query transparency')).toHaveTextContent(
      'Backward from ReasoningThread THR-0001 to ReasoningEvent EVT-0001',
    )

    const reconstructionRegion = screen.getByRole('region', { name: 'Reasoning reconstruction' })
    expect(
      within(reconstructionRegion).getByLabelText('Reasoning reconstruction authority'),
    ).toHaveTextContent('Non-authoritative')
    expect(within(reconstructionRegion).getByLabelText('Reconstruction evidence')).toHaveTextContent(
      'HypothesisRaised: Event substrate can stay narrow',
    )
    expect(
      within(reconstructionRegion).getByLabelText('Reconstruction confidence rationale'),
    ).toHaveTextContent('Event evidence and relationship evidence were both reachable')
    expect(
      within(reconstructionRegion).getByLabelText('Reconstruction confidence rationale'),
    ).toHaveTextContent('No missing evidence reported.')
    expect(within(reconstructionRegion).getByLabelText('Reconstruction scope')).toHaveTextContent(
      'Backward',
    )
    expect(
      within(reconstructionRegion).getByLabelText('Reachable reconstruction evidence'),
    ).toHaveTextContent('HypothesisRaised: Event substrate can stay narrow')
    expect(
      within(reconstructionRegion).getByLabelText('Known unreachable reconstruction evidence'),
    ).toHaveTextContent('No known unreachable evidence reported.')
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
    expect(within(reconstructionRegion).getByLabelText('Grouped reconstruction diagnostics')).toHaveTextContent(
      'Reconstruction evidence',
    )
    expect(within(reconstructionRegion).getByLabelText('Grouped reconstruction diagnostics')).toHaveTextContent(
      'Confidence rationale',
    )
    expect(within(reconstructionRegion).getByLabelText('Grouped reconstruction diagnostics')).toHaveTextContent(
      'Trace direction: Backward.',
    )

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

  it('surfaces limited reconstruction rationale, blockers, historical scope, and unreachable evidence', () => {
    renderTab({ queryResult: limitedQueryResult, reconstruction: limitedReconstruction })

    const queryRegion = screen.getByRole('region', { name: 'Reasoning query' })
    expect(within(queryRegion).getByLabelText('Reasoning query transparency')).toHaveTextContent(
      'Only event evidence was reachable and trace diagnostics were present.',
    )
    expect(within(queryRegion).getByLabelText('Reasoning query transparency')).toHaveTextContent(
      'No relationship evidence was reachable for this query.',
    )
    expect(within(queryRegion).getByLabelText('Reasoning query transparency')).toHaveTextContent(
      'Historical cutoff excluded later evidence.',
    )
    expect(within(queryRegion).getByLabelText('Reasoning query transparency')).toHaveTextContent(
      'Forward from unreported source to ReasoningEvent EVT-0001',
    )
    expect(within(queryRegion).getByLabelText('Reasoning query transparency')).toHaveTextContent(
      '2026-06-22T16:03:00.0000000Z',
    )
    expect(within(queryRegion).getByLabelText('Executed reasoning query')).toHaveTextContent(
      'Forward',
    )
    expect(within(queryRegion).getByLabelText('Executed reasoning query')).toHaveTextContent(
      '2026-06-22T16:03:00.0000000Z',
    )
    expect(within(queryRegion).getByLabelText('Reasoning query transparency')).toHaveTextContent(
      '1 known item(s)',
    )

    const reconstructionRegion = screen.getByRole('region', { name: 'Reasoning reconstruction' })
    expect(
      within(reconstructionRegion).getByLabelText('Reconstruction confidence rationale'),
    ).toHaveTextContent('Relationship evidence')
    expect(
      within(reconstructionRegion).getByLabelText('Reconstruction confidence rationale'),
    ).toHaveTextContent('Not present')
    expect(
      within(reconstructionRegion).getByLabelText('Reconstruction confidence rationale'),
    ).toHaveTextContent('Trace diagnostics')
    expect(
      within(reconstructionRegion).getByLabelText('Reconstruction confidence rationale'),
    ).toHaveTextContent('Present')
    expect(
      within(reconstructionRegion).getByLabelText('Reconstruction confidence rationale'),
    ).toHaveTextContent('No relationship evidence was reachable for this query.')
    expect(
      within(reconstructionRegion).getByLabelText('Reconstruction confidence rationale'),
    ).toHaveTextContent('Trace diagnostics were present.')
    expect(within(reconstructionRegion).getByLabelText('Reconstruction scope')).toHaveTextContent(
      'Forward',
    )
    expect(within(reconstructionRegion).getByLabelText('Reconstruction scope')).toHaveTextContent(
      'Not reported',
    )
    expect(within(reconstructionRegion).getByLabelText('Reconstruction scope')).toHaveTextContent(
      '2026-06-22T16:03:00.0000000Z',
    )
    expect(
      within(reconstructionRegion).getByLabelText('Known unreachable reconstruction evidence'),
    ).toHaveTextContent('AlternativeRejected: Specialized entity storage deferred')
    expect(reconstructionRegion).toHaveTextContent('Historical cutoff excluded one future event.')
    expect(within(reconstructionRegion).getByLabelText('Grouped reconstruction diagnostics')).toHaveTextContent(
      'Reconstruction scope',
    )
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
    expect(within(reviewRegion).getByLabelText('Hypothesis materialization threshold basis')).toHaveTextContent(
      'RemainDerived',
    )
    expect(within(reviewRegion).getByLabelText('Hypothesis materialization threshold basis')).toHaveTextContent(
      'No threshold was met',
    )
    expect(within(reviewRegion).getByLabelText('Direction materialization threshold basis')).toHaveTextContent(
      'Failed scenarios 0/2',
    )
    expect(within(reviewRegion).getByLabelText('Direction materialization threshold basis')).toHaveTextContent(
      'Repeated workflow 0/3',
    )
    expect(within(reviewRegion).getByText('Direction materialization can imply strategic authority.')).toBeInTheDocument()
    expect(within(reviewRegion).getByLabelText('Materialization taxonomy findings')).toHaveTextContent(
      'Hypothesis remains classification vocabulary.',
    )
    expect(within(reviewRegion).getByLabelText('Materialization taxonomy findings')).toHaveTextContent(
      '1/4 event types',
    )
    expect(within(reviewRegion).getByLabelText('Materialization taxonomy findings')).toHaveTextContent(
      'terminal event types absent',
    )
    expect(within(reviewRegion).getByLabelText('Materialization taxonomy findings')).toHaveTextContent(
      'threshold 4',
    )
    expect(within(reviewRegion).getByLabelText('Grouped materialization diagnostics')).toHaveTextContent(
      'Materialization review',
    )
    expect(within(reviewRegion).getByLabelText('Grouped materialization diagnostics')).toHaveTextContent(
      'Authority boundary',
    )
    expect(within(reviewRegion).getByLabelText('Grouped materialization diagnostics')).toHaveTextContent(
      'Taxonomy lifecycle risk',
    )
    expect(within(reviewRegion).queryByText('Approved')).toBeNull()
    expect(within(reviewRegion).queryByText('Rejected')).toBeNull()

    fireEvent.click(within(reviewRegion).getByRole('button', { name: 'Run Review' }))
    expect(onRunMaterializationReview).toHaveBeenCalledTimes(1)
  })

  it('shows reasoning certification as non-authoritative answerability evidence', () => {
    const onRunCertification = vi.fn()
    renderTab({ onRunCertification })

    const certificationRegion = screen.getByRole('region', { name: 'Reasoning certification' })
    expect(within(certificationRegion).getByLabelText('Reasoning certification authority')).toHaveTextContent(
      'Non-authoritative',
    )
    expect(within(certificationRegion).getByLabelText('Reasoning certification summary')).toHaveTextContent(
      'Result: Passed',
    )
    expect(within(certificationRegion).getByLabelText('Reasoning certification evidence')).toHaveTextContent(
      'Alternative rejection and thread reconstruction are answerable.',
    )
    expect(within(certificationRegion).getByLabelText('Reasoning certification report history')).toHaveTextContent(
      'certification.current',
    )

    fireEvent.click(within(certificationRegion).getByRole('button', { name: 'Run Certification' }))
    expect(onRunCertification).toHaveBeenCalledTimes(1)
  })

  it('surfaces failed reasoning certification evidence with references and diagnostics', () => {
    renderTab({
      certificationReport: failedCertificationReport,
      certificationReports: [failedCertificationReport],
    })

    const certificationRegion = screen.getByRole('region', { name: 'Reasoning certification' })
    expect(within(certificationRegion).getByLabelText('Reasoning certification summary')).toHaveTextContent(
      'Result: Failed',
    )
    expect(within(certificationRegion).getByLabelText('Reasoning certification diagnostics')).toHaveTextContent(
      'Unresolved external reference: .agents/missing.md',
    )
    expect(within(certificationRegion).getByText('Provenance completeness')).toBeInTheDocument()
    expect(within(certificationRegion).getByText('EVT-9999 is missing provenance.')).toBeInTheDocument()
    expect(within(certificationRegion).getByLabelText('Reasoning certification evidence')).toHaveTextContent(
      'ReasoningEvent',
    )
    expect(within(certificationRegion).getByLabelText('Reasoning certification evidence')).toHaveTextContent(
      'EVT-9999',
    )
  })
})
