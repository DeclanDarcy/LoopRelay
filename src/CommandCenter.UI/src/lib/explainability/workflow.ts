import type {
  CertificationFindingExplanation,
} from '../../components/explainability'
import type {
  ExplanationDiagnostic,
  ExplanationEvidence,
  ExplanationHealthDimension,
  ExplanationTone,
  WorkflowCertificationResult,
  WorkflowHealthAssessment,
} from '../../types'

function evidenceFromStrings(values: string[]): ExplanationEvidence[] {
  return values.map((value) => ({ label: value }))
}

function diagnosticsFromStrings(values: string[], label = 'Diagnostic'): ExplanationDiagnostic[] {
  return values.map((value) => ({ label, detail: value }))
}

function toneFromStatus(status: string): ExplanationTone {
  const normalized = status.toLowerCase()

  if (normalized.includes('pass') || normalized.includes('certif') || normalized.includes('healthy')) {
    return 'success'
  }

  if (normalized.includes('fail') || normalized.includes('error') || normalized.includes('blocked')) {
    return 'danger'
  }

  if (normalized.includes('warn') || normalized.includes('attention') || normalized.includes('pending')) {
    return 'warning'
  }

  return 'neutral'
}

export function workflowHealthDimensionsToExplanation(
  health: WorkflowHealthAssessment,
): ExplanationHealthDimension[] {
  return health.dimensions.map((dimension) => ({
    name: dimension.name,
    status: dimension.status,
    tone: toneFromStatus(dimension.status),
    reason: dimension.reason,
    evidence: evidenceFromStrings(dimension.evidence),
    diagnostics: diagnosticsFromStrings(dimension.diagnostics),
  }))
}

export function workflowCertificationFindingsToExplanation(
  certification: WorkflowCertificationResult,
): CertificationFindingExplanation[] {
  return certification.findings.map((finding) => ({
    id: finding.id,
    title: finding.summary,
    category: finding.category,
    passed: finding.passed,
    detail: finding.detail,
    evidence: evidenceFromStrings(finding.evidence),
    diagnostics: diagnosticsFromStrings(finding.diagnostics),
  }))
}

export function workflowDiagnosticsToExplanation(values: string[]): ExplanationDiagnostic[] {
  return diagnosticsFromStrings(values)
}
