import { EvidenceList } from '../../components/explainability'
import { operationalContextSemanticChangeToEvidence } from '../../lib/explainability'
import type { OperationalContextSemanticChange } from '../../types'

type OperationalContextSemanticChangeListProps = {
  semanticChanges: OperationalContextSemanticChange[]
  grouping?: 'category' | 'outcome'
  title?: string
  emptyText?: string
}

const semanticChangeCategories: Record<string, string> = {
  ModifiedArchitecture: 'Architecture',
  ModifiedConstraint: 'Constraints',
  ModifiedWorkflow: 'Workflow',
  ModifiedDecision: 'Decisions',
  ModifiedUnderstanding: 'Understanding',
  LostUnderstanding: 'Understanding',
  ResolvedUnderstanding: 'Understanding',
  DuplicateRemoved: 'Compression',
  TransientRemoved: 'Compression',
}

const semanticChangeOutcomes: Record<string, string> = {
  ModifiedArchitecture: 'Modified',
  ModifiedConstraint: 'Modified',
  ModifiedWorkflow: 'Modified',
  ModifiedDecision: 'Modified',
  ModifiedUnderstanding: 'Modified',
  LostUnderstanding: 'Lost',
  ResolvedUnderstanding: 'Resolved',
  DuplicateRemoved: 'Removed',
  TransientRemoved: 'Removed',
}

function getSemanticChangeCategory(type: string) {
  const category = semanticChangeCategories[type]
  if (category) {
    return category
  }

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
  const outcome = semanticChangeOutcomes[type]
  if (outcome) {
    return outcome
  }

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
  const modificationChanges = semanticChanges.filter(isModificationChange)
  const remainingChanges = semanticChanges.filter((change) => !isModificationChange(change))
  const groupedChanges = remainingChanges.reduce<Record<string, OperationalContextSemanticChange[]>>(
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
          {modificationChanges.length > 0 ? (
            <div>
              <h6>Modified</h6>
              <ul>
                {modificationChanges.map((change, index) => (
                  <SemanticChangeItem change={change} key={`${change.type}-${change.itemId ?? index}`} />
                ))}
              </ul>
            </div>
          ) : null}
          {Object.entries(groupedChanges).map(([category, changes]) => (
            <div key={category}>
              <h6>{category}</h6>
              <ul>
                {changes.map((change, index) => (
                  <SemanticChangeItem change={change} key={`${change.type}-${change.itemId ?? index}`} />
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}
    </>
  )
}

function SemanticChangeItem({
  change,
}: {
  change: OperationalContextSemanticChange
}) {
  return (
    <li className="semantic-change-item">
      <strong>
        {change.type}: {change.description}
      </strong>
      <SemanticChangeMetadata change={change} />
    </li>
  )
}

function SemanticChangeMetadata({ change }: { change: OperationalContextSemanticChange }) {
  const evidence = operationalContextSemanticChangeToEvidence(change)

  return evidence.length > 0 ? (
    <EvidenceList evidence={evidence} title={`Supporting evidence for ${change.type}`} />
  ) : null
}

function isModificationChange(change: OperationalContextSemanticChange) {
  return (
    change.type.includes('Modified') ||
    change.type.includes('Changed') ||
    change.previousState !== null ||
    change.currentState !== null
  )
}
