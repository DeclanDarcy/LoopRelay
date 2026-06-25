import { EmptyState } from '../../components/design'
import type { DecisionGenerationDiagnostics } from '../../types'
import { DecisionEvidenceBlock } from './DecisionEvidenceFragments'

export function DecisionRejectedOptionList({
  diagnostics,
}: {
  diagnostics: DecisionGenerationDiagnostics | null | undefined
}) {
  const rejectedOptions = diagnostics?.rejectedOptions ?? []
  const deduplicatedOptions = diagnostics?.deduplicatedOptions ?? []
  const invalidResults = diagnostics?.optionValidationResults.filter((result) => !result.isValid) ?? []

  return (
    <section className="decision-inspection-list" aria-label="Rejected and hidden proposal options">
      <h6>Rejected And Hidden Options</h6>
      {rejectedOptions.length === 0 && deduplicatedOptions.length === 0 && invalidResults.length === 0 ? (
        <EmptyState className="empty-state">
          No rejected, deduplicated, or invalid backend options are attached to this proposal.
        </EmptyState>
      ) : null}
      <OptionBucket title="Rejected options" options={rejectedOptions} />
      <OptionBucket title="Deduplicated options" options={deduplicatedOptions} />
      {invalidResults.length > 0 ? (
        <div className="decision-inspection-list" aria-label="Invalid option validation results">
          {invalidResults.map((result) => (
            <article className="decision-inspection-card" key={result.optionId}>
              <div>
                <span>Invalid option</span>
                <strong>{result.optionId}</strong>
              </div>
              <div className="decision-warning-list" aria-label={`Validation issues for ${result.optionId}`}>
                {result.issues.map((issue) => (
                  <span key={`${result.optionId}-${issue.type}-${issue.message}`}>
                    {issue.type}: {issue.message}
                  </span>
                ))}
              </div>
            </article>
          ))}
        </div>
      ) : null}
    </section>
  )
}

function OptionBucket({
  title,
  options,
}: {
  title: string
  options: NonNullable<DecisionGenerationDiagnostics['rejectedOptions']>
}) {
  if (options.length === 0) {
    return null
  }

  return (
    <div className="decision-option-grid" aria-label={title}>
      {options.map((option) => (
        <article className="decision-inspection-card" key={`${title}-${option.id}`}>
          <div>
            <span>{option.id}</span>
            <strong>{option.title}</strong>
          </div>
          <p>{option.description}</p>
          {option.diagnostics?.length ? (
            <div className="decision-warning-list" aria-label={`Diagnostics for ${option.id}`}>
              {option.diagnostics.map((diagnostic) => (
                <span key={diagnostic}>{diagnostic}</span>
              ))}
            </div>
          ) : null}
          <DecisionEvidenceBlock title={`${option.id} Evidence`} evidence={option.evidence} />
        </article>
      ))}
    </div>
  )
}
