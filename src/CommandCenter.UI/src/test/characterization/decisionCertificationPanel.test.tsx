import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DecisionCertificationPanel } from '../../features/decisions/DecisionCertificationPanel'
import type { DecisionCertificationReport } from '../../types'

afterEach(() => {
  cleanup()
})

describe('DecisionCertificationPanel', () => {
  it('shows certification evidence and governance findings without lifecycle mutation controls', () => {
    const onRunCertification = vi.fn()

    render(
      <DecisionCertificationPanel
        currentReport={createReport()}
        reports={[createReport('certification.202606221800000000001')]}
        isLoading={false}
        isRunning={false}
        error={null}
        onRunCertification={onRunCertification}
      />,
    )

    expect(screen.getByText('Result: Failed')).toBeInTheDocument()
    expect(screen.getByText('Category: Context')).toBeInTheDocument()
    expect(screen.getByText('Category: Authority')).toBeInTheDocument()
    expect(screen.getByText('Authority: authority-boundaries')).toBeInTheDocument()
    expect(screen.getByText('Blocks execution projection')).toBeInTheDocument()
    expect(screen.getByText(/\.agents\/decisions\/records\/DEC-0001\/decision\.json/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /resolve|accept|reject|promote|repair/i })).not.toBeInTheDocument()

    fireEvent.click(within(screen.getByLabelText('Decision certification')).getByRole('button', {
      name: 'Run Certification',
    }))

    expect(onRunCertification).toHaveBeenCalledTimes(1)
  })
})

function createReport(id = 'certification.current'): DecisionCertificationReport {
  return {
    id,
    repositoryId: 'repo-alpha',
    generatedAt: '2026-06-22T18:00:00.000Z',
    inputFingerprint: 'certification-fingerprint',
    result: {
      kind: 'Failed',
      passedEvidenceCount: 1,
      failedEvidenceCount: 1,
    },
    health: 'Blocked',
    diagnostics: ['Certification remains advisory and does not mutate lifecycle state.'],
    evidence: [
      {
        id: 'context-resolution',
        area: 'Context',
        passed: true,
        detail: 'Decision context rebuilt from repository artifacts.',
        sources: [
          {
            sourceKind: 'Plan',
            relativePath: '.agents/plan.md',
            section: null,
            itemId: null,
            decisionId: null,
            proposalId: null,
            candidateId: null,
            excerpt: 'Repository files remain authoritative.',
          },
        ],
        relatedDecisionIds: [],
        relatedCandidateIds: [],
        relatedProposalIds: [],
      },
      {
        id: 'authority-boundaries',
        area: 'Authority',
        passed: false,
        detail: 'A resolved decision claimed system authority.',
        sources: [
          {
            sourceKind: 'DecisionRecord',
            relativePath: '.agents/decisions/records/DEC-0001/decision.json',
            section: null,
            itemId: null,
            decisionId: 'DEC-0001',
            proposalId: null,
            candidateId: null,
            excerpt: 'ResolvedBy: governance',
          },
        ],
        relatedDecisionIds: ['DEC-0001'],
        relatedCandidateIds: [],
        relatedProposalIds: [],
      },
    ],
    findings: [
      {
        id: 'GOV-0001',
        category: 'AuthorityBoundary',
        severity: 'Blocking',
        blocksExecutionProjection: true,
        title: 'System authority boundary failed',
        detail: 'Governance cannot resolve decisions.',
        sources: [],
        relatedDecisionIds: ['DEC-0001'],
        relatedCandidateIds: [],
        relatedProposalIds: [],
      },
    ],
  }
}
