import { useMemo, useState } from 'react'
import type { ReasoningReconstruction, ReasoningReconstructionEvidence, ReasoningReference } from '../../types'

type ReasoningReconstructionPanelProps = {
  reconstruction: ReasoningReconstruction | null
  isRunning: boolean
  error: string | null
}

export function ReasoningReconstructionPanel({
  reconstruction,
  isRunning,
  error,
}: ReasoningReconstructionPanelProps) {
  const [horizon, setHorizon] = useState<ReasoningReconstructionHorizon>('Project')
  const groupedDetails = useMemo(
    () => parseReconstructionDetails(reconstruction?.narrative.details ?? ''),
    [reconstruction],
  )

  return (
    <section
      className="reasoning-panel reasoning-reconstruction-panel"
      id="reasoning-reconstruction"
      aria-label="Reasoning reconstruction"
    >
      <div className="decision-panel-heading">
        <h5>Reconstruction</h5>
        <span>{isRunning ? 'Reconstructing...' : 'Derived explanation'}</span>
      </div>

      <div className="reasoning-derived-status" aria-label="Reasoning reconstruction authority">
        <strong>Non-authoritative</strong>
        <span>Evidence-backed explanation</span>
      </div>

      {error ? <p className="notice error">{error}</p> : null}

      {reconstruction ? (
        <div className="reasoning-reconstruction-body" aria-label="Reasoning reconstruction result">
          <div className="context-summary">
            <span>{reconstruction.confidenceRationale.level} confidence</span>
            <span>{reconstruction.trace.nodes.length} trace nodes</span>
            <span>{reconstruction.trace.relationships.length} trace relationships</span>
            <span>{reconstruction.evidence.length} evidence items</span>
          </div>

          <section className="reasoning-transparency-panel" aria-label="Reconstruction confidence rationale">
            <div className="decision-panel-heading">
              <h5>Confidence Rationale</h5>
              <span>{reconstruction.confidenceRationale.level}</span>
            </div>
            <p>{reconstruction.confidenceRationale.rationale}</p>
            <dl className="reasoning-reconstruction-metadata">
              <div>
                <dt>Event evidence</dt>
                <dd>{formatPresence(reconstruction.confidenceRationale.eventEvidencePresent)}</dd>
              </div>
              <div>
                <dt>Relationship evidence</dt>
                <dd>{formatPresence(reconstruction.confidenceRationale.relationshipEvidencePresent)}</dd>
              </div>
              <div>
                <dt>Trace diagnostics</dt>
                <dd>{formatPresence(reconstruction.confidenceRationale.traceDiagnosticsPresent)}</dd>
              </div>
            </dl>
            <EvidenceReasonList
              title="Missing Evidence"
              emptyText="No missing evidence reported."
              items={reconstruction.confidenceRationale.missingEvidence}
            />
            <EvidenceReasonList
              title="Why Confidence Was Not Higher"
              emptyText="No confidence blockers reported."
              items={reconstruction.confidenceRationale.whyNotHigher}
            />
          </section>

          <section className="reasoning-transparency-panel" aria-label="Reconstruction scope">
            <div className="decision-panel-heading">
              <h5>Scope</h5>
              <span>{reconstruction.scope.direction}</span>
            </div>
            <dl className="reasoning-reconstruction-metadata">
              <div>
                <dt>Target</dt>
                <dd>{formatReference(reconstruction.scope.target)}</dd>
              </div>
              <div>
                <dt>Source</dt>
                <dd>{formatReference(reconstruction.scope.source)}</dd>
              </div>
              <div>
                <dt>Historical cutoff</dt>
                <dd>{reconstruction.scope.historicalCutoff ?? 'Current graph'}</dd>
              </div>
              <div>
                <dt>Reachable evidence</dt>
                <dd>{reconstruction.scope.reachableEvidence.length} item(s)</dd>
              </div>
            </dl>
            <EvidenceList
              label="Reachable reconstruction evidence"
              title="Reachable Evidence"
              items={reconstruction.scope.reachableEvidence}
              emptyText="No scoped reachable evidence reported."
            />
            <EvidenceList
              label="Known unreachable reconstruction evidence"
              title="Known Unreachable Evidence"
              items={reconstruction.scope.unreachableEvidence}
              emptyText="No known unreachable evidence reported."
            />
          </section>

          <article className="reasoning-event-row">
            <strong>{reconstruction.narrative.summary}</strong>
            {groupedDetails.metadata.length > 0 ? (
              <dl className="reasoning-reconstruction-metadata">
                {groupedDetails.metadata.map((item) => (
                  <div key={item.label}>
                    <dt>{item.label}</dt>
                    <dd>{item.value}</dd>
                  </div>
                ))}
              </dl>
            ) : (
              <p>{reconstruction.narrative.details}</p>
            )}
          </article>

          <section className="reasoning-project-narrative" aria-label="Project narrative reconstruction">
            <div className="decision-panel-heading">
              <h5>Project Narrative</h5>
              <span>{horizon} horizon</span>
            </div>
            <label className="reasoning-horizon-selector">
              Horizon
              <select
                value={horizon}
                onChange={(event) => setHorizon(event.target.value as ReasoningReconstructionHorizon)}
              >
                {reconstructionHorizons.map((item) => (
                  <option value={item} key={item}>
                    {item}
                  </option>
                ))}
              </select>
            </label>
            <p>{horizonNarrative(horizon, reconstruction)}</p>
          </section>

          {groupedDetails.sections.length > 0 ? (
            <div className="reasoning-reconstruction-sections" aria-label="Grouped reconstruction details">
              {groupedDetails.sections.map((section, index) => (
                <details
                  className="reasoning-reconstruction-section"
                  key={section.title}
                  open={index === 0}
                >
                  <summary>
                    {section.title}
                    <span>{section.items.length} items</span>
                  </summary>
                  <ul>
                    {section.items.map((item) => (
                      <li key={item}>{item}</li>
                    ))}
                  </ul>
                </details>
              ))}
            </div>
          ) : null}

          <div className="reasoning-graph-table" aria-label="Reconstruction evidence">
            <div className="reasoning-table-title">Evidence</div>
            <div className="reasoning-table-header">
              <span>Item</span>
              <span>Summary</span>
              <span>Provenance</span>
            </div>
            {reconstruction.evidence.map((item) => (
              <div className="reasoning-table-row" key={`${item.kind}:${item.id}:${item.title}`}>
                <span>{`${item.kind} ${item.id}: ${item.title}`}</span>
                <span>{item.summary}</span>
                <span>
                  {item.provenance
                    ? `${item.provenance.sourceKind} by ${item.provenance.capturedBy}`
                    : item.reference
                      ? `${item.reference.kind} ${item.reference.id}`
                      : 'Derived graph edge'}
                </span>
              </div>
            ))}
          </div>

          {reconstruction.diagnostics.length > 0 ? (
            <div className="reasoning-diagnostics">
              {reconstruction.diagnostics.map((diagnostic) => (
                <p key={diagnostic}>{diagnostic}</p>
              ))}
            </div>
          ) : null}
        </div>
      ) : (
        <p className="empty-state compact">Run a reasoning query to reconstruct its explanation.</p>
      )}
    </section>
  )
}

type EvidenceReasonListProps = {
  title: string
  emptyText: string
  items: string[]
}

function EvidenceReasonList({ title, emptyText, items }: EvidenceReasonListProps) {
  return (
    <div className="reasoning-reconstruction-section">
      <div className="reasoning-list-title">
        <strong>{title}</strong>
        <span>{items.length} items</span>
      </div>
      {items.length > 0 ? (
        <ul>
          {items.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      ) : (
        <p>{emptyText}</p>
      )}
    </div>
  )
}

type EvidenceListProps = {
  label: string
  title: string
  items: ReasoningReconstructionEvidence[]
  emptyText: string
}

function EvidenceList({ label, title, items, emptyText }: EvidenceListProps) {
  return (
    <div className="reasoning-graph-table" aria-label={label}>
      <div className="reasoning-table-title">{title}</div>
      {items.length > 0 ? (
        <>
          <div className="reasoning-table-header">
            <span>Item</span>
            <span>Summary</span>
            <span>Provenance</span>
          </div>
          {items.map((item) => (
            <div className="reasoning-table-row" key={`${item.kind}:${item.id}:${item.title}`}>
              <span>{`${item.kind} ${item.id}: ${item.title}`}</span>
              <span>{item.summary}</span>
              <span>{formatEvidenceProvenance(item)}</span>
            </div>
          ))}
        </>
      ) : (
        <p className="reasoning-empty-note">{emptyText}</p>
      )}
    </div>
  )
}

type ReasoningReconstructionHorizon = 'Decision' | 'Milestone' | 'Epic' | 'Project' | 'Multi-year'

type ParsedReconstructionDetails = {
  metadata: Array<{ label: string; value: string }>
  sections: Array<{ title: string; items: string[] }>
}

const reconstructionHorizons: ReasoningReconstructionHorizon[] = [
  'Decision',
  'Milestone',
  'Epic',
  'Project',
  'Multi-year',
]

const groupedSectionTitles = new Set(['Events', 'Relationships', 'External References', 'Threads'])

function parseReconstructionDetails(details: string): ParsedReconstructionDetails {
  const metadata: ParsedReconstructionDetails['metadata'] = []
  const sections: ParsedReconstructionDetails['sections'] = []
  let currentSection: ParsedReconstructionDetails['sections'][number] | null = null

  details
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .forEach((line) => {
      const sectionTitle = line.endsWith(':') ? line.slice(0, -1) : null
      if (sectionTitle && groupedSectionTitles.has(sectionTitle)) {
        currentSection = { title: sectionTitle, items: [] }
        sections.push(currentSection)
        return
      }

      if (currentSection && line.startsWith('- ')) {
        currentSection.items.push(line.slice(2))
        return
      }

      currentSection = null
      const separatorIndex = line.indexOf(':')
      if (separatorIndex > 0) {
        metadata.push({
          label: line.slice(0, separatorIndex),
          value: line.slice(separatorIndex + 1).trim(),
        })
      } else {
        metadata.push({ label: 'Detail', value: line })
      }
    })

  return { metadata, sections }
}

function horizonNarrative(
  horizon: ReasoningReconstructionHorizon,
  reconstruction: ReasoningReconstruction,
) {
  const eventCount = reconstruction.evidence.filter((item) => item.kind === 'Event').length
  const relationshipCount = reconstruction.evidence.filter(
    (item) => item.kind === 'Relationship' || item.kind === 'GraphRelationship',
  ).length
  const referenceCount = reconstruction.evidence.filter((item) => item.reference !== null).length

  return `${horizon} reconstruction uses ${eventCount} event evidence item(s), ${relationshipCount} relationship evidence item(s), and ${referenceCount} referenced source item(s) without promoting hypotheses, alternatives, contradictions, or direction into authority.`
}

function formatPresence(value: boolean) {
  return value ? 'Present' : 'Not present'
}

function formatReference(reference: ReasoningReference | null) {
  if (!reference) {
    return 'Not reported'
  }

  const qualifiers = [reference.relativePath, reference.section].filter(Boolean)
  return qualifiers.length > 0
    ? `${reference.kind} ${reference.id} (${qualifiers.join(' - ')})`
    : `${reference.kind} ${reference.id}`
}

function formatEvidenceProvenance(item: ReasoningReconstructionEvidence) {
  if (item.provenance) {
    return `${item.provenance.sourceKind} by ${item.provenance.capturedBy}`
  }

  if (item.reference) {
    return formatReference(item.reference)
  }

  return 'Derived graph edge'
}
