import { act, renderHook, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  mergeExecutionEvents,
  useArtifactContent,
  useContinuityDiagnostics,
  useContinuityReports,
  useExecutionContextPreview,
  useExecutionEvents,
  useExecutionGitEligibility,
  useExecutionPromptManifest,
  useExecutionSession,
  useExecutionTransparency,
  useGitStatus,
  useRepositories,
  useRepositoryWorkspace,
} from '../../hooks'
import type {
  ExecutionContextPreview,
  ExecutionGitActionEligibility,
  ExecutionPromptManifest,
  ExecutionSessionTransparency,
  ExecutionStatus,
  ContinuityDiagnostics,
  ContinuityReport,
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

const decisionSessionSummary = {
  decisionSessionId: null,
  state: null,
  lifecycleDecision: null,
  transferEligibilityStatus: null,
  estimatedTokenCount: null,
  estimatedCacheTtl: null,
  cacheMissRisk: null,
  coherenceScore: null,
  transferPressure: null,
  healthDimensions: [],
  recentTransferLineage: [],
  diagnostics: [],
  generatedAt: null,
} satisfies RepositoryDashboardProjection['decisionSessionSummary']

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
    reasoningSummary: {
      eventCount: 0,
      threadCount: 0,
      relationshipCount: 0,
      hypothesisEventCount: 0,
      alternativeEventCount: 0,
      contradictionEventCount: 0,
      directionEventCount: 0,
      decisionEvolutionEventCount: 0,
      assumptionEvolutionEventCount: 0,
      constraintEvolutionEventCount: 0,
      evidenceEventCount: 0,
      lastEventAt: null,
      lastThreadActivityAt: null,
      lastRelationshipAt: null,
      lastActivityAt: null,
      lastReconstructionAt: null,
      lastCertificationAt: null,
      certificationResult: null,
    },
    decisionSessionSummary,
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
    reasoningSummary: {
      eventCount: 0,
      threadCount: 0,
      relationshipCount: 0,
      hypothesisEventCount: 0,
      alternativeEventCount: 0,
      contradictionEventCount: 0,
      directionEventCount: 0,
      decisionEvolutionEventCount: 0,
      assumptionEvolutionEventCount: 0,
      constraintEvolutionEventCount: 0,
      evidenceEventCount: 0,
      lastEventAt: null,
      lastThreadActivityAt: null,
      lastRelationshipAt: null,
      lastActivityAt: null,
      lastReconstructionAt: null,
      lastCertificationAt: null,
      certificationResult: null,
    },
    decisionSessionSummary,
  }
}

function createExecutionContextPreview(repositoryId = 'repo-alpha'): ExecutionContextPreview {
  return {
    id: repositoryId,
    name: 'AlphaRepo',
    path: 'C:\\workspace\\AlphaRepo',
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
    snapshot: null,
    diagnostics: {
      totalBytes: 6,
      totalCharacters: 6,
      warningThresholdBytes: 100,
      hardLimitBytes: 200,
      warningThresholdExceeded: false,
      hardLimitExceeded: false,
      artifactDiagnostics: [],
      validationErrors: [],
      governedConflicts: [],
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

function createExecutionPromptManifest(sessionId = 'session-alpha'): ExecutionPromptManifest {
  return {
    sessionId,
    generatedAt: '2026-01-01T00:00:30Z',
    promptText: 'Launched prompt',
    promptArtifactPath: null,
    requestedArtifacts: [
      {
        role: 'Milestone',
        relativePath: '.agents/milestones/m5.md',
        byteCount: 100,
        characterCount: 100,
        delivered: true,
      },
      {
        role: 'CurrentHandoff',
        relativePath: '.agents/handoffs/handoff.md',
        byteCount: null,
        characterCount: null,
        delivered: false,
      },
    ],
    requestedContextBytes: 100,
    requestedContextCharacters: 100,
    deliveredArtifacts: [
      {
        role: 'Milestone',
        relativePath: '.agents/milestones/m5.md',
        byteCount: 100,
        characterCount: 100,
        delivered: true,
      },
    ],
    deliveredContextBytes: 100,
    deliveredContextCharacters: 100,
    dirtyRepositoryAtRequestTime: true,
    dirtyRepositoryAtDeliveryTime: true,
    governedDecisionCountRequested: 2,
    governedDecisionCountDelivered: 2,
    operationalContextSourceRequested: '.agents/operational_context.md',
    operationalContextSourceDelivered: '.agents/operational_context.md',
    handoffSourceRequested: '.agents/handoffs/handoff.md',
    handoffSourceDelivered: null,
    milestoneSourceRequested: null,
    milestoneSourceDelivered: null,
    providerDeliveryStatus: 'Delivered',
    providerAdjustments: [],
    divergenceReason: null,
    diagnostics: ['NoProviderDivergenceSignal'],
  }
}

function createExecutionTransparency(sessionId = 'session-alpha'): ExecutionSessionTransparency {
  return {
    sessionId,
    promptMetadata: {
      generatedAt: '2026-01-01T00:00:30Z',
      repositoryPath: 'C:\\workspace\\AlphaRepo',
      includedArtifactPaths: ['.agents/plan.md'],
    },
    recovery: {
      recoveryRan: true,
      recoveryTrigger: 'StartupRecovery',
      reattachAttempted: true,
      reattachSucceeded: true,
      orphanedProviderState: false,
      sessionMarkedFailedByRecovery: false,
      recoveryEventTimestamp: '2026-01-01T00:01:00Z',
      recoveryMessage: 'Active provider process was reattached after backend restart.',
    },
    monitoring: {
      providerProcessState: 'Running',
      exitCode: null,
      lastActivityAt: '2026-01-01T00:01:00Z',
      staleActivity: false,
      retainedEventCount: 2,
      firstRetainedEventSequence: 1,
      lastRetainedEventSequence: 2,
      eventRetentionTrimmingDetected: false,
      monitoringWarnings: [],
    },
    handoffProcessing: {
      handoffProduced: true,
      handoffMissing: false,
      handoffArchived: false,
      archivePath: null,
      archiveSequence: null,
      archiveFailed: false,
      handoffValidated: true,
      validationFailure: null,
      resultingSessionState: 'Completed',
      resultingRepositoryState: 'AwaitingAcceptance',
      processedAt: '2026-01-01T00:01:05Z',
      providerFailureDistinctFromHandoffFailure: false,
      providerFailureReason: null,
      handoffFailureReason: null,
      diagnostics: [],
    },
  }
}

function createExecutionGitEligibility(sessionId = 'session-alpha'): ExecutionGitActionEligibility {
  return {
    sessionId,
    sessionExists: true,
    repositoryState: 'AwaitingCommit',
    commitPreparationLoaded: true,
    commitPreparationCurrent: true,
    commitPreparationId: 'prep-alpha',
    preparedStatusSnapshotId: 'snapshot-alpha',
    currentStatusSnapshotId: 'snapshot-alpha',
    selectedPathCount: 1,
    preparedPathCount: 2,
    unknownSelectedPaths: [],
    commitMessagePresent: true,
    repositoryAllowsCommit: true,
    awaitingPush: false,
    commitShaExists: false,
    commitSha: null,
    previousPushAttemptedAt: null,
    previousPushFailure: null,
    remoteBranchState: null,
    canCommit: true,
    canPush: false,
    commitDisabledReasons: [],
    pushDisabledReasons: ['Repository is not awaiting push.'],
    diagnostics: [],
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

function createContinuityDiagnostics(repositoryId = 'repo-alpha'): ContinuityDiagnostics {
  const emptyTrend = {
    addedCount: 0,
    modifiedCount: 0,
    removedCount: 0,
    resolvedCount: 0,
    lostCount: 0,
  }

  return {
    repositoryId,
    generatedAt: '2026-01-01T00:00:00Z',
    revisionCount: 2,
    currentContextByteCount: 1024,
    currentContextCharacterCount: 900,
    contextByteGrowth: 128,
    averageBytesPerRevision: 512,
    operationalEvolution: {
      addedCount: 0,
      modifiedCount: 0,
      removedCount: 0,
      preservedCount: 0,
      lostCount: 0,
      resolvedCount: 0,
      semanticChanges: [],
      timelineEntries: [],
      diagnosticGroups: [],
    },
    architectureTrend: emptyTrend,
    constraintTrend: emptyTrend,
    decisionTrend: emptyTrend,
    rationaleTrend: emptyTrend,
    openQuestionTrend: emptyTrend,
    activeRiskTrend: emptyTrend,
    compressionTrend: {
      proposalCount: 1,
      compressedItemCount: 2,
      removedItemCount: 0,
      resolvedQuestionCount: 1,
      retiredRiskCount: 0,
      warningCount: 0,
      warnings: [],
      noiseRemovedIndicators: [],
    },
    repeatedInvestigationIndicators: [],
    repeatedQuestionIndicators: [],
    decisionReworkIndicators: [],
    continuityWarnings: [],
    diagnosticGroups: [],
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

    const { result } = renderHook(() => useExecutionContextPreview('repo-alpha'))

    expect(result.current.data).toBeNull()
    expect(invoke).not.toHaveBeenCalled()

    await act(async () => {
      await result.current.load()
    })

    expect(result.current.data).toBe(preview)
    expect(result.current.error).toBeNull()
    expect(invoke).toHaveBeenCalledWith('preview_execution_context', {
      repositoryId: 'repo-alpha',
    }, undefined)
  })

  it('keeps stale execution context previews visible until rebuilt or cleared', async () => {
    const firstPreview = createExecutionContextPreview('repo-alpha')
    const secondPreview = createExecutionContextPreview('repo-alpha')
    const invoke = vi
      .fn()
      .mockResolvedValueOnce(firstPreview)
      .mockResolvedValueOnce(secondPreview)
    installInvokeMock(invoke)

    const { result } = renderHook(() => useExecutionContextPreview('repo-alpha'))

    await act(async () => {
      await result.current.load()
    })
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

  it('loads the launched prompt manifest through the shell detail command', async () => {
    const manifest = createExecutionPromptManifest('session-alpha')
    const invoke = vi.fn().mockResolvedValue(manifest)
    installInvokeMock(invoke)

    const { result } = renderHook(() => useExecutionPromptManifest('session-alpha'))

    await waitFor(() => expect(result.current.data).toBe(manifest))
    expect(invoke).toHaveBeenCalledWith('get_execution_prompt_manifest', {
      sessionId: 'session-alpha',
    }, undefined)
  })

  it('loads execution transparency through the shell detail command', async () => {
    const transparency = createExecutionTransparency('session-alpha')
    const invoke = vi.fn().mockResolvedValue(transparency)
    installInvokeMock(invoke)

    const { result } = renderHook(() => useExecutionTransparency('session-alpha'))

    await waitFor(() => expect(result.current.data).toBe(transparency))
    expect(invoke).toHaveBeenCalledWith('get_execution_transparency', {
      sessionId: 'session-alpha',
    }, undefined)
  })

  it('loads execution git eligibility through the shell detail command', async () => {
    const eligibility = createExecutionGitEligibility('session-alpha')
    const invoke = vi.fn().mockResolvedValue(eligibility)
    installInvokeMock(invoke)

    const { result } = renderHook(() =>
      useExecutionGitEligibility({
        sessionId: 'session-alpha',
        commitMessage: 'Commit message',
        selectedPaths: ['src/App.tsx'],
      }),
    )

    await waitFor(() => expect(result.current.data).toBe(eligibility))
    expect(invoke).toHaveBeenCalledWith('get_execution_git_eligibility', {
      sessionId: 'session-alpha',
      commitMessage: 'Commit message',
      selectedPaths: ['src/App.tsx'],
    }, undefined)
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

  it('merges execution status snapshots with streamed events by replacing duplicate sequences', () => {
    const snapshotEvents = [
      createExecutionEvent(1, 'Snapshot event 1'),
      createExecutionEvent(3, 'Snapshot event 3'),
    ]
    const streamedEvents = [
      createExecutionEvent(2, 'Streamed event 2'),
      createExecutionEvent(3, 'Streamed event 3 replaced'),
    ]

    const mergedEvents = mergeExecutionEvents(snapshotEvents, streamedEvents)

    expect(mergedEvents.map((event) => event.sequence)).toEqual([1, 2, 3])
    expect(mergedEvents.map((event) => event.message)).toEqual([
      'Snapshot event 1',
      'Streamed event 2',
      'Streamed event 3 replaced',
    ])
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

  it('loads and refreshes continuity diagnostics as a read-only projection', async () => {
    const initialDiagnostics = createContinuityDiagnostics('repo-alpha')
    const refreshedDiagnostics = {
      ...initialDiagnostics,
      revisionCount: 3,
    } satisfies ContinuityDiagnostics
    const invoke = vi
      .fn()
      .mockResolvedValueOnce(initialDiagnostics)
      .mockResolvedValueOnce(refreshedDiagnostics)
    installInvokeMock(invoke)

    const { result } = renderHook(() => useContinuityDiagnostics('repo-alpha'))

    await waitFor(() => expect(result.current.data).toBe(initialDiagnostics))
    expect(invoke).toHaveBeenCalledWith('get_continuity_diagnostics', {
      repositoryId: 'repo-alpha',
    }, undefined)

    await act(async () => {
      await result.current.refresh()
    })

    expect(result.current.data).toBe(refreshedDiagnostics)
    expect(invoke).toHaveBeenCalledTimes(2)
  })

  it('clears continuity diagnostics when the repository selection is removed', async () => {
    const diagnostics = createContinuityDiagnostics()
    const invoke = vi.fn().mockResolvedValue(diagnostics)
    installInvokeMock(invoke)

    const { result, rerender } = renderHook(
      ({ repositoryId }: { repositoryId: string | null }) => useContinuityDiagnostics(repositoryId),
      { initialProps: { repositoryId: 'repo-alpha' as string | null } },
    )

    await waitFor(() => expect(result.current.data).toBe(diagnostics))

    rerender({ repositoryId: null })

    await waitFor(() => expect(result.current.data).toBeNull())
    expect(result.current.isLoading).toBe(false)
  })

  it('loads and refreshes continuity reports as supporting artifacts', async () => {
    const initialDiagnostics = createContinuityDiagnostics('repo-alpha')
    const initialReport = {
      reportId: 'continuity.1',
      repositoryId: 'repo-alpha',
      generatedAt: '2026-01-02T00:00:00Z',
      relativePath: '.agents/continuity/continuity.1.json',
      diagnostics: initialDiagnostics,
    } satisfies ContinuityReport
    const refreshedReport = {
      ...initialReport,
      reportId: 'continuity.2',
      relativePath: '.agents/continuity/continuity.2.json',
    } satisfies ContinuityReport
    const invoke = vi.fn().mockResolvedValueOnce([initialReport]).mockResolvedValueOnce([refreshedReport])
    installInvokeMock(invoke)

    const { result } = renderHook(() => useContinuityReports('repo-alpha'))

    await waitFor(() => expect(result.current.data).toEqual([initialReport]))
    expect(invoke).toHaveBeenCalledWith('list_continuity_reports', {
      repositoryId: 'repo-alpha',
    }, undefined)

    await act(async () => {
      await result.current.refresh()
    })

    expect(result.current.data).toEqual([refreshedReport])
    expect(invoke).toHaveBeenCalledTimes(2)
  })

  it('clears continuity reports when the repository selection is removed', async () => {
    const report = {
      reportId: 'continuity.1',
      repositoryId: 'repo-alpha',
      generatedAt: '2026-01-02T00:00:00Z',
      relativePath: '.agents/continuity/continuity.1.json',
      diagnostics: createContinuityDiagnostics('repo-alpha'),
    } satisfies ContinuityReport
    const invoke = vi.fn().mockResolvedValue([report])
    installInvokeMock(invoke)

    const { result, rerender } = renderHook(
      ({ repositoryId }: { repositoryId: string | null }) => useContinuityReports(repositoryId),
      { initialProps: { repositoryId: 'repo-alpha' as string | null } },
    )

    await waitFor(() => expect(result.current.data).toEqual([report]))

    rerender({ repositoryId: null })

    await waitFor(() => expect(result.current.data).toEqual([]))
    expect(result.current.isLoading).toBe(false)
  })
})
