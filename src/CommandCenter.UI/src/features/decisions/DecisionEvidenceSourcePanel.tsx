import { useEffect, useMemo, useState } from 'react'
import { EmptyState } from '../../components/design'
import type {
  DecisionEvidenceInspection,
  DecisionEvidenceInspectionItem,
  DecisionSourceAttribution,
} from '../../types'

type DecisionEvidenceSourcePanelProps = {
  inspection: DecisionEvidenceInspection | null
  attributions: DecisionSourceAttribution[]
  isLoading: boolean
}

export function DecisionEvidenceSourcePanel({
  inspection,
  attributions,
  isLoading,
}: DecisionEvidenceSourcePanelProps) {
  const [selectedKey, setSelectedKey] = useState<string | null>(null)
  const items = useMemo(() => inspection?.items ?? [], [inspection?.items])
  const selectedItem = useMemo(
    () => items.find((item, index) => evidenceKey(item, index) === selectedKey) ?? items[0] ?? null,
    [items, selectedKey],
  )

  useEffect(() => {
    if (!selectedKey || items.some((item, index) => evidenceKey(item, index) === selectedKey)) {
      return
    }

    setSelectedKey(items.length > 0 ? evidenceKey(items[0], 0) : null)
  }, [items, selectedKey])

  if (!inspection) {
    return (
      <section className="decision-lifecycle-panel decision-evidence-source-panel" aria-label="Evidence and sources">
        <h5>Evidence & Sources</h5>
        <EmptyState className="empty-state">
          {isLoading ? 'Loading evidence and sources...' : 'Select a proposal to inspect evidence.'}
        </EmptyState>
      </section>
    )
  }

  return (
    <section className="decision-lifecycle-panel decision-evidence-source-panel" aria-label="Evidence and sources">
      <div className="decision-panel-heading">
        <h5>Evidence & Sources</h5>
        <span>{inspection.proposalId}</span>
      </div>

      <div className="decision-evidence-source-grid">
        <div className="decision-row-list" role="list" aria-label="Evidence inspection rows">
          {items.map((item, index) => {
            const key = evidenceKey(item, index)
            const isSelected = selectedItem === item
            return (
              <button
                type="button"
                className={`decision-row decision-row-button${isSelected ? ' selected' : ''}`}
                aria-pressed={isSelected}
                onClick={() => setSelectedKey(key)}
                key={key}
              >
                <strong>{item.summary}</strong>
                <span>{item.appliesToKind}{item.itemId ? ` | ${item.itemId}` : ''}</span>
                <p>{item.sources.length} source attribution(s)</p>
              </button>
            )
          })}
        </div>

        {selectedItem ? (
          <aside className="decision-selection-panel" aria-label="Selected evidence source">
            <div>
              <span>{selectedItem.appliesToKind}</span>
              <strong>{selectedItem.itemId ?? 'Proposal'}</strong>
            </div>
            <p>{selectedItem.summary}</p>
            <SourceAttributionList sources={selectedItem.sources} />
          </aside>
        ) : (
          <EmptyState className="empty-state">No evidence items recorded.</EmptyState>
        )}
      </div>

      {attributions.length > 0 ? (
        <div className="decision-inspection-list" aria-label="All source attributions">
          <h6>All Sources</h6>
          <SourceAttributionList sources={attributions} />
        </div>
      ) : null}
    </section>
  )
}

function SourceAttributionList({ sources }: { sources: DecisionSourceAttribution[] }) {
  if (sources.length === 0) {
    return <EmptyState className="empty-state">No source attribution recorded.</EmptyState>
  }

  return (
    <ul className="decision-source-list">
      {sources.map((source, index) => (
        <li key={`${source.appliesToKind}-${source.relativePath ?? 'none'}-${source.itemId ?? 'none'}-${index}`}>
          <strong>{source.sourceKind}</strong>
          {source.relativePath ? <span>{source.relativePath}</span> : null}
          {source.section ? <span>{source.section}</span> : null}
          <span>{source.appliesToKind}{source.itemId ? ` | ${source.itemId}` : ''}</span>
          {source.excerpt ? <p>{source.excerpt}</p> : null}
        </li>
      ))}
    </ul>
  )
}

function evidenceKey(item: DecisionEvidenceInspectionItem, index: number) {
  return `${item.appliesToKind}-${item.itemId ?? 'proposal'}-${item.summary}-${index}`
}
