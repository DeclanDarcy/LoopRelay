import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
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

async function renderToDecisionPhase() {
  installWorkspaceCertificationMock()
  render(<PlanAuthoringScreen repositoryId="repo-empty" repositoryName="EmptyRepo" />)

  fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
    target: { value: 'Ship the dashboard.' },
  })
  fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))
  await screen.findByRole('region', { name: 'Rendered plan' })

  fireEvent.click(screen.getByRole('button', { name: 'Execute Plan' }))
  // The execution run auto-completes, which begins the decision phase in place.
  return screen.findByRole('region', { name: 'Decision runtime' })
}

describe('DecisionRuntimeView surface', () => {
  it('surfaces the decision runtime once the execution run completes', async () => {
    await renderToDecisionPhase()

    expect(
      await screen.findByRole('button', { name: 'Generate decisions' }),
    ).toBeInTheDocument()
  })

  it('keeps the decisions textarea absent until review-ready, then editable after', async () => {
    await renderToDecisionPhase()

    fireEvent.click(await screen.findByRole('button', { name: 'Generate decisions' }))

    // While the run is in flight (before review-ready), the editable gate must stay closed.
    expect(screen.queryByRole('textbox', { name: 'Decisions' })).not.toBeInTheDocument()

    // review-ready opens the gate: the textarea appears, editable and prefilled.
    const decisions = await screen.findByRole('textbox', { name: 'Decisions' })
    expect(decisions).toBeInTheDocument()
    expect(decisions).not.toBeDisabled()
    expect((decisions as HTMLTextAreaElement).value).toContain('Stream the decision run over SSE')

    // The reviewer can edit the captured text in place.
    fireEvent.change(decisions, { target: { value: '- Use SSE for the decision stream.' } })
    expect((screen.getByRole('textbox', { name: 'Decisions' }) as HTMLTextAreaElement).value).toBe(
      '- Use SSE for the decision stream.',
    )
  })

  it('confirms submission and shows the rotated path while the continuation turn runs', async () => {
    await renderToDecisionPhase()

    fireEvent.click(await screen.findByRole('button', { name: 'Generate decisions' }))
    await screen.findByRole('textbox', { name: 'Decisions' })

    fireEvent.click(screen.getByRole('button', { name: 'Submit decisions' }))

    // Submit is no longer terminal: the gate closes and the surface shows the continuation handoff,
    // carrying the rotated numbered submission path rather than ending the run.
    const confirmation = await screen.findByRole('status', { name: 'Decisions submitted' })
    expect(confirmation).toHaveTextContent('Decisions persisted.')
    // The rotated path lands once the backend confirms the submission.
    expect(await screen.findByText('.agents/decisions/decisions.0001.md')).toBeInTheDocument()
  })

  it('renders a decision-run failure with phase, reason, detail, and a way back', () => {
    // The mock run auto-completes, so failure rendering is exercised directly on the view —
    // the reducer's failure handling is covered exhaustively in decisionRunMachine.test.ts.
    const failedState: DecisionRunState = {
      status: 'Failed',
      phase: null,
      streamedText: 'Proposing decisions…\n',
      diagnostics: { sandbox: 'read-only', approvals: 'never', seeded: true },
      proposedDecisions: null,
      editableDecisions: null,
      completion: null,
      submittedPath: null,
      submittedNumberedPath: null,
      submittedSequence: null,
      iteration: 1,
      failure: { phase: 'GetNextDecisions', reason: 'Agent process exited', detail: 'exit 1' },
    }
    const onDismissFailure = vi.fn()

    render(
      <DecisionRuntimeView
        state={failedState}
        onGenerate={() => undefined}
        onEditDecisions={() => undefined}
        onSubmitDecisions={() => undefined}
        onDismissFailure={onDismissFailure}
      />,
    )

    const failure = screen.getByRole('alert', { name: 'Decision run failed' })
    expect(failure).toHaveTextContent('Decision run failed: GetNextDecisions')
    expect(failure).toHaveTextContent('Agent process exited')
    expect(failure).toHaveTextContent('exit 1')

    fireEvent.click(screen.getByRole('button', { name: 'Back to plan' }))
    expect(onDismissFailure).toHaveBeenCalledTimes(1)
  })

  it('does not show the editable gate while a failed state is active', () => {
    const failedState: DecisionRunState = {
      status: 'Failed',
      phase: null,
      streamedText: '',
      diagnostics: null,
      proposedDecisions: null,
      editableDecisions: null,
      completion: null,
      submittedPath: null,
      submittedNumberedPath: null,
      submittedSequence: null,
      iteration: 1,
      failure: { phase: null, reason: 'boom', detail: null },
    }

    render(
      <DecisionRuntimeView
        state={failedState}
        onGenerate={() => undefined}
        onEditDecisions={() => undefined}
        onSubmitDecisions={() => undefined}
      />,
    )

    expect(screen.queryByRole('textbox', { name: 'Decisions' })).not.toBeInTheDocument()
  })
})
