import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import { certificationExecutionStates } from '../fixtures/certification'
import { installWorkspaceCertificationMock, renderWithWorkspaceCertification } from '../render'

afterEach(() => {
  cleanup()
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

  it('keeps artifact draft edits local without reloading projections', async () => {
    installWorkspaceCertificationMock()

    const invoke = window.__TAURI_INTERNALS__?.invoke
    expect(invoke).toBeDefined()
    if (!invoke) {
      return
    }

    const invokeSpy = vi.fn(invoke)
    window.__TAURI_INTERNALS__!.invoke = invokeSpy

    render(<App />)

    const editor = await screen.findByRole('textbox')
    await waitFor(() => expect(editor).toHaveValue('# Plan\n\nInitial plan content.'))

    const projectionCommands = [
      'list_repositories',
      'get_repository_workspace',
      'refresh_repository_workspace',
      'load_artifact_content',
    ]
    const projectionCallCounts = () =>
      Object.fromEntries(
        projectionCommands.map((command) => [
          command,
          invokeSpy.mock.calls.filter(([calledCommand]) => calledCommand === command).length,
        ]),
      )

    await waitFor(async () => {
      const currentCounts = projectionCallCounts()
      await new Promise((resolve) => window.setTimeout(resolve, 0))
      expect(projectionCallCounts()).toEqual(currentCounts)
    })
    const beforeDraftEdit = projectionCallCounts()

    fireEvent.change(editor, {
      target: {
        value: '# Plan\n\nInitial plan content.\n\nLocal draft boundary.',
      },
    })

    await waitFor(() =>
      expect(screen.getAllByRole('button', { name: 'Save' }).some((button) => !button.hasAttribute('disabled'))).toBe(
        true,
      ),
    )

    await new Promise((resolve) => window.setTimeout(resolve, 0))
    expect(projectionCallCounts()).toEqual(beforeDraftEdit)
  })

  it('loads the selected repository workspace through the workspace projection', async () => {
    installWorkspaceCertificationMock()

    const invoke = window.__TAURI_INTERNALS__?.invoke
    expect(invoke).toBeDefined()
    if (!invoke) {
      return
    }

    const invokeSpy = vi.fn(invoke)
    window.__TAURI_INTERNALS__!.invoke = invokeSpy

    render(<App />)

    await screen.findAllByRole('heading', { name: 'AlphaRepo' })

    fireEvent.click(screen.getByRole('button', { name: /EmptyRepo/ }))

    await waitFor(() => expect(screen.getByRole('heading', { name: 'EmptyRepo' })).toBeInTheDocument())
    await screen.findAllByText('Missing plan')

    expect(
      invokeSpy.mock.calls.some(
        ([command, args]) =>
          command === 'get_repository_workspace' &&
          typeof args === 'object' &&
          args !== null &&
          'repositoryId' in args &&
          args.repositoryId === 'repo-empty',
      ),
    ).toBe(true)
  })

  it('refreshes the workspace projection and reconciles a removed selected artifact', async () => {
    installWorkspaceCertificationMock()

    const invoke = window.__TAURI_INTERNALS__?.invoke
    expect(invoke).toBeDefined()
    if (!invoke) {
      return
    }

    const invokeSpy = vi.fn(invoke)
    window.__TAURI_INTERNALS__!.invoke = invokeSpy

    render(<App />)

    const editor = await screen.findByRole('textbox')
    await waitFor(() => expect(editor).toHaveValue('# Plan\n\nInitial plan content.'))

    fireEvent.click(screen.getByRole('button', { name: /decisions\.md/ }))
    await waitFor(() => expect(editor).toHaveValue('# Decisions\n\nCurrent decisions content.'))

    const state = window.__COMMAND_CENTER_MOCK_STATE__
    expect(state).toBeDefined()
    if (!state) {
      return
    }

    state.workspaces['repo-alpha'].artifactInventory.currentDecisions = null
    state.workspaces['repo-alpha'].hasCurrentDecisions = false

    fireEvent.click(screen.getByRole('button', { name: 'Refresh Workspace' }))

    await waitFor(() => expect(screen.getByRole('heading', { name: 'plan.md' })).toBeInTheDocument())
    await waitFor(() => expect(editor).toHaveValue('# Plan\n\nInitial plan content.'))

    expect(
      invokeSpy.mock.calls.some(
        ([command, args]) =>
          command === 'refresh_repository_workspace' &&
          typeof args === 'object' &&
          args !== null &&
          'repositoryId' in args &&
          args.repositoryId === 'repo-alpha',
      ),
    ).toBe(true)
  })
})
