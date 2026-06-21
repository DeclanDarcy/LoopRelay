import type { OperationalContextSemanticChange } from '../../types'

type OperationalContextSemanticChangeListProps = {
  semanticChanges: OperationalContextSemanticChange[]
}

export function OperationalContextSemanticChangeList({
  semanticChanges,
}: OperationalContextSemanticChangeListProps) {
  return (
    <>
      <h5>Semantic Changes</h5>
      {semanticChanges.length === 0 ? (
        <p>No coarse semantic changes detected.</p>
      ) : (
        <ul>
          {semanticChanges.map((change, index) => (
            <li key={`${change.type}-${change.itemId ?? index}`}>
              {change.type}: {change.description}
            </li>
          ))}
        </ul>
      )}
    </>
  )
}
