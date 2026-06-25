import { describe, expect, it } from 'vitest'
import {
  governanceAnalysisWarningsToDiagnostics,
  governanceCertificationDiagnosticsToExplanation,
  governanceCertificationFindingsToExplanation,
  governanceEligibilityFindingsToDiagnostics,
  governanceEligibilityToActions,
  governanceHealthDimensionsToExplanation,
  governancePolicyFactorsToEvidence,
  governanceRecoveryDiagnosticsToExplanation,
  governanceRecoveryFindingsToDiagnostics,
  governanceRecoveryResult,
  governanceRecoveryToActions,
  governanceRecoveryToEvidence,
  governanceTransferResult,
  governanceTransferToDiagnostics,
  governanceTransferToEvidence,
} from '../../lib/explainability'
import type {
  DecisionSessionCertificationReport,
  DecisionSessionContinuityArtifact,
  DecisionSessionRecoveryDiagnostics,
  DecisionSessionRecoveryResult,
  DecisionSessionTransfer,
  DecisionSessionTransferDiagnostics,
  DecisionSessionTransferEligibility,
  WorkflowGovernanceHealthProjection,
} from '../../types'

describe('governance explainability adapters', () => {
  it('preserves lifecycle factors and analysis warnings through shared explainability models', () => {
    expect(governancePolicyFactorsToEvidence(['Cache risk exceeds target.'])).toEqual([
      { label: 'Cache risk exceeds target.', detail: 'Lifecycle contributing factor' },
    ])
    expect(governanceAnalysisWarningsToDiagnostics(['Cache risk exceeds target.'])).toEqual([
      { label: 'Analysis Warning', detail: 'Cache risk exceeds target.' },
    ])
  })

  it('preserves certification findings, evidence, and diagnostics without changing report result', () => {
    const certification: DecisionSessionCertificationReport = {
      reportId: 'cert-1',
      repositoryId: 'repo-alpha',
      generatedAt: '2026-01-01T00:00:00Z',
      result: {
        passed: false,
        findings: [
          {
            id: 'finding-1',
            severity: 'Error',
            message: 'Governance transfer evidence is incomplete.',
            evidence: ['transfer-1', 'artifact-1'],
          },
        ],
        diagnostics: ['Certification inspected transfer evidence.'],
      },
    }

    expect(governanceCertificationFindingsToExplanation(certification)).toEqual([
      {
        id: 'finding-1',
        title: 'Error: Governance transfer evidence is incomplete.',
        category: 'Error',
        passed: false,
        detail: 'Governance transfer evidence is incomplete.',
        evidence: [
          { label: 'transfer-1', detail: 'Certification evidence' },
          { label: 'artifact-1', detail: 'Certification evidence' },
        ],
        diagnostics: [],
      },
    ])
    expect(governanceCertificationDiagnosticsToExplanation(certification)).toEqual([
      { label: 'Certification', detail: 'Certification inspected transfer evidence.' },
    ])
  })

  it('preserves recovery findings, source evidence, transfer assessments, events, and warnings', () => {
    const recovery = createRecoveryResult()
    const diagnostics = createRecoveryDiagnostics()

    expect(governanceRecoveryFindingsToDiagnostics(recovery)).toEqual([
      {
        label: 'Error',
        detail: 'Duplicate active sessions require intervention.',
        tone: 'danger',
        evidence: [{ label: 'session-active', detail: 'Session' }],
      },
      {
        label: 'Warning',
        detail: 'Discarded stale snapshot.',
        tone: 'warning',
        evidence: [{ label: 'snapshot-1', detail: 'Evidence' }],
      },
    ])
    expect(governanceRecoveryDiagnosticsToExplanation(diagnostics, recovery)).toEqual([
      { label: 'Registry Error', detail: 'Two sessions are active.' },
      { label: 'Registry Warning', detail: 'Registry scan found stale session metadata.' },
      { label: 'Recovery Warning', detail: 'Transfer did not complete.' },
      {
        label: 'Interrupted',
        detail: 'Transfer did not complete.',
        evidence: [
          { label: 'transfer-1', detail: 'Transfer' },
          { label: 'session-active', detail: 'Source session' },
          { label: 'session-next', detail: 'Target session' },
          { label: 'artifact-1', detail: 'Continuity artifact' },
        ],
      },
      {
        label: 'SnapshotRebuilt',
        detail: 'Rebuilt recovery snapshot.',
        evidence: [{ label: 'event-1', detail: '2026-01-01T00:05:00Z' }],
      },
      { label: 'SnapshotRebuilt', detail: 'Snapshot rebuilt from registry.' },
    ])
    expect(governanceRecoveryToActions(diagnostics, recovery)).toEqual([
      {
        label: 'Recover governance',
        detail: 'Rebuild governance recovery state from the decision-session registry and transfer evidence.',
        eligible: true,
        reason: 'Recovery has projected registry or transfer issues to reconcile.',
        command: 'decision_session_recover',
        constraints: [
          {
            label: 'Registry active session count',
            detail: '2 active session(s) projected.',
            satisfied: false,
          },
          {
            label: 'Interrupted transfers',
            detail: '1 interrupted transfer assessment(s) projected.',
            satisfied: false,
          },
          {
            label: 'duplicate-active-session',
            detail: 'Duplicate active sessions require intervention.',
            satisfied: false,
          },
        ],
      },
    ])
    expect(governanceRecoveryToEvidence(diagnostics, recovery)).toEqual([
      {
        id: 'recovery-1',
        label: 'Recovery run',
        detail: 'Requires review | recovered 2026-01-01T00:05:00Z',
      },
      {
        id: 'recovery-1-active-session',
        label: 'Active session',
        detail: 'session-active',
      },
      {
        id: 'recovery-1-active-session-count',
        label: 'Active sessions',
        detail: '2 active session(s) after recovery.',
      },
      {
        id: 'event-1',
        label: 'Recovery event: SnapshotRebuilt',
        detail: 'Rebuilt recovery snapshot.',
      },
      {
        id: 'repo-alpha-recovery-diagnostics',
        label: 'Recovery diagnostics',
        detail: 'Generated 2026-01-01T00:00:00Z',
      },
      {
        id: 'repo-alpha-registry-diagnostics',
        label: 'Registry diagnostics',
        detail: '3 session(s), 2 active.',
      },
      {
        id: 'transfer-1',
        label: 'Transfer assessment: Interrupted',
        detail: 'Transfer did not complete.',
      },
    ])
    expect(governanceRecoveryResult(diagnostics, recovery)).toBe('Recovery recovery-1 requires review.')
  })

  it('preserves health status, findings, and evidence without computing aggregate health', () => {
    const dimensions: WorkflowGovernanceHealthProjection[] = [
      {
        name: 'Lifecycle',
        status: 'Warning',
        findings: ['Transfer pressure is elevated.'],
        evidence: ['session-active'],
      },
    ]

    expect(governanceHealthDimensionsToExplanation(dimensions)).toEqual([
      {
        name: 'Lifecycle',
        status: 'Warning',
        tone: 'warning',
        reason: 'Transfer pressure is elevated.',
        evidence: [{ label: 'session-active', detail: 'Health evidence' }],
        diagnostics: [{ label: 'Health Finding', detail: 'Transfer pressure is elevated.' }],
      },
    ])
  })

  it('preserves transfer eligibility status, reason, findings, and action command', () => {
    const eligibility: DecisionSessionTransferEligibility = {
      status: 'Eligible',
      sourceSessionId: 'session-active',
      checkedAt: '2026-01-01T00:00:00Z',
      policyEvaluation: {
        decision: 'Transfer',
        reuseScore: 0.2,
        transferScore: 0.9,
        reason: 'Transfer is recommended because reuse value is low.',
        contributingFactors: ['Cache risk exceeds target.'],
        evaluatedAt: '2026-01-01T00:00:00Z',
      },
      findings: [
        {
          code: 'eligible',
          severity: 'Info',
          message: 'Transfer policy permits execution.',
        },
      ],
    }

    expect(governanceEligibilityToActions(eligibility)).toEqual([
      {
        label: 'Execute transfer',
        detail: 'Transfer eligibility status: Eligible.',
        eligible: true,
        reason: 'Transfer is recommended because reuse value is low.',
        command: 'decision_session_transfer',
        constraints: [
          {
            label: 'eligible',
            detail: 'Info: Transfer policy permits execution.',
            satisfied: true,
          },
        ],
      },
    ])
    expect(governanceEligibilityFindingsToDiagnostics(eligibility)).toEqual([
      {
        label: 'Info',
        detail: 'Transfer policy permits execution.',
        tone: 'info',
      },
    ])
  })

  it('preserves transfer action evidence, ownership context, continuity readiness, result, and diagnostics', () => {
    const eligibility = createTransferEligibility()
    const transfer = createTransfer()
    const transferDiagnostics = createTransferDiagnostics(eligibility)
    const continuityArtifact = createContinuityArtifact(eligibility)

    expect(
      governanceTransferToEvidence({
        eligibility,
        transfers: [transfer],
        transferDiagnostics,
        continuityArtifacts: [continuityArtifact],
      }),
    ).toEqual([
      {
        id: 'session-active-transfer-eligibility',
        label: 'Transfer eligibility',
        detail: 'Eligible | checked 2026-01-01T00:00:00Z',
      },
      {
        id: 'session-active-source-session',
        label: 'Source session',
        detail: 'session-active',
      },
      {
        id: 'transfer-1',
        label: 'Latest transfer',
        detail: 'Pending or failed',
      },
      {
        id: 'transfer-1-ownership',
        label: 'Ownership context',
        detail: 'session-active to session-next',
      },
      {
        id: 'artifact-1',
        label: 'Continuity artifact',
        detail: 'artifact-1',
      },
      {
        id: 'artifact-1',
        label: 'Continuity readiness',
        detail: 'session-active to session-next | fingerprint continuity-fingerprint',
      },
      {
        id: 'event-1',
        label: 'Transfer event: Started',
        detail: 'Transfer started.',
      },
    ])
    expect(
      governanceTransferToDiagnostics({
        eligibility,
        transferDiagnostics,
        transfers: [transfer],
      }),
    ).toEqual([
      {
        label: 'Info',
        detail: 'Transfer policy permits execution.',
        tone: 'info',
      },
      {
        label: 'Transfer Warning',
        detail: 'Transfer needs operator review.',
      },
      {
        label: 'Transfer Diagnostic',
        detail: 'Transfer is awaiting completion.',
      },
      {
        label: 'Started',
        detail: 'Provider handoff confirmed.',
      },
    ])
    expect(governanceTransferResult(eligibility, [transfer])).toBe('Transfer transfer-1 is pending or failed.')
  })
})

function createTransferEligibility(): DecisionSessionTransferEligibility {
  return {
    status: 'Eligible',
    sourceSessionId: 'session-active',
    checkedAt: '2026-01-01T00:00:00Z',
    policyEvaluation: {
      decision: 'Transfer',
      reuseScore: 0.2,
      transferScore: 0.9,
      reason: 'Transfer is recommended because reuse value is low.',
      contributingFactors: ['Cache risk exceeds target.'],
      evaluatedAt: '2026-01-01T00:00:00Z',
    },
    findings: [
      {
        code: 'eligible',
        severity: 'Info',
        message: 'Transfer policy permits execution.',
      },
    ],
  }
}

function createTransfer(): DecisionSessionTransfer {
  return {
    transferId: 'transfer-1',
    repositoryId: 'repo-alpha',
    sourceSessionId: 'session-active',
    targetSessionId: 'session-next',
    continuityArtifactId: 'artifact-1',
    startedAt: '2026-01-01T00:01:00Z',
    completedAt: null,
    succeeded: false,
    events: [
      {
        eventId: 'event-1',
        eventType: 'Started',
        repositoryId: 'repo-alpha',
        sourceSessionId: 'session-active',
        targetSessionId: 'session-next',
        continuityArtifactId: 'artifact-1',
        occurredAt: '2026-01-01T00:01:00Z',
        message: 'Transfer started.',
        diagnostics: ['Provider handoff confirmed.'],
      },
    ],
    diagnostics: ['Transfer is awaiting completion.'],
  }
}

function createTransferDiagnostics(
  eligibility: DecisionSessionTransferEligibility,
): DecisionSessionTransferDiagnostics {
  return {
    repositoryId: 'repo-alpha',
    generatedAt: '2026-01-01T00:02:00Z',
    eligibility,
    events: createTransfer().events,
    warnings: ['Transfer needs operator review.'],
  }
}

function createContinuityArtifact(
  eligibility: DecisionSessionTransferEligibility,
): DecisionSessionContinuityArtifact {
  return {
    artifactId: 'artifact-1',
    repositoryId: 'repo-alpha',
    sourceSessionId: 'session-active',
    targetSessionId: 'session-next',
    createdAt: '2026-01-01T00:01:00Z',
    policyEvaluation: eligibility.policyEvaluation,
    metrics: {
      estimatedTokenCount: 1,
      contextByteSize: 1,
      reasoningEventCount: 1,
      reasoningThreadCount: 1,
      reasoningRelationshipCount: 1,
      decisionCount: 1,
      decisionCandidateCount: 1,
      decisionProposalCount: 1,
      operationalContextRevisionCount: 1,
      lastActivityAt: '2026-01-01T00:00:00Z',
      measuredAt: '2026-01-01T00:00:00Z',
    },
    economics: {
      estimatedReuseValue: 0.2,
      estimatedTransferValue: 0.9,
      estimatedContextCost: 0.6,
      estimatedReasoningCost: 0.4,
      estimatedContinuityBenefit: 0.8,
      estimatedCacheBenefit: 0.3,
      estimatedCacheMissRisk: 0.42,
    },
    coherence: {
      coherenceScore: 0.81,
      fragmentationScore: 0.19,
      densityScore: 0.7,
      continuityScore: 0.84,
      transferPressure: 0.73,
    },
    cache: {},
    decisionReferences: [],
    reasoningReferences: [],
    operationalContextReferences: [],
    continuityFingerprint: 'continuity-fingerprint',
    diagnostics: [],
  }
}

function createRecoveryResult(): DecisionSessionRecoveryResult {
  return {
    recoveryId: 'recovery-1',
    repositoryId: 'repo-alpha',
    succeeded: false,
    activeSessionId: 'session-active',
    activeSessionCount: 2,
    findings: [
      {
        code: 'duplicate-active-session',
        severity: 'Error',
        message: 'Duplicate active sessions require intervention.',
        sessionId: 'session-active',
        evidenceId: null,
      },
      {
        code: 'discarded-snapshot',
        severity: 'Warning',
        message: 'Discarded stale snapshot.',
        sessionId: null,
        evidenceId: 'snapshot-1',
      },
    ],
    diagnostics: createRecoveryDiagnostics(),
    events: [
      {
        eventId: 'event-1',
        repositoryId: 'repo-alpha',
        eventType: 'SnapshotRebuilt',
        occurredAt: '2026-01-01T00:05:00Z',
        message: 'Rebuilt recovery snapshot.',
        diagnostics: ['Snapshot rebuilt from registry.'],
      },
    ],
    recoveredAt: '2026-01-01T00:05:00Z',
  }
}

function createRecoveryDiagnostics(): DecisionSessionRecoveryDiagnostics {
  return {
    repositoryId: 'repo-alpha',
    generatedAt: '2026-01-01T00:00:00Z',
    registryDiagnostics: {
      repositoryId: 'repo-alpha',
      isValid: false,
      sessionCount: 3,
      activeSessionCount: 2,
      errors: ['Two sessions are active.'],
      warnings: ['Registry scan found stale session metadata.'],
      generatedAt: '2026-01-01T00:00:00Z',
    },
    transferAssessments: [
      {
        transferId: 'transfer-1',
        sourceSessionId: 'session-active',
        targetSessionId: 'session-next',
        continuityArtifactId: 'artifact-1',
        status: 'Interrupted',
        message: 'Transfer did not complete.',
        events: [],
      },
    ],
    warnings: ['Transfer did not complete.'],
  }
}
