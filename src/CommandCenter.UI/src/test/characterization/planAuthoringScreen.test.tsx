import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { PlanAuthoringScreen } from '../../features/planning/PlanAuthoringScreen'
import { installWorkspaceCertificationMock } from '../render'

afterEach(() => {
  cleanup()
  delete window.__COMMAND_CENTER_MOCK_STATE__
  delete window.__TAURI_INTERNALS__
  delete window.__COMMAND_CENTER_MOCK_PLAN_STREAM__
  window.history.pushState({}, '', '/')
})

function renderScreen() {
  installWorkspaceCertificationMock()
  return render(
    <PlanAuthoringScreen repositoryId="repo-empty" repositoryName="EmptyRepo" />,
  )
}

describe('PlanAuthoringScreen', () => {
  it('disables Write Plan until the roadmap has non-whitespace text', () => {
    renderScreen()

    const writeButton = screen.getByRole('button', { name: 'Write Plan' })
    expect(writeButton).toBeDisabled()

    fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
      target: { value: '   ' },
    })
    expect(writeButton).toBeDisabled()

    fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
      target: { value: 'Ship the dashboard.' },
    })
    expect(writeButton).toBeEnabled()
  })

  it('adds and removes spec textareas', () => {
    renderScreen()

    expect(screen.queryByRole('textbox', { name: 'Specification 1' })).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Add Spec' }))
    expect(screen.getByRole('textbox', { name: 'Specification 1' })).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Remove specification 1' }))
    expect(screen.queryByRole('textbox', { name: 'Specification 1' })).not.toBeInTheDocument()
  })

  it('streams deltas then shows the rendered plan with an accessible copy button', async () => {
    renderScreen()

    fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
      target: { value: 'Ship the dashboard.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))

    const planRegion = await screen.findByRole('region', { name: 'Rendered plan' })
    expect(planRegion).toBeInTheDocument()
    expect(planRegion).toHaveTextContent('Implementation Plan')

    expect(screen.getByRole('button', { name: 'Copy plan' })).toBeInTheDocument()
  })

  it('keeps Revise Plan disabled until feedback has text, after a plan exists', async () => {
    renderScreen()

    fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
      target: { value: 'Ship the dashboard.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))

    await screen.findByRole('region', { name: 'Rendered plan' })

    const reviseButton = screen.getByRole('button', { name: 'Revise Plan' })
    expect(reviseButton).toBeDisabled()

    fireEvent.change(screen.getByRole('textbox', { name: 'Feedback' }), {
      target: { value: 'Split the milestone.' },
    })
    expect(reviseButton).toBeEnabled()
  })

  it('surfaces a failed planning event with its reason', async () => {
    installWorkspaceCertificationMock()
    render(<PlanAuthoringScreen repositoryId="repo-empty" repositoryName="EmptyRepo" />)

    await waitFor(() => {
      expect(window.__COMMAND_CENTER_MOCK_PLAN_STREAM__).toBeDefined()
    })

    const bridge = window.__COMMAND_CENTER_MOCK_PLAN_STREAM__!
    const state = window.__COMMAND_CENTER_MOCK_STATE__!
    // Drive a failure directly through the stream bridge the screen is subscribed to.
    fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
      target: { value: 'Ship the dashboard.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))

    // Subscribe a noop to ensure a subscriber list exists, then emit a failure.
    bridge.subscribe('repo-empty', () => undefined)
    state.planStreamSubscribers['repo-empty'].forEach((notify) =>
      notify({ type: 'failed', reason: 'Agent process exited', detail: 'exit code 1' }),
    )

    expect(await screen.findByRole('alert', { name: 'Planning failed' })).toHaveTextContent(
      'Agent process exited',
    )
  })
})
