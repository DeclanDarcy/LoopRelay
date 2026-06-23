import type { ReasoningReconstruction } from '../../types'

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
            <span>{reconstruction.confidence} confidence</span>
            <span>{reconstruction.trace.nodes.length} trace nodes</span>
            <span>{reconstruction.trace.relationships.length} trace relationships</span>
            <span>{reconstruction.evidence.length} evidence items</span>
          </div>

          <article className="reasoning-event-row">
            <strong>{reconstruction.narrative.summary}</strong>
            <p>{reconstruction.narrative.details}</p>
          </article>

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
