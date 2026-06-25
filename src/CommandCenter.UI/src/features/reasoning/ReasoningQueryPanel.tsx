import { useEffect, useMemo, useState, type FormEvent } from 'react'
import type {
  ReasoningGraph,
  ReasoningQuery,
  ReasoningQueryCategory,
  ReasoningQueryResult,
  ReasoningReference,
  ReasoningReferenceKind,
  ReasoningTraceDirection,
} from '../../types'
import { ReasoningDiagnosticGroups } from './ReasoningDiagnosticGroups'

type ReasoningQueryPanelProps = {
  graph: ReasoningGraph | null
  queryResult: ReasoningQueryResult | null
  isRunning: boolean
  error: string | null
  onRunQuery: (query: ReasoningQuery) => Promise<unknown>
}

export function ReasoningQueryPanel({
  graph,
  queryResult,
  isRunning,
  error,
  onRunQuery,
}: ReasoningQueryPanelProps) {
  const targetOptions = useMemo(
    () =>
      (graph?.nodes ?? []).map((node) => ({
        value: `${node.kind}:${node.referenceId}`,
        label: `${node.kind} ${node.referenceId} - ${node.label}`,
        kind: node.kind,
        id: node.referenceId,
      })),
    [graph],
  )
  const [category, setCategory] = useState<ReasoningQueryCategory>('Decision')
  const [question, setQuestion] = useState('Why did this reasoning target change?')
  const [direction, setDirection] = useState<ReasoningTraceDirection>('Backward')
  const [targetValue, setTargetValue] = useState(targetOptions[0]?.value ?? '')
  const [manualKind, setManualKind] = useState<ReasoningReferenceKind>('ReasoningEvent')
  const [manualId, setManualId] = useState('')

  useEffect(() => {
    if (!targetValue && targetOptions.length > 0) {
      setTargetValue(targetOptions[0].value)
    } else if (!targetValue && targetOptions.length === 0) {
      setTargetValue('manual')
    }
  }, [targetOptions, targetValue])

  const selectedTarget = targetOptions.find((target) => target.value === targetValue) ?? null
  const canSubmit =
    question.trim().length > 0 &&
    ((selectedTarget !== null && targetValue !== 'manual') ||
      (targetValue === 'manual' && manualId.trim().length > 0))

  const submitQuery = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!canSubmit) {
      return
    }

    const target =
      targetValue === 'manual'
        ? {
            kind: manualKind,
            id: manualId.trim(),
            relativePath: null,
            section: null,
            excerpt: null,
          }
        : {
            kind: selectedTarget!.kind,
            id: selectedTarget!.id,
            relativePath: null,
            section: null,
            excerpt: null,
          }

    await onRunQuery({
      category,
      question: question.trim(),
      target,
      direction,
    })
  }

  return (
    <section className="reasoning-panel reasoning-query-panel" id="reasoning-query" aria-label="Reasoning query">
      <div className="decision-panel-heading">
        <h5>Query Reasoning</h5>
        <span>Derived trace discovery</span>
      </div>

      <form className="decision-refinement-form reasoning-query-form" onSubmit={submitQuery}>
        <label>
          Category
          <select
            value={category}
            onChange={(event) => setCategory(event.target.value as ReasoningQueryCategory)}
            disabled={isRunning}
          >
            {queryCategories.map((item) => (
              <option value={item} key={item}>
                {item}
              </option>
            ))}
          </select>
        </label>
        <label>
          Direction
          <select
            value={direction}
            onChange={(event) => setDirection(event.target.value as ReasoningTraceDirection)}
            disabled={isRunning}
          >
            <option value="Backward">Backward</option>
            <option value="Forward">Forward</option>
          </select>
        </label>
        <label>
          Target
          <select
            value={targetValue || (targetOptions[0]?.value ?? 'manual')}
            onChange={(event) => setTargetValue(event.target.value)}
            disabled={isRunning}
          >
            {targetOptions.map((target) => (
              <option value={target.value} key={target.value}>
                {target.label}
              </option>
            ))}
            <option value="manual">Manual target</option>
          </select>
        </label>
        {targetValue === 'manual' || targetOptions.length === 0 ? (
          <>
            <label>
              Target kind
              <select
                value={manualKind}
                onChange={(event) => setManualKind(event.target.value as ReasoningReferenceKind)}
                disabled={isRunning}
              >
                {referenceKinds.map((kind) => (
                  <option value={kind} key={kind}>
                    {kind}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Target id
              <input
                type="text"
                value={manualId}
                onChange={(event) => setManualId(event.target.value)}
                disabled={isRunning}
              />
            </label>
          </>
        ) : null}
        <label>
          Question
          <textarea
            value={question}
            onChange={(event) => setQuestion(event.target.value)}
            disabled={isRunning}
            rows={3}
          />
        </label>
        <div className="decision-form-actions">
          <button type="submit" className="primary-action" disabled={!canSubmit || isRunning}>
            {isRunning ? 'Querying...' : 'Run Query'}
          </button>
        </div>
      </form>

      <div className="reasoning-derived-status" aria-label="Reasoning query authority">
        <strong>Derived query</strong>
        <span>Trace candidates only</span>
      </div>

      {error ? <p className="notice error">{error}</p> : null}

      {queryResult ? (
        <div className="reasoning-query-result" aria-label="Reasoning query result">
          <div className="context-summary">
            <span>{queryResult.reconstruction.trace.nodes.length} nodes</span>
            <span>{queryResult.reconstruction.trace.relationships.length} relationships</span>
            <span>{queryResult.reconstruction.evidence.length} evidence items</span>
            <span>{queryResult.reconstruction.confidenceRationale.level} confidence</span>
          </div>
          <p>{queryResult.reconstruction.narrative.summary}</p>
          <div className="reasoning-query-transparency" aria-label="Executed reasoning query">
            <dl className="reasoning-reconstruction-metadata">
              <div>
                <dt>Question</dt>
                <dd>{queryResult.query.question}</dd>
              </div>
              <div>
                <dt>Category</dt>
                <dd>{queryResult.query.category}</dd>
              </div>
              <div>
                <dt>Direction</dt>
                <dd>{queryResult.query.direction}</dd>
              </div>
              <div>
                <dt>Target</dt>
                <dd>{formatReference(queryResult.query.target)}</dd>
              </div>
              <div>
                <dt>Historical cutoff</dt>
                <dd>{queryResult.query.historicalAt ?? 'Current graph'}</dd>
              </div>
            </dl>
          </div>
          <div className="reasoning-query-transparency" aria-label="Reasoning query transparency">
            <dl className="reasoning-reconstruction-metadata">
              <div>
                <dt>Confidence basis</dt>
                <dd>{queryResult.reconstruction.confidenceRationale.rationale}</dd>
              </div>
              <div>
                <dt>Scope</dt>
                <dd>
                  {queryResult.reconstruction.scope.direction} from{' '}
                  {queryResult.reconstruction.scope.source
                    ? `${queryResult.reconstruction.scope.source.kind} ${queryResult.reconstruction.scope.source.id}`
                    : 'unreported source'}{' '}
                  to {queryResult.reconstruction.scope.target.kind}{' '}
                  {queryResult.reconstruction.scope.target.id}
                </dd>
              </div>
              <div>
                <dt>Historical cutoff</dt>
                <dd>{queryResult.reconstruction.scope.historicalCutoff ?? 'Current graph'}</dd>
              </div>
              <div>
                <dt>Reachable evidence</dt>
                <dd>{queryResult.reconstruction.scope.reachableEvidence.length} known item(s)</dd>
              </div>
              <div>
                <dt>Unreachable evidence</dt>
                <dd>{queryResult.reconstruction.scope.unreachableEvidence.length} known item(s)</dd>
              </div>
            </dl>
            {queryResult.reconstruction.confidenceRationale.missingEvidence.length > 0 ? (
              <div className="reasoning-reconstruction-section">
                <div className="reasoning-list-title">
                  <strong>Missing Evidence</strong>
                  <span>{queryResult.reconstruction.confidenceRationale.missingEvidence.length} items</span>
                </div>
                <ul>
                  {queryResult.reconstruction.confidenceRationale.missingEvidence.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              </div>
            ) : null}
            {queryResult.reconstruction.confidenceRationale.whyNotHigher.length > 0 ? (
              <div className="reasoning-reconstruction-section">
                <div className="reasoning-list-title">
                  <strong>Why Confidence Was Not Higher</strong>
                  <span>{queryResult.reconstruction.confidenceRationale.whyNotHigher.length} items</span>
                </div>
                <ul>
                  {queryResult.reconstruction.confidenceRationale.whyNotHigher.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              </div>
            ) : null}
          </div>
          <ReasoningDiagnosticGroups
            groups={queryResult.diagnosticGroups ?? queryResult.reconstruction.diagnosticGroups}
            label="Grouped query diagnostics"
          />

          {(!(queryResult.diagnosticGroups?.length || queryResult.reconstruction.diagnosticGroups?.length) &&
            queryResult.diagnostics.length > 0) ? (
            <div className="reasoning-diagnostics">
              {queryResult.diagnostics.map((diagnostic) => (
                <p key={diagnostic}>{diagnostic}</p>
              ))}
            </div>
          ) : null}
        </div>
      ) : null}
    </section>
  )
}

const queryCategories: ReasoningQueryCategory[] = [
  'Decision',
  'Hypothesis',
  'Alternative',
  'Contradiction',
  'Direction',
  'Thread',
  'Assumption',
]

const referenceKinds: ReasoningReferenceKind[] = [
  'Decision',
  'Proposal',
  'ProposalRevision',
  'Candidate',
  'OperationalContextRevision',
  'GovernanceFinding',
  'ExecutionProjection',
  'ExecutionOutput',
  'Handoff',
  'Artifact',
  'ReasoningEvent',
  'ReasoningThread',
]

function formatReference(reference: ReasoningReference) {
  const qualifiers = [reference.relativePath, reference.section].filter(Boolean)
  return qualifiers.length > 0
    ? `${reference.kind} ${reference.id} (${qualifiers.join(' - ')})`
    : `${reference.kind} ${reference.id}`
}
