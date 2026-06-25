import { DiagnosticList } from '../../components/explainability'
import { reasoningDiagnosticGroupsToExplanation } from '../../lib/explainability'
import type { ReasoningDiagnosticGroup } from '../../types'

type ReasoningDiagnosticGroupsProps = {
  groups: ReasoningDiagnosticGroup[] | null | undefined
  label: string
}

export function ReasoningDiagnosticGroups({ groups, label }: ReasoningDiagnosticGroupsProps) {
  const visibleGroups = (groups ?? []).filter((group) => group.diagnostics.length > 0)
  if (visibleGroups.length === 0) {
    return null
  }

  return (
    <div aria-label={label}>
      <DiagnosticList
        title="Grouped Diagnostics"
        diagnostics={reasoningDiagnosticGroupsToExplanation(visibleGroups)}
      />
    </div>
  )
}
