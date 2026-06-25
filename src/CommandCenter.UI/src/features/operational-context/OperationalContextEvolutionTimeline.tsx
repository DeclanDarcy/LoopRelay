import { EvidenceList } from '../../components/explainability'
import { operationalEvolutionTimelineEntryToEvidence } from '../../lib/explainability'
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
  const evidence = operationalEvolutionTimelineEntryToEvidence(entry)

  return evidence.length > 0 ? <EvidenceList evidence={evidence} title={`Evidence for ${entry.semanticEventType}`} /> : null
}
