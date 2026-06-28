import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import { certificationExecutionStates } from '../fixtures/certification'
import { installWorkspaceCertificationMock, renderWithWorkspaceCertification } from '../render'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  delete window.__COMMAND_CENTER_MOCK_STATE__
  delete window.__TAURI_INTERNALS__
  window.history.pushState({}, '', '/')
})

describe('workspace certification mock', () => {
  const findArtifactEditor = () => screen.findByRole('textbox', { name: 'Artifact markdown editor' })

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

    const editor = await findArtifactEditor()
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

    const editor = await findArtifactEditor()
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

    const buildButton = await screen.findByRole('button', { name: 'Build Execution Context' })
    await waitFor(() => expect(buildButton).not.toBeDisabled())
    expect(invokeSpy.mock.calls.some(([command]) => command === 'preview_execution_context')).toBe(false)

    fireEvent.click(buildButton)

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'preview_execution_context' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-alpha',
        ),
      ).toBe(true),
    )
    expect(await screen.findByText('Execution context built.')).toBeInTheDocument()
  })

  it('keeps artifact save and rotation behind explicit artifact actions', async () => {
    installWorkspaceCertificationMock()

    const invoke = window.__TAURI_INTERNALS__?.invoke
    expect(invoke).toBeDefined()
    if (!invoke) {
      return
    }

    const invokeSpy = vi.fn(invoke)
    window.__TAURI_INTERNALS__!.invoke = invokeSpy
    vi.spyOn(window, 'confirm').mockReturnValue(true)

    render(<App />)

    const editor = await findArtifactEditor()
    await waitFor(() => expect(editor).toHaveValue('# Plan\n\nInitial plan content.'))

    const artifactWorkflowCommands = [
      'save_artifact_content',
      'rotate_current_handoff',
      'rotate_current_decisions',
    ]
    const artifactWorkflowCallCounts = () =>
      Object.fromEntries(
        artifactWorkflowCommands.map((command) => [
          command,
          invokeSpy.mock.calls.filter(([calledCommand]) => calledCommand === command).length,
        ]),
      )

    const beforeDraftEdit = artifactWorkflowCallCounts()
    fireEvent.change(editor, {
      target: { value: '# Plan\n\nInitial plan content.\n\nExplicit save boundary.' },
    })
    await waitFor(() =>
      expect(screen.getAllByRole('button', { name: 'Save' }).some((button) => !button.hasAttribute('disabled'))).toBe(
        true,
      ),
    )
    await new Promise((resolve) => window.setTimeout(resolve, 0))
    expect(artifactWorkflowCallCounts()).toEqual(beforeDraftEdit)

    const enabledSaveButton = screen
      .getAllByRole('button', { name: 'Save' })
      .find((button) => !button.hasAttribute('disabled'))
    expect(enabledSaveButton).toBeDefined()
    if (!enabledSaveButton) {
      return
    }
    fireEvent.click(enabledSaveButton)

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'save_artifact_content' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-alpha' &&
            'relativePath' in args &&
            args.relativePath === '.agents/plan.md' &&
            'content' in args &&
            args.content === '# Plan\n\nInitial plan content.\n\nExplicit save boundary.',
        ),
      ).toBe(true),
    )

    fireEvent.click(screen.getByRole('button', { name: /handoff\.md/ }))
    await waitFor(() => expect(screen.getByRole('heading', { name: 'handoff.md' })).toBeInTheDocument())
    await waitFor(() => expect(editor).toHaveValue('# Handoff\n\nCurrent handoff content.'))
    const beforeRotate = artifactWorkflowCallCounts()
    await waitFor(() => expect(screen.getByRole('button', { name: 'Rotate' })).not.toBeDisabled())
    await new Promise((resolve) => window.setTimeout(resolve, 0))
    expect(artifactWorkflowCallCounts()).toEqual(beforeRotate)

    fireEvent.click(screen.getByRole('button', { name: 'Rotate' }))

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'rotate_current_handoff' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-alpha',
        ),
      ).toBe(true),
    )

    fireEvent.click(screen.getByRole('button', { name: /decisions\.md/ }))
    await waitFor(() => expect(screen.getByRole('heading', { name: 'decisions.md' })).toBeInTheDocument())
    await waitFor(() => expect(editor).toHaveValue('# Decisions\n\nCurrent decisions content.'))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Rotate' })).not.toBeDisabled())
    fireEvent.click(screen.getByRole('button', { name: 'Rotate' }))

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'rotate_current_decisions' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-alpha',
        ),
      ).toBe(true),
    )
  })

  it('keeps execution launch and handoff decisions behind explicit execution actions', async () => {
    installWorkspaceCertificationMock()

    const invoke = window.__TAURI_INTERNALS__?.invoke
    expect(invoke).toBeDefined()
    if (!invoke) {
      return
    }

    const invokeSpy = vi.fn(invoke)
    window.__TAURI_INTERNALS__!.invoke = invokeSpy
    vi.spyOn(window, 'confirm').mockReturnValue(true)

    render(<App />)

    await screen.findAllByRole('heading', { name: 'AlphaRepo' })
    expect(invokeSpy.mock.calls.some(([command]) => command === 'start_execution')).toBe(false)
    expect(invokeSpy.mock.calls.some(([command]) => command === 'accept_execution_handoff')).toBe(false)
    expect(invokeSpy.mock.calls.some(([command]) => command === 'reject_execution_handoff')).toBe(false)

    const buildContextButton = await screen.findByRole('button', { name: 'Build Execution Context' })
    await waitFor(() => expect(buildContextButton).not.toBeDisabled())
    fireEvent.click(buildContextButton)
    await waitFor(() => expect(screen.getByText('Launch: Ready')).toBeInTheDocument(), {
      timeout: 3000,
    })
    expect(invokeSpy.mock.calls.some(([command]) => command === 'start_execution')).toBe(false)

    fireEvent.click(screen.getByRole('button', { name: 'Start Execution' }))

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'start_execution' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-alpha',
        ),
      ).toBe(true),
    )
    await waitFor(() => expect(screen.getByRole('button', { name: 'Accept Handoff' })).toBeInTheDocument())
    expect(invokeSpy.mock.calls.some(([command]) => command === 'accept_execution_handoff')).toBe(false)
    expect(invokeSpy.mock.calls.some(([command]) => command === 'reject_execution_handoff')).toBe(false)

    fireEvent.click(screen.getByRole('button', { name: 'Accept Handoff' }))

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'accept_execution_handoff' &&
            typeof args === 'object' &&
            args !== null &&
            'sessionId' in args &&
            args.sessionId === 'session-7',
        ),
      ).toBe(true),
    )

    fireEvent.click(screen.getByRole('button', { name: /CertificationAwaitingAcceptance/ }))
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: 'CertificationAwaitingAcceptance' })).toBeInTheDocument(),
    )
    await waitFor(() => expect(screen.getByRole('button', { name: 'Reject Handoff' })).toBeInTheDocument())

    const acceptCallCount = invokeSpy.mock.calls.filter(([command]) => command === 'accept_execution_handoff').length
    const rejectCallCount = invokeSpy.mock.calls.filter(([command]) => command === 'reject_execution_handoff').length
    await new Promise((resolve) => window.setTimeout(resolve, 0))
    expect(invokeSpy.mock.calls.filter(([command]) => command === 'accept_execution_handoff')).toHaveLength(
      acceptCallCount,
    )
    expect(invokeSpy.mock.calls.filter(([command]) => command === 'reject_execution_handoff')).toHaveLength(
      rejectCallCount,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Reject Handoff' }))

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'reject_execution_handoff' &&
            typeof args === 'object' &&
            args !== null &&
            'sessionId' in args &&
            args.sessionId === 'cert-repo-cert-awaiting-acceptance',
        ),
      ).toBe(true),
    )
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

    const commitWorkflowPanel = screen.getByText('Git Evidence').closest('section')
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
    const commitMessageEditor = within(commitWorkflowPanel as HTMLElement).getByRole('textbox', {
      name: 'Commit message',
    })
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

    await waitFor(() => expect(screen.getByRole('button', { name: 'Commit Selected' })).not.toBeDisabled())
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

    const pushWorkflowPanel = screen.getByText('Git Evidence').closest('section')
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

  it('keeps operational-context proposal generation, loading, editing, acceptance, and promotion behind explicit actions', async () => {
    installWorkspaceCertificationMock()

    const invoke = window.__TAURI_INTERNALS__?.invoke
    expect(invoke).toBeDefined()
    if (!invoke) {
      return
    }

    await invoke('generate_operational_context_proposal', { repositoryId: 'repo-alpha' })

    const invokeSpy = vi.fn(invoke)
    window.__TAURI_INTERNALS__!.invoke = invokeSpy

    render(<App />)

    await screen.findAllByRole('heading', { name: 'AlphaRepo' })
    await screen.findByText('Latest: mock-proposal-1')
    await new Promise((resolve) => window.setTimeout(resolve, 0))

    const proposalWorkflowCommands = [
      'generate_operational_context_proposal',
      'get_operational_context_proposal',
      'edit_operational_context_proposal',
      'accept_operational_context_proposal',
      'reject_operational_context_proposal',
      'promote_operational_context_proposal',
    ]
    const proposalWorkflowCallCounts = () =>
      Object.fromEntries(
        proposalWorkflowCommands.map((command) => [
          command,
          invokeSpy.mock.calls.filter(([calledCommand]) => calledCommand === command).length,
        ]),
      )

    expect(proposalWorkflowCallCounts()).toEqual({
      generate_operational_context_proposal: 0,
      get_operational_context_proposal: 0,
      edit_operational_context_proposal: 0,
      accept_operational_context_proposal: 0,
      reject_operational_context_proposal: 0,
      promote_operational_context_proposal: 0,
    })

    fireEvent.click(screen.getByRole('button', { name: /operational_context\.md/ }))
    await waitFor(() => expect(screen.getByRole('heading', { name: 'operational_context.md' })).toBeInTheDocument())
    expect(proposalWorkflowCallCounts()).toEqual({
      generate_operational_context_proposal: 0,
      get_operational_context_proposal: 0,
      edit_operational_context_proposal: 0,
      accept_operational_context_proposal: 0,
      reject_operational_context_proposal: 0,
      promote_operational_context_proposal: 0,
    })

    fireEvent.click(screen.getByRole('button', { name: 'Load Latest' }))

    await waitFor(() =>
      expect(
        screen.getByText((_, element) => element?.textContent === 'Proposal: mock-proposal-1'),
      ).toBeInTheDocument(),
    )
    expect(
      invokeSpy.mock.calls.some(
        ([command, args]) =>
          command === 'get_operational_context_proposal' &&
          typeof args === 'object' &&
          args !== null &&
          'repositoryId' in args &&
          args.repositoryId === 'repo-alpha' &&
          'proposalId' in args &&
          args.proposalId === 'mock-proposal-1',
      ),
    ).toBe(true)

    const beforeDraftEdits = proposalWorkflowCallCounts()
    const proposalEditor = screen.getByLabelText('Proposed markdown')
    const reviewNoteEditor = screen.getByLabelText('Review note')

    fireEvent.change(proposalEditor, {
      target: { value: '# Operational Context\n\n## Constraints\n\n- Reviewer edited proposal.' },
    })
    fireEvent.change(reviewNoteEditor, {
      target: { value: 'Reviewed proposal boundary.' },
    })

    await waitFor(() => expect(screen.getByRole('button', { name: 'Save Edits' })).not.toBeDisabled())
    await new Promise((resolve) => window.setTimeout(resolve, 0))
    expect(proposalWorkflowCallCounts()).toEqual(beforeDraftEdits)

    fireEvent.click(screen.getByRole('button', { name: 'Save Edits' }))

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'edit_operational_context_proposal' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-alpha' &&
            'proposalId' in args &&
            args.proposalId === 'mock-proposal-1' &&
            'content' in args &&
            args.content === '# Operational Context\n\n## Constraints\n\n- Reviewer edited proposal.',
        ),
      ).toBe(true),
    )

    const beforeAccept = proposalWorkflowCallCounts()
    expect(beforeAccept.accept_operational_context_proposal).toBe(0)
    expect(beforeAccept.promote_operational_context_proposal).toBe(0)

    fireEvent.change(screen.getByLabelText('Review note'), {
      target: { value: 'Reviewed proposal boundary.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Accept' }))

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'accept_operational_context_proposal' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-alpha' &&
            'proposalId' in args &&
            args.proposalId === 'mock-proposal-1' &&
            'reviewNote' in args &&
            args.reviewNote === 'Reviewed proposal boundary.',
        ),
      ).toBe(true),
    )
    expect(invokeSpy.mock.calls.some(([command]) => command === 'promote_operational_context_proposal')).toBe(
      false,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Promote' }))

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'promote_operational_context_proposal' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-alpha' &&
            'proposalId' in args &&
            args.proposalId === 'mock-proposal-1',
        ),
      ).toBe(true),
    )
  })

  it('keeps operational-context proposal rejection behind the explicit reject action', async () => {
    installWorkspaceCertificationMock()

    const invoke = window.__TAURI_INTERNALS__?.invoke
    expect(invoke).toBeDefined()
    if (!invoke) {
      return
    }

    await invoke('generate_operational_context_proposal', { repositoryId: 'repo-alpha' })

    const invokeSpy = vi.fn(invoke)
    window.__TAURI_INTERNALS__!.invoke = invokeSpy

    render(<App />)

    await screen.findAllByRole('heading', { name: 'AlphaRepo' })
    await screen.findByText('Latest: mock-proposal-1')
    fireEvent.click(screen.getByRole('button', { name: 'Load Latest' }))
    await waitFor(() =>
      expect(
        screen.getByText((_, element) => element?.textContent === 'Proposal: mock-proposal-1'),
      ).toBeInTheDocument(),
    )

    fireEvent.change(screen.getByLabelText('Review note'), {
      target: { value: 'Rejecting after explicit review.' },
    })
    await new Promise((resolve) => window.setTimeout(resolve, 0))
    expect(invokeSpy.mock.calls.some(([command]) => command === 'reject_operational_context_proposal')).toBe(
      false,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Reject' }))

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'reject_operational_context_proposal' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-alpha' &&
            'proposalId' in args &&
            args.proposalId === 'mock-proposal-1' &&
            'reviewNote' in args &&
            args.reviewNote === 'Rejecting after explicit review.',
        ),
      ).toBe(true),
    )
    expect(invokeSpy.mock.calls.some(([command]) => command === 'accept_operational_context_proposal')).toBe(
      false,
    )
    expect(invokeSpy.mock.calls.some(([command]) => command === 'promote_operational_context_proposal')).toBe(
      false,
    )
  })

  it('keeps operational-context proposal generation behind the explicit generate action', async () => {
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
    await screen.findByText('No operational-context proposal has been generated.')
    await new Promise((resolve) => window.setTimeout(resolve, 0))
    expect(invokeSpy.mock.calls.some(([command]) => command === 'generate_operational_context_proposal')).toBe(
      false,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Generate Proposal' }))

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'generate_operational_context_proposal' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-alpha',
        ),
      ).toBe(true),
    )
    await waitFor(() => expect(screen.getByText('Proposal: mock-proposal-1')).toBeInTheDocument())
  })

  it('keeps Workspace cross-links navigation-only without backend calls', async () => {
    installWorkspaceCertificationMock()

    const invoke = window.__TAURI_INTERNALS__?.invoke
    expect(invoke).toBeDefined()
    if (!invoke) {
      return
    }

    const scrollIntoView = vi.fn()
    Element.prototype.scrollIntoView = scrollIntoView
    const invokeSpy = vi.fn(invoke)
    window.__TAURI_INTERNALS__!.invoke = invokeSpy

    render(<App />)

    await screen.findAllByRole('heading', { name: 'AlphaRepo' })
    const editor = await findArtifactEditor()
    await waitFor(() => expect(editor).toHaveValue('# Plan\n\nInitial plan content.'))
    await screen.findByRole('button', { name: 'Proposal' })
    await new Promise((resolve) => window.setTimeout(resolve, 0))

    const commandCounts = () =>
      new Map(
        invokeSpy.mock.calls.map(([command]) => [
          command,
          invokeSpy.mock.calls.filter(([calledCommand]) => calledCommand === command).length,
        ]),
      )
    const beforeNavigation = commandCounts()

    fireEvent.click(screen.getByRole('button', { name: 'Proposal' }))

    await waitFor(() => expect(screen.getByRole('button', { name: 'Load Latest' })).toBeInTheDocument())
    await new Promise((resolve) => window.setTimeout(resolve, 0))
    expect(commandCounts()).toEqual(beforeNavigation)

    fireEvent.click(screen.getByRole('button', { name: 'Workspace' }))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Current' })).toBeInTheDocument())
    fireEvent.click(screen.getByRole('button', { name: 'Current' }))

    await waitFor(() =>
      expect(screen.getByRole('heading', { name: 'Current Understanding' })).toBeInTheDocument(),
    )
    await new Promise((resolve) => window.setTimeout(resolve, 0))
    expect(commandCounts()).toEqual(beforeNavigation)

    fireEvent.click(screen.getByRole('button', { name: 'Workspace' }))
    const milestonesPanel = await screen.findByRole('region', { name: 'Workspace milestones' })
    await waitFor(() =>
      expect(within(milestonesPanel).getAllByRole('progressbar').length).toBeGreaterThan(0),
    )
    expect(within(milestonesPanel).queryByRole('button')).not.toBeInTheDocument()
    await new Promise((resolve) => window.setTimeout(resolve, 0))
    expect(commandCounts()).toEqual(beforeNavigation)
  })

  it('keeps Operational Context cross-links navigation-only without workflow mutations', async () => {
    installWorkspaceCertificationMock()

    const invoke = window.__TAURI_INTERNALS__?.invoke
    expect(invoke).toBeDefined()
    if (!invoke) {
      return
    }

    await invoke('generate_operational_context_proposal', { repositoryId: 'repo-alpha' })
    const state = window.__COMMAND_CENTER_MOCK_STATE__
    expect(state).toBeDefined()
    if (!state) {
      return
    }

    const proposal = state.operationalContextProposals['repo-alpha'][0]
    proposal.compressionSummary.warnings = ['Compression warning preserves reviewer context.']
    proposal.compressionSummary.stableUnderstandingRetentionWarnings = [
      'Decision retention warning preserves backend authority.',
    ]
    proposal.compressionSummary.warningCount = 2
    proposal.promotion.archivedRelativePath = '.agents/operational_context.0001.md'
    state.workspaces['repo-alpha'].artifactInventory.historicalOperationalContexts.push({
      relativePath: proposal.promotion.archivedRelativePath,
      name: 'operational_context.0001.md',
      type: 'OperationalContext',
      family: 'OperationalContext',
      versionKind: 'Historical',
    })
    state.content[proposal.promotion.archivedRelativePath] = '# Archived Operational Context'
    state.workspaces['repo-alpha'].operationalContext.continuityWarnings = [
      'Continuity warning preserves questions.',
    ]

    const scrollIntoView = vi.fn()
    Element.prototype.scrollIntoView = scrollIntoView
    const invokeSpy = vi.fn(invoke)
    window.__TAURI_INTERNALS__!.invoke = invokeSpy

    render(<App />)

    await screen.findAllByRole('heading', { name: 'AlphaRepo' })
    fireEvent.click(screen.getByRole('button', { name: 'Operational Context' }))
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: 'Current Understanding' })).toBeInTheDocument(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Load Latest' }))
    await waitFor(() =>
      expect(
        screen.getByText((_, element) => element?.textContent === 'Proposal: mock-proposal-1'),
      ).toBeInTheDocument(),
    )

    const workflowCommands = [
      'save_artifact_content',
      'rotate_current_handoff',
      'rotate_current_decisions',
      'generate_operational_context_proposal',
      'edit_operational_context_proposal',
      'accept_operational_context_proposal',
      'reject_operational_context_proposal',
      'promote_operational_context_proposal',
      'generate_continuity_report',
      'start_execution',
      'accept_execution_handoff',
      'reject_execution_handoff',
      'prepare_commit',
      'commit_execution',
      'push_execution',
    ]
    const workflowCallCounts = () =>
      Object.fromEntries(
        workflowCommands.map((command) => [
          command,
          invokeSpy.mock.calls.filter(([calledCommand]) => calledCommand === command).length,
        ]),
      )
    const beforeNavigation = workflowCallCounts()

    fireEvent.click(
      screen.getByRole('button', { name: 'Which warning categories should be shown first?' }),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Projection drift could confuse review state.' }))
    fireEvent.click(screen.getByRole('button', { name: /^Continuity warning preserves questions\./ }))

    await waitFor(() => expect(screen.getByLabelText('Continuity diagnostics')).toBeInTheDocument())
    expect(workflowCallCounts()).toEqual(beforeNavigation)

    fireEvent.click(screen.getByRole('button', { name: 'Operational Context' }))
    fireEvent.click(screen.getByRole('button', { name: 'Load Latest' }))
    await screen.findByText('Compression warning preserves reviewer context.')
    fireEvent.click(screen.getByRole('button', { name: 'Open compression diagnostics' }))
    await waitFor(() => expect(scrollIntoView).toHaveBeenCalled())
    expect(workflowCallCounts()).toEqual(beforeNavigation)

    fireEvent.click(screen.getByRole('button', { name: 'Operational Context' }))
    await screen.findAllByText('Decision retention warning preserves backend authority.')
    fireEvent.click(screen.getAllByRole('button', { name: 'Open decision retention' })[0])
    await waitFor(() => expect(scrollIntoView).toHaveBeenCalled())
    expect(workflowCallCounts()).toEqual(beforeNavigation)

    fireEvent.click(screen.getByRole('button', { name: 'Operational Context' }))
    fireEvent.click(screen.getByRole('button', { name: '.agents/operational_context.0001.md' }))
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: 'operational_context.0001.md' })).toBeInTheDocument(),
    )
    expect(workflowCallCounts()).toEqual(beforeNavigation)
  }, 10000)

  it('keeps continuity diagnostics read-only and report generation behind the explicit action', async () => {
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
    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'get_continuity_diagnostics' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-alpha',
        ),
      ).toBe(true),
    )
    let continuityPanel = await screen.findByLabelText('Continuity diagnostics')
    await within(continuityPanel).findByText((_, element) => element?.textContent === 'Revisions: 1')
    expect(invokeSpy.mock.calls.some(([command]) => command === 'generate_continuity_report')).toBe(false)

    const continuityWorkflowCommands = ['get_continuity_diagnostics', 'generate_continuity_report']
    const continuityWorkflowCallCounts = () =>
      Object.fromEntries(
        continuityWorkflowCommands.map((command) => [
          command,
          invokeSpy.mock.calls.filter(([calledCommand]) => calledCommand === command).length,
        ]),
      )

    const beforeRefresh = continuityWorkflowCallCounts()
    await waitFor(() =>
      expect(within(continuityPanel).getByRole('button', { name: 'Refresh Diagnostics' })).not.toBeDisabled(),
    )
    fireEvent.click(within(continuityPanel).getByRole('button', { name: 'Refresh Diagnostics' }))

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.filter(
          ([command, args]) =>
            command === 'get_continuity_diagnostics' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-alpha',
        ),
      ).toHaveLength(beforeRefresh.get_continuity_diagnostics + 1),
    )
    expect(invokeSpy.mock.calls.some(([command]) => command === 'generate_continuity_report')).toBe(false)

    fireEvent.click(screen.getByRole('button', { name: /EmptyRepo/ }))
    await waitFor(() => expect(screen.getByRole('heading', { name: 'EmptyRepo' })).toBeInTheDocument())
    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'get_continuity_diagnostics' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-empty',
        ),
      ).toBe(true),
    )
    expect(invokeSpy.mock.calls.some(([command]) => command === 'generate_continuity_report')).toBe(false)

    fireEvent.click(screen.getByRole('button', { name: /AlphaRepo/ }))
    await waitFor(() => expect(screen.getByRole('heading', { name: 'AlphaRepo' })).toBeInTheDocument())
    await waitFor(() => expect(screen.getByRole('button', { name: /decisions\.md/ })).toBeInTheDocument())
    const beforeNavigation = continuityWorkflowCallCounts()
    fireEvent.click(screen.getByRole('button', { name: /decisions\.md/ }))
    await waitFor(() => expect(screen.getByRole('heading', { name: 'decisions.md' })).toBeInTheDocument())
    await new Promise((resolve) => window.setTimeout(resolve, 0))
    expect(continuityWorkflowCallCounts()).toEqual(beforeNavigation)

    continuityPanel = screen.getByLabelText('Continuity diagnostics')
    fireEvent.click(within(continuityPanel).getByRole('button', { name: 'Generate Report' }))

    await waitFor(() =>
      expect(
        invokeSpy.mock.calls.some(
          ([command, args]) =>
            command === 'generate_continuity_report' &&
            typeof args === 'object' &&
            args !== null &&
            'repositoryId' in args &&
            args.repositoryId === 'repo-alpha',
        ),
      ).toBe(true),
    )
    await waitFor(() =>
      expect(screen.getByText(/Continuity report generated: \.agents\/.*\/continuity\./)).toBeInTheDocument(),
    )
  })
})
