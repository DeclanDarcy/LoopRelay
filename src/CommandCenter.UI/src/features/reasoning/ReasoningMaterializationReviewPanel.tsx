import type {
  ReasoningConceptMaterializationReview,
  ReasoningMaterializationReviewReport,
  ReasoningMaterializationOutcome,
} from '../../types'

type ReasoningMaterializationReviewPanelProps = {
  review: ReasoningMaterializationReviewReport | null
  isLoading: boolean
  isRunning: boolean
  error: string | null
  onRunReview: () => void
}

export function ReasoningMaterializationReviewPanel({
  review,
  isLoading,
  isRunning,
  error,
  onRunReview,
}: ReasoningMaterializationReviewPanelProps) {
  return (
    <section
      className="reasoning-panel reasoning-materialization-review-panel"
      id="reasoning-materialization-review"
      aria-label="Reasoning materialization review"
    >
      <div className="decision-panel-heading">
        <h5>Materialization Review</h5>
        <span>{review ? `Generated ${formatTimestamp(review.generatedAt)}` : 'Advisory'}</span>
      </div>

      <div className="reasoning-derived-status" aria-label="Reasoning materialization authority">
        <strong>Architecture review</strong>
        <span>Advisory only</span>
        <span>No artifact approval workflow</span>
      </div>

      <div className="decision-form-actions">
        <button
          type="button"
          className="secondary-action"
          onClick={onRunReview}
          disabled={isLoading || isRunning}
        >
          {isRunning ? 'Reviewing...' : 'Run Review'}
        </button>
      </div>

      {error ? <p className="notice error">{error}</p> : null}

      {review ? (
        <>
          <div className="reasoning-materialization-grid">
            {review.concepts.map((concept) => (
              <ConceptReviewCard concept={concept} key={concept.concept} />
            ))}
          </div>

          {review.taxonomyFindings.length > 0 ? (
            <div className="reasoning-diagnostics" aria-label="Materialization taxonomy findings">
              <strong>Taxonomy findings</strong>
              {review.taxonomyFindings.map((finding) => (
                <p key={finding.family}>
                  {finding.family}: {finding.summary} ({finding.eventTypeCount} event types)
                </p>
              ))}
            </div>
          ) : null}

          {review.diagnostics.length > 0 ? (
            <div className="reasoning-diagnostics" aria-label="Materialization diagnostics">
              {review.diagnostics.map((diagnostic) => (
                <p key={diagnostic}>{diagnostic}</p>
              ))}
            </div>
          ) : null}
        </>
      ) : (
        <p className="empty-state compact">
          {isLoading ? 'Loading materialization review...' : 'No materialization review available.'}
        </p>
      )}
    </section>
  )
}

function ConceptReviewCard({ concept }: { concept: ReasoningConceptMaterializationReview }) {
  return (
    <article className="reasoning-materialization-card">
      <div className="reasoning-event-heading">
        <strong>{formatConcept(concept.concept)}</strong>
        <span>{formatOutcome(concept.recommendation)}</span>
      </div>
      <p>{concept.summary}</p>
      <dl className="reasoning-provenance">
        <div>
          <dt>Evidence</dt>
          <dd>{concept.evidence.length > 0 ? concept.evidence.join(' / ') : 'Evidence insufficient'}</dd>
        </div>
        <div>
          <dt>Risk</dt>
          <dd>{concept.risks.length > 0 ? concept.risks.join(' / ') : riskFor(concept.recommendation)}</dd>
        </div>
      </dl>
    </article>
  )
}

function formatConcept(value: string) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function formatOutcome(value: ReasoningMaterializationOutcome) {
  switch (value) {
    case 'RemainDerived':
      return 'Derived remains sufficient'
    case 'AddDerivedCache':
      return 'Materialization pressure observed'
    case 'AddReadModelReport':
      return 'Further review recommended'
    case 'PromoteToFirstClassEntity':
      return 'Evidence requires decision review'
    case 'RejectConcept':
      return 'Evidence insufficient'
    default:
      return value
  }
}

function riskFor(value: ReasoningMaterializationOutcome) {
  if (value === 'RemainDerived') {
    return 'Premature entity persistence would imply authority the reasoning domain does not own.'
  }

  return 'Any stronger persistence still requires Decision Lifecycle adoption before authority changes.'
}

function formatTimestamp(value: string) {
  return new Date(value).toLocaleString()
}
