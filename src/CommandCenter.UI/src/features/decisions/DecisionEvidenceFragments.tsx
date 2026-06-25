import type { DecisionEvidence, DecisionSourceReference } from '../../types'

export function DecisionEvidenceBlock({ title, evidence }: { title: string; evidence: DecisionEvidence[] }) {
  if (evidence.length === 0) {
    return null
  }

  return (
    <div className="decision-evidence-block" aria-label={title}>
      <span>{title}</span>
      {evidence.map((evidenceItem) => (
        <article key={`${title}-${evidenceItem.summary}`}>
          <p>{evidenceItem.summary}</p>
          <DecisionSourceList sources={evidenceItem.sources} />
        </article>
      ))}
    </div>
  )
}

export function DecisionSourceList({ sources }: { sources: DecisionSourceReference[] }) {
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
