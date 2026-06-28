import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { DecisionRuntimeView } from '../../features/decision/DecisionRuntimeView'
import { PlanAuthoringScreen } from '../../features/planning/PlanAuthoringScreen'
import type { DecisionRunState } from '../../types'
import { installWorkspaceCertificationMock } from '../render'

afterEach(() => {
  cleanup()
  delete window.__COMMAND_CENTER_MOCK_STATE__
  delete window.__TAURI_INTERNALS__
  delete window.__COMMAND_CENTER_MOCK_PLAN_STREAM__
  delete window.__COMMAND_CENTER_MOCK_EXECUTION_STREAM__
  delete window.__COMMAND_CENTER_MOCK_DECISION_STREAM__
  window.history.pushState({}, '', '/')
})

const runningDecision: DecisionRunState = {
  status: 'Running',
  phase: 'GetNextDecisions',
  streamedText: 'Proposing…\n',
  diagnostics: { sandbox: 'read-only', approvals: 'never', seeded: true },
  proposedDecisions: null,
  editableDecisions: null,
  completion: null,
  submittedPath: null,
  submittedNumberedPath: null,
  submittedSequence: null,
  iteration: 1,
  transferring: false,
  failure: null,
}

const reviewReadyDecision: DecisionRunState = {
  ...runningDecision,
  status: 'Completed',
  phase: null,
  proposedDecisions: '- proposed\n',
  editableDecisions: '- proposed\n',
  completion: { promptTokens: 10, outputTokens: 5 },
}

const submittedDecision: DecisionRunState = {
  ...runningDecision,
  status: 'Submitted',
  phase: null,
  editableDecisions: null,
  submittedPath: '.agents/decisions/decisions.md',
  submittedNumberedPath: '.agents/decisions/decisions.0001.md',
  submittedSequence: 1,
}

describe('Decision runtime accessibility', () => {
  it('exposes the Proposed decisions output as a polite live region with an active step', () => {
    render(
      <DecisionRuntimeView
        state={runningDecision}
        onGenerate={() => undefined}
        onEditDecisions={() => undefined}
        onSubmitDecisions={() => undefined}
      />,
    )

    const output = screen.getByRole('region', { name: 'Proposed decisions output' })
    expect(output.querySelector('pre[aria-live="polite"]')).not.toBeNull()

    // The decision timeline exposes its active step via aria-current="step".
    const timeline = screen.getByRole('list', { name: 'Decision phases' })
    expect(timeline.querySelector('[aria-current="step"]')).not.toBeNull()
  })

  it('shows the editable Decisions control only after the proposal completes', () => {
    const { rerender } = render(
      <DecisionRuntimeView
        state={runningDecision}
        onGenerate={() => undefined}
        onEditDecisions={() => undefined}
        onSubmitDecisions={() => undefined}
      />,
    )

    // Before review-ready, the editable gate is absent.
    expect(screen.queryByRole('textbox', { name: 'Decisions' })).not.toBeInTheDocument()

    rerender(
      <DecisionRuntimeView
        state={reviewReadyDecision}
        onGenerate={() => undefined}
        onEditDecisions={() => undefined}
        onSubmitDecisions={() => undefined}
      />,
    )

    // Once the proposal completes, the labelled, editable Decisions control appears.
    const decisions = screen.getByRole('textbox', { name: 'Decisions' })
    expect(decisions).toBeInTheDocument()
    expect(decisions).not.toBeDisabled()
  })

  it('keeps the raw submitted decision path out of the primary surface', () => {
    render(
      <DecisionRuntimeView
        state={submittedDecision}
        onGenerate={() => undefined}
        onEditDecisions={() => undefined}
        onSubmitDecisions={() => undefined}
      />,
    )

    // The primary timeline step and the Continuing banner must read as human confirmations, never
    // raw `.agents/...` filesystem paths.
    const timeline = screen.getByRole('list', { name: 'Decision phases' })
    expect(timeline.textContent ?? '').not.toMatch(/\.agents\//)
    const banner = screen.getByRole('status', { name: 'Decisions submitted' })
    expect(banner.textContent ?? '').not.toMatch(/\.agents\//)

    // The raw paths are still addressable, but only inside the closed Diagnostics disclosure.
    const diagnostics = screen.getByLabelText('Decision diagnostics')
    expect(diagnostics.tagName.toLowerCase()).toBe('details')
    expect(diagnostics).not.toHaveAttribute('open')
    expect(diagnostics.textContent ?? '').toContain('.agents/decisions/decisions.0001.md')
  })

  it('keeps router/sandbox mechanics out of the primary flow, inside a Diagnostics disclosure', async () => {
    installWorkspaceCertificationMock()
    render(<PlanAuthoringScreen repositoryId="repo-empty" repositoryName="EmptyRepo" />)

    fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
      target: { value: 'Ship the dashboard.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))
    await screen.findByRole('region', { name: 'Rendered plan' })
    fireEvent.click(screen.getByRole('button', { name: 'Execute Plan' }))
    await screen.findByRole('region', { name: 'Decision runtime' })
    fireEvent.click(await screen.findByRole('button', { name: 'Generate decisions' }))
    await screen.findByRole('textbox', { name: 'Decisions' })

    // The sandbox diagnostics are addressable but live inside the secondary Diagnostics disclosure,
    // closed by default — not the primary reading flow.
    const diagnosticsDisclosure = screen.getByLabelText('Decision diagnostics')
    expect(diagnosticsDisclosure.tagName.toLowerCase()).toBe('details')
    expect(diagnosticsDisclosure).not.toHaveAttribute('open')
    expect(diagnosticsDisclosure).toContainElement(screen.getByLabelText('Sandbox diagnostics'))
  })
})
