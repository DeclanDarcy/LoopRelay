import { useMemo, useState } from 'react'
import { DiagnosticList, EvidenceList, UncertaintyView } from '../../components/explainability'
import {
  reasoningDiagnosticsToExplanation,
  reasoningReconstructionConfidenceToDiagnostics,
  reasoningReconstructionConfidenceToUncertainty,
  reasoningReconstructionEvidenceToEvidence,
  reasoningReconstructionScopeToEvidence,
} from '../../lib/explainability'
import type { ReasoningReconstruction } from '../../types'
import { ReasoningDiagnosticGroups } from './ReasoningDiagnosticGroups'

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
            <DiagnosticList
              title="Confidence Diagnostics"
              diagnostics={reasoningReconstructionConfidenceToDiagnostics(reconstruction.confidenceRationale)}
            />
            <UncertaintyView
              title="Confidence Uncertainty"
              uncertainty={reasoningReconstructionConfidenceToUncertainty(reconstruction.confidenceRationale)}
              emptyLabel="No missing evidence reported. No confidence blockers reported."
            />
          </section>

          <section className="reasoning-transparency-panel" aria-label="Reconstruction scope">
            <div className="decision-panel-heading">
              <h5>Scope</h5>
              <span>{reconstruction.scope.direction}</span>
            </div>
            <EvidenceList
              title="Scope Evidence"
              evidence={reasoningReconstructionScopeToEvidence(reconstruction)}
            />
            <div aria-label="Reachable reconstruction evidence">
              <EvidenceList
                title="Reachable Evidence"
                evidence={reasoningReconstructionEvidenceToEvidence(reconstruction.scope.reachableEvidence)}
                emptyLabel="No scoped reachable evidence reported."
              />
            </div>
            <div aria-label="Known unreachable reconstruction evidence">
              <EvidenceList
                title="Known Unreachable Evidence"
                evidence={reasoningReconstructionEvidenceToEvidence(reconstruction.scope.unreachableEvidence)}
                emptyLabel="No known unreachable evidence reported."
              />
            </div>
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

          <div aria-label="Reconstruction evidence">
            <EvidenceList
              title="Reconstruction Evidence"
              evidence={reasoningReconstructionEvidenceToEvidence(reconstruction.evidence)}
            />
          </div>

          <ReasoningDiagnosticGroups
            groups={reconstruction.diagnosticGroups}
            label="Grouped reconstruction diagnostics"
          />

          {(!reconstruction.diagnosticGroups?.length && reconstruction.diagnostics.length > 0) ? (
            <DiagnosticList
              diagnostics={reasoningDiagnosticsToExplanation(reconstruction.diagnostics)}
              title="Reconstruction Diagnostics"
            />
          ) : null}
        </div>
      ) : (
        <p className="empty-state compact">Run a reasoning query to reconstruct its explanation.</p>
      )}
    </section>
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
