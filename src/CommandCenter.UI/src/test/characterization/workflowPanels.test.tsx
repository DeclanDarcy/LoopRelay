import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import {
  WorkflowCertificationPanel,
  WorkflowHealthPanel,
  WorkflowRecoveryPanel,
} from '../../features/workflow/WorkflowPanels'
import type {
  WorkflowCertificationResult,
  WorkflowHealthAssessment,
  WorkflowRecoveryDiagnostics,
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
})
