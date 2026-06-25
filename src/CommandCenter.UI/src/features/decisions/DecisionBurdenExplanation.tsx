import { DecisionBasis } from '../../components/explainability'
import {
  humanAuthoringBurdenExplanationToExplanation,
} from '../../lib/explainability'
import type { HumanAuthoringBurdenExplanation } from '../../types'

export function DecisionBurdenExplanation({
  explanation,
}: {
  explanation: HumanAuthoringBurdenExplanation
}) {
  return (
    <article className="decision-inspection-card" aria-label={`Burden explanation for ${explanation.decisionId}`}>
      <DecisionBasis explanation={humanAuthoringBurdenExplanationToExplanation(explanation)} />
    </article>
  )
}
