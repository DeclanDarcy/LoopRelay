import { describe, expect, it } from 'vitest'
import {
  workflowCertificationFindingsToExplanation,
  workflowDiagnosticsToExplanation,
  workflowHealthDimensionsToExplanation,
} from '../../lib/explainability'
import type { WorkflowCertificationResult, WorkflowHealthAssessment } from '../../types'

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
  })
})
