import { describe, expect, it } from 'vitest'
import {
  workflowCertificationFindingsToExplanation,
  workflowCertificationFailuresToDiagnostics,
  workflowContinuationDiagnosticsToExplanation,
  workflowContinuationToActions,
  workflowDiagnosticsToExplanation,
  workflowGateDiagnosticsToExplanation,
  workflowGatesToActions,
  workflowHealthDimensionsToExplanation,
  workflowRecoveryArtifactsToEvidence,
  workflowRecoveryDiagnosticsToExplanation,
  workflowReportDiagnosticsToExplanation,
  workflowReportEvidenceToExplanation,
} from '../../lib/explainability'
import type {
  HumanGovernanceReport,
  RepositoryWorkflowReport,
  WorkflowCertificationResult,
  WorkflowContinuationEvaluation,
  WorkflowGateCatalogProjection,
  WorkflowHealthAssessment,
  WorkflowProgressionReport,
  WorkflowReadinessReport,
  WorkflowRecoveryDiagnostics,
} from '../../types'

describe('workflow explainability adapters', () => {
  it('preserves workflow health reason, evidence, and diagnostics without changing status text', () => {
    const health: WorkflowHealthAssessment = {
      repositoryId: 'repo-alpha',
      generatedAt: '2026-01-01T00:00:00Z',
      overallStatus: 'Attention',
      dimensions: [
        {
          name: 'Gate Integrity',
          status: 'Warning',
          reason: 'Open commit gate is waiting for human approval.',
          evidence: ['gate-commit', '.agents/workflow/gates.json'],
          diagnostics: ['Commit gate has satisfying command commit_execution.'],
        },
      ],
      influenceTrace: {
        repositoryId: 'repo-alpha',
        generatedAt: '2026-01-01T00:00:00Z',
        currentStage: 'Commit',
        progressState: 'WaitingForHuman',
        blockingGate: 'CommitApproval',
        evidencePaths: [],
        stageInfluences: [],
        progressionInfluences: [],
        preparationInfluences: [],
        gateInfluences: [],
        blockingInfluences: [],
        conflicts: [],
        fingerprint: 'health-fingerprint',
        governanceInfluence: null,
      },
      diagnostics: [],
      governanceHealth: null,
    }

    expect(workflowHealthDimensionsToExplanation(health)).toEqual([
      {
        name: 'Gate Integrity',
        status: 'Warning',
        tone: 'warning',
        reason: 'Open commit gate is waiting for human approval.',
        evidence: [{ label: 'gate-commit' }, { label: '.agents/workflow/gates.json' }],
        diagnostics: [{ label: 'Diagnostic', detail: 'Commit gate has satisfying command commit_execution.' }],
      },
    ])
  })

  it('preserves certification finding fields and maps diagnostics as presentation diagnostics', () => {
    const certification: WorkflowCertificationResult = {
      id: 'cert-alpha',
      repositoryId: 'repo-alpha',
      generatedAt: '2026-01-01T00:00:00Z',
      inputFingerprint: 'cert-fingerprint',
      certified: false,
      currentStage: 'Commit',
      progressState: 'WaitingForHuman',
      blockingGate: 'CommitApproval',
      passedFindingCount: 0,
      failedFindingCount: 1,
      findings: [
        {
          id: 'finding-gate',
          category: 'Gate',
          passed: false,
          summary: 'Commit gate lacks approval',
          detail: 'The workflow cannot certify completion until commit approval is recorded.',
          evidence: ['gate-commit'],
          diagnostics: ['CommitApproval is open.'],
        },
      ],
      failures: [],
      diagnostics: ['Certification is observational only.'],
    }

    expect(workflowCertificationFindingsToExplanation(certification)).toEqual([
      {
        id: 'finding-gate',
        title: 'Commit gate lacks approval',
        category: 'Gate',
        passed: false,
        detail: 'The workflow cannot certify completion until commit approval is recorded.',
        evidence: [{ label: 'gate-commit' }],
        diagnostics: [{ label: 'Diagnostic', detail: 'CommitApproval is open.' }],
      },
    ])
    expect(workflowDiagnosticsToExplanation(certification.diagnostics)).toEqual([
      { label: 'Diagnostic', detail: 'Certification is observational only.' },
    ])
    expect(workflowCertificationFailuresToDiagnostics(['Commit gate lacks approval'])).toEqual([
      { label: 'Certification Failure', detail: 'Commit gate lacks approval' },
    ])
  })

  it('preserves recovery diagnostics and artifact evidence without inferring recovery state', () => {
    const recovery: WorkflowRecoveryDiagnostics = {
      repositoryId: 'repo-alpha',
      recoveredAt: '2026-01-01T00:00:00Z',
      domainFingerprint: 'domain-fingerprint',
      persistedFingerprint: 'persisted-fingerprint',
      rebuilt: true,
      persistedEvidenceMatchedDomain: false,
      recoveredArtifacts: ['.agents/workflow/timeline.json'],
      discardedArtifacts: ['.agents/workflow/stale.json'],
      diagnostics: ['Persisted workflow evidence did not match domain projection.'],
    }

    expect(workflowRecoveryDiagnosticsToExplanation(recovery)).toEqual([
      { label: 'Recovery', detail: 'Persisted workflow evidence did not match domain projection.' },
    ])
    expect(workflowRecoveryArtifactsToEvidence(recovery.recoveredArtifacts, 'Recovered artifact')).toEqual([
      { label: '.agents/workflow/timeline.json', detail: 'Recovered artifact' },
    ])
  })

  it('maps open gates to blocked actions while preserving commands, reasons, and evidence', () => {
    const gates = createGateCatalog()

    expect(workflowGatesToActions(gates)).toEqual([
      {
        label: 'CommitApproval',
        detail: 'Approve commit',
        eligible: false,
        reason: 'Commit requires human approval.',
        command: 'commit_execution',
        constraints: [
          {
            label: 'Open',
            detail: 'Commit requires human approval.',
            satisfied: false,
            evidence: [
              {
                label: 'Commit gate projected from git status.',
                source: '.agents/workflow/gates.json',
                detail: 'Git',
                fingerprint: 'gate-fingerprint',
              },
            ],
          },
        ],
      },
    ])
    expect(workflowGateDiagnosticsToExplanation(gates)).toEqual([
      { label: 'Gate Reasoning', detail: 'CommitApproval is open.' },
      { label: 'Missing Evidence', detail: 'commit approval' },
      { label: 'Conflict', detail: 'push requested before commit approval' },
    ])
  })

  it('preserves continuation action, stop reasons, and diagnostic categories', () => {
    const evaluation = createContinuationEvaluation()

    expect(workflowContinuationToActions(evaluation)).toEqual([
      {
        label: 'WaitingForHuman',
        detail: 'Remain at Commit.',
        eligible: false,
        reason: 'Commit approval is required.',
        command: null,
        constraints: [
          {
            label: 'CommitApproval',
            detail: 'Approve commit',
            satisfied: false,
          },
        ],
      },
    ])
    expect(workflowContinuationDiagnosticsToExplanation(evaluation)).toEqual([
      { label: 'Continuation', detail: 'Continuation stopped at open gate.' },
      { label: 'State Machine', detail: 'Commit can transition to Push after approval.' },
      { label: 'Gate', detail: 'CommitApproval is open.' },
      { label: 'Stop Reason', detail: 'Commit approval is required.' },
      { label: 'Conflict', detail: 'push requested before approval' },
    ])
  })

  it('preserves workflow report evidence and diagnostics across report projections', () => {
    const repositoryReport: RepositoryWorkflowReport = {
      repositoryId: 'repo-alpha',
      generatedAt: '2026-01-01T00:00:00Z',
      currentStage: 'Commit',
      progressState: 'WaitingForHuman',
      blockingGate: 'CommitApproval',
      requiredHumanAction: 'Approve commit',
      timelineEntryCount: 3,
      openGateCount: 1,
      satisfiedGateCount: 2,
      continuationEventCount: 1,
      preparationEventCount: 0,
      healthStatus: 'Attention',
      certified: false,
      failedCertificationFindingCount: 1,
      diagnostics: ['Repository report is observational.'],
    }
    const progressionReport: WorkflowProgressionReport = {
      repositoryId: 'repo-alpha',
      generatedAt: '2026-01-01T00:00:00Z',
      currentStage: 'Commit',
      progressState: 'WaitingForHuman',
      blockingGate: 'CommitApproval',
      validTransitionCount: 1,
      blockedTransitionCount: 1,
      continuationEventCount: 1,
      validTransitions: ['Commit -> Push'],
      blockedTransitions: ['Push blocked by CommitApproval'],
      continuationEvidence: ['continuation.1'],
      diagnostics: ['Progression report preserved gate state.'],
    }
    const governanceReport: HumanGovernanceReport = {
      repositoryId: 'repo-alpha',
      generatedAt: '2026-01-01T00:00:00Z',
      blockingGate: 'CommitApproval',
      requiredHumanAction: 'Approve commit',
      openGateCount: 1,
      satisfiedGateCount: 2,
      openGates: ['CommitApproval'],
      satisfiedGates: ['ExecutionAcceptance'],
      authorityFindings: ['Commit approval belongs to workflow authority.'],
      diagnostics: ['Human governance report is observational.'],
    }
    const readinessReport: WorkflowReadinessReport = {
      repositoryId: 'repo-alpha',
      generatedAt: '2026-01-01T00:00:00Z',
      ready: false,
      certified: false,
      healthStatus: 'Attention',
      currentStage: 'Commit',
      progressState: 'WaitingForHuman',
      blockingGate: 'CommitApproval',
      blockingReasons: ['Commit approval is required.'],
      failedCertificationFindings: ['Commit gate lacks approval'],
      healthDiagnostics: ['Readiness report preserved health diagnostics.'],
    }

    expect(workflowReportEvidenceToExplanation(progressionReport, governanceReport, readinessReport)).toEqual([
      { label: 'continuation.1', detail: 'Continuation evidence' },
      { label: 'Commit approval belongs to workflow authority.', detail: 'Authority finding' },
      { label: 'Commit gate lacks approval', detail: 'Failed certification finding' },
    ])
    expect(
      workflowReportDiagnosticsToExplanation(repositoryReport, progressionReport, governanceReport, readinessReport),
    ).toEqual([
      { label: 'Repository Report', detail: 'Repository report is observational.' },
      { label: 'Progression Report', detail: 'Progression report preserved gate state.' },
      { label: 'Human Governance Report', detail: 'Human governance report is observational.' },
      { label: 'Readiness Health', detail: 'Readiness report preserved health diagnostics.' },
    ])
  })
})

function createGateCatalog(): WorkflowGateCatalogProjection {
  return {
    openGates: [
      {
        gateId: 'gate-commit',
        type: 'CommitApproval',
        repositoryId: 'repo-alpha',
        stage: 'Commit',
        status: 'Open',
        requiredAction: 'Approve commit',
        satisfyingCommand: 'commit_execution',
        satisfyingCommands: ['commit_execution'],
        sourceDomain: 'Git',
        sourceArtifact: '.agents/workflow/gates.json',
        createdAt: '2026-01-01T00:00:00Z',
        satisfiedAt: null,
        satisfiedActor: null,
        reason: 'Commit requires human approval.',
        evidence: [
          {
            sourceDomain: 'Git',
            sourceArtifact: '.agents/workflow/gates.json',
            summary: 'Commit gate projected from git status.',
            observedAt: '2026-01-01T00:00:00Z',
            fingerprint: 'gate-fingerprint',
          },
        ],
      },
    ],
    satisfiedGates: [],
    gateHistory: [],
    diagnostics: {
      repositoryId: 'repo-alpha',
      blockingGate: 'CommitApproval',
      openGates: [],
      satisfiedGates: [],
      gateCommandMap: [],
      reasoning: ['CommitApproval is open.'],
      missingEvidence: ['commit approval'],
      conflicts: ['push requested before commit approval'],
    },
  }
}

function createContinuationEvaluation(): WorkflowContinuationEvaluation {
  return {
    repositoryId: 'repo-alpha',
    fromStage: 'Commit',
    toStage: null,
    progressState: 'WaitingForHuman',
    blockingGate: 'CommitApproval',
    canAdvanceMechanically: false,
    isWaitingForHuman: true,
    isComplete: false,
    requiredHumanAction: 'Approve commit',
    outcome: 'WaitingForHuman',
    stopReason: 'Commit approval is required.',
    fingerprint: { value: 'continuation-fingerprint' },
    transition: null,
    completion: {
      repositoryId: 'repo-alpha',
      isComplete: false,
      completionReason: 'Commit approval is required.',
      completionArtifact: null,
      evidence: ['continuation.1'],
      diagnostics: ['Completion waits for commit approval.'],
    },
    diagnostics: {
      repositoryId: 'repo-alpha',
      projectionInputs: ['workflow projection'],
      stateMachineReasoning: ['Commit can transition to Push after approval.'],
      gateReasoning: ['CommitApproval is open.'],
      completionEvidence: ['continuation.1'],
      reasoning: ['Continuation stopped at open gate.'],
      stopReasons: ['Commit approval is required.'],
      conflicts: ['push requested before approval'],
      openGateCount: 1,
      satisfiedGateCount: 2,
      fingerprint: { value: 'continuation-fingerprint' },
    },
  }
}
