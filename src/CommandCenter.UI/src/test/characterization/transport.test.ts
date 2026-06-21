import { afterEach, describe, expect, it, vi } from 'vitest'
import { refreshRepositoryWorkspace, subscribeToExecutionEvents } from '../../api'
import type { ExecutionEvent, RepositoryWorkspaceProjection } from '../../types'

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  delete window.__TAURI_INTERNALS__
})

describe('transport boundary characterization', () => {
  it('preserves repository refresh command request and response handling', async () => {
    const workspace = {
      repository: { id: 'repo-alpha', name: 'AlphaRepo', path: 'C:\\workspace\\AlphaRepo' },
      availability: 'Available',
      readiness: 'Ready',
      executionState: 'Ready',
      executionSummary: null,
      executionHistory: [],
      artifactInventory: {
        plan: null,
        operationalContext: null,
        historicalOperationalContexts: [],
        milestones: [],
        currentHandoff: null,
        historicalHandoffs: [],
        currentDecisions: null,
        historicalDecisions: [],
      },
      milestoneCount: 0,
      hasPlan: false,
      hasOperationalContext: false,
      hasCurrentHandoff: false,
      hasCurrentDecisions: false,
      operationalContextProposalSummary: {
        pendingProposalExists: false,
        latestProposalId: null,
        generatedAt: null,
        status: null,
        sourceInputCount: 0,
        contentByteCount: 0,
        contentCharacterCount: 0,
        lastPromotedAt: null,
        lastArchivedRelativePath: null,
      },
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
        pendingProposalSummary: {
          pendingProposalExists: false,
          latestProposalId: null,
          generatedAt: null,
          status: null,
          sourceInputCount: 0,
          contentByteCount: 0,
          contentCharacterCount: 0,
          lastPromotedAt: null,
          lastArchivedRelativePath: null,
        },
        latestReviewState: null,
        continuityWarnings: [],
      },
    } satisfies RepositoryWorkspaceProjection
    const invoke = vi.fn().mockResolvedValue(workspace)

    window.__TAURI_INTERNALS__ = {
      invoke,
      transformCallback: vi.fn(),
      unregisterCallback: vi.fn(),
      callbacks: {},
      convertFileSrc: vi.fn(),
    }

    await expect(refreshRepositoryWorkspace('repo-alpha')).resolves.toBe(workspace)
    expect(invoke).toHaveBeenCalledWith('refresh_repository_workspace', {
      repositoryId: 'repo-alpha',
    }, undefined)
  })

  it('parses execution event stream payloads and owns cleanup', () => {
    const close = vi.fn()
    const executionEventListeners: Array<(event: MessageEvent<string>) => void> = []

    class MockEventSource {
      static CLOSED = 2
      readyState = 1

      url: string

      constructor(url: string) {
        this.url = url
      }

      addEventListener(eventName: string, listener: (event: MessageEvent<string>) => void) {
        if (eventName === 'execution-event') {
          executionEventListeners.push(listener)
        }
      }

      close() {
        close()
      }
    }

    vi.stubGlobal('EventSource', MockEventSource)
    const onExecutionEvent = vi.fn()
    const subscription = subscribeToExecutionEvents(
      'http://127.0.0.1:5000',
      'session-1',
      onExecutionEvent,
    )
    const executionEvent = {
      sequence: 1,
      timestamp: '2026-06-21T00:00:00Z',
      type: 'Started',
      message: 'Execution started.',
    } satisfies ExecutionEvent

    executionEventListeners[0](new MessageEvent('execution-event', {
      data: JSON.stringify(executionEvent),
    }))
    subscription.close()

    expect(onExecutionEvent).toHaveBeenCalledWith(executionEvent)
    expect(close).toHaveBeenCalledTimes(1)
  })
})
