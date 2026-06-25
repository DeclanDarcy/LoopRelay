import { EmptyState } from '../../components/design'
import { AlternativeExplorer, DiagnosticList } from '../../components/explainability'
import {
  decisionDiagnosticsToExplanation,
  decisionGenerationDiagnosticsToRejectedOptionDiagnostics,
  decisionOptionsToAlternatives,
} from '../../lib/explainability'
import type { DecisionGenerationDiagnostics } from '../../types'

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
        <DiagnosticList
          title="Invalid Option Validation Results"
          diagnostics={decisionGenerationDiagnosticsToRejectedOptionDiagnostics(diagnostics!)}
        />
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
      <AlternativeExplorer title={title} alternatives={decisionOptionsToAlternatives(options, title)} />
      <DiagnosticList
        title={`${title} Diagnostics`}
        diagnostics={options.flatMap((option) =>
          decisionDiagnosticsToExplanation(option.diagnostics ?? [], `Diagnostics for ${option.id}`),
        )}
      />
    </div>
  )
}
