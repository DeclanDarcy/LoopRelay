import { useMemo, useState } from 'react'
import { Badge, EmptyState } from '../../components/design'
import type {
  DecisionProposalLineage,
  DecisionProposalRevisionSnapshot,
  DecisionSourceReference,
} from '../../types'

type DecisionRevisionHistoryProps = {
  lineage: DecisionProposalLineage | null
  isLoading: boolean
}

export function DecisionRevisionHistory({ lineage, isLoading }: DecisionRevisionHistoryProps) {
  const [selectedRevisionId, setSelectedRevisionId] = useState<string | null>(null)
  const selectedRevision = useMemo(() => {
    if (!lineage) {
      return null
    }

    return (
      lineage.revisions.find((revision) => revision.revision.id === selectedRevisionId) ??
      lineage.revisions[0] ??
      null
    )
  }, [lineage, selectedRevisionId])

  if (!lineage) {
    return (
      <section className="decision-lifecycle-panel decision-revision-history" aria-label="Revision history">
        <h5>Revision History</h5>
        <EmptyState className="empty-state">
          {isLoading ? 'Loading proposal lineage...' : 'Select a proposal to inspect lineage.'}
        </EmptyState>
      </section>
    )
  }

  return (
    <section className="decision-lifecycle-panel decision-revision-history" aria-label="Revision history">
      <div className="decision-panel-heading">
        <h5>Revision History</h5>
        <span>{lineage.proposalId}</span>
      </div>

      <div className="decision-lineage-summary" aria-label="Lineage summary">
        <span>{lineage.currentState} current state</span>
        <span>{lineage.revisions.length} revisions</span>
        <span>{lineage.events.length} lineage events</span>
        <span>{lineage.review.state} review state</span>
      </div>

      <article className="decision-authority-callout" aria-label="Current proposal authority">
        <div>
          <span>Current Proposal</span>
          <strong>{lineage.currentProposal.title}</strong>
        </div>
        <p>Authoritative proposal content is loaded from the backend current proposal projection.</p>
        <small>{lineage.currentProposalFingerprint}</small>
      </article>

      {lineage.diagnostics.length > 0 ? (
        <div className="decision-warning-list" aria-label="Lineage diagnostics">
          {lineage.diagnostics.map((diagnostic) => (
            <span key={diagnostic}>{diagnostic}</span>
          ))}
        </div>
      ) : null}

      <div className="decision-revision-grid">
        <section className="decision-inspection-list" aria-label="Revision list">
          <h6>Revision List</h6>
          {lineage.revisions.length > 0 ? (
            lineage.revisions.map((snapshot) => {
              const revision = snapshot.revision
              const isSelected = revision.id === selectedRevision?.revision.id
              const retiredCount =
                snapshot.comparison.retiredOptions.length + snapshot.comparison.retiredAssumptions.length

              return (
                <button
                  className={`decision-row decision-row-button ${isSelected ? 'selected' : ''}`}
                  key={revision.id}
                  type="button"
                  aria-pressed={isSelected}
                  onClick={() => setSelectedRevisionId(revision.id)}
                >
                  <span>{formatDate(revision.createdAt)}</span>
                  <strong>{revision.id}</strong>
                  <p>{revision.reason}</p>
                  <small>
                    {revision.changedFields.join(', ') || 'No changed fields recorded'}; {retiredCount} retired items
                  </small>
                </button>
              )
            })
          ) : (
            <EmptyState className="empty-state">No proposal revisions recorded.</EmptyState>
          )}
        </section>

        <DecisionRevisionComparison snapshot={selectedRevision} />
      </div>

      <section className="decision-inspection-list" aria-label="Lineage event sequence">
        <h6>Evolution Sequence</h6>
        {lineage.events.map((event) => (
          <article className="decision-inspection-card" key={`${event.kind}-${event.itemId ?? event.occurredAt}`}>
            <div>
              <span>{formatDate(event.occurredAt)}</span>
              <strong>{event.kind}{event.itemId ? ` ${event.itemId}` : ''}</strong>
            </div>
            <p>{event.summary}</p>
            {event.fromState || event.toState ? (
              <small>{event.fromState ?? 'none'} -&gt; {event.toState ?? 'none'}</small>
            ) : null}
            <SourceList sources={event.sources} />
          </article>
        ))}
      </section>
    </section>
  )
}

function DecisionRevisionComparison({ snapshot }: { snapshot: DecisionProposalRevisionSnapshot | null }) {
  if (!snapshot) {
    return (
      <section className="decision-inspection-list" aria-label="Revision comparison">
        <h6>Revision Comparison</h6>
        <EmptyState className="empty-state">Select a revision to inspect comparison.</EmptyState>
      </section>
    )
  }

  const { revision, comparison } = snapshot

  return (
    <section className="decision-inspection-list" aria-label="Revision comparison">
      <div className="decision-panel-heading">
        <h6>Revision Comparison</h6>
        <Badge tone={snapshot.isCurrentProposal ? 'done' : 'info'}>
          {snapshot.isCurrentProposal ? 'Current' : 'Historical'}
        </Badge>
      </div>
      <article className="decision-inspection-card decision-historical-revision">
        <div>
          <span>{revision.id}</span>
          <strong>{snapshot.authorityBoundary}</strong>
        </div>
        <p>{revision.reason}</p>
        <small>
          {comparison.sourceMatchesCurrentProposal
            ? 'Source fingerprint matches current proposal'
            : 'Source fingerprint differs from current proposal'}
        </small>
      </article>

      <div className="decision-diagnostics-grid" aria-label="Revision comparison counts">
        <span>{comparison.fieldComparisons.length} field comparisons</span>
        <span>{comparison.acceptedChanges.length} accepted changes</span>
        <span>{comparison.rejectedChanges.length} rejected changes</span>
        <span>{comparison.retiredOptions.length} retired options</span>
        <span>{comparison.retiredAssumptions.length} retired assumptions</span>
        <span>{comparison.changedFields.length} changed fields</span>
      </div>

      {comparison.fieldComparisons.length > 0 ? (
        <div className="decision-inspection-list" aria-label="Backend field comparisons">
          {comparison.fieldComparisons.map((field) => (
            <article className="decision-inspection-card" key={`${revision.id}-${field.field}`}>
              <div>
                <span>{field.changeType}</span>
                <strong>{field.field}</strong>
              </div>
              <p>{field.previousValue ?? 'No previous value recorded.'}</p>
              <p>{field.revisedValue ?? 'No revised value recorded.'}</p>
            </article>
          ))}
        </div>
      ) : (
        <EmptyState className="empty-state">No backend field comparisons recorded.</EmptyState>
      )}

      <SourceList sources={comparison.sources} />
    </section>
  )
}

function SourceList({ sources }: { sources: DecisionSourceReference[] }) {
  if (sources.length === 0) {
    return null
  }

  return (
    <ul className="decision-source-list" aria-label="Source attribution">
      {sources.map((source, index) => (
        <li key={`${source.sourceKind}-${source.relativePath ?? 'none'}-${index}`}>
          <strong>{source.sourceKind}</strong>
          {source.relativePath ? <span>{source.relativePath}</span> : null}
          {source.section ? <span>{source.section}</span> : null}
          {source.excerpt ? <p>{source.excerpt}</p> : null}
        </li>
      ))}
    </ul>
  )
}

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  return date.toLocaleString()
}
