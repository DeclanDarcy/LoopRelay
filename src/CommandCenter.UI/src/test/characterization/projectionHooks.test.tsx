import { act, renderHook, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  useArtifactContent,
  useExecutionContextPreview,
  useExecutionEvents,
  useExecutionSession,
  useGitStatus,
  useRepositories,
  useRepositoryWorkspace,
} from '../../hooks'
import type {
  ExecutionContextPreview,
  ExecutionStatus,
  RepositoryDashboardProjection,
  RepositoryGitStatus,
  RepositoryWorkspaceProjection,
} from '../../types'

const repository = { id: 'repo-alpha', name: 'AlphaRepo', path: 'C:\\workspace\\AlphaRepo' }

const proposalSummary = {
  pendingProposalExists: false,
  latestProposalId: null,
  generatedAt: null,
  status: null,
  sourceInputCount: 0,
  contentByteCount: 0,
  contentCharacterCount: 0,
  lastPromotedAt: null,
  lastArchivedRelativePath: null,
} satisfies RepositoryWorkspaceProjection['operationalContextProposalSummary']

function createDashboardProjection(): RepositoryDashboardProjection {
  return {
    repository,
    availability: 'Available',
    readiness: 'Ready',
    executionState: 'Ready',
    activeExecutionSession: null,
    executionSummary: null,
    executionHistory: [],
    milestoneCount: 1,
    hasCurrentHandoff: true,
    hasCurrentDecisions: true,
    continuitySummary: {
      operationalContextExists: true,
      operationalContextRevisionCount: 1,
      operationalContextLastUpdatedAt: null,
      openQuestionCount: 0,
      activeRiskCount: 0,
      pendingProposalExists: false,
    },
  }
}

function createWorkspaceProjection(): RepositoryWorkspaceProjection {
  return {
    repository,
    availability: 'Available',
    readiness: 'Ready',
    executionState: 'Ready',
    executionSummary: null,
    executionHistory: [],
    artifactInventory: {
      plan: {
        relativePath: '.agents/plan.md',
        name: 'plan.md',
        type: 'Plan',
        family: 'Plan',
        versionKind: 'Current',
      },
      operationalContext: null,
      historicalOperationalContexts: [],
      milestones: [],
      currentHandoff: null,
      historicalHandoffs: [],
      currentDecisions: null,
      historicalDecisions: [],
    },
    milestoneCount: 0,
    hasPlan: true,
    hasOperationalContext: false,
    hasCurrentHandoff: false,
    hasCurrentDecisions: false,
    operationalContextProposalSummary: proposalSummary,
    operationalContext: {
      exists: false,
      currentRelativePath: null,
      revisionCount: 0,
      currentRevisionNumber: 0,
      lastUpdatedAt: null,
      lastPromotionAt: null,
      currentUnderstandingSummary: [],
      architecture: [],
      authorityBoundaries: [],
      constraints: [],
      stableDecisions: [],
      decisionRationale: [],
      openQuestions: [],
      activeRisks: [],
      recentUnderstandingChanges: [],
      pendingProposalSummary: proposalSummary,
      latestReviewState: null,
      continuityWarnings: [],
    },
  }
}

function createExecutionContextPreview(
  repositoryId = 'repo-alpha',
  milestonePath = '.agents/milestones/m0.md',
): ExecutionContextPreview {
  return {
    repositoryId,
    repositoryName: 'AlphaRepo',
    repositoryPath: 'C:\\workspace\\AlphaRepo',
    milestonePath,
    generatedAt: '2026-01-01T00:00:00Z',
    artifacts: [
      {
        role: 'Plan',
        relativePath: '.agents/plan.md',
        name: 'plan.md',
        content: '# Plan',
        byteCount: 6,
        characterCount: 6,
      },
    ],
    repositorySnapshot: null,
    diagnostics: {
      totalBytes: 6,
      totalCharacters: 6,
      warningThresholdBytes: 100,
      hardLimitBytes: 200,
      warningThresholdExceeded: false,
      hardLimitExceeded: false,
      artifactDiagnostics: [],
      validationErrors: [],
      missingOptionalArtifacts: [],
      launchBlocked: false,
    },
  }
}

function createExecutionStatus(sessionId = 'session-alpha'): ExecutionStatus {
  return {
    sessionId,
    state: 'Executing',
    repositoryState: 'Executing',
    startedAt: '2026-01-01T00:00:00Z',
    completedAt: null,
    duration: null,
    acceptedAt: null,
    rejectedAt: null,
    decisionNote: null,
    lastActivityAt: '2026-01-01T00:01:00Z',
    providerName: 'codex',
    providerExecutablePath: null,
    providerProcessId: null,
    providerStartedAt: null,
    handoffPath: null,
    failureReason: null,
    recentEvents: [
      {
        sequence: 1,
        timestamp: '2026-01-01T00:01:00Z',
        type: 'status',
        message: 'Execution started',
      },
    ],
  }
}

function createExecutionEvent(sequence: number, message = `Event ${sequence}`) {
  return {
    sequence,
    timestamp: `2026-01-01T00:0${sequence}:00Z`,
    type: 'status',
    message,
  }
}

function createGitStatus(branch = 'main'): RepositoryGitStatus {
  return {
    branch,
    aheadCount: 1,
    behindCount: 0,
    dirtyState: {
      isClean: false,
      stagedPaths: ['src/app.ts'],
      modifiedPaths: ['README.md'],
      addedPaths: [],
      deletedPaths: [],
      renamedPaths: [],
      untrackedPaths: ['notes.md'],
    },
    capturedAt: '2026-01-01T00:00:00Z',
  }
}

function createFetchResponse(value: unknown) {
  return {
    ok: true,
    json: () => Promise.resolve(value),
  } as Response
}

function createDeferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((nextResolve) => {
    resolve = nextResolve
  })

  return { promise, resolve }
}

function installInvokeMock(invoke: unknown) {
  window.__TAURI_INTERNALS__ = {
    invoke: invoke as (cmd: string, args?: Record<string, unknown>) => Promise<unknown>,
    transformCallback: vi.fn(),
    unregisterCallback: vi.fn(),
    callbacks: {},
    convertFileSrc: vi.fn(),
  }
}

type MockEventListener = (event: { data: string }) => void

function installEventSourceMock() {
  class MockEventSource {
    static CLOSED = 2
    static instances: MockEventSource[] = []

    readyState = 0
    listeners = new Map<string, MockEventListener[]>()
    onerror: (() => void) | null = null

    readonly url: string

    constructor(url: string) {
      this.url = url
      MockEventSource.instances.push(this)
    }

    addEventListener(type: string, listener: MockEventListener) {
      this.listeners.set(type, [...(this.listeners.get(type) ?? []), listener])
    }

    emitExecutionEvent(event: unknown) {
      for (const listener of this.listeners.get('execution-event') ?? []) {
        listener({ data: JSON.stringify(event) })
      }
    }

    close() {
      this.readyState = MockEventSource.CLOSED
    }
  }

  vi.stubGlobal('EventSource', MockEventSource)
  return MockEventSource
}

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  delete window.__TAURI_INTERNALS__
})

describe('projection hook characterization', () => {
  it('loads and refreshes repository projections through the repository transport', async () => {
    const repositories = [createDashboardProjection()]
    const invoke = vi.fn().mockResolvedValue(repositories)
    installInvokeMock(invoke)

    const { result } = renderHook(() => useRepositories())

    expect(result.current.isLoading).toBe(true)
    await waitFor(() => expect(result.current.data).toBe(repositories))
    expect(result.current.error).toBeNull()
    expect(invoke).toHaveBeenCalledWith('list_repositories', {}, undefined)

    await act(async () => {
      await result.current.refresh()
    })

    expect(invoke).toHaveBeenCalledTimes(2)
    expect(result.current.data).toBe(repositories)
  })

  it('loads workspace projections and preserves manual refresh command separation', async () => {
    const loadedWorkspace = createWorkspaceProjection()
    const refreshedWorkspace = {
      ...loadedWorkspace,
      milestoneCount: 2,
    } satisfies RepositoryWorkspaceProjection
    const invoke = vi.fn((command: string) => {
      if (command === 'get_repository_workspace') {
        return Promise.resolve(loadedWorkspace)
      }

      if (command === 'refresh_repository_workspace') {
        return Promise.resolve(refreshedWorkspace)
      }

      return Promise.reject(new Error(`Unexpected command: ${command}`))
    })
    installInvokeMock(invoke)

    const { result } = renderHook(() => useRepositoryWorkspace('repo-alpha'))

    await waitFor(() => expect(result.current.data).toBe(loadedWorkspace))
    expect(invoke).toHaveBeenCalledWith('get_repository_workspace', {
      repositoryId: 'repo-alpha',
    }, undefined)

    await act(async () => {
      await result.current.refresh()
    })

    expect(result.current.data).toBe(refreshedWorkspace)
    expect(invoke).toHaveBeenCalledWith('refresh_repository_workspace', {
      repositoryId: 'repo-alpha',
    }, undefined)
  })

  it('loads artifact content and clears content when selection is removed', async () => {
    const invoke = vi.fn().mockResolvedValue('# Plan')
    installInvokeMock(invoke)

    const { result, rerender } = renderHook(
      ({ relativePath }: { relativePath: string | null }) =>
        useArtifactContent('repo-alpha', relativePath),
      { initialProps: { relativePath: '.agents/plan.md' as string | null } },
    )

    await waitFor(() => expect(result.current.data).toBe('# Plan'))
    expect(invoke).toHaveBeenCalledWith('load_artifact_content', {
      repositoryId: 'repo-alpha',
      relativePath: '.agents/plan.md',
    }, undefined)

    rerender({ relativePath: null })

    await waitFor(() => expect(result.current.data).toBe(''))
    expect(result.current.isLoading).toBe(false)
  })

  it('builds execution context previews only when explicitly loaded', async () => {
    const preview = createExecutionContextPreview()
    const invoke = vi.fn().mockResolvedValue(preview)
    installInvokeMock(invoke)

    const { result } = renderHook(() =>
      useExecutionContextPreview('repo-alpha', '.agents/milestones/m0.md'),
    )

    expect(result.current.data).toBeNull()
    expect(invoke).not.toHaveBeenCalled()

    await act(async () => {
      await result.current.load()
    })

    expect(result.current.data).toBe(preview)
    expect(result.current.error).toBeNull()
    expect(invoke).toHaveBeenCalledWith('preview_execution_context', {
      repositoryId: 'repo-alpha',
      milestonePath: '.agents/milestones/m0.md',
    }, undefined)
  })

  it('keeps stale execution context previews visible across milestone changes until rebuilt or cleared', async () => {
    const firstPreview = createExecutionContextPreview('repo-alpha', '.agents/milestones/m0.md')
    const secondPreview = createExecutionContextPreview('repo-alpha', '.agents/milestones/m1.md')
    const invoke = vi
      .fn()
      .mockResolvedValueOnce(firstPreview)
      .mockResolvedValueOnce(secondPreview)
    installInvokeMock(invoke)

    const { result, rerender } = renderHook(
      ({ milestonePath }: { milestonePath: string | null }) =>
        useExecutionContextPreview('repo-alpha', milestonePath),
      { initialProps: { milestonePath: '.agents/milestones/m0.md' as string | null } },
    )

    await act(async () => {
      await result.current.load()
    })
    expect(result.current.data).toBe(firstPreview)

    rerender({ milestonePath: '.agents/milestones/m1.md' })

    expect(result.current.data).toBe(firstPreview)

    await act(async () => {
      await result.current.refresh()
    })

    expect(result.current.data).toBe(secondPreview)
  })

  it('loads and refreshes execution session status from the backend status projection', async () => {
    const initialStatus = createExecutionStatus('session-alpha')
    const refreshedStatus = {
      ...initialStatus,
      lastActivityAt: '2026-01-01T00:02:00Z',
      recentEvents: [
        ...initialStatus.recentEvents,
        {
          sequence: 2,
          timestamp: '2026-01-01T00:02:00Z',
          type: 'status',
          message: 'Execution progressed',
        },
      ],
    } satisfies ExecutionStatus
    const invoke = vi.fn().mockResolvedValue('http://backend.test')
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(createFetchResponse(initialStatus))
      .mockResolvedValueOnce(createFetchResponse(refreshedStatus))
    installInvokeMock(invoke)
    vi.stubGlobal('fetch', fetch)

    const { result } = renderHook(() => useExecutionSession('repo-alpha', 'session-alpha'))

    await waitFor(() => expect(result.current.data).toBe(initialStatus))
    expect(fetch).toHaveBeenCalledWith(
      'http://backend.test/api/execution-sessions/session-alpha/status',
    )

    await act(async () => {
      await result.current.refresh()
    })

    expect(result.current.data).toBe(refreshedStatus)
    expect(fetch).toHaveBeenCalledTimes(2)
  })

  it('reattaches to an existing execution session id by loading its current status', async () => {
    const status = createExecutionStatus('session-existing')
    const invoke = vi.fn().mockResolvedValue('http://backend.test')
    const fetch = vi.fn().mockResolvedValue(createFetchResponse(status))
    installInvokeMock(invoke)
    vi.stubGlobal('fetch', fetch)

    const { result } = renderHook(() => useExecutionSession('repo-alpha', 'session-existing'))

    await waitFor(() => expect(result.current.data).toBe(status))
    expect(fetch).toHaveBeenCalledWith(
      'http://backend.test/api/execution-sessions/session-existing/status',
    )
  })

  it('does not leak stale execution session loads across repository boundaries', async () => {
    const alphaStatus = createExecutionStatus('session-alpha')
    const betaStatus = createExecutionStatus('session-beta')
    const alphaFetch = createDeferred<Response>()
    const invoke = vi.fn().mockResolvedValue('http://backend.test')
    const fetch = vi
      .fn()
      .mockReturnValueOnce(alphaFetch.promise)
      .mockResolvedValueOnce(createFetchResponse(betaStatus))
    installInvokeMock(invoke)
    vi.stubGlobal('fetch', fetch)

    const { result, rerender } = renderHook(
      ({ repositoryId, sessionId }: { repositoryId: string; sessionId: string }) =>
        useExecutionSession(repositoryId, sessionId),
      { initialProps: { repositoryId: 'repo-alpha', sessionId: 'session-alpha' } },
    )

    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1))

    rerender({ repositoryId: 'repo-beta', sessionId: 'session-beta' })

    await waitFor(() => expect(result.current.data).toBe(betaStatus))

    await act(async () => {
      alphaFetch.resolve(createFetchResponse(alphaStatus))
      await alphaFetch.promise
    })

    expect(result.current.data).toBe(betaStatus)
  })

  it('stores streamed execution events by sequence order and replaces duplicate sequences', async () => {
    const invoke = vi.fn().mockResolvedValue('http://backend.test')
    const MockEventSource = installEventSourceMock()
    installInvokeMock(invoke)

    const { result } = renderHook(() => useExecutionEvents('session-alpha'))

    await waitFor(() => expect(MockEventSource.instances).toHaveLength(1))
    expect(MockEventSource.instances[0].url).toBe(
      'http://backend.test/api/execution-sessions/session-alpha/events/stream',
    )

    act(() => {
      MockEventSource.instances[0].emitExecutionEvent(createExecutionEvent(3))
      MockEventSource.instances[0].emitExecutionEvent(createExecutionEvent(1))
      MockEventSource.instances[0].emitExecutionEvent(createExecutionEvent(2))
      MockEventSource.instances[0].emitExecutionEvent(createExecutionEvent(2, 'Event 2 replaced'))
    })

    expect(result.current.data.map((event) => event.sequence)).toEqual([1, 2, 3])
    expect(result.current.data[1].message).toBe('Event 2 replaced')
  })

  it('closes streamed execution event subscriptions on session change and unmount', async () => {
    const invoke = vi.fn().mockResolvedValue('http://backend.test')
    const MockEventSource = installEventSourceMock()
    installInvokeMock(invoke)

    const { result, rerender, unmount } = renderHook(
      ({ sessionId }: { sessionId: string }) => useExecutionEvents(sessionId),
      { initialProps: { sessionId: 'session-alpha' } },
    )

    await waitFor(() => expect(MockEventSource.instances).toHaveLength(1))

    act(() => {
      MockEventSource.instances[0].emitExecutionEvent(createExecutionEvent(1, 'Alpha event'))
    })
    expect(result.current.data).toHaveLength(1)

    rerender({ sessionId: 'session-beta' })

    await waitFor(() => expect(MockEventSource.instances).toHaveLength(2))
    expect(MockEventSource.instances[0].readyState).toBe(MockEventSource.CLOSED)
    expect(result.current.data).toEqual([])

    act(() => {
      MockEventSource.instances[0].emitExecutionEvent(createExecutionEvent(2, 'Stale alpha event'))
      MockEventSource.instances[1].emitExecutionEvent(createExecutionEvent(1, 'Beta event'))
    })

    expect(result.current.data).toEqual([createExecutionEvent(1, 'Beta event')])

    unmount()

    expect(MockEventSource.instances[1].readyState).toBe(MockEventSource.CLOSED)
  })

  it('keeps silent execution status refresh failures out of hook error state', async () => {
    const status = createExecutionStatus('session-alpha')
    const invoke = vi.fn().mockResolvedValue('http://backend.test')
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(createFetchResponse(status))
      .mockRejectedValueOnce(new Error('refresh failed'))
    installInvokeMock(invoke)
    vi.stubGlobal('fetch', fetch)

    const { result } = renderHook(() => useExecutionSession('repo-alpha', 'session-alpha'))

    await waitFor(() => expect(result.current.data).toBe(status))

    await act(async () => {
      await result.current.refresh({ silent: true })
    })

    expect(result.current.data).toBe(status)
    expect(result.current.error).toBeNull()
  })

  it('loads and refreshes git status as a read-only projection', async () => {
    const initialStatus = createGitStatus('main')
    const refreshedStatus = createGitStatus('feature/m0')
    const invoke = vi
      .fn()
      .mockResolvedValueOnce(initialStatus)
      .mockResolvedValueOnce(refreshedStatus)
    installInvokeMock(invoke)

    const { result } = renderHook(() => useGitStatus('repo-alpha'))

    await waitFor(() => expect(result.current.data).toBe(initialStatus))
    expect(invoke).toHaveBeenCalledWith('get_git_status', {
      repositoryId: 'repo-alpha',
    }, undefined)

    await act(async () => {
      await result.current.refresh()
    })

    expect(result.current.data).toBe(refreshedStatus)
    expect(invoke).toHaveBeenCalledTimes(2)
  })

  it('clears git status when the repository selection is removed', async () => {
    const status = createGitStatus()
    const invoke = vi.fn().mockResolvedValue(status)
    installInvokeMock(invoke)

    const { result, rerender } = renderHook(
      ({ repositoryId }: { repositoryId: string | null }) => useGitStatus(repositoryId),
      { initialProps: { repositoryId: 'repo-alpha' as string | null } },
    )

    await waitFor(() => expect(result.current.data).toBe(status))

    rerender({ repositoryId: null })

    await waitFor(() => expect(result.current.data).toBeNull())
    expect(result.current.isLoading).toBe(false)
  })
})
