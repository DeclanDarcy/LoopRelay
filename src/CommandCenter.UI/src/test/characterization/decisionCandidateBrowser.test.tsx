import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DecisionCandidateBrowser } from '../../features/decisions/DecisionCandidateBrowser'
import type {
  DecisionCandidate,
  DecisionCandidateState,
  DecisionLifecycleEntityEligibility,
} from '../../types'

afterEach(() => {
  cleanup()
})

describe('DecisionCandidateBrowser', () => {
  it('renders active candidate rows by default', () => {
    render(
      <DecisionCandidateBrowser
        candidates={createCandidates()}
        isLoading={false}
        onSelectedCandidateChange={vi.fn()}
      />,
    )

    const rows = screen.getByRole('list', { name: 'Candidate browser rows' })
    expect(within(rows).getByText('Review workspace boundary')).toBeInTheDocument()
    expect(within(rows).getByText('Candidate browser navigation')).toBeInTheDocument()
    expect(within(rows).queryByText('Inline mutation controls')).not.toBeInTheDocument()
  })

  it('filters dismissed, expired, and duplicate candidates without mutating lifecycle state', () => {
    render(<DecisionCandidateBrowser candidates={createCandidates()} isLoading={false} />)

    fireEvent.click(screen.getByRole('button', { name: 'Dismissed' }))
    fireEvent.click(screen.getByRole('button', { name: 'Expired' }))
    fireEvent.click(screen.getByRole('button', { name: 'Duplicate' }))

    const rows = screen.getByRole('list', { name: 'Candidate browser rows' })
    expect(within(rows).getByText('Inline mutation controls')).toBeInTheDocument()
    expect(within(rows).getByText('Outdated review shortcut')).toBeInTheDocument()
    expect(within(rows).getByText('Duplicate proposal browser work')).toBeInTheDocument()

    expect(screen.queryByText('Promote candidate')).not.toBeInTheDocument()
    expect(screen.queryByText('Dismiss candidate')).not.toBeInTheDocument()
  })

  it('keeps selected candidate as local presentation state', () => {
    const onSelectedCandidateChange = vi.fn()

    render(
      <DecisionCandidateBrowser
        candidates={createCandidates()}
        isLoading={false}
        onSelectedCandidateChange={onSelectedCandidateChange}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: /Candidate browser navigation/ }))

    const selectedPanel = screen.getByLabelText('Selected candidate')
    expect(within(selectedPanel).getByText('CAND-0002')).toBeInTheDocument()
    expect(within(selectedPanel).getByText('Tactical')).toBeInTheDocument()
    expect(onSelectedCandidateChange).toHaveBeenLastCalledWith('CAND-0002')
  })

  it('renders backend lifecycle eligibility and disables blocked candidate actions', () => {
    render(
      <DecisionCandidateBrowser
        candidates={createCandidates()}
        isLoading={false}
        eligibility={[
          createEligibility('CAND-0001', 'Promoted', [
            createAction('dismiss_decision_candidate', 'Dismiss', 'Dismissed', true, null),
          ], [
            createAction(
              'promote_decision_candidate',
              'Promote',
              'Promoted',
              false,
              'Transition from Promoted to Promoted is not currently allowed.',
            ),
          ]),
        ]}
        onPromote={vi.fn()}
        onDismiss={vi.fn()}
      />,
    )

    expect(screen.getByLabelText('Candidate lifecycle eligibility')).toHaveTextContent('Dismiss')
    expect(screen.getByText('Transition from Promoted to Promoted is not currently allowed.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Promote Candidate' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Dismiss' })).toBeEnabled()
  })
})

function createCandidates(): DecisionCandidate[] {
  return [
    createCandidate('CAND-0001', 'Promoted', 'Review workspace boundary'),
    createCandidate('CAND-0002', 'Discovered', 'Candidate browser navigation'),
    createCandidate('CAND-0003', 'Dismissed', 'Inline mutation controls'),
    createCandidate('CAND-0004', 'Expired', 'Outdated review shortcut'),
    createCandidate('CAND-0005', 'Duplicate', 'Duplicate proposal browser work'),
  ]
}

function createCandidate(
  id: string,
  state: DecisionCandidateState,
  title: string,
): DecisionCandidate {
  return {
    id,
    repositoryId: 'repo-alpha',
    state,
    priority: state === 'Promoted' ? 'High' : 'Medium',
    classification: id === 'CAND-0002' ? 'Tactical' : 'Architectural',
    title,
    summary: `${title} summary.`,
    sourceFingerprint: `fingerprint-${id}`,
    signals: [],
    evidence: [],
    sources: [],
    diagnostics: [],
    history: [],
  }
}

function createEligibility(
  entityId: string,
  currentState: string,
  allowedActions: DecisionLifecycleEntityEligibility['allowedActions'],
  blockedActions: DecisionLifecycleEntityEligibility['blockedActions'],
): DecisionLifecycleEntityEligibility {
  return {
    entityKind: 'Candidate',
    entityId,
    currentState,
    allowedActions,
    blockedActions,
    allowedNextStates: allowedActions.map((action) => action.targetState),
    blockedNextStates: [],
    diagnostics: [],
  }
}

function createAction(
  commandName: string,
  displayName: string,
  targetState: string,
  isAllowed: boolean,
  reason: string | null,
): DecisionLifecycleEntityEligibility['allowedActions'][number] {
  return {
    commandName,
    displayName,
    targetState,
    isAllowed,
    requiredInputs: ['reason'],
    reason,
    governingRule: 'DecisionLifecycleRules.ValidateCandidateTransition',
  }
}
