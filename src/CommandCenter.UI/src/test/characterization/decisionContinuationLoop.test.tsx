import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { PlanAuthoringScreen } from '../../features/planning/PlanAuthoringScreen'
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

async function renderToFirstReview() {
  installWorkspaceCertificationMock()
  const view = render(
    <PlanAuthoringScreen repositoryId="repo-empty" repositoryName="EmptyRepo" />,
  )

  fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
    target: { value: 'Ship the planning workflow.' },
  })
  fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))
  await screen.findByRole('region', { name: 'Rendered plan' })

  // Execute, which auto-completes and opens the decision phase in place.
  fireEvent.click(screen.getByRole('button', { name: 'Execute Plan' }))
  await screen.findByRole('region', { name: 'Decision runtime' })

  // Propose the first set of decisions and reach the review gate.
  fireEvent.click(await screen.findByRole('button', { name: 'Generate decisions' }))
  await screen.findByRole('textbox', { name: 'Decisions' })

  return view
}

describe('Decision submit and continuation loop', () => {
  it('completes two decision/continuation iterations without unmounting the screen', async () => {
    const { container } = await renderToFirstReview()
    const screenRoot = container.querySelector('.cc-plan-screen')
    expect(screenRoot).not.toBeNull()

    // --- Iteration 1: submit the first decisions ---
    fireEvent.click(screen.getByRole('button', { name: 'Submit decisions' }))

    // The continuation turn streams on the execution stream, in place, labelled ContinueExecution.
    const firstExecution = await screen.findByRole('region', { name: 'Plan execution' })
    await within(firstExecution).findByText('Continuing execution…')

    // The server then auto-starts the next decision run, reopening the review gate (iteration 2).
    await screen.findByText(/Turn 2/)
    const secondGate = await screen.findByRole('textbox', { name: 'Decisions' })
    expect(secondGate).toBeEnabled()

    // The screen never unmounted across the continuation.
    expect(container.querySelector('.cc-plan-screen')).toBe(screenRoot)

    // --- Iteration 2: submit again, proving the loop repeats ---
    fireEvent.click(screen.getByRole('button', { name: 'Submit decisions' }))

    // The second continuation rotates a new handoff, distinct from the first iteration's.
    await waitFor(() => {
      const execution = screen.getByRole('region', { name: 'Plan execution' })
      expect(within(execution).queryByText('.agents/handoffs/handoff.0002.md')).toBeInTheDocument()
    })

    // And the gate reopens once more for a third turn — the loop is genuinely repeating.
    await screen.findByText(/Turn 3/)
    expect(await screen.findByRole('textbox', { name: 'Decisions' })).toBeEnabled()

    // Still the same mounted screen after two full iterations.
    expect(container.querySelector('.cc-plan-screen')).toBe(screenRoot)
  })

  it('surfaces the transfer affordance on the Transfer-routed continuation, then clears it at review', async () => {
    await renderToFirstReview()

    // The first turn is the warm Continue path: no transfer affordance is ever shown.
    expect(
      screen.queryByRole('status', { name: 'Transferring decision session' }),
    ).not.toBeInTheDocument()

    // Submit turn 1; the mock routes the auto-started turn-2 decision run as a Transfer.
    fireEvent.click(screen.getByRole('button', { name: 'Submit decisions' }))

    // The transfer affordance appears while turn 2's session is handed off.
    await screen.findByRole('status', { name: 'Transferring decision session' })

    // It resolves once the proposal arrives and the review gate reopens for turn 2.
    await screen.findByText(/Turn 2/)
    expect(await screen.findByRole('textbox', { name: 'Decisions' })).toBeEnabled()
    await waitFor(() => {
      expect(
        screen.queryByRole('status', { name: 'Transferring decision session' }),
      ).not.toBeInTheDocument()
    })
  })

  it('does not navigate to the workspace on submit; only an explicit finish leaves the loop', async () => {
    let finishedCount = 0
    installWorkspaceCertificationMock()
    render(
      <PlanAuthoringScreen
        repositoryId="repo-empty"
        repositoryName="EmptyRepo"
        onExecuted={() => {
          finishedCount += 1
        }}
      />,
    )

    fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
      target: { value: 'Ship the planning workflow.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))
    await screen.findByRole('region', { name: 'Rendered plan' })

    fireEvent.click(screen.getByRole('button', { name: 'Execute Plan' }))
    await screen.findByRole('region', { name: 'Decision runtime' })
    fireEvent.click(await screen.findByRole('button', { name: 'Generate decisions' }))
    await screen.findByRole('textbox', { name: 'Decisions' })

    // Submitting drives the loop forward but never fires onExecuted.
    fireEvent.click(screen.getByRole('button', { name: 'Submit decisions' }))
    await screen.findByText(/Turn 2/)
    expect(finishedCount).toBe(0)

    // Only the explicit "Go to workspace" exit leaves the loop.
    fireEvent.click(await screen.findByRole('button', { name: 'Go to workspace' }))
    await waitFor(() => {
      expect(finishedCount).toBe(1)
    })
  })
})
