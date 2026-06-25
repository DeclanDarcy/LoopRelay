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
    <div className="reasoning-diagnostics" aria-label={label}>
      <h5>Grouped Diagnostics</h5>
      {visibleGroups.map((group) => (
        <article className="reasoning-reconstruction-section" key={`${group.category}:${group.title ?? ''}`}>
          <div className="reasoning-list-title">
            <strong>{group.title ?? group.category}</strong>
            <span>{group.category}</span>
          </div>
          <ul>
            {group.diagnostics.map((diagnostic) => (
              <li key={diagnostic}>{diagnostic}</li>
            ))}
          </ul>
        </article>
      ))}
    </div>
  )
}
