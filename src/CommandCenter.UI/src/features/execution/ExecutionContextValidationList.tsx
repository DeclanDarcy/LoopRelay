import type { ExecutionGovernedConflictDiagnostic } from '../../types'
import { DiagnosticList } from '../../components/explainability'
import { executionGovernedConflictsToDiagnostics } from '../../lib/explainability'

type ExecutionContextValidationListProps = {
  validationErrors: string[]
  governedConflicts?: ExecutionGovernedConflictDiagnostic[]
}

export function ExecutionContextValidationList({
  validationErrors,
  governedConflicts = [],
}: ExecutionContextValidationListProps) {
  if (validationErrors.length === 0 && governedConflicts.length === 0) {
    return <p>No validation errors</p>
  }

  return (
    <div className="execution-validation-list">
      {governedConflicts.length > 0 ? (
        <div className="execution-governed-conflicts" aria-label="Governed decision conflict diagnostics">
          <DiagnosticList
            diagnostics={executionGovernedConflictsToDiagnostics(governedConflicts)}
            title="Governed Conflict Diagnostics"
          />
          {governedConflicts.map((conflict) => (
            <article key={conflict.id} className="execution-governed-conflict">
              <div className="execution-governed-conflict-heading">
                <strong>{conflict.decisionId}</strong>
                <span>{conflict.severity}</span>
              </div>
              <p>{conflict.conflictReason}</p>
              <dl>
                <div>
                  <dt>Conflicting excerpt</dt>
                  <dd>{conflict.conflictingExcerpt}</dd>
                </div>
                <div>
                  <dt>Affected context</dt>
                  <dd>{conflict.affectedContext}</dd>
                </div>
                <div>
                  <dt>Affected prompt</dt>
                  <dd>{conflict.affectedPromptSection}</dd>
                </div>
                <div>
                  <dt>Resolution path</dt>
                  <dd>{conflict.recommendedResolution}</dd>
                </div>
                <div>
                  <dt>Authority</dt>
                  <dd>{conflict.originatingAuthority}</dd>
                </div>
              </dl>
              {conflict.evidence.length > 0 ? (
                <ul aria-label={`Evidence for ${conflict.decisionId}`}>
                  {conflict.evidence.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              ) : null}
            </article>
          ))}
        </div>
      ) : null}
      {validationErrors.length > 0 ? (
        <ul>
          {validationErrors.map((validationError, index) => (
            <li key={`${index}:${validationError}`}>{validationError}</li>
          ))}
        </ul>
      ) : null}
    </div>
  )
}
