import type { OperationalContextSemanticChange } from '../../types'

type OperationalContextSemanticChangeListProps = {
  semanticChanges: OperationalContextSemanticChange[]
  grouping?: 'category' | 'outcome'
  title?: string
  emptyText?: string
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

function getSemanticChangeOutcome(type: string) {
  if (type.includes('Modified') || type.includes('Changed')) {
    return 'Modified'
  }

  if (type.includes('Added')) {
    return 'Added'
  }

  if (type.includes('Removed')) {
    return 'Removed'
  }

  if (type.includes('Resolved') || type.includes('Retired')) {
    return 'Resolved'
  }

  if (type.includes('Lost')) {
    return 'Lost'
  }

  if (type.includes('Preserved') || type.includes('Retention')) {
    return 'Preserved'
  }

  return 'Other Changes'
}

export function OperationalContextSemanticChangeList({
  semanticChanges,
  grouping = 'category',
  title = 'Semantic Changes',
  emptyText = 'No coarse semantic changes detected.',
}: OperationalContextSemanticChangeListProps) {
  const groupedChanges = semanticChanges.reduce<Record<string, OperationalContextSemanticChange[]>>(
    (groups, change) => {
      const category =
        grouping === 'outcome' ? getSemanticChangeOutcome(change.type) : getSemanticChangeCategory(change.type)
      groups[category] = groups[category] ?? []
      groups[category].push(change)
      return groups
    },
    {},
  )

  return (
    <>
      <h5>{title}</h5>
      {semanticChanges.length === 0 ? (
        <p>{emptyText}</p>
      ) : (
        <div className="semantic-change-groups">
          {Object.entries(groupedChanges).map(([category, changes]) => (
            <div key={category}>
              <h6>{category}</h6>
              <ul>
                {changes.map((change, index) => (
                  <li className="semantic-change-item" key={`${change.type}-${change.itemId ?? index}`}>
                    <strong>
                      {change.type}: {change.description}
                    </strong>
                    <SemanticChangeMetadata change={change} />
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

function SemanticChangeMetadata({ change }: { change: OperationalContextSemanticChange }) {
  const metadata = [
    change.identityBasis ? ['Identity basis', change.identityBasis] : null,
    change.modificationReason ? ['Reason', change.modificationReason] : null,
    change.previousState ? ['Previous', change.previousState] : null,
    change.currentState ? ['Current', change.currentState] : null,
  ].filter((item): item is [string, string] => item !== null)

  return (
    <>
      {metadata.length > 0 ? (
        <dl className="semantic-change-metadata">
          {metadata.map(([label, value]) => (
            <div key={`${change.type}-${label}`}>
              <dt>{label}</dt>
              <dd>{value}</dd>
            </div>
          ))}
        </dl>
      ) : null}
      {change.supportingEvidence.length > 0 ? (
        <ul className="semantic-change-evidence" aria-label={`Supporting evidence for ${change.type}`}>
          {change.supportingEvidence.map((evidence) => (
            <li key={`${change.type}-${evidence}`}>{evidence}</li>
          ))}
        </ul>
      ) : null}
    </>
  )
}
