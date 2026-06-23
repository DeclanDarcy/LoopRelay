import { useMemo, useState, type FormEvent } from 'react'
import { EmptyState, Panel, SectionHeader } from '../../components/design'
import type {
  ManualReasoningCaptureCommand,
  ManualReasoningCaptureTemplate,
  ReasoningEvent,
  ReasoningEventFamily,
  ReasoningGraph,
  ReasoningGraphNode,
  ReasoningQuery,
  ReasoningQueryResult,
  ReasoningReconstruction,
  ReasoningRelationship,
  ReasoningThread,
  ReasoningTrace,
} from '../../types'
import { ReasoningEventFeed } from './ReasoningEventFeed'
import { ReasoningGraphPanel } from './ReasoningGraphPanel'
import { ReasoningQueryPanel } from './ReasoningQueryPanel'
import { ReasoningReconstructionPanel } from './ReasoningReconstructionPanel'
import { ReasoningThreadPanel } from './ReasoningThreadPanel'
import { ReasoningTracePanel } from './ReasoningTracePanel'

type ReasoningTrajectoryTabProps = {
  events: ReasoningEvent[]
  threads: ReasoningThread[]
  relationships: ReasoningRelationship[]
  graph: ReasoningGraph | null
  backwardTrace: ReasoningTrace | null
  forwardTrace: ReasoningTrace | null
  queryResult: ReasoningQueryResult | null
  reconstruction: ReasoningReconstruction | null
  templates?: ManualReasoningCaptureTemplate[]
  hasSelectedRepository: boolean
  isLoading: boolean
  isTracingGraph: boolean
  isQuerying: boolean
  isReconstructing: boolean
  error: string | null
  queryError: string | null
  reconstructionError: string | null
  onRefresh: () => void
  onTraceGraphNode: (node: ReasoningGraphNode) => void
  onRunQuery: (query: ReasoningQuery) => Promise<unknown>
  onCaptureManualReasoning?: (command: ManualReasoningCaptureCommand) => Promise<void>
}

export function ReasoningTrajectoryTab({
  events,
  threads,
  relationships,
  graph,
  backwardTrace,
  forwardTrace,
  queryResult,
  reconstruction,
  templates = [],
  hasSelectedRepository,
  isLoading,
  isTracingGraph,
  isQuerying,
  isReconstructing,
  error,
  queryError,
  reconstructionError,
  onRefresh,
  onTraceGraphNode,
  onRunQuery,
  onCaptureManualReasoning,
}: ReasoningTrajectoryTabProps) {
  const [selectedThreadId, setSelectedThreadId] = useState<string | null>(null)
  const [selectedFamilies, setSelectedFamilies] = useState<ReasoningEventFamily[]>([])
  const [captureKind, setCaptureKind] = useState('')
  const [captureTitle, setCaptureTitle] = useState('')
  const [captureSummary, setCaptureSummary] = useState('')
  const [captureDetails, setCaptureDetails] = useState('')
  const [captureSourcePath, setCaptureSourcePath] = useState('')
  const [captureSection, setCaptureSection] = useState('')
  const [captureExcerpt, setCaptureExcerpt] = useState('')
  const [captureTags, setCaptureTags] = useState('')
  const [isSubmittingCapture, setIsSubmittingCapture] = useState(false)
  const selectedThread = useMemo(
    () => threads.find((thread) => thread.id === selectedThreadId) ?? null,
    [selectedThreadId, threads],
  )
  const selectedTemplate =
    templates.find((template) => template.kind === captureKind) ?? templates[0] ?? null
  const familyCounts = countFamilies(events)

  const toggleFamily = (family: ReasoningEventFamily) => {
    setSelectedFamilies((currentFamilies) =>
      currentFamilies.includes(family)
        ? currentFamilies.filter((currentFamily) => currentFamily !== family)
        : [...currentFamilies, family],
    )
  }

  const submitManualCapture = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!selectedTemplate || !onCaptureManualReasoning) {
      return
    }

    const title = captureTitle.trim()
    const summary = captureSummary.trim()
    const sourcePath = captureSourcePath.trim()
    if (!title || !summary || !sourcePath) {
      return
    }

    setIsSubmittingCapture(true)
    try {
      await onCaptureManualReasoning({
        kind: selectedTemplate.kind,
        title,
        narrative: {
          summary,
          details: captureDetails.trim(),
        },
        references: [],
        provenance: {
          sourceKind: selectedTemplate.provenanceSourceKind,
          capturedBy: 'user',
          relativePath: sourcePath,
          section: captureSection.trim() || null,
          excerpt: captureExcerpt.trim() || null,
          fingerprint: null,
        },
        threadIds: selectedThreadId ? [selectedThreadId] : [],
        tags: parseTags(captureTags),
      })
      setCaptureTitle('')
      setCaptureSummary('')
      setCaptureDetails('')
      setCaptureSourcePath('')
      setCaptureSection('')
      setCaptureExcerpt('')
      setCaptureTags('')
    } finally {
      setIsSubmittingCapture(false)
    }
  }

  return (
    <Panel
      className="execution-context-panel reasoning-trajectory-tab tab-panel tab-reasoning"
      id="reasoning-trajectory"
      aria-label="Reasoning trajectory"
    >
      <SectionHeader
        className="context-toolbar"
        eyebrow="Reasoning"
        title="Trajectory"
        headingLevel={4}
        actions={
          <div className="context-controls">
            <button
              type="button"
              className="secondary-action"
              onClick={onRefresh}
              disabled={!hasSelectedRepository || isLoading}
            >
              {isLoading ? 'Loading...' : 'Refresh Reasoning'}
            </button>
          </div>
        }
      />

      {hasSelectedRepository ? (
        <div className="reasoning-grid">
          <div className="context-summary" aria-label="Reasoning summary">
            <span>{events.length} events</span>
            <span>{threads.length} threads</span>
            <span>{relationships.length} relationships</span>
            <span>{familyCounts}</span>
          </div>

          {error ? <p className="notice error">{error}</p> : null}

          <section className="reasoning-panel reasoning-capture-panel" aria-label="Record reasoning event">
            <div className="decision-panel-heading">
              <h5>Record Reasoning</h5>
              <span>{selectedTemplate ? `${selectedTemplate.family} / ${selectedTemplate.type}` : 'No templates'}</span>
            </div>

            <form className="decision-refinement-form reasoning-capture-form" onSubmit={submitManualCapture}>
              <label>
                Capture template
                <select
                  value={selectedTemplate?.kind ?? ''}
                  onChange={(event) => setCaptureKind(event.target.value)}
                  disabled={!onCaptureManualReasoning || templates.length === 0 || isSubmittingCapture}
                >
                  {templates.length === 0 ? <option value="">No templates available</option> : null}
                  {templates.map((template) => (
                    <option value={template.kind} key={template.kind}>
                      {formatCaptureKind(template.kind)}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Title
                <input
                  type="text"
                  value={captureTitle}
                  onChange={(event) => setCaptureTitle(event.target.value)}
                  disabled={!onCaptureManualReasoning || isSubmittingCapture}
                />
              </label>
              <label>
                Summary
                <textarea
                  value={captureSummary}
                  onChange={(event) => setCaptureSummary(event.target.value)}
                  disabled={!onCaptureManualReasoning || isSubmittingCapture}
                  rows={3}
                />
              </label>
              <label>
                Details
                <textarea
                  value={captureDetails}
                  onChange={(event) => setCaptureDetails(event.target.value)}
                  disabled={!onCaptureManualReasoning || isSubmittingCapture}
                  rows={3}
                />
              </label>
              <label>
                Source path
                <input
                  type="text"
                  value={captureSourcePath}
                  onChange={(event) => setCaptureSourcePath(event.target.value)}
                  placeholder=".agents/plan.md"
                  disabled={!onCaptureManualReasoning || isSubmittingCapture}
                />
              </label>
              <label>
                Source section
                <input
                  type="text"
                  value={captureSection}
                  onChange={(event) => setCaptureSection(event.target.value)}
                  disabled={!onCaptureManualReasoning || isSubmittingCapture}
                />
              </label>
              <label>
                Excerpt
                <textarea
                  value={captureExcerpt}
                  onChange={(event) => setCaptureExcerpt(event.target.value)}
                  disabled={!onCaptureManualReasoning || isSubmittingCapture}
                  rows={2}
                />
              </label>
              <label>
                Tags
                <input
                  type="text"
                  value={captureTags}
                  onChange={(event) => setCaptureTags(event.target.value)}
                  placeholder="milestone-2, manual"
                  disabled={!onCaptureManualReasoning || isSubmittingCapture}
                />
              </label>
              <div className="decision-form-actions">
                <button
                  type="submit"
                  className="primary-action"
                  disabled={
                    !onCaptureManualReasoning ||
                    !selectedTemplate ||
                    !captureTitle.trim() ||
                    !captureSummary.trim() ||
                    !captureSourcePath.trim() ||
                    isSubmittingCapture
                  }
                >
                  {isSubmittingCapture ? 'Recording...' : 'Record Event'}
                </button>
              </div>
            </form>
          </section>

          <div className="decision-filter-bar reasoning-family-filter-bar" aria-label="Reasoning event family filters">
            <button
              type="button"
              className={`decision-filter${selectedFamilies.length === 0 ? ' selected' : ''}`}
              aria-pressed={selectedFamilies.length === 0}
              onClick={() => setSelectedFamilies([])}
            >
              All Events
            </button>
            {reasoningFamilyFilters.map((filter) => (
              <button
                type="button"
                className={`decision-filter${selectedFamilies.includes(filter.family) ? ' selected' : ''}`}
                aria-pressed={selectedFamilies.includes(filter.family)}
                onClick={() => toggleFamily(filter.family)}
                key={filter.family}
              >
                {filter.label}
              </button>
            ))}
          </div>

          <ReasoningThreadPanel
            threads={threads}
            events={events}
            selectedThreadId={selectedThreadId}
            isLoading={isLoading}
            onSelectThread={setSelectedThreadId}
          />
          <ReasoningEventFeed
            events={events}
            selectedThreadId={selectedThreadId}
            selectedFamilies={selectedFamilies}
            isLoading={isLoading}
          />
          <ReasoningTracePanel
            events={events}
            relationships={relationships}
            selectedThread={selectedThread}
            isLoading={isLoading}
          />
          <ReasoningGraphPanel
            graph={graph}
            backwardTrace={backwardTrace}
            forwardTrace={forwardTrace}
            isLoading={isLoading}
            isTracing={isTracingGraph}
            onTraceNode={onTraceGraphNode}
          />
          <ReasoningQueryPanel
            graph={graph}
            queryResult={queryResult}
            isRunning={isQuerying || isReconstructing}
            error={queryError}
            onRunQuery={onRunQuery}
          />
          <ReasoningReconstructionPanel
            reconstruction={reconstruction}
            isRunning={isReconstructing}
            error={reconstructionError}
          />
        </div>
      ) : (
        <EmptyState className="empty-state">Select or add a repository.</EmptyState>
      )}
    </Panel>
  )
}

const reasoningFamilyFilters: Array<{ family: ReasoningEventFamily; label: string }> = [
  { family: 'Hypothesis', label: 'Hypothesis Events' },
  { family: 'Alternative', label: 'Alternative Events' },
  { family: 'Contradiction', label: 'Contradiction Events' },
  { family: 'Direction', label: 'Direction Events' },
  { family: 'DecisionEvolution', label: 'Decision Evolution Events' },
  { family: 'AssumptionEvolution', label: 'Assumption Evolution Events' },
  { family: 'ConstraintEvolution', label: 'Constraint Evolution Events' },
]

function countFamilies(events: ReasoningEvent[]) {
  const counts = events.reduce<Record<string, number>>((familyCounts, event) => {
    familyCounts[event.family] = (familyCounts[event.family] ?? 0) + 1
    return familyCounts
  }, {})

  const entries = Object.entries(counts)
  if (entries.length === 0) {
    return '0 families'
  }

  return entries.map(([family, count]) => `${family}: ${count}`).join(' / ')
}

function formatCaptureKind(value: string) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function parseTags(value: string) {
  return value
    .split(',')
    .map((tag) => tag.trim())
    .filter(Boolean)
}
