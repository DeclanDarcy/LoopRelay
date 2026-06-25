import { Panel, SectionHeader } from '../design'
import type { Explanation, ExplanationAssumption, ExplanationRecommendation } from '../../types'
import { ActionEligibilityView } from './ActionEligibilityView'
import { AlternativeExplorer } from './AlternativeExplorer'
import { ConstraintViewer } from './ConstraintViewer'
import { DiagnosticList } from './DiagnosticList'
import { EvidenceList } from './EvidenceList'
import { HealthView } from './HealthView'
import { UncertaintyView } from './UncertaintyView'

type DecisionBasisProps = {
  explanation: Explanation
}

export function DecisionBasis({ explanation }: DecisionBasisProps) {
  return (
    <Panel className="explainability-basis" aria-label={`${explanation.domain} explanation`}>
      <SectionHeader eyebrow={explanation.domain} title={explanation.title} headingLevel={4} />
      <p className="explainability-summary">{explanation.summary}</p>
      {explanation.why ? <p className="explainability-why">Why: {explanation.why}</p> : null}
      <EvidenceList evidence={explanation.evidence ?? []} />
      <AlternativeExplorer alternatives={explanation.alternatives ?? []} />
      <ConstraintViewer constraints={explanation.constraints ?? []} />
      <AssumptionList assumptions={explanation.assumptions ?? []} />
      <RecommendationList recommendations={explanation.recommendations ?? []} />
      <UncertaintyView uncertainty={explanation.uncertainty ?? []} />
      <DiagnosticList diagnostics={explanation.diagnostics ?? []} />
      <HealthView dimensions={explanation.healthDimensions ?? []} />
      <ActionEligibilityView actions={explanation.actions ?? []} />
    </Panel>
  )
}

function AssumptionList({ assumptions }: { assumptions: ExplanationAssumption[] }) {
  return (
    <div className="explainability-list explainability-assumption-list">
      <h5>Assumptions</h5>
      {assumptions.length === 0 ? (
        <p className="cc-empty-state empty-state">No assumptions projected.</p>
      ) : (
        <ul>
          {assumptions.map((assumption, index) => (
            <li key={`${assumption.label}-${index}`}>
              <strong>{assumption.label}</strong>
              <span>{assumption.detail}</span>
              {assumption.evidence?.length ? <EvidenceList evidence={assumption.evidence} title="Assumption Evidence" /> : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function RecommendationList({ recommendations }: { recommendations: ExplanationRecommendation[] }) {
  return (
    <div className="explainability-list explainability-recommendation-list">
      <h5>Recommendations</h5>
      {recommendations.length === 0 ? (
        <p className="cc-empty-state empty-state">No recommendations projected.</p>
      ) : (
        <ul>
          {recommendations.map((recommendation, index) => (
            <li key={`${recommendation.label}-${index}`}>
              <strong>{recommendation.label}</strong>
              <span>{recommendation.detail}</span>
              {recommendation.evidence?.length ? (
                <EvidenceList evidence={recommendation.evidence} title="Recommendation Evidence" />
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
