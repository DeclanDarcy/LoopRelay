import type {
  CertificationFindingExplanation,
} from '../../components/explainability'
import type {
  DecisionSessionCertificationReport,
  DecisionSessionRecoveryDiagnostics,
  DecisionSessionRecoveryResult,
  DecisionSessionTransfer,
  DecisionSessionTransferDiagnostics,
  DecisionSessionTransferEligibility,
  DecisionSessionContinuityArtifact,
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

export function governanceRecoveryToActions(
  diagnostics: DecisionSessionRecoveryDiagnostics | null,
  recovery: DecisionSessionRecoveryResult | null,
): ExplanationAction[] {
  const blockingFindings = recovery?.findings.filter((finding) => finding.severity.toLowerCase() === 'error') ?? []
  const interruptedTransfers = diagnostics?.transferAssessments.filter((assessment) => assessment.status !== 'Completed') ?? []
  const activeSessionCount = diagnostics?.registryDiagnostics.activeSessionCount ?? recovery?.activeSessionCount ?? null

  return [
    {
      label: 'Recover governance',
      detail: 'Rebuild governance recovery state from the decision-session registry and transfer evidence.',
      eligible: true,
      reason:
        blockingFindings.length > 0 || interruptedTransfers.length > 0 || (activeSessionCount !== null && activeSessionCount > 1)
          ? 'Recovery has projected registry or transfer issues to reconcile.'
          : 'Recovery can be rerun to verify governance state.',
      command: 'decision_session_recover',
      constraints: [
        {
          label: 'Registry active session count',
          detail: activeSessionCount === null ? 'Not projected' : `${activeSessionCount} active session(s) projected.`,
          satisfied: activeSessionCount === null || activeSessionCount <= 1,
        },
        {
          label: 'Interrupted transfers',
          detail: `${interruptedTransfers.length} interrupted transfer assessment(s) projected.`,
          satisfied: interruptedTransfers.length === 0,
        },
        ...blockingFindings.map((finding) => ({
          label: finding.code,
          detail: finding.message,
          satisfied: false,
        })),
      ],
    },
  ]
}

export function governanceRecoveryToEvidence(
  diagnostics: DecisionSessionRecoveryDiagnostics | null,
  recovery: DecisionSessionRecoveryResult | null,
): ExplanationEvidence[] {
  const evidence: ExplanationEvidence[] = []

  if (recovery) {
    evidence.push({
      id: recovery.recoveryId,
      label: 'Recovery run',
      detail: `${recovery.succeeded ? 'Succeeded' : 'Requires review'} | recovered ${recovery.recoveredAt}`,
    })

    if (recovery.activeSessionId) {
      evidence.push({
        id: `${recovery.recoveryId}-active-session`,
        label: 'Active session',
        detail: recovery.activeSessionId,
      })
    }

    evidence.push({
      id: `${recovery.recoveryId}-active-session-count`,
      label: 'Active sessions',
      detail: `${recovery.activeSessionCount} active session(s) after recovery.`,
    })

    for (const event of recovery.events.slice(0, 5)) {
      evidence.push({
        id: event.eventId,
        label: `Recovery event: ${event.eventType}`,
        detail: event.message,
      })
    }
  }

  if (diagnostics) {
    evidence.push({
      id: `${diagnostics.repositoryId}-recovery-diagnostics`,
      label: 'Recovery diagnostics',
      detail: `Generated ${diagnostics.generatedAt}`,
    })

    evidence.push({
      id: `${diagnostics.repositoryId}-registry-diagnostics`,
      label: 'Registry diagnostics',
      detail: `${diagnostics.registryDiagnostics.sessionCount} session(s), ${diagnostics.registryDiagnostics.activeSessionCount} active.`,
    })

    for (const assessment of diagnostics.transferAssessments.slice(0, 5)) {
      evidence.push({
        id: assessment.transferId ?? `${assessment.sourceSessionId}-transfer-assessment`,
        label: `Transfer assessment: ${assessment.status}`,
        detail: assessment.message,
      })
    }
  }

  return evidence
}

export function governanceRecoveryResult(
  diagnostics: DecisionSessionRecoveryDiagnostics | null,
  recovery: DecisionSessionRecoveryResult | null,
) {
  if (recovery?.succeeded) {
    return `Recovery ${recovery.recoveryId} completed.`
  }

  if (recovery) {
    return `Recovery ${recovery.recoveryId} requires review.`
  }

  if (diagnostics) {
    return 'Recovery diagnostics are projected and waiting for execution.'
  }

  return 'No recovery result has been projected.'
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

export function governanceTransferToEvidence({
  eligibility,
  transfers,
  transferDiagnostics,
  continuityArtifacts,
}: {
  eligibility: DecisionSessionTransferEligibility | null
  transfers: DecisionSessionTransfer[]
  transferDiagnostics: DecisionSessionTransferDiagnostics | null
  continuityArtifacts: DecisionSessionContinuityArtifact[]
}): ExplanationEvidence[] {
  const evidence: ExplanationEvidence[] = []
  const latestTransfer = transfers[0] ?? null

  if (eligibility) {
    evidence.push({
      id: `${eligibility.sourceSessionId ?? 'unknown'}-transfer-eligibility`,
      label: 'Transfer eligibility',
      detail: `${eligibility.status} | checked ${eligibility.checkedAt}`,
    })

    if (eligibility.sourceSessionId) {
      evidence.push({
        id: `${eligibility.sourceSessionId}-source-session`,
        label: 'Source session',
        detail: eligibility.sourceSessionId,
      })
    }
  }

  if (latestTransfer) {
    evidence.push(
      {
        id: latestTransfer.transferId,
        label: 'Latest transfer',
        detail: latestTransfer.succeeded ? 'Succeeded' : 'Pending or failed',
      },
      {
        id: `${latestTransfer.transferId}-ownership`,
        label: 'Ownership context',
        detail: `${latestTransfer.sourceSessionId} to ${latestTransfer.targetSessionId ?? 'not assigned'}`,
      },
    )

    if (latestTransfer.continuityArtifactId) {
      evidence.push({
        id: latestTransfer.continuityArtifactId,
        label: 'Continuity artifact',
        detail: latestTransfer.continuityArtifactId,
      })
    }
  }

  for (const artifact of continuityArtifacts.slice(0, 3)) {
    evidence.push({
      id: artifact.artifactId,
      label: 'Continuity readiness',
      detail: `${artifact.sourceSessionId} to ${artifact.targetSessionId ?? 'not assigned'} | fingerprint ${artifact.continuityFingerprint}`,
    })
  }

  for (const event of transferDiagnostics?.events ?? []) {
    evidence.push({
      id: event.eventId,
      label: `Transfer event: ${event.eventType}`,
      detail: event.message,
    })
  }

  return evidence
}

export function governanceTransferToDiagnostics({
  eligibility,
  transferDiagnostics,
  transfers,
}: {
  eligibility: DecisionSessionTransferEligibility | null
  transferDiagnostics: DecisionSessionTransferDiagnostics | null
  transfers: DecisionSessionTransfer[]
}): ExplanationDiagnostic[] {
  const diagnostics: ExplanationDiagnostic[] = [
    ...(eligibility ? governanceEligibilityFindingsToDiagnostics(eligibility) : []),
    ...diagnosticsFromStrings(transferDiagnostics?.warnings ?? [], 'Transfer Warning'),
  ]

  for (const transfer of transfers.slice(0, 3)) {
    diagnostics.push(...diagnosticsFromStrings(transfer.diagnostics, 'Transfer Diagnostic'))
    for (const event of transfer.events) {
      diagnostics.push(...diagnosticsFromStrings(event.diagnostics, event.eventType))
    }
  }

  return diagnostics
}

export function governanceTransferResult(
  eligibility: DecisionSessionTransferEligibility | null,
  transfers: DecisionSessionTransfer[],
) {
  const latestTransfer = transfers[0] ?? null

  if (latestTransfer?.succeeded) {
    return `Transfer ${latestTransfer.transferId} completed.`
  }

  if (latestTransfer) {
    return `Transfer ${latestTransfer.transferId} is pending or failed.`
  }

  if (eligibility?.status === 'Eligible') {
    return 'Transfer is eligible and waiting for execution.'
  }

  if (eligibility) {
    return `Transfer is ${eligibility.status.toLowerCase()}.`
  }

  return 'No transfer result has been projected.'
}
