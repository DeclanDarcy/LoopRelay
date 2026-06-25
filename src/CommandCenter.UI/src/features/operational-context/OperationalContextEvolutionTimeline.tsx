import type { OperationalContextSemanticChange, OperationalEvolutionTimelineEntry } from '../../types'

type OperationalContextEvolutionTimelineProps = {
  semanticChanges?: OperationalContextSemanticChange[]
  timelineEntries?: OperationalEvolutionTimelineEntry[]
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
  semanticChanges = [],
  timelineEntries,
}: OperationalContextEvolutionTimelineProps) {
  const entries =
    timelineEntries ??
    semanticChanges.map((change): OperationalEvolutionTimelineEntry => ({
      outcome: getOutcome(change),
      semanticEventType: change.type,
      section: change.section,
      description: change.description,
      itemId: change.itemId,
      previousState: change.previousState,
      currentState: change.currentState,
      reason: change.modificationReason,
      identityBasis: change.identityBasis,
      supportingEvidence: change.supportingEvidence,
    }))
  const timelineItems = entries.map((entry, index) => ({
    entry,
    outcome: entry.outcome || 'Other',
    key: `${entry.semanticEventType}-${entry.itemId ?? index}`,
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
                {group.items.map(({ entry, outcome, key }) => (
                  <li className="operational-evolution-item" data-outcome={outcome} key={key}>
                    <div className="operational-evolution-item-header">
                      <strong>{entry.description}</strong>
                      <span>{entry.semanticEventType}</span>
                    </div>
                    <span className="operational-evolution-section">{entry.section}</span>
                    <EvolutionFacts entry={entry} />
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

function EvolutionFacts({ entry }: { entry: OperationalEvolutionTimelineEntry }) {
  const facts = [
    entry.previousState ? ['Previous state', entry.previousState] : null,
    entry.currentState ? ['Current state', entry.currentState] : null,
    entry.reason ? ['Reason', entry.reason] : null,
    entry.identityBasis ? ['Identity basis', entry.identityBasis] : null,
    entry.itemId ? ['Item id', entry.itemId] : null,
    entry.previousRevisionNumber ? ['Previous revision', String(entry.previousRevisionNumber)] : null,
    entry.currentRevisionNumber ? ['Current revision', String(entry.currentRevisionNumber)] : null,
  ].filter((fact): fact is [string, string] => fact !== null)

  return (
    <>
      {facts.length > 0 ? (
        <dl className="operational-evolution-facts">
          {facts.map(([label, value]) => (
            <div key={`${entry.semanticEventType}-${label}`}>
              <dt>{label}</dt>
              <dd>{value}</dd>
            </div>
          ))}
        </dl>
      ) : null}
      {entry.supportingEvidence.length > 0 ? (
        <ul className="operational-evolution-evidence" aria-label={`Evidence for ${entry.semanticEventType}`}>
          {entry.supportingEvidence.map((evidence) => (
            <li key={`${entry.semanticEventType}-${evidence}`}>{evidence}</li>
          ))}
        </ul>
      ) : null}
    </>
  )
}
