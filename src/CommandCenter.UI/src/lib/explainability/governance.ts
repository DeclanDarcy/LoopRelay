import type {
  CertificationFindingExplanation,
} from '../../components/explainability'
import type {
  DecisionSessionCertificationReport,
  DecisionSessionRecoveryDiagnostics,
  DecisionSessionRecoveryResult,
  DecisionSessionTransferEligibility,
  ExplanationAction,
  ExplanationDiagnostic,
  ExplanationEvidence,
  ExplanationHealthDimension,
  ExplanationTone,
  RepositoryDecisionSessionHealthDimension,
  WorkflowGovernanceHealthProjection,
} from '../../types'

function evidenceFromStrings(values: string[], detail?: string): ExplanationEvidence[] {
  return values.map((value) => ({ label: value, detail }))
}

function diagnosticsFromStrings(values: string[], label: string): ExplanationDiagnostic[] {
  return values.map((value) => ({ label, detail: value }))
}

function toneFromStatus(status: string): ExplanationTone {
  const normalized = status.toLowerCase()

  if (normalized.includes('healthy') || normalized.includes('eligible') || normalized.includes('pass')) {
    return 'success'
  }

  if (normalized.includes('error') || normalized.includes('fail') || normalized.includes('blocked')) {
    return 'danger'
  }

  if (normalized.includes('warn') || normalized.includes('defer') || normalized.includes('attention')) {
    return 'warning'
  }

  return 'neutral'
}

function toneFromSeverity(severity: string): ExplanationTone {
  const normalized = severity.toLowerCase()

  if (normalized.includes('error') || normalized.includes('critical')) {
    return 'danger'
  }

  if (normalized.includes('warn')) {
    return 'warning'
  }

  if (normalized.includes('info')) {
    return 'info'
  }

  return 'neutral'
}

export function governanceCertificationFindingsToExplanation(
  certification: DecisionSessionCertificationReport,
): CertificationFindingExplanation[] {
  return certification.result.findings.map((finding) => ({
    id: finding.id,
    title: `${finding.severity}: ${finding.message}`,
    category: finding.severity,
    passed: false,
    detail: finding.message,
    evidence: evidenceFromStrings(finding.evidence, 'Certification evidence'),
    diagnostics: [],
  }))
}

export function governanceCertificationDiagnosticsToExplanation(
  certification: DecisionSessionCertificationReport,
): ExplanationDiagnostic[] {
  return diagnosticsFromStrings(certification.result.diagnostics, 'Certification')
}

export function governanceRecoveryFindingsToDiagnostics(
  recovery: DecisionSessionRecoveryResult | null,
): ExplanationDiagnostic[] {
  return (recovery?.findings ?? []).map((finding) => ({
    label: finding.severity,
    detail: finding.message,
    tone: toneFromSeverity(finding.severity),
    evidence: [
      ...(finding.sessionId ? [{ label: finding.sessionId, detail: 'Session' }] : []),
      ...(finding.evidenceId ? [{ label: finding.evidenceId, detail: 'Evidence' }] : []),
    ],
  }))
}

export function governanceRecoveryDiagnosticsToExplanation(
  diagnostics: DecisionSessionRecoveryDiagnostics | null,
  recovery: DecisionSessionRecoveryResult | null = null,
): ExplanationDiagnostic[] {
  if (!diagnostics && !recovery) {
    return []
  }

  return [
    ...diagnosticsFromStrings(diagnostics?.registryDiagnostics.errors ?? [], 'Registry Error'),
    ...diagnosticsFromStrings(diagnostics?.registryDiagnostics.warnings ?? [], 'Registry Warning'),
    ...diagnosticsFromStrings(diagnostics?.warnings ?? [], 'Recovery Warning'),
    ...(diagnostics?.transferAssessments ?? []).map((assessment) => ({
      label: assessment.status,
      detail: assessment.message,
      evidence: [
        ...(assessment.transferId ? [{ label: assessment.transferId, detail: 'Transfer' }] : []),
        { label: assessment.sourceSessionId, detail: 'Source session' },
        ...(assessment.targetSessionId ? [{ label: assessment.targetSessionId, detail: 'Target session' }] : []),
        ...(assessment.continuityArtifactId
          ? [{ label: assessment.continuityArtifactId, detail: 'Continuity artifact' }]
          : []),
      ],
    })),
    ...(recovery?.events ?? []).flatMap((event) => [
      {
        label: event.eventType,
        detail: event.message,
        evidence: [{ label: event.eventId, detail: event.occurredAt }],
      },
      ...diagnosticsFromStrings(event.diagnostics, event.eventType),
    ]),
  ]
}

export function governanceHealthDimensionsToExplanation(
  dimensions: Array<WorkflowGovernanceHealthProjection | RepositoryDecisionSessionHealthDimension>,
): ExplanationHealthDimension[] {
  return dimensions.map((dimension) => {
    const evidence = 'evidence' in dimension ? dimension.evidence : []

    return {
      name: dimension.name,
      status: dimension.status,
      tone: toneFromStatus(dimension.status),
      reason: dimension.findings[0] ?? 'No health findings projected.',
      evidence: evidenceFromStrings(evidence, 'Health evidence'),
      diagnostics: diagnosticsFromStrings(dimension.findings, 'Health Finding'),
    }
  })
}

export function governanceEligibilityToActions(
  eligibility: DecisionSessionTransferEligibility,
): ExplanationAction[] {
  const eligible = eligibility.status === 'Eligible'

  return [
    {
      label: 'Execute transfer',
      detail: `Transfer eligibility status: ${eligibility.status}.`,
      eligible,
      reason: eligibility.policyEvaluation.reason,
      command: 'decision_session_transfer',
      constraints: eligibility.findings.map((finding) => ({
        label: finding.code,
        detail: `${finding.severity}: ${finding.message}`,
        satisfied: eligible,
      })),
    },
  ]
}

export function governanceEligibilityFindingsToDiagnostics(
  eligibility: DecisionSessionTransferEligibility,
): ExplanationDiagnostic[] {
  return eligibility.findings.map((finding) => ({
    label: finding.severity,
    detail: finding.message,
    tone: toneFromSeverity(finding.severity),
  }))
}
