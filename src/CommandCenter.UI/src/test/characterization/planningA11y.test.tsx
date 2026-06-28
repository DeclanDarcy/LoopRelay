import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
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

function renderScreen() {
  installWorkspaceCertificationMock()
  return render(<PlanAuthoringScreen repositoryId="repo-empty" repositoryName="EmptyRepo" />)
}

describe('Plan authoring accessibility', () => {
  it('labels the authoring controls: Roadmap, Specifications group, and New codebase checkbox', () => {
    renderScreen()

    // The Roadmap control is a labelled textbox.
    expect(screen.getByRole('textbox', { name: 'Roadmap' })).toBeInTheDocument()
    // The Specifications group carries an accessible name.
    expect(screen.getByLabelText('Specifications')).toBeInTheDocument()
    // The New codebase checkbox is labelled by its wrapping label text.
    const newCodebase = screen.getByRole('checkbox', { name: 'New codebase' })
    expect(newCodebase).toBeInTheDocument()
  })

  it('exposes the Planning stream as a polite live region while a turn runs', async () => {
    renderScreen()

    fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
      target: { value: 'Ship the dashboard.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))

    const stream = await screen.findByRole('region', { name: 'Planning stream' })
    // The accumulating output sits in a polite live region so it is announced as it streams.
    const live = stream.querySelector('pre[aria-live="polite"]')
    expect(live).not.toBeNull()
  })

  it('labels the Feedback control and exposes the rendered-plan copy status as a live region', async () => {
    renderScreen()

    fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
      target: { value: 'Ship the dashboard.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))
    const planRegion = await screen.findByRole('region', { name: 'Rendered plan' })

    // Feedback becomes a labelled textbox once the plan is rendered.
    expect(screen.getByRole('textbox', { name: 'Feedback' })).toBeInTheDocument()

    // The copy button reports its status through a polite live region.
    const copyButton = screen.getByRole('button', { name: 'Copy plan' })
    expect(copyButton.querySelector('[aria-live="polite"]')).not.toBeNull()
    expect(planRegion).toBeInTheDocument()
  })

  it('exposes the Execution output as a polite live region with an aria-current active step', () => {
    // A deterministically-Running execution state: the first phase is active, the rest pending.
    const runningExecution: ExecutionRunState = {
      status: 'Running',
      phase: 'ExtractMilestones',
      streamedText: 'Reading plan…\n',
      milestoneCount: null,
      commit: null,
      handoff: null,
      completion: null,
      failure: null,
    }

    render(<ExecutionStreamView state={runningExecution} />)

    const output = screen.getByRole('region', { name: 'Execution output' })
    expect(output.querySelector('pre[aria-live="polite"]')).not.toBeNull()

    // The execution timeline exposes its active step via aria-current="step".
    const timeline = screen.getByRole('list', { name: 'Execution phases' })
    expect(timeline.querySelector('[aria-current="step"]')).not.toBeNull()
  })
})
