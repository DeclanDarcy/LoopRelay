import { cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import App from '../../App'
import { renderWithWorkspaceCertification } from '../render'

afterEach(() => {
  cleanup()
  delete window.__COMMAND_CENTER_MOCK_STATE__
  delete window.__TAURI_INTERNALS__
  delete window.__COMMAND_CENTER_MOCK_PLAN_STREAM__
  delete window.__COMMAND_CENTER_MOCK_EXECUTION_STREAM__
  delete window.__COMMAND_CENTER_MOCK_DECISION_STREAM__
  window.history.pushState({}, '', '/')
})

async function selectEmptyRepository() {
  const repositoryList = await screen.findByRole('region', { name: 'Registered repositories' })
  const emptyRepositoryButton = await within(repositoryList).findByRole('button', {
    name: /EmptyRepo/,
  })
  fireEvent.click(emptyRepositoryButton)
}

describe('App plan-authoring gate', () => {
  it('renders the Plan Authoring screen when the selected repository has no plan', async () => {
    renderWithWorkspaceCertification(<App />)
    await selectEmptyRepository()

    expect(await screen.findByRole('region', { name: 'Plan authoring' })).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { name: 'Author the implementation plan' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Write Plan' })).toBeInTheDocument()
  })

  it('does not show the normal repository workspace while the gate is active', async () => {
    renderWithWorkspaceCertification(<App />)
    await selectEmptyRepository()

    await screen.findByRole('region', { name: 'Plan authoring' })

    await waitFor(() => {
      expect(screen.queryByLabelText('Repository workspace')).not.toBeInTheDocument()
    })
  })

  it('shows the repository workspace (not the authoring screen) when a plan already exists', async () => {
    renderWithWorkspaceCertification(<App />)

    // AlphaRepo is the default-selected fixture and has planExists === true.
    expect(await screen.findByLabelText('Repository workspace')).toBeInTheDocument()

    await waitFor(() => {
      expect(screen.queryByRole('region', { name: 'Plan authoring' })).not.toBeInTheDocument()
    })

    expect(
      screen.queryByRole('heading', { name: 'Author the implementation plan' }),
    ).not.toBeInTheDocument()
  })

  it('keeps the authoring screen mounted at PlanReady so Revise and Execute stay reachable', async () => {
    renderWithWorkspaceCertification(<App />)
    await selectEmptyRepository()
    await screen.findByRole('region', { name: 'Plan authoring' })

    fireEvent.change(screen.getByRole('textbox', { name: 'Epic' }), {
      target: { value: 'Ship the planning workflow.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))

    // The plan stream completes; the screen must NOT unmount even though planExists is now true.
    await screen.findByRole('region', { name: 'Rendered plan' })

    expect(screen.getByRole('region', { name: 'Plan authoring' })).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: 'Feedback' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Revise Plan' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Execute Plan' })).toBeInTheDocument()
    expect(screen.queryByLabelText('Repository workspace')).not.toBeInTheDocument()
  })

  it('revises, executes, reviews a decision, loops to Turn 2, then leaves — one mounted flow', async () => {
    renderWithWorkspaceCertification(<App />)
    await selectEmptyRepository()
    await screen.findByRole('region', { name: 'Plan authoring' })

    // 1. Author the first plan.
    fireEvent.change(screen.getByRole('textbox', { name: 'Epic' }), {
      target: { value: 'Ship the planning workflow.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))
    const firstPlan = await screen.findByRole('region', { name: 'Rendered plan' })
    expect(firstPlan).toHaveTextContent('Implementation Plan')

    // Pin the authoring region's DOM node so we can assert true mount continuity (same node) across
    // the whole loop — a mid-loop remount of the same-keyed screen would re-show "Plan authoring"
    // but as a NEW node, which the workspace-absence proxy alone would not catch.
    const regionBefore = screen.getByRole('region', { name: 'Plan authoring' })

    // 2. Revise it in place: type feedback, click Revise, await the re-rendered plan.
    fireEvent.change(screen.getByRole('textbox', { name: 'Feedback' }), {
      target: { value: 'Split the first milestone into setup and migration.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Revise Plan' }))
    await waitFor(() => {
      expect(screen.getByRole('region', { name: 'Rendered plan' })).toHaveTextContent('revised')
    })
    // Never left the authoring screen during the revise turn — and it is the SAME mounted node.
    expect(screen.queryByLabelText('Repository workspace')).not.toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Plan authoring' })).toBe(regionBefore)

    // 3. Execute the revised plan. Wait for the async plan-status refresh to enable Execute first
    // (the gate intentionally blocks it until the backend confirms the written plan).
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Execute Plan' })).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Execute Plan' }))
    expect(await screen.findByRole('region', { name: 'Plan execution' })).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Plan authoring' })).toBe(regionBefore)

    // 4. Decision review gate opens in place when the run completes.
    await screen.findByRole('region', { name: 'Decision runtime' })
    fireEvent.click(await screen.findByRole('button', { name: 'Generate decisions' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Submit decisions' }))

    // 5. Submit loops the continuation: Turn 2 reopens the gate, still no navigation.
    await screen.findByText(/Turn 2/)
    expect(screen.queryByLabelText('Repository workspace')).not.toBeInTheDocument()
    // The screen never unmounted across revise, execute, and the loop — still the same DOM node.
    expect(screen.getByRole('region', { name: 'Plan authoring' })).toBe(regionBefore)

    // 6. Only the explicit exit navigates to the workspace.
    fireEvent.click(await screen.findByRole('button', { name: 'Go to workspace' }))
    expect(await screen.findByLabelText('Repository workspace')).toBeInTheDocument()
  })

  it('runs execution, the decision review gate, then loops on submit until the user leaves', async () => {
    renderWithWorkspaceCertification(<App />)
    await selectEmptyRepository()
    await screen.findByRole('region', { name: 'Plan authoring' })

    fireEvent.change(screen.getByRole('textbox', { name: 'Epic' }), {
      target: { value: 'Ship the planning workflow.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))
    await screen.findByRole('region', { name: 'Rendered plan' })

    // Still on the authoring screen before Execute.
    expect(screen.queryByLabelText('Repository workspace')).not.toBeInTheDocument()

    // Wait for the async plan-status refresh to enable Execute (the gate blocks it until the backend
    // confirms the written plan); clicking before then would be a no-op on a disabled button.
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Execute Plan' })).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Execute Plan' }))

    // The screen stays mounted and shows the execution run, not the dead-end label.
    expect(await screen.findByRole('region', { name: 'Plan execution' })).toBeInTheDocument()

    // Decisions follow execution: when the run completes, the decision runtime appears in place
    // and the app does NOT navigate yet — the human-review gate must run first.
    await screen.findByRole('region', { name: 'Decision runtime' })
    expect(screen.queryByLabelText('Repository workspace')).not.toBeInTheDocument()

    // Propose decisions, then submit them through the review gate.
    fireEvent.click(await screen.findByRole('button', { name: 'Generate decisions' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Submit decisions' }))

    // Submit is no longer a navigation: the server runs the continuation turn and reopens the gate
    // for the next turn. The app stays on the authoring screen, not the workspace.
    await screen.findByText(/Turn 2/)
    expect(screen.queryByLabelText('Repository workspace')).not.toBeInTheDocument()

    // Only an explicit exit leaves the loop for the workspace.
    fireEvent.click(await screen.findByRole('button', { name: 'Go to workspace' }))
    expect(await screen.findByLabelText('Repository workspace')).toBeInTheDocument()
    await waitFor(() => {
      expect(screen.queryByRole('region', { name: 'Plan authoring' })).not.toBeInTheDocument()
    })
  })
})
