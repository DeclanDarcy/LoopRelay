export type ExplanationTone = 'neutral' | 'success' | 'warning' | 'danger' | 'info'

export type ExplanationEvidence = {
  id?: string
  label: string
  detail?: string | null
  source?: string | null
  fingerprint?: string | null
}

export type ExplanationConstraint = {
  label: string
  detail: string
  satisfied?: boolean | null
  evidence?: ExplanationEvidence[]
}

export type ExplanationAlternative = {
  label: string
  detail: string
  selected?: boolean
  reason?: string | null
  evidence?: ExplanationEvidence[]
}

export type ExplanationAssumption = {
  label: string
  detail: string
  evidence?: ExplanationEvidence[]
}

export type ExplanationDiagnostic = {
  label: string
  detail: string
  tone?: ExplanationTone
  evidence?: ExplanationEvidence[]
}

export type ExplanationUncertainty = {
  label: string
  detail: string
  severity?: ExplanationTone
  missingEvidence?: ExplanationEvidence[]
}

export type ExplanationRecommendation = {
  label: string
  detail: string
  evidence?: ExplanationEvidence[]
}

export type ExplanationAction = {
  label: string
  detail: string
  eligible: boolean
  reason?: string | null
  command?: string | null
  constraints?: ExplanationConstraint[]
}

export type ExplanationHealthDimension = {
  name: string
  status: string
  tone?: ExplanationTone
  reason: string
  evidence: ExplanationEvidence[]
  diagnostics: ExplanationDiagnostic[]
}

export type Explanation = {
  id?: string
  domain: string
  title: string
  summary: string
  why?: string | null
  evidence?: ExplanationEvidence[]
  constraints?: ExplanationConstraint[]
  alternatives?: ExplanationAlternative[]
  assumptions?: ExplanationAssumption[]
  diagnostics?: ExplanationDiagnostic[]
  uncertainty?: ExplanationUncertainty[]
  recommendations?: ExplanationRecommendation[]
  actions?: ExplanationAction[]
  healthDimensions?: ExplanationHealthDimension[]
}
