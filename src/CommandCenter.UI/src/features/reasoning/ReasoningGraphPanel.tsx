import { useMemo, useState } from 'react'
import { EmptyState } from '../../components/design'
import type {
  ReasoningGraph,
  ReasoningGraphNode,
  ReasoningGraphRelationship,
  ReasoningReferenceKind,
  ReasoningTrace,
} from '../../types'
import { ReasoningDiagnosticGroups } from './ReasoningDiagnosticGroups'

type ReasoningGraphPanelProps = {
  graph: ReasoningGraph | null
  backwardTrace: ReasoningTrace | null
  forwardTrace: ReasoningTrace | null
  isLoading: boolean
  isTracing: boolean
  onTraceNode: (node: ReasoningGraphNode) => void
}

export function ReasoningGraphPanel({
  graph,
  backwardTrace,
  forwardTrace,
  isLoading,
  isTracing,
  onTraceNode,
}: ReasoningGraphPanelProps) {
  const [selectedKind, setSelectedKind] = useState<ReasoningReferenceKind | 'All'>('All')
  const [selectedRelationshipType, setSelectedRelationshipType] = useState<string>('All')
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null)

  const nodes = useMemo(() => graph?.nodes ?? [], [graph])
  const relationships = useMemo(() => graph?.relationships ?? [], [graph])
  const selectedNode = nodes.find((node) => node.id === selectedNodeId) ?? nodes[0] ?? null
  const visibleNodes = useMemo(
    () => nodes.filter((node) => selectedKind === 'All' || node.kind === selectedKind),
    [nodes, selectedKind],
  )
  const visibleRelationships = useMemo(
    () =>
      relationships.filter(
        (relationship) =>
          selectedRelationshipType === 'All' || relationship.type === selectedRelationshipType,
      ),
    [relationships, selectedRelationshipType],
  )
  const nodeKinds = Array.from(new Set(nodes.map((node) => node.kind))).sort()
  const relationshipTypes = Array.from(new Set(relationships.map((relationship) => relationship.type))).sort()

  const traceNode = selectedNode

  return (
    <section className="reasoning-panel reasoning-graph-panel" id="reasoning-graph" aria-label="Reasoning graph">
      <div className="decision-panel-heading">
        <h5>Graph Navigation</h5>
        <span>{nodes.length} nodes / {relationships.length} relationships</span>
      </div>

      <div className="reasoning-derived-status" aria-label="Reasoning graph authority">
        <span>Derived graph</span>
        <strong>{graph ? `Generated ${formatDate(graph.generatedAt)}` : 'No graph loaded'}</strong>
      </div>

      <div className="reasoning-graph-filters" aria-label="Reasoning graph filters">
        <label>
          Node kind
          <select
            value={selectedKind}
            onChange={(event) => setSelectedKind(event.target.value as ReasoningReferenceKind | 'All')}
          >
            <option value="All">All node kinds</option>
            {nodeKinds.map((kind) => (
              <option value={kind} key={kind}>{kind}</option>
            ))}
          </select>
        </label>
        <label>
          Relationship type
          <select
            value={selectedRelationshipType}
            onChange={(event) => setSelectedRelationshipType(event.target.value)}
          >
            <option value="All">All relationship types</option>
            {relationshipTypes.map((type) => (
              <option value={type} key={type}>{type}</option>
            ))}
          </select>
        </label>
      </div>

      {graph && nodes.length > 0 ? (
        <div className="reasoning-graph-layout">
          <div className="reasoning-graph-table" aria-label="Reasoning graph nodes">
            <div className="reasoning-table-header">
              <span>Node</span>
              <span>Kind</span>
              <span>Status</span>
            </div>
            {visibleNodes.map((node) => (
              <button
                type="button"
                className={`reasoning-table-row reasoning-node-row${selectedNode?.id === node.id ? ' selected' : ''}`}
                onClick={() => setSelectedNodeId(node.id)}
                key={node.id}
              >
                <span>{node.label}</span>
                <span>{node.kind}</span>
                <span>{node.resolved ? 'Resolved' : 'Unresolved'}</span>
              </button>
            ))}
          </div>

          <div className="reasoning-node-detail" aria-label="Selected reasoning graph node">
            {selectedNode ? (
              <>
                <div className="decision-panel-heading">
                  <h5>{selectedNode.label}</h5>
                  <span>{selectedNode.id}</span>
                </div>
                <dl className="reasoning-provenance">
                  <div>
                    <dt>Kind</dt>
                    <dd>{selectedNode.kind}</dd>
                  </div>
                  <div>
                    <dt>Reference</dt>
                    <dd>{selectedNode.referenceId}</dd>
                  </div>
                  <div>
                    <dt>Status</dt>
                    <dd>{selectedNode.resolved ? 'Resolved' : 'Unresolved'}</dd>
                  </div>
                  {selectedNode.reference?.relativePath ? (
                    <div>
                      <dt>Path</dt>
                      <dd>{selectedNode.reference.relativePath}</dd>
                    </div>
                  ) : null}
                </dl>
                <button
                  type="button"
                  className="secondary-action"
                  disabled={isTracing || !traceNode}
                  onClick={() => traceNode && onTraceNode(traceNode)}
                >
                  {isTracing ? 'Tracing...' : 'Trace Node'}
                </button>
              </>
            ) : (
              <EmptyState className="empty-state">No node selected.</EmptyState>
            )}
          </div>
        </div>
      ) : (
        <EmptyState className="empty-state">
          {isLoading ? 'Loading reasoning graph...' : 'No reasoning graph nodes available.'}
        </EmptyState>
      )}

      {graph && visibleRelationships.length > 0 ? (
        <ReasoningRelationshipTable
          title="Graph Relationships"
          relationships={visibleRelationships}
        />
      ) : null}

      <div className="reasoning-trace-grid">
        <ReasoningTraceList title="Backward Trace" trace={backwardTrace} />
        <ReasoningTraceList title="Forward Trace" trace={forwardTrace} />
      </div>

      <ReasoningDiagnosticGroups groups={graph?.diagnosticGroups} label="Grouped graph diagnostics" />

      {graph?.diagnostics.length && !graph.diagnosticGroups?.length ? (
        <div className="reasoning-diagnostics" aria-label="Reasoning graph diagnostics">
          <h5>Diagnostics</h5>
          {graph.diagnostics.map((diagnostic) => (
            <p key={diagnostic}>{diagnostic}</p>
          ))}
        </div>
      ) : null}
    </section>
  )
}

function ReasoningRelationshipTable({
  title,
  relationships,
}: {
  title: string
  relationships: ReasoningGraphRelationship[]
}) {
  return (
    <div className="reasoning-graph-table" aria-label={title}>
      <div className="reasoning-table-title">{title}</div>
      <div className="reasoning-table-header">
        <span>Type</span>
        <span>Source</span>
        <span>Target</span>
      </div>
      {relationships.map((relationship) => (
        <div className="reasoning-table-row" key={relationship.id}>
          <span>{relationship.type}</span>
          <span>{relationship.sourceNodeId}</span>
          <span>{relationship.targetNodeId}</span>
        </div>
      ))}
    </div>
  )
}

function ReasoningTraceList({
  title,
  trace,
}: {
  title: string
  trace: ReasoningTrace | null
}) {
  return (
    <div className="reasoning-trace-list" aria-label={title}>
      <div className="decision-panel-heading">
        <h5>{title}</h5>
        <span>{trace ? `${trace.nodes.length} nodes / ${trace.relationships.length} relationships` : 'Not traced'}</span>
      </div>
      {trace ? (
        <>
          <div className="reasoning-badge-row">
            <span>{trace.direction}</span>
            <span>{trace.target.kind} {trace.target.id}</span>
          </div>
          {trace.relationships.length > 0 ? (
            <div className="decision-row-list">
              {trace.relationships.map((relationship) => (
                <article className="decision-row reasoning-relationship-row" key={relationship.id}>
                  <strong>{relationship.type}</strong>
                  <span>{relationship.sourceNodeId} {'->'} {relationship.targetNodeId}</span>
                  <small>{relationship.provenance}</small>
                </article>
              ))}
            </div>
          ) : (
            <EmptyState className="empty-state">No trace relationships found.</EmptyState>
          )}
          <ReasoningDiagnosticGroups groups={trace.diagnosticGroups} label={`${title} grouped diagnostics`} />

          {trace.diagnostics.length && !trace.diagnosticGroups?.length ? (
            <div className="reasoning-diagnostics">
              {trace.diagnostics.map((diagnostic) => (
                <p key={diagnostic}>{diagnostic}</p>
              ))}
            </div>
          ) : null}
        </>
      ) : (
        <EmptyState className="empty-state">Select a graph node and trace it.</EmptyState>
      )}
    </div>
  )
}

function formatDate(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString()
}
