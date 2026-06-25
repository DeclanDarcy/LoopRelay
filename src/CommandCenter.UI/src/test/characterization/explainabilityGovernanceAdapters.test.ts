import { describe, expect, it } from 'vitest'
import {
  governanceCertificationDiagnosticsToExplanation,
  governanceCertificationFindingsToExplanation,
  governanceEligibilityFindingsToDiagnostics,
  governanceEligibilityToActions,
  governanceHealthDimensionsToExplanation,
  governanceRecoveryDiagnosticsToExplanation,
  governanceRecoveryFindingsToDiagnostics,
} from '../../lib/explainability'
import type {
  DecisionSessionCertificationReport,
  DecisionSessionRecoveryDiagnostics,
  DecisionSessionRecoveryResult,
  DecisionSessionTransferEligibility,
  WorkflowGovernanceHealthProjection,
} from '../../types'

describe('governance explainability adapters', () => {
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
})

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
