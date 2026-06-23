import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DecisionGenerationCertificationPanel } from '../../features/decisions/DecisionGenerationCertificationPanel'
import type { DecisionGenerationCertificationReport } from '../../types'

afterEach(() => {
  cleanup()
})

describe('DecisionGenerationCertificationPanel', () => {
  it('shows advisory workflow replacement evidence without authority controls', () => {
    const onRunCertification = vi.fn()

    render(
      <DecisionGenerationCertificationPanel
        currentReport={createReport()}
        reports={[createReport('generation-certification.202606221800000000001')]}
        isLoading={false}
        isRunning={false}
        error={null}
        onRunCertification={onRunCertification}
      />,
    )

    expect(screen.getByText('Certified: No')).toBeInTheDocument()
    expect(screen.getByText('Generation: Passed')).toBeInTheDocument()
    expect(screen.getByText('Workflow Replacement: Failed')).toBeInTheDocument()
    expect(screen.getByText('workflow-replacement failed because quality evidence is missing.')).toBeInTheDocument()
    expect(screen.getByText('ReviewOnly')).toBeInTheDocument()
    expect(screen.getByText('.agents/decisions/proposals/PROP-0001/proposal.json')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /resolve|accept|reject|promote|approve/i })).not.toBeInTheDocument()

    fireEvent.click(within(screen.getByLabelText('Decision generation certification')).getByRole('button', {
      name: 'Run Generation Certification',
    }))

    expect(onRunCertification).toHaveBeenCalledTimes(1)
  })
})

function createReport(
  id = 'generation-certification.current',
): DecisionGenerationCertificationReport {
  return {
    id,
    repositoryId: 'repo-alpha',
    generatedAt: '2026-06-22T18:00:00.000Z',
    inputFingerprint: 'generation-certification-fingerprint',
    candidateCount: 1,
    generatedProposalCount: 1,
    generatedPackageCount: 1,
    generatedResolvedDecisionCount: 1,
    executionInfluenceTraceCount: 0,
    result: {
      generationCertified: true,
      governanceCertified: true,
      throughputCertified: true,
      qualityCertified: false,
      consumptionCertified: false,
      workflowReplacementCertified: false,
      certified: false,
      failures: ['workflow-replacement failed because quality evidence is missing.'],
      findings: [
        {
          id: 'generation-capability',
          category: 'GenerationCapability',
          passed: true,
          summary: 'Generated options exist',
          detail: 'Generated proposal packages include alternatives and tradeoffs.',
          sources: [
            {
              sourceKind: 'DecisionProposal',
              relativePath: '.agents/decisions/proposals/PROP-0001/proposal.json',
              section: null,
              itemId: null,
              decisionId: null,
              proposalId: 'PROP-0001',
              candidateId: 'CAND-0001',
              excerpt: 'Options generated from repository evidence.',
            },
          ],
          relatedDecisionIds: [],
          relatedCandidateIds: ['CAND-0001'],
          relatedProposalIds: ['PROP-0001'],
        },
        {
          id: 'workflow-replacement',
          category: 'WorkflowReplacement',
          passed: false,
          summary: 'Workflow replacement not certified',
          detail: 'Quality evidence and execution influence are incomplete.',
          sources: [],
          relatedDecisionIds: ['DEC-0001'],
          relatedCandidateIds: [],
          relatedProposalIds: [],
        },
      ],
    },
    humanAuthoringBurden: {
      repositoryId: 'repo-alpha',
      decisionCount: 1,
      reviewOnlyCount: 1,
      minorEditCount: 0,
      majorRefinementCount: 0,
      fullRewriteCount: 0,
      generationBypassedCount: 0,
      unknownCount: 0,
      signals: [
        {
          id: 'burden-DEC-0001',
          repositoryId: 'repo-alpha',
          decisionId: 'DEC-0001',
          burden: 'ReviewOnly',
          sourceKind: 'DecisionResolution',
          summary: 'Human resolved generated content without edits.',
          sources: [],
        },
      ],
    },
    qualityAssessments: [],
    diagnostics: ['Generation certification is advisory and does not mutate lifecycle authority.'],
  }
}
