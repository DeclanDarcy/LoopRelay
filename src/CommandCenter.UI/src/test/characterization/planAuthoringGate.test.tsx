import { cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import App from '../../App'
import { renderWithWorkspaceCertification } from '../render'

afterEach(() => {
  cleanup()
  delete window.__COMMAND_CENTER_MOCK_STATE__
  delete window.__TAURI_INTERNALS__
  delete window.__COMMAND_CENTER_MOCK_PLAN_STREAM__
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
})
