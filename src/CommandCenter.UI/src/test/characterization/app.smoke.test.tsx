import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
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

  it('builds execution context only after an explicit request for the selected milestone', async () => {
    installWorkspaceCertificationMock()

    const state = window.__COMMAND_CENTER_MOCK_STATE__
    expect(state).toBeDefined()
    if (!state) {
      return
    }

    const alternateMilestone = {
      relativePath: '.agents/milestones/m6.md',
      name: 'm6.md',
      type: 'Milestone' as const,
      family: 'Milestone' as const,
      versionKind: 'Current' as const,
    }
    state.workspaces['repo-alpha'].artifactInventory.milestones.push(alternateMilestone)
    state.workspaces['repo-alpha'].milestoneCount += 1
    state.content[alternateMilestone.relativePath] = '# M6\n\nContinuity milestone.'

    const invoke = window.__TAURI_INTERNALS__?.invoke
    expect(invoke).toBeDefined()
    if (!invoke) {
      return
    }

    const invokeSpy = vi.fn(invoke)
    window.__TAURI_INTERNALS__!.invoke = invokeSpy

    render(<App />)

    const milestoneSelector = await screen.findByRole('combobox')
    await waitFor(() => expect(milestoneSelector).toHaveValue('.agents/milestones/m5.md'))
    expect(invokeSpy.mock.calls.some(([command]) => command === 'preview_execution_context')).toBe(false)

    fireEvent.change(milestoneSelector, {
      target: { value: alternateMilestone.relativePath },
    })

    await waitFor(() => expect(milestoneSelector).toHaveValue(alternateMilestone.relativePath))
    await new Promise((resolve) => window.setTimeout(resolve, 0))
    expect(invokeSpy.mock.calls.some(([command]) => command === 'preview_execution_context')).toBe(false)

    fireEvent.click(screen.getByRole('button', { name: 'Build Execution Context' }))

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'preview_execution_context' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-alpha' &&
            'milestonePath' in args &&
            args.milestonePath === alternateMilestone.relativePath,
        ),
      ).toBe(true),
    )
    expect(await screen.findByText('Continuity milestone.')).toBeInTheDocument()
  })

  it('keeps commit preparation and commit execution behind explicit workflow actions', async () => {
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

    fireEvent.click(screen.getByRole('button', { name: /CertificationAwaitingCommit/ }))

    await waitFor(() =>
      expect(screen.getByRole('heading', { name: 'CertificationAwaitingCommit' })).toBeInTheDocument(),
    )
    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'get_repository_workspace' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-cert-awaiting-commit',
        ),
      ).toBe(true),
    )
    await screen.findByText('Commit preparation is not loaded.')
    expect(invokeSpy.mock.calls.some(([command]) => command === 'prepare_commit')).toBe(false)
    expect(invokeSpy.mock.calls.some(([command]) => command === 'commit_execution')).toBe(false)

    const commitWorkflowPanel = screen.getByText('Git Workflow').closest('section')
    expect(commitWorkflowPanel).not.toBeNull()
    if (!commitWorkflowPanel) {
      return
    }

    fireEvent.click(within(commitWorkflowPanel as HTMLElement).getByRole('button', { name: 'Refresh' }))

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'prepare_commit' &&
            typeof args === 'object' &&
            args !== null &&
            'sessionId' in args &&
            args.sessionId === 'cert-repo-cert-awaiting-commit',
        ),
      ).toBe(true),
    )
    await waitFor(() =>
      expect(screen.getByText('Preparation: prep-cert-repo-cert-awaiting-commit')).toBeInTheDocument(),
    )
    const commitMessageEditor = screen
      .getAllByRole('textbox')
      .find((textbox) => !textbox.classList.contains('artifact-editor'))
    expect(commitMessageEditor).toBeDefined()
    if (!commitMessageEditor) {
      return
    }
    expect(commitMessageEditor).toHaveValue('m5\n\n- 2 files changed')

    const prepareCallCount = invokeSpy.mock.calls.filter(([command]) => command === 'prepare_commit').length
    const commitCallCount = invokeSpy.mock.calls.filter(([command]) => command === 'commit_execution').length

    fireEvent.change(commitMessageEditor, {
      target: { value: 'Reviewed commit boundary' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Select None' }))

    await waitFor(() => expect(screen.getByText('Selected: 0')).toBeInTheDocument())
    expect(screen.getByRole('button', { name: 'Commit Selected' })).toBeDisabled()
    expect(invokeSpy.mock.calls.filter(([command]) => command === 'prepare_commit')).toHaveLength(
      prepareCallCount,
    )
    expect(invokeSpy.mock.calls.filter(([command]) => command === 'commit_execution')).toHaveLength(
      commitCallCount,
    )

    const commitScope = screen.getByLabelText('Commit scope')
    fireEvent.click(within(commitScope).getByLabelText(/src\/CommandCenter\.UI\/src\/App\.tsx/))

    await waitFor(() => expect(screen.getByText('Selected: 1')).toBeInTheDocument())
    expect(invokeSpy.mock.calls.filter(([command]) => command === 'prepare_commit')).toHaveLength(
      prepareCallCount,
    )
    expect(invokeSpy.mock.calls.filter(([command]) => command === 'commit_execution')).toHaveLength(
      commitCallCount,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Commit Selected' }))

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'commit_execution' &&
            typeof args === 'object' &&
            args !== null &&
            'sessionId' in args &&
            args.sessionId === 'cert-repo-cert-awaiting-commit' &&
            'message' in args &&
            args.message === 'Reviewed commit boundary' &&
            'statusSnapshotId' in args &&
            args.statusSnapshotId === 'snapshot-cert-repo-cert-awaiting-commit' &&
            'selectedPaths' in args &&
            Array.isArray(args.selectedPaths) &&
            args.selectedPaths.length === 1 &&
            args.selectedPaths[0] === 'src/CommandCenter.UI/src/App.tsx',
        ),
      ).toBe(true),
    )
  })

  it('keeps push execution behind the explicit push action', async () => {
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

    fireEvent.click(screen.getByRole('button', { name: /CertificationAwaitingPush/ }))

    await waitFor(() =>
      expect(screen.getByRole('heading', { name: 'CertificationAwaitingPush' })).toBeInTheDocument(),
    )
    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'get_repository_workspace' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-cert-awaiting-push',
        ),
      ).toBe(true),
    )
    await screen.findByRole('button', { name: 'Push Commit' })
    await new Promise((resolve) => window.setTimeout(resolve, 0))
    expect(invokeSpy.mock.calls.some(([command]) => command === 'push_execution')).toBe(false)

    const pushWorkflowPanel = screen.getByText('Git Workflow').closest('section')
    expect(pushWorkflowPanel).not.toBeNull()
    if (!pushWorkflowPanel) {
      return
    }

    fireEvent.click(within(pushWorkflowPanel as HTMLElement).getByRole('button', { name: 'Refresh' }))

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'get_git_status' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-cert-awaiting-push',
        ),
      ).toBe(true),
    )
    expect(invokeSpy.mock.calls.some(([command]) => command === 'push_execution')).toBe(false)

    fireEvent.click(screen.getByRole('button', { name: 'Push Commit' }))

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'push_execution' &&
            typeof args === 'object' &&
            args !== null &&
            'sessionId' in args &&
            args.sessionId === 'cert-repo-cert-awaiting-push',
        ),
      ).toBe(true),
    )
  })
})
