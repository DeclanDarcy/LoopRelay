import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ExecutionStreamView } from '../../features/planning/ExecutionStreamView'
import { PlanAuthoringScreen } from '../../features/planning/PlanAuthoringScreen'
import type { ExecutionRunState } from '../../types'
import { installWorkspaceCertificationMock } from '../render'

afterEach(() => {
  cleanup()
  delete window.__COMMAND_CENTER_MOCK_STATE__
  delete window.__TAURI_INTERNALS__
  delete window.__COMMAND_CENTER_MOCK_PLAN_STREAM__
  delete window.__COMMAND_CENTER_MOCK_EXECUTION_STREAM__
  window.history.pushState({}, '', '/')
})

async function renderToPlanReady() {
  installWorkspaceCertificationMock()
  render(<PlanAuthoringScreen repositoryId="repo-empty" repositoryName="EmptyRepo" />)

  fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
    target: { value: 'Ship the dashboard.' },
  })
  fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))
  await screen.findByRole('region', { name: 'Rendered plan' })
}

describe('ExecutionStreamView surface', () => {
  it('renders the execution surface and phase timeline when Execute Plan is clicked', async () => {
    await renderToPlanReady()

    fireEvent.click(screen.getByRole('button', { name: 'Execute Plan' }))

    expect(await screen.findByRole('region', { name: 'Plan execution' })).toBeInTheDocument()
    expect(screen.getByRole('list', { name: 'Execution phases' })).toBeInTheDocument()
  })

  it('shows the milestone count and commit sha as a product result on completion', async () => {
    await renderToPlanReady()

    fireEvent.click(screen.getByRole('button', { name: 'Execute Plan' }))

    const result = await screen.findByRole('group', { name: 'Execution result' })
    expect(result).toHaveTextContent('3')
    // The commit sha is a product-meaningful result and stays in the primary summary.
    expect(result).toHaveTextContent('a1b2c3d4e5')
    // The handoff is confirmed in human terms; the raw path moved to the Diagnostics disclosure.
    expect(result).toHaveTextContent('Handoff recorded')
    expect(result).not.toHaveTextContent('.agents/handoffs/handoff.0001.md')
  })

  it('keeps the raw handoff path in a secondary Diagnostics disclosure, closed by default', async () => {
    await renderToPlanReady()

    fireEvent.click(screen.getByRole('button', { name: 'Execute Plan' }))
    await screen.findByRole('group', { name: 'Execution result' })

    const diagnostics = screen.getByLabelText('Execution diagnostics')
    expect(diagnostics).toBeInTheDocument()
    // Diagnostics is secondary: a closed-by-default details disclosure, not the primary flow.
    expect(diagnostics).not.toHaveAttribute('open')
    // The raw filesystem path lives only inside Diagnostics.
    expect(diagnostics).toHaveTextContent('.agents/handoffs/handoff.0001.md')
  })

  it('does not call onExecuted when the run completes — the decision review gate runs first', async () => {
    installWorkspaceCertificationMock()
    let executedCount = 0
    render(
      <PlanAuthoringScreen
        repositoryId="repo-empty"
        repositoryName="EmptyRepo"
        onExecuted={() => {
          executedCount += 1
        }}
      />,
    )

    fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
      target: { value: 'Ship the dashboard.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))
    await screen.findByRole('region', { name: 'Rendered plan' })

    fireEvent.click(screen.getByRole('button', { name: 'Execute Plan' }))

    // The run is in flight: onExecuted has not fired yet.
    await screen.findByRole('region', { name: 'Plan execution' })
    expect(executedCount).toBe(0)

    // When the run completes, the decision phase begins in place rather than navigating —
    // onExecuted is deferred until the human-review gate closes (see decisionRuntime.test.tsx).
    await screen.findByRole('region', { name: 'Decision runtime' })
    expect(executedCount).toBe(0)
  })

  it('renders an execution failure with phase, reason, detail, and a way back to the plan', () => {
    // The mock run auto-completes, so failure rendering is exercised directly on the view —
    // the reducer's failure handling is covered exhaustively in executionRunMachine.test.ts.
    const failedState: ExecutionRunState = {
      status: 'Failed',
      phase: null,
      streamedText: 'Launching execution agent…\n',
      milestoneCount: 3,
      commit: null,
      handoff: null,
      completion: null,
      failure: { phase: 'StartExecution', reason: 'Agent process exited', detail: 'exit 1' },
    }
    const onDismissFailure = vi.fn()

    render(<ExecutionStreamView state={failedState} onDismissFailure={onDismissFailure} />)

    const failure = screen.getByRole('alert', { name: 'Execution failed' })
    expect(failure).toHaveTextContent('Execution failed: StartExecution')
    expect(failure).toHaveTextContent('Agent process exited')
    expect(failure).toHaveTextContent('exit 1')

    const backButton = screen.getByRole('button', { name: 'Back to plan' })
    fireEvent.click(backButton)
    expect(onDismissFailure).toHaveBeenCalledTimes(1)
  })
})
