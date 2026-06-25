import type { DecisionEvidence, DecisionSourceReference } from '../../types'
import { EvidenceList } from '../../components/explainability'
import {
  decisionEvidenceToEvidence,
  decisionSourceReferencesToEvidence,
} from '../../lib/explainability'

export function DecisionEvidenceBlock({ title, evidence }: { title: string; evidence: DecisionEvidence[] }) {
  if (evidence.length === 0) {
    return null
  }

  return (
    <div aria-label={title}>
      <EvidenceList evidence={decisionEvidenceToEvidence(evidence, title)} title={title} />
    </div>
  )
}

export function DecisionSourceList({ sources }: { sources: DecisionSourceReference[] }) {
  if (sources.length === 0) {
    return null
  }

  return (
    <div aria-label="Source attribution">
      <EvidenceList evidence={decisionSourceReferencesToEvidence(sources)} title="Source attribution" />
    </div>
  )
}
