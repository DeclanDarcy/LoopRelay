import { Panel, SectionHeader } from '../design'
import type { Explanation } from '../../types'
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
      <UncertaintyView uncertainty={explanation.uncertainty ?? []} />
      <DiagnosticList diagnostics={explanation.diagnostics ?? []} />
      <HealthView dimensions={explanation.healthDimensions ?? []} />
      <ActionEligibilityView actions={explanation.actions ?? []} />
    </Panel>
  )
}
