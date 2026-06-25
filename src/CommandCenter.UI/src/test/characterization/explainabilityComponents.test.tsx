import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import {
  ActionEligibilityView,
  AlternativeExplorer,
  CertificationFindingsView,
  ConstraintViewer,
  DecisionBasis,
  DiagnosticList,
  EvidenceList,
  HealthView,
  UncertaintyView,
} from '../../components/explainability'
import type { Explanation } from '../../types'

afterEach(() => {
  cleanup()
})

describe('explainability components', () => {
  it('renders evidence with source and fingerprint without changing labels', () => {
    render(
      <EvidenceList
        evidence={[
          {
            label: 'gate-commit',
            detail: 'Commit approval gate',
            source: '.agents/workflow/gates.json',
            fingerprint: 'abc123',
          },
        ]}
      />,
    )

    expect(screen.getByText('gate-commit')).toBeInTheDocument()
    expect(screen.getByText('Commit approval gate')).toBeInTheDocument()
    expect(screen.getByText('.agents/workflow/gates.json')).toBeInTheDocument()
    expect(screen.getByText('abc123')).toBeInTheDocument()
  })

  it('renders constraints, alternatives, uncertainty, diagnostics, actions, health, and certification findings', () => {
    render(
      <section>
        <ConstraintViewer constraints={[{ label: 'Commit gate', detail: 'Approval required.', satisfied: false }]} />
        <AlternativeExplorer
          alternatives={[{ label: 'Defer commit', detail: 'Keep workflow waiting.', selected: true, reason: 'Gate open.' }]}
        />
        <UncertaintyView
          uncertainty={[
            {
              label: 'Missing trace',
              detail: 'No trace artifact was found.',
              severity: 'warning',
              missingEvidence: [{ label: '.agents/workflow/trace.json' }],
            },
          ]}
        />
        <DiagnosticList diagnostics={[{ label: 'Workflow', detail: 'Certification is observational only.' }]} />
        <ActionEligibilityView
          actions={[{ label: 'Commit execution', detail: 'Wait for approval.', eligible: false, command: 'commit_execution' }]}
        />
        <HealthView
          dimensions={[
            {
              name: 'Gate Integrity',
              status: 'Warning',
              tone: 'warning',
              reason: 'Commit gate is open.',
              evidence: [{ label: 'gate-commit' }],
              diagnostics: [{ label: 'Diagnostic', detail: 'CommitApproval is open.' }],
            },
          ]}
        />
        <CertificationFindingsView
          findings={[
            {
              id: 'finding-gate',
              title: 'Commit gate lacks approval',
              category: 'Gate',
              passed: false,
              detail: 'Approval is required.',
              evidence: [{ label: 'gate-commit' }],
              diagnostics: [{ label: 'Diagnostic', detail: 'CommitApproval is open.' }],
            },
          ]}
        />
      </section>,
    )

    expect(screen.getByText('Commit gate')).toBeInTheDocument()
    expect(screen.getByText('Defer commit')).toBeInTheDocument()
    expect(screen.getByText('Missing trace')).toBeInTheDocument()
    expect(screen.getByText('Certification is observational only.')).toBeInTheDocument()
    expect(screen.getByText('Commit execution')).toBeInTheDocument()
    expect(screen.getByText('Gate Integrity')).toBeInTheDocument()
    expect(screen.getByText('Commit gate lacks approval')).toBeInTheDocument()
  })

  it('renders a decision basis from presentation-only explanation fields', () => {
    const explanation: Explanation = {
      domain: 'Workflow',
      title: 'Commit Approval',
      summary: 'Workflow is waiting for human approval.',
      why: 'CommitApproval is the blocking gate.',
      evidence: [{ label: 'gate-commit' }],
      constraints: [{ label: 'Approval', detail: 'A human approval must be recorded.', satisfied: false }],
      alternatives: [{ label: 'Continue waiting', detail: 'No command runs until approval.', selected: true }],
      diagnostics: [{ label: 'Gate', detail: 'CommitApproval is open.' }],
      uncertainty: [{ label: 'Remote state', detail: 'Push state is not evaluated until commit.' }],
      actions: [{ label: 'Approve commit', detail: 'Record approval.', eligible: true }],
      healthDimensions: [
        {
          name: 'Gate Integrity',
          status: 'Warning',
          reason: 'Open commit gate.',
          evidence: [{ label: 'gate-commit' }],
          diagnostics: [],
        },
      ],
    }

    render(<DecisionBasis explanation={explanation} />)

    const basis = screen.getByLabelText('Workflow explanation')
    expect(within(basis).getByText('Commit Approval')).toBeInTheDocument()
    expect(within(basis).getByText('Workflow is waiting for human approval.')).toBeInTheDocument()
    expect(within(basis).getByText('Why: CommitApproval is the blocking gate.')).toBeInTheDocument()
    expect(within(basis).getAllByText('gate-commit').length).toBeGreaterThan(0)
  })
})
