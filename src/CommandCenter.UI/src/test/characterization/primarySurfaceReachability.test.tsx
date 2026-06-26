import { cleanup, fireEvent, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import { renderWithWorkspaceCertification } from '../render'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  delete window.__COMMAND_CENTER_MOCK_STATE__
  delete window.__TAURI_INTERNALS__
  window.history.pushState({}, '', '/')
})

describe('primary surface reachability', () => {
  it('keeps every primary workspace reachable through the workspace tab strip', async () => {
    renderWithWorkspaceCertification(<App />)

    await screen.findAllByRole('heading', { name: 'AlphaRepo' })

    const expectedSurfaces = [
      { label: 'Workspace', activeTab: 'workspace', landmark: 'Workspace overview' },
      { label: 'Execution', activeTab: 'execution', landmark: 'Execution workspace' },
      { label: 'Operational Context', activeTab: 'operational-context', landmark: 'Current understanding' },
      { label: 'Governance', activeTab: 'governance', landmark: 'Governance workspace' },
      { label: 'Decisions', activeTab: 'decisions', landmark: 'Decision lifecycle' },
      { label: 'Reasoning', activeTab: 'reasoning', landmark: 'Reasoning trajectory' },
      { label: 'Continuity', activeTab: 'continuity', landmark: 'Continuity diagnostics' },
    ] as const

    for (const surface of expectedSurfaces) {
      fireEvent.click(screen.getByRole('button', { name: surface.label }))

      await waitFor(() => {
        expect(screen.getByRole('button', { name: surface.label })).toHaveAttribute('aria-pressed', 'true')
        expect(document.querySelector('.details-body')).toHaveAttribute('data-active-tab', surface.activeTab)
      })

      expect(screen.getByLabelText(surface.landmark)).toBeInTheDocument()
    }
  }, 15_000)
})
