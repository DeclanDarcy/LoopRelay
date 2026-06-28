import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PlanAuthoringScreen } from '../../features/planning/PlanAuthoringScreen'
import { installWorkspaceCertificationMock } from '../render'

afterEach(() => {
  cleanup()
  delete window.__COMMAND_CENTER_MOCK_STATE__
  delete window.__TAURI_INTERNALS__
  delete window.__COMMAND_CENTER_MOCK_PLAN_STREAM__
  window.history.pushState({}, '', '/')
})

describe('Execute gating on backend-verified plan', () => {
  it('keeps Execute disabled until the backend reports planExists, even with an in-memory plan', async () => {
    installWorkspaceCertificationMock()
    render(
      <PlanAuthoringScreen
        repositoryId="repo-empty"
        repositoryName="EmptyRepo"
        // The backend has NOT confirmed a written plan yet.
        planExists={false}
        refreshPlanStatus={async () => undefined}
      />,
    )

    fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
      target: { value: 'Ship the dashboard.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))

    // The in-memory streamed plan has arrived (PlanReady), but the backend flag is still false.
    await screen.findByRole('region', { name: 'Rendered plan' })

    // Execute must stay disabled: an in-memory plan alone is not enough.
    expect(screen.getByRole('button', { name: 'Execute Plan' })).toBeDisabled()
    // Revise, which only needs the in-memory plan, is reachable once feedback is typed.
    fireEvent.change(screen.getByRole('textbox', { name: 'Feedback' }), {
      target: { value: 'Tighten the milestone.' },
    })
    expect(screen.getByRole('button', { name: 'Revise Plan' })).toBeEnabled()
  })

  it('enables Execute once planExists flips, without unmounting the screen', async () => {
    installWorkspaceCertificationMock()
    const { rerender } = render(
      <PlanAuthoringScreen
        repositoryId="repo-empty"
        repositoryName="EmptyRepo"
        planExists={false}
        refreshPlanStatus={async () => undefined}
      />,
    )

    fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
      target: { value: 'Ship the dashboard.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))
    await screen.findByRole('region', { name: 'Rendered plan' })

    expect(screen.getByRole('button', { name: 'Execute Plan' })).toBeDisabled()

    // The backend now confirms the written plan; the parent flips planExists in place.
    rerender(
      <PlanAuthoringScreen
        repositoryId="repo-empty"
        repositoryName="EmptyRepo"
        planExists={true}
        refreshPlanStatus={async () => undefined}
      />,
    )

    // The screen did not unmount — the rendered plan and Execute control are still here — and
    // Execute is now enabled.
    expect(screen.getByRole('region', { name: 'Rendered plan' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Execute Plan' })).toBeEnabled()
  })

  it('refreshes plan status in place when the plan stream reaches PlanReady', async () => {
    installWorkspaceCertificationMock()
    const refreshPlanStatus = vi.fn(async () => undefined)
    render(
      <PlanAuthoringScreen
        repositoryId="repo-empty"
        repositoryName="EmptyRepo"
        planExists={false}
        refreshPlanStatus={refreshPlanStatus}
      />,
    )

    fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
      target: { value: 'Ship the dashboard.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))
    await screen.findByRole('region', { name: 'Rendered plan' })

    // Reaching PlanReady triggers an in-place plan-status refresh so Execute can become enabled
    // once the backend confirms the just-authored plan. The screen stays mounted throughout.
    await waitFor(() => {
      expect(refreshPlanStatus).toHaveBeenCalled()
    })
    expect(screen.getByRole('region', { name: 'Plan authoring' })).toBeInTheDocument()
  })

  it('retries the PlanReady refresh when a transient status fetch fails', async () => {
    installWorkspaceCertificationMock()
    // usePlanStatus.refresh resolves to null on a transient get_plan_status failure (and to the
    // PlanStatus on success). A single fire-and-forget refresh would leave Execute stranded; the
    // PlanReady effect must retry until the status fetch succeeds.
    const refreshPlanStatus = vi
      .fn<() => Promise<unknown>>()
      .mockResolvedValueOnce(null)
      .mockResolvedValue({ planExists: true, state: 'PlanReady' })

    render(
      <PlanAuthoringScreen
        repositoryId="repo-empty"
        repositoryName="EmptyRepo"
        planExists={false}
        refreshPlanStatus={refreshPlanStatus}
      />,
    )

    fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
      target: { value: 'Ship the dashboard.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))
    await screen.findByRole('region', { name: 'Rendered plan' })

    // The first refresh failed (null); the effect retries so a flaky fetch doesn't strand Execute.
    await waitFor(() => {
      expect(refreshPlanStatus.mock.calls.length).toBeGreaterThanOrEqual(2)
    })
    expect(screen.getByRole('region', { name: 'Plan authoring' })).toBeInTheDocument()
  })

  it('does not retry the PlanReady refresh once the status fetch succeeds', async () => {
    installWorkspaceCertificationMock()
    // A successful refresh resolves to a truthy PlanStatus; the effect must not keep re-fetching.
    const refreshPlanStatus = vi
      .fn<() => Promise<unknown>>()
      .mockResolvedValue({ planExists: true, state: 'PlanReady' })

    render(
      <PlanAuthoringScreen
        repositoryId="repo-empty"
        repositoryName="EmptyRepo"
        planExists={false}
        refreshPlanStatus={refreshPlanStatus}
      />,
    )

    fireEvent.change(screen.getByRole('textbox', { name: 'Roadmap' }), {
      target: { value: 'Ship the dashboard.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))
    await screen.findByRole('region', { name: 'Rendered plan' })

    await waitFor(() => {
      expect(refreshPlanStatus).toHaveBeenCalled()
    })
    // Give any erroneous retry a chance to fire, then assert the happy path fetched exactly once.
    await new Promise((resolve) => setTimeout(resolve, 250))
    expect(refreshPlanStatus).toHaveBeenCalledTimes(1)
  })
})
