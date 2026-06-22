import type { OperationalContextSemanticChange } from '../../types'

type OperationalContextSemanticChangeListProps = {
  semanticChanges: OperationalContextSemanticChange[]
}

function getSemanticChangeCategory(type: string) {
  if (type.includes('Decision') || type.includes('Rationale')) {
    return 'Decisions'
  }

  if (type.includes('Constraint')) {
    return 'Constraints'
  }

  if (type.includes('Question')) {
    return 'Questions'
  }

  if (type.includes('Risk')) {
    return 'Risks'
  }

  if (type.includes('Section')) {
    return 'Sections'
  }

  if (type.includes('Preservation') || type.includes('Retention')) {
    return 'Preservation Warnings'
  }

  return 'Other Changes'
}

export function OperationalContextSemanticChangeList({
  semanticChanges,
}: OperationalContextSemanticChangeListProps) {
  const groupedChanges = semanticChanges.reduce<Record<string, OperationalContextSemanticChange[]>>(
    (groups, change) => {
      const category = getSemanticChangeCategory(change.type)
      groups[category] = groups[category] ?? []
      groups[category].push(change)
      return groups
    },
    {},
  )

  return (
    <>
      <h5>Semantic Changes</h5>
      {semanticChanges.length === 0 ? (
        <p>No coarse semantic changes detected.</p>
      ) : (
        <div className="semantic-change-groups">
          {Object.entries(groupedChanges).map(([category, changes]) => (
            <div key={category}>
              <h6>{category}</h6>
              <ul>
                {changes.map((change, index) => (
                  <li key={`${change.type}-${change.itemId ?? index}`}>
                    {change.type}: {change.description}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}
    </>
  )
}
