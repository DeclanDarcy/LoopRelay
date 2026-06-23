import { useMemo, useState } from 'react'
import { EmptyState, Panel, SectionHeader } from '../../components/design'
import type { ReasoningEvent, ReasoningRelationship, ReasoningThread } from '../../types'
import { ReasoningEventFeed } from './ReasoningEventFeed'
import { ReasoningThreadPanel } from './ReasoningThreadPanel'
import { ReasoningTracePanel } from './ReasoningTracePanel'

type ReasoningTrajectoryTabProps = {
  events: ReasoningEvent[]
  threads: ReasoningThread[]
  relationships: ReasoningRelationship[]
  hasSelectedRepository: boolean
  isLoading: boolean
  error: string | null
  onRefresh: () => void
}

export function ReasoningTrajectoryTab({
  events,
  threads,
  relationships,
  hasSelectedRepository,
  isLoading,
  error,
  onRefresh,
}: ReasoningTrajectoryTabProps) {
  const [selectedThreadId, setSelectedThreadId] = useState<string | null>(null)
  const selectedThread = useMemo(
    () => threads.find((thread) => thread.id === selectedThreadId) ?? null,
    [selectedThreadId, threads],
  )
  const familyCounts = countFamilies(events)

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
            isLoading={isLoading}
          />
          <ReasoningTracePanel
            events={events}
            relationships={relationships}
            selectedThread={selectedThread}
            isLoading={isLoading}
          />
        </div>
      ) : (
        <EmptyState className="empty-state">Select or add a repository.</EmptyState>
      )}
    </Panel>
  )
}

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
