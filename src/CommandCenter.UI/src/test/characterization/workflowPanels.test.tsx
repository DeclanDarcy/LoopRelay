import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import {
  WorkflowCertificationPanel,
  WorkflowContinuationPanel,
  WorkflowGatePanel,
  WorkflowHealthPanel,
  WorkflowRecoveryPanel,
  WorkflowReportsPanel,
} from '../../features/workflow/WorkflowPanels'
import type {
  HumanGovernanceReport,
  WorkflowCertificationResult,
  WorkflowContinuationEvaluation,
  WorkflowGateCatalogProjection,
  WorkflowHealthAssessment,
  WorkflowProgressionReport,
  WorkflowRecoveryDiagnostics,
  WorkflowReadinessReport,
  RepositoryWorkflowReport,
} from '../../types'

afterEach(() => {
  cleanup()
})

describe('workflow panel rendering characterization', () => {
  it('renders recovery diagnostics and artifact evidence from workflow authority', () => {
    const diagnostics: WorkflowRecoveryDiagnostics = {
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

    render(<WorkflowRecoveryPanel diagnostics={diagnostics} />)

    const panel = screen.getByLabelText('Workflow recovery')
    expect(within(panel).getByText('Rebuilt')).toBeInTheDocument()
    expect(within(panel).getByText('Evidence matched: No')).toBeInTheDocument()
    expect(
      within(panel).getByText('Persisted workflow evidence did not match domain projection.'),
    ).toBeInTheDocument()
    expect(within(panel).getByText('.agents/workflow/timeline.json')).toBeInTheDocument()
    expect(within(panel).getByText('.agents/workflow/stale.json')).toBeInTheDocument()
  })

  it('renders open gates as shared action eligibility with diagnostics and evidence', () => {
    render(<WorkflowGatePanel gates={createGateCatalog()} />)

    const panel = screen.getByLabelText('Workflow gates')
    expect(within(panel).getByText('Open Gate Actions')).toBeInTheDocument()
    expect(within(panel).getByText('CommitApproval')).toBeInTheDocument()
    expect(within(panel).getByText('Blocked')).toBeInTheDocument()
    expect(within(panel).getByText('Approve commit')).toBeInTheDocument()
    expect(within(panel).getByText('Command: commit_execution')).toBeInTheDocument()
    expect(within(panel).getByText('Commit gate projected from git status.')).toBeInTheDocument()
    expect(within(panel).getByText('CommitApproval is open.')).toBeInTheDocument()
  })

  it('renders continuation action eligibility and diagnostics through shared components', () => {
    render(<WorkflowContinuationPanel evaluation={createContinuationEvaluation()} />)

    const panel = screen.getByLabelText('Workflow continuation')
    expect(within(panel).getAllByText('WaitingForHuman')).toHaveLength(2)
    expect(within(panel).getByText('Continuation Action')).toBeInTheDocument()
    expect(within(panel).getByText('Remain at Commit.')).toBeInTheDocument()
    expect(within(panel).getByText('Reason: Commit approval is required.')).toBeInTheDocument()
    expect(within(panel).getByText('Continuation stopped at open gate.')).toBeInTheDocument()
    expect(within(panel).getByText('Commit can transition to Push after approval.')).toBeInTheDocument()
  })

  it('renders decomposed health dimensions with evidence and diagnostics', () => {
    const health: WorkflowHealthAssessment = {
      repositoryId: 'repo-alpha',
      generatedAt: '2026-01-01T00:00:00Z',
      overallStatus: 'Attention',
      dimensions: [
        {
          name: 'Gate Integrity',
          status: 'Warning',
          reason: 'Open commit gate is waiting for human approval.',
          evidence: ['gate-commit'],
          diagnostics: ['Commit gate has satisfying command commit_execution.'],
        },
      ],
      influenceTrace: {
        repositoryId: 'repo-alpha',
        generatedAt: '2026-01-01T00:00:00Z',
        currentStage: 'Commit',
        progressState: 'WaitingForHuman',
        blockingGate: 'CommitApproval',
        evidencePaths: ['.agents/handoffs/handoff.md'],
        stageInfluences: [],
        progressionInfluences: [],
        preparationInfluences: [],
        gateInfluences: [],
        blockingInfluences: [],
        conflicts: ['commit approval is pending'],
        fingerprint: 'health-fingerprint',
        governanceInfluence: null,
      },
      diagnostics: ['Health assessment composed from workflow projection.'],
      governanceHealth: null,
    }

    render(<WorkflowHealthPanel health={health} />)

    const panel = screen.getByLabelText('Workflow health')
    expect(within(panel).getByText('Attention')).toBeInTheDocument()
    expect(within(panel).getByText('Gate Integrity')).toBeInTheDocument()
    expect(within(panel).getByText('Open commit gate is waiting for human approval.')).toBeInTheDocument()
    expect(within(panel).getByText('gate-commit')).toBeInTheDocument()
    expect(within(panel).getByText('Commit gate has satisfying command commit_execution.')).toBeInTheDocument()
    expect(within(panel).getByText('Health assessment composed from workflow projection.')).toBeInTheDocument()
  })

  it('renders certification findings, failures, evidence, and diagnostics', () => {
    const certification: WorkflowCertificationResult = {
      id: 'cert-alpha',
      repositoryId: 'repo-alpha',
      generatedAt: '2026-01-01T00:00:00Z',
      inputFingerprint: 'cert-fingerprint',
      certified: false,
      currentStage: 'Commit',
      progressState: 'WaitingForHuman',
      blockingGate: 'CommitApproval',
      passedFindingCount: 1,
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
      failures: ['Commit gate lacks approval'],
      diagnostics: ['Certification is observational only.'],
    }

    render(<WorkflowCertificationPanel certification={certification} />)

    const panel = screen.getByLabelText('Workflow certification')
    expect(within(panel).getAllByText('Findings')).toHaveLength(2)
    expect(within(panel).getByText('Failed')).toBeInTheDocument()
    expect(within(panel).getAllByText('Commit gate lacks approval')).toHaveLength(2)
    expect(
      within(panel).getByText('The workflow cannot certify completion until commit approval is recorded.'),
    ).toBeInTheDocument()
    expect(within(panel).getByText('gate-commit')).toBeInTheDocument()
    expect(within(panel).getByText('CommitApproval is open.')).toBeInTheDocument()
    expect(within(panel).getByText('Certification is observational only.')).toBeInTheDocument()
  })

  it('renders workflow reports through shared evidence and diagnostics components', () => {
    const reports = createReports()

    render(
      <WorkflowReportsPanel
        repositoryReport={reports.repositoryReport}
        progressionReport={reports.progressionReport}
        humanGovernanceReport={reports.governanceReport}
        readinessReport={reports.readinessReport}
      />,
    )

    const panel = screen.getByLabelText('Workflow reports')
    expect(within(panel).getByText('Attention')).toBeInTheDocument()
    expect(within(panel).getByText('Ready: No')).toBeInTheDocument()
    expect(within(panel).getByText('continuation.1')).toBeInTheDocument()
    expect(within(panel).getByText('Commit approval belongs to workflow authority.')).toBeInTheDocument()
    expect(within(panel).getByText('Commit gate lacks approval')).toBeInTheDocument()
    expect(within(panel).getByText('Repository report is observational.')).toBeInTheDocument()
    expect(within(panel).getByText('Readiness report preserved health diagnostics.')).toBeInTheDocument()
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

function createReports(): {
  repositoryReport: RepositoryWorkflowReport
  progressionReport: WorkflowProgressionReport
  governanceReport: HumanGovernanceReport
  readinessReport: WorkflowReadinessReport
} {
  return {
    repositoryReport: {
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
    },
    progressionReport: {
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
    },
    governanceReport: {
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
    },
    readinessReport: {
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
    },
  }
}
