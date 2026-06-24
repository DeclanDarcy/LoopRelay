import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { GovernanceWorkspace } from '../../features/governance/GovernanceWorkspace'
import type {
  DecisionSessionGovernanceSnapshot,
  RepositoryDecisionSessionSummary,
  WorkflowInstance,
} from '../../types'

afterEach(() => {
  cleanup()
})

const repositorySummary: RepositoryDecisionSessionSummary = {
  decisionSessionId: 'session-active',
  state: 'Active',
  lifecycleDecision: 'Transfer',
  transferEligibilityStatus: 'Eligible',
  estimatedTokenCount: 4200,
  estimatedCacheTtl: '00:30:00',
  cacheMissRisk: 0.42,
  coherenceScore: 0.81,
  transferPressure: 0.73,
  healthDimensions: [
    {
      name: 'Lifecycle',
      status: 'Warning',
      findings: ['Transfer pressure is elevated.'],
    },
  ],
  recentTransferLineage: [],
  diagnostics: [],
  generatedAt: '2026-06-21T17:30:00.000Z',
}

const snapshot: DecisionSessionGovernanceSnapshot = {
  sessions: [
    {
      id: 'session-active',
      repositoryId: 'repo-alpha',
      state: 'Active',
      createdAt: '2026-06-21T16:00:00.000Z',
      activatedAt: '2026-06-21T16:01:00.000Z',
      retiredAt: null,
      createdBy: 'system',
    },
  ],
  activeSession: {
    id: 'session-active',
    repositoryId: 'repo-alpha',
    state: 'Active',
    createdAt: '2026-06-21T16:00:00.000Z',
    activatedAt: '2026-06-21T16:01:00.000Z',
    retiredAt: null,
    createdBy: 'system',
  },
  diagnostics: null,
  metrics: {
    estimatedTokenCount: 4200,
    contextByteSize: 10000,
    reasoningEventCount: 7,
    reasoningThreadCount: 3,
    reasoningRelationshipCount: 2,
    decisionCount: 5,
    decisionCandidateCount: 2,
    decisionProposalCount: 1,
    operationalContextRevisionCount: 4,
    lastActivityAt: '2026-06-21T17:00:00.000Z',
    measuredAt: '2026-06-21T17:30:00.000Z',
  },
  statistics: null,
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
  analysisDiagnostics: {
    repositoryId: 'repo-alpha',
    generatedAt: '2026-06-21T17:30:00.000Z',
    metrics: {},
    economics: {},
    coherence: {},
    warnings: ['Cache risk exceeds target.'],
  },
  lifecyclePolicy: {
    decision: 'Transfer',
    reuseScore: 0.22,
    transferScore: 0.88,
    reason: 'Transfer is recommended because reuse value is low.',
    contributingFactors: ['Cache risk exceeds target.', 'Continuity benefit is high.'],
    evaluatedAt: '2026-06-21T17:30:00.000Z',
  },
  lifecyclePolicyDiagnostics: null,
  transferEligibility: {
    status: 'Eligible',
    policyEvaluation: {
      decision: 'Transfer',
      reuseScore: 0.22,
      transferScore: 0.88,
      reason: 'Transfer is recommended because reuse value is low.',
      contributingFactors: ['Cache risk exceeds target.'],
      evaluatedAt: '2026-06-21T17:30:00.000Z',
    },
    sourceSessionId: 'session-active',
    findings: [
      {
        code: 'eligible',
        severity: 'Info',
        message: 'Transfer policy permits execution.',
      },
    ],
    checkedAt: '2026-06-21T17:31:00.000Z',
  },
  transferEligibilityDiagnostics: null,
  lifecycleProjection: null,
  lifecycleHistory: null,
  lifecycleInfluence: null,
  health: null,
  continuityArtifacts: [
    {
      artifactId: 'artifact-1',
      repositoryId: 'repo-alpha',
      sourceSessionId: 'session-active',
      targetSessionId: 'session-next',
      createdAt: '2026-06-21T17:32:00.000Z',
      policyEvaluation: {
        decision: 'Transfer',
        reuseScore: 0.22,
        transferScore: 0.88,
        reason: 'Transfer is recommended because reuse value is low.',
        contributingFactors: [],
        evaluatedAt: '2026-06-21T17:30:00.000Z',
      },
      metrics: {
        estimatedTokenCount: 4200,
        contextByteSize: 10000,
        reasoningEventCount: 7,
        reasoningThreadCount: 3,
        reasoningRelationshipCount: 2,
        decisionCount: 5,
        decisionCandidateCount: 2,
        decisionProposalCount: 1,
        operationalContextRevisionCount: 4,
        lastActivityAt: '2026-06-21T17:00:00.000Z',
        measuredAt: '2026-06-21T17:30:00.000Z',
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
    },
  ],
  transfers: [
    {
      transferId: 'transfer-1',
      repositoryId: 'repo-alpha',
      sourceSessionId: 'session-active',
      targetSessionId: 'session-next',
      continuityArtifactId: 'artifact-1',
      startedAt: '2026-06-21T17:32:00.000Z',
      completedAt: null,
      succeeded: false,
      events: [],
      diagnostics: [],
    },
  ],
  transferHistory: [],
  transferDiagnostics: null,
  recovery: {
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
    diagnostics: {
      repositoryId: 'repo-alpha',
      generatedAt: '2026-06-21T17:33:00.000Z',
      registryDiagnostics: {
        repositoryId: 'repo-alpha',
        isValid: false,
        sessionCount: 3,
        activeSessionCount: 2,
        errors: [],
        warnings: [],
        generatedAt: '2026-06-21T17:33:00.000Z',
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
      warnings: [],
    },
    events: [
      {
        eventId: 'event-1',
        repositoryId: 'repo-alpha',
        eventType: 'SnapshotRebuilt',
        occurredAt: '2026-06-21T17:34:00.000Z',
        message: 'Rebuilt recovery snapshot.',
        diagnostics: [],
      },
    ],
    recoveredAt: '2026-06-21T17:34:00.000Z',
  },
  recoveryHistory: null,
  recoveryDiagnostics: {
    repositoryId: 'repo-alpha',
    generatedAt: '2026-06-21T17:33:00.000Z',
    registryDiagnostics: {
      repositoryId: 'repo-alpha',
      isValid: false,
      sessionCount: 3,
      activeSessionCount: 2,
      errors: [],
      warnings: [],
      generatedAt: '2026-06-21T17:33:00.000Z',
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
    warnings: [],
  },
  workflow: null,
  workflowSummary: null,
  workflowHealth: null,
  workflowInfluence: null,
  certification: null,
  certificationReport: {
    reportId: 'cert-1',
    repositoryId: 'repo-alpha',
    generatedAt: '2026-06-21T17:35:00.000Z',
    result: {
      passed: false,
      findings: [
        {
          id: 'finding-1',
          severity: 'Error',
          message: 'Governance transfer evidence is incomplete.',
          evidence: ['transfer-1'],
        },
      ],
      diagnostics: ['Certification inspected transfer evidence.'],
    },
  },
}

const workflow = {
  currentStage: 'Decision',
  progressState: 'WaitingForHuman',
  blockingGate: 'DecisionResolution',
  requiredHumanAction: 'Review transfer readiness.',
  timeline: [],
} as unknown as WorkflowInstance

describe('governance workspace characterization', () => {
  it('renders lifecycle explanation, transfer readiness, recovery, health, and certification facts', () => {
    const executeTransfer = vi.fn()
    const recover = vi.fn()
    const runCertification = vi.fn()

    render(
      <GovernanceWorkspace
        repositorySummary={repositorySummary}
        snapshot={snapshot}
        workflow={workflow}
        onExecuteTransfer={executeTransfer}
        onRecover={recover}
        onRunCertification={runCertification}
      />,
    )

    expect(screen.getByRole('heading', { name: 'session-active' })).toBeInTheDocument()

    const lifecycle = screen.getByLabelText('Governance lifecycle')
    expect(within(lifecycle).getByText('Reuse score: 0.22')).toBeInTheDocument()
    expect(within(lifecycle).getByText('Transfer score: 0.88')).toBeInTheDocument()
    expect(within(lifecycle).getByText('Transfer pressure: 0.73')).toBeInTheDocument()
    expect(within(lifecycle).getByText('Cache risk: 0.42')).toBeInTheDocument()
    expect(within(lifecycle).getByText('Continuity benefit: 0.80')).toBeInTheDocument()
    expect(within(lifecycle).getByText('Fragmentation: 0.19')).toBeInTheDocument()
    expect(within(lifecycle).getByText('Workflow gate: DecisionResolution')).toBeInTheDocument()
    expect(within(lifecycle).getByText('Required action: Review transfer readiness.')).toBeInTheDocument()
    expect(
      within(lifecycle).getByText('Transfer is recommended because reuse value is low.'),
    ).toBeInTheDocument()

    const eligibility = screen.getByLabelText('Governance transfer eligibility')
    expect(within(eligibility).getByText('Transfer recommended: Yes')).toBeInTheDocument()
    expect(within(eligibility).getByText('Currently executable: Yes')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Execute' }))
    fireEvent.click(screen.getByRole('button', { name: 'Recover' }))
    fireEvent.click(screen.getByRole('button', { name: 'Run' }))
    expect(executeTransfer).toHaveBeenCalledTimes(1)
    expect(recover).toHaveBeenCalledTimes(1)
    expect(runCertification).toHaveBeenCalledTimes(1)

    const recovery = screen.getByLabelText('Governance recovery')
    expect(within(recovery).getByText('Recovered: No')).toBeInTheDocument()
    expect(within(recovery).getByText('Diagnosed: Yes')).toBeInTheDocument()
    expect(within(recovery).getByText('Requires intervention: Yes')).toBeInTheDocument()
    expect(within(recovery).getByText('Duplicate active sessions: Yes')).toBeInTheDocument()
    expect(within(recovery).getByText('Interrupted transfers: 1')).toBeInTheDocument()
    expect(within(recovery).getByText('Discarded snapshots: 1')).toBeInTheDocument()
    expect(within(recovery).getByText('Rebuilt snapshots: 1')).toBeInTheDocument()

    const health = screen.getByLabelText('Governance health')
    expect(within(health).getByText('Lifecycle')).toBeInTheDocument()
    expect(within(health).getByText('Transfer pressure is elevated.')).toBeInTheDocument()

    const certification = screen.getByLabelText('Governance certification')
    expect(
      within(certification).getByText('Error: Governance transfer evidence is incomplete.'),
    ).toBeInTheDocument()
  })
})
