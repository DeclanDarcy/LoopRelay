import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import App from '../../App'
import { certificationExecutionStates } from '../fixtures/certification'
import { renderWithWorkspaceCertification } from '../render'

afterEach(() => {
  delete window.__COMMAND_CENTER_MOCK_STATE__
  delete window.__TAURI_INTERNALS__
  window.history.pushState({}, '', '/')
})

describe('workspace certification mock', () => {
  it('renders the repository workspace using the dev Tauri mock', async () => {
    renderWithWorkspaceCertification(<App />)

    expect(await screen.findAllByText('AlphaRepo')).toHaveLength(2)
    expect(await screen.findByLabelText('Repository workspace')).toBeInTheDocument()
  })

  it('documents one mock repository for every execution state required by M0', () => {
    expect(certificationExecutionStates.map((state) => state.executionState)).toEqual([
      'Ready',
      'Executing',
      'AwaitingAcceptance',
      'AwaitingCommit',
      'AwaitingPush',
      'Failed',
      'Cancelled',
    ])
  })
})
