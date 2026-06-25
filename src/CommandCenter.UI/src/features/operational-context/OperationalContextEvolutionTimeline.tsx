import type { OperationalContextSemanticChange } from '../../types'

type OperationalContextEvolutionTimelineProps = {
  semanticChanges: OperationalContextSemanticChange[]
}

const semanticChangeOutcomes: Record<string, string> = {
  ItemAdded: 'Added',
  ConstraintAdded: 'Added',
  QuestionAdded: 'Added',
  RiskAdded: 'Added',
  DecisionAdded: 'Added',
  ImportantDecisionIntroduced: 'Added',
  ItemChanged: 'Modified',
  SectionChanged: 'Modified',
  RationaleChanged: 'Modified',
  ModifiedArchitecture: 'Modified',
  ModifiedConstraint: 'Modified',
  ModifiedWorkflow: 'Modified',
  ModifiedDecision: 'Modified',
  ModifiedUnderstanding: 'Modified',
  StableUnderstandingPreserved: 'Preserved',
  OpenDecisionPreserved: 'Preserved',
  ItemRemoved: 'Removed',
  ConstraintRemoved: 'Removed',
  QuestionRemoved: 'Removed',
  RiskRemoved: 'Removed',
  DecisionRemoved: 'Removed',
  DecisionRetired: 'Removed',
  RationaleLostWarning: 'Lost',
  LostUnderstanding: 'Lost',
  OpenQuestionResolved: 'Resolved',
  OpenDecisionResolved: 'Resolved',
  RiskRetired: 'Resolved',
  ResolvedUnderstanding: 'Resolved',
  DuplicateRemoved: 'Removed',
  TransientRemoved: 'Removed',
}

const outcomeOrder = ['Added', 'Modified', 'Removed', 'Preserved', 'Lost', 'Resolved', 'Other']

function getOutcome(change: OperationalContextSemanticChange) {
  return semanticChangeOutcomes[change.type] ?? 'Other'
}

export function OperationalContextEvolutionTimeline({
  semanticChanges,
}: OperationalContextEvolutionTimelineProps) {
  const timelineItems = semanticChanges.map((change, index) => ({
    change,
    outcome: getOutcome(change),
    key: `${change.type}-${change.itemId ?? index}`,
  }))
  const groupedItems = outcomeOrder
    .map((outcome) => ({
      outcome,
      items: timelineItems.filter((item) => item.outcome === outcome),
    }))
    .filter((group) => group.items.length > 0)

  return (
    <section className="operational-evolution-timeline" aria-label="Operational evolution timeline">
      <h5>Operational Evolution Timeline</h5>
      {timelineItems.length === 0 ? (
        <p>No operational evolution events detected.</p>
      ) : (
        <div className="operational-evolution-lanes">
          {groupedItems.map((group) => (
            <div className="operational-evolution-lane" key={group.outcome}>
              <h6>{group.outcome}</h6>
              <ol>
                {group.items.map(({ change, outcome, key }) => (
                  <li className="operational-evolution-item" data-outcome={outcome} key={key}>
                    <div className="operational-evolution-item-header">
                      <strong>{change.description}</strong>
                      <span>{change.type}</span>
                    </div>
                    <span className="operational-evolution-section">{change.section}</span>
                    <EvolutionFacts change={change} />
                  </li>
                ))}
              </ol>
            </div>
          ))}
        </div>
      )}
    </section>
  )
}

function EvolutionFacts({ change }: { change: OperationalContextSemanticChange }) {
  const facts = [
    change.previousState ? ['Previous state', change.previousState] : null,
    change.currentState ? ['Current state', change.currentState] : null,
    change.modificationReason ? ['Reason', change.modificationReason] : null,
    change.identityBasis ? ['Identity basis', change.identityBasis] : null,
    change.itemId ? ['Item id', change.itemId] : null,
  ].filter((fact): fact is [string, string] => fact !== null)

  return (
    <>
      {facts.length > 0 ? (
        <dl className="operational-evolution-facts">
          {facts.map(([label, value]) => (
            <div key={`${change.type}-${label}`}>
              <dt>{label}</dt>
              <dd>{value}</dd>
            </div>
          ))}
        </dl>
      ) : null}
      {change.supportingEvidence.length > 0 ? (
        <ul className="operational-evolution-evidence" aria-label={`Evidence for ${change.type}`}>
          {change.supportingEvidence.map((evidence) => (
            <li key={`${change.type}-${evidence}`}>{evidence}</li>
          ))}
        </ul>
      ) : null}
    </>
  )
}
