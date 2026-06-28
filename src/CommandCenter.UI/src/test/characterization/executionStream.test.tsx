import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
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

  it('shows the milestone count, commit sha, and rotated handoff path on completion', async () => {
    await renderToPlanReady()

    fireEvent.click(screen.getByRole('button', { name: 'Execute Plan' }))

    const result = await screen.findByRole('group', { name: 'Execution result' })
    expect(result).toHaveTextContent('3')
    expect(result).toHaveTextContent('a1b2c3d4e5')
    expect(result).toHaveTextContent('.agents/handoffs/handoff.0001.md')
  })

  it('calls onExecuted only after the run completes, not when execute is submitted', async () => {
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

    // Once the run completes, onExecuted fires exactly once.
    await waitFor(() => expect(executedCount).toBe(1))
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
