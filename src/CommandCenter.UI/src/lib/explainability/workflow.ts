import type {
  CertificationFindingExplanation,
} from '../../components/explainability'
import type {
  ExplanationDiagnostic,
  ExplanationEvidence,
  ExplanationAction,
  ExplanationHealthDimension,
  ExplanationTone,
  HumanGovernanceReport,
  RepositoryWorkflowReport,
  WorkflowCertificationResult,
  WorkflowContinuationEvaluation,
  WorkflowGate,
  WorkflowGateCatalogProjection,
  WorkflowHealthAssessment,
  WorkflowProgressionReport,
  WorkflowReadinessReport,
  WorkflowRecoveryDiagnostics,
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

export function workflowRecoveryDiagnosticsToExplanation(
  recovery: WorkflowRecoveryDiagnostics,
): ExplanationDiagnostic[] {
  return diagnosticsFromStrings(recovery.diagnostics, 'Recovery')
}

export function workflowRecoveryArtifactsToEvidence(
  artifacts: string[],
  detail: string,
): ExplanationEvidence[] {
  return artifacts.map((artifact) => ({ label: artifact, detail }))
}

export function workflowGatesToActions(gates: WorkflowGateCatalogProjection): ExplanationAction[] {
  return gates.openGates.map((gate) => ({
    label: gate.type,
    detail: gate.requiredAction,
    eligible: false,
    reason: gate.reason,
    command: gate.satisfyingCommands[0] ?? gate.satisfyingCommand,
    constraints: [
      {
        label: gate.status,
        detail: gate.reason,
        satisfied: false,
        evidence: workflowGateEvidenceToExplanation(gate),
      },
    ],
  }))
}

export function workflowGateDiagnosticsToExplanation(
  gates: WorkflowGateCatalogProjection,
): ExplanationDiagnostic[] {
  return [
    ...diagnosticsFromStrings(gates.diagnostics.reasoning, 'Gate Reasoning'),
    ...diagnosticsFromStrings(gates.diagnostics.missingEvidence, 'Missing Evidence'),
    ...diagnosticsFromStrings(gates.diagnostics.conflicts, 'Conflict'),
  ]
}

export function workflowContinuationToActions(
  evaluation: WorkflowContinuationEvaluation,
): ExplanationAction[] {
  return [
    {
      label: evaluation.outcome,
      detail: evaluation.toStage
        ? `Advance from ${evaluation.fromStage} to ${evaluation.toStage}.`
        : `Remain at ${evaluation.fromStage}.`,
      eligible: evaluation.canAdvanceMechanically,
      reason: evaluation.stopReason || evaluation.transition?.reason || evaluation.completion.completionReason,
      command: null,
      constraints: [
        {
          label: evaluation.blockingGate,
          detail: evaluation.requiredHumanAction || 'No human action required.',
          satisfied: !evaluation.isWaitingForHuman,
        },
      ],
    },
  ]
}

export function workflowContinuationDiagnosticsToExplanation(
  evaluation: WorkflowContinuationEvaluation,
): ExplanationDiagnostic[] {
  return [
    ...diagnosticsFromStrings(evaluation.diagnostics.reasoning, 'Continuation'),
    ...diagnosticsFromStrings(evaluation.diagnostics.stateMachineReasoning, 'State Machine'),
    ...diagnosticsFromStrings(evaluation.diagnostics.gateReasoning, 'Gate'),
    ...diagnosticsFromStrings(evaluation.diagnostics.stopReasons, 'Stop Reason'),
    ...diagnosticsFromStrings(evaluation.diagnostics.conflicts, 'Conflict'),
  ]
}

export function workflowReportDiagnosticsToExplanation(
  repositoryReport: RepositoryWorkflowReport | null,
  progressionReport: WorkflowProgressionReport | null,
  humanGovernanceReport: HumanGovernanceReport | null,
  readinessReport: WorkflowReadinessReport | null,
): ExplanationDiagnostic[] {
  return [
    ...diagnosticsFromStrings(repositoryReport?.diagnostics ?? [], 'Repository Report'),
    ...diagnosticsFromStrings(progressionReport?.diagnostics ?? [], 'Progression Report'),
    ...diagnosticsFromStrings(humanGovernanceReport?.diagnostics ?? [], 'Human Governance Report'),
    ...diagnosticsFromStrings(readinessReport?.healthDiagnostics ?? [], 'Readiness Health'),
  ]
}

export function workflowReportEvidenceToExplanation(
  progressionReport: WorkflowProgressionReport | null,
  humanGovernanceReport: HumanGovernanceReport | null,
  readinessReport: WorkflowReadinessReport | null,
): ExplanationEvidence[] {
  return [
    ...(progressionReport?.continuationEvidence ?? []).map((value) => ({
      label: value,
      detail: 'Continuation evidence',
    })),
    ...(humanGovernanceReport?.authorityFindings ?? []).map((value) => ({
      label: value,
      detail: 'Authority finding',
    })),
    ...(readinessReport?.failedCertificationFindings ?? []).map((value) => ({
      label: value,
      detail: 'Failed certification finding',
    })),
  ]
}

function workflowGateEvidenceToExplanation(gate: WorkflowGate): ExplanationEvidence[] {
  return gate.evidence.map((evidence) => ({
    label: evidence.summary,
    source: evidence.sourceArtifact,
    detail: evidence.sourceDomain,
    fingerprint: evidence.fingerprint,
  }))
}
