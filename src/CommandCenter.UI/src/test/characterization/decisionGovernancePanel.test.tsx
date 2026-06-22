import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DecisionGovernancePanel } from '../../features/decisions/DecisionGovernancePanel'
import type { DecisionGovernanceReport } from '../../types'

afterEach(() => {
  cleanup()
})

describe('DecisionGovernancePanel', () => {
  it('groups advisory findings by severity and category without repair actions', () => {
    const onGenerateReport = vi.fn()
    const onSelectProposal = vi.fn()

    render(
      <DecisionGovernancePanel
        currentReport={createReport()}
        reports={[createReport('governance.202606221800000000001')]}
        isLoading={false}
        isGenerating={false}
        error={null}
        onGenerateReport={onGenerateReport}
        onSelectProposal={onSelectProposal}
      />,
    )

    expect(screen.getByText('Blocking')).toBeInTheDocument()
    expect(screen.getByText('Warning')).toBeInTheDocument()
    expect(screen.getByText('ExecutionProjectionReadiness')).toBeInTheDocument()
    expect(screen.getByText('DecisionCoverage')).toBeInTheDocument()
    expect(screen.getByText('Blocks execution projection')).toBeInTheDocument()
    expect(screen.getByText('.agents/decisions/proposals/PROP-0001/proposal.json')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /repair|fix|resolve|correct/i })).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'View PROP-0001' }))

    expect(onSelectProposal).toHaveBeenCalledWith('PROP-0001')
  })

  it('generates a persistent report only through the report action', () => {
    const onGenerateReport = vi.fn()

    render(
      <DecisionGovernancePanel
        currentReport={createReport()}
        reports={[]}
        isLoading={false}
        isGenerating={false}
        error={null}
        onGenerateReport={onGenerateReport}
        onSelectProposal={vi.fn()}
      />,
    )

    fireEvent.click(within(screen.getByLabelText('Decision governance')).getByRole('button', {
      name: 'Generate Report',
    }))

    expect(onGenerateReport).toHaveBeenCalledTimes(1)
  })
})

function createReport(id = 'governance.current'): DecisionGovernanceReport {
  return {
    id,
    repositoryId: 'repo-alpha',
    generatedAt: '2026-06-22T18:00:00.000Z',
    inputFingerprint: 'governance-fingerprint',
    health: 'Blocked',
    summary: {
      decisionCount: 1,
      resolvedDecisionCount: 1,
      activeCandidateCount: 1,
      activeProposalCount: 1,
      assimilationRecommendationCount: 0,
      findingCount: 2,
      blockingFindingCount: 1,
    },
    diagnostics: ['Current governance is advisory.'],
    findings: [
      {
        id: 'GOV-0001',
        category: 'ExecutionProjectionReadiness',
        severity: 'Blocking',
        blocksExecutionProjection: true,
        title: 'Resolved decision is not projection-ready',
        detail: 'A blocking finding prevents projection until execution decides how to act.',
        sources: [
          {
            sourceKind: 'DecisionProposal',
            relativePath: '.agents/decisions/proposals/PROP-0001/proposal.json',
            section: null,
            itemId: null,
            decisionId: null,
            proposalId: 'PROP-0001',
            candidateId: 'CAND-0001',
            excerpt: 'Proposal evidence remains source-linked.',
          },
        ],
        relatedDecisionIds: ['DEC-0001'],
        relatedCandidateIds: ['CAND-0001'],
        relatedProposalIds: ['PROP-0001'],
      },
      {
        id: 'GOV-0002',
        category: 'DecisionCoverage',
        severity: 'Warning',
        blocksExecutionProjection: false,
        title: 'Candidate coverage gap',
        detail: 'A promoted candidate still needs a resolved decision.',
        sources: [],
        relatedDecisionIds: [],
        relatedCandidateIds: ['CAND-0002'],
        relatedProposalIds: [],
      },
    ],
  }
}
