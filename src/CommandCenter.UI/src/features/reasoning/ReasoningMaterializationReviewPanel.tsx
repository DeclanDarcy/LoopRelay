import { ConstraintViewer, DiagnosticList, EvidenceList } from '../../components/explainability'
import {
  reasoningDiagnosticsToExplanation,
  reasoningMaterializationConceptToConstraints,
  reasoningMaterializationConceptToDiagnostics,
  reasoningMaterializationConceptToEvidence,
  reasoningTaxonomyFindingsToDiagnostics,
} from '../../lib/explainability'
import type {
  ReasoningConceptMaterializationReview,
  ReasoningMaterializationReviewReport,
  ReasoningMaterializationOutcome,
} from '../../types'
import { ReasoningDiagnosticGroups } from './ReasoningDiagnosticGroups'

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
            <div aria-label="Materialization taxonomy findings">
              <DiagnosticList
                title="Taxonomy Findings"
                diagnostics={reasoningTaxonomyFindingsToDiagnostics(review.taxonomyFindings)}
              />
            </div>
          ) : null}

          <ReasoningDiagnosticGroups
            groups={review.diagnosticGroups}
            label="Grouped materialization diagnostics"
          />

          {(!review.diagnosticGroups?.length && review.diagnostics.length > 0) ? (
            <DiagnosticList
              title="Materialization Diagnostics"
              diagnostics={reasoningDiagnosticsToExplanation(review.diagnostics)}
            />
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
      <div className="reasoning-derived-status" aria-label={`${concept.concept} materialization threshold basis`}>
        <strong>{concept.recommendation}</strong>
        <span>{concept.branchReason}</span>
        <span>
          Failed scenarios {concept.failedScenarioCount}/{concept.failedScenarioThreshold}
        </span>
        <span>
          Repeated workflow {concept.repeatedWorkflowCount}/{concept.repeatedWorkflowThreshold}
        </span>
      </div>
      <EvidenceList
        title={`${concept.concept} Evidence`}
        evidence={reasoningMaterializationConceptToEvidence(concept)}
        emptyLabel="Evidence insufficient"
      />
      <ConstraintViewer
        title={`${concept.concept} Thresholds`}
        constraints={reasoningMaterializationConceptToConstraints(concept)}
      />
      <DiagnosticList
        title={`${concept.concept} Materialization Diagnostics`}
        diagnostics={reasoningMaterializationConceptToDiagnostics(concept)}
      />
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

function formatTimestamp(value: string) {
  return new Date(value).toLocaleString()
}
