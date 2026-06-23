import { afterEach, describe, expect, it, vi } from 'vitest'
import { listContinuityReports, refreshRepositoryWorkspace, subscribeToExecutionEvents } from '../../api'
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

  it('preserves continuity report list command request and response handling', async () => {
    const report = {
      reportId: 'continuity.1',
      repositoryId: 'repo-alpha',
      generatedAt: '2026-01-02T00:00:00Z',
      relativePath: '.agents/continuity/continuity.1.json',
      diagnostics: {
        repositoryId: 'repo-alpha',
        generatedAt: '2026-01-02T00:00:00Z',
        revisionCount: 1,
        currentContextByteCount: 100,
        currentContextCharacterCount: 90,
        contextByteGrowth: 0,
        averageBytesPerRevision: 100,
        architectureTrend: { addedCount: 0, removedCount: 0, resolvedCount: 0, lostCount: 0 },
        constraintTrend: { addedCount: 0, removedCount: 0, resolvedCount: 0, lostCount: 0 },
        decisionTrend: { addedCount: 0, removedCount: 0, resolvedCount: 0, lostCount: 0 },
        rationaleTrend: { addedCount: 0, removedCount: 0, resolvedCount: 0, lostCount: 0 },
        openQuestionTrend: { addedCount: 0, removedCount: 0, resolvedCount: 0, lostCount: 0 },
        activeRiskTrend: { addedCount: 0, removedCount: 0, resolvedCount: 0, lostCount: 0 },
        compressionTrend: {
          proposalCount: 0,
          compressedItemCount: 0,
          removedItemCount: 0,
          resolvedQuestionCount: 0,
          retiredRiskCount: 0,
          warningCount: 0,
          warnings: [],
          noiseRemovedIndicators: [],
        },
        repeatedInvestigationIndicators: [],
        repeatedQuestionIndicators: [],
        decisionReworkIndicators: [],
        continuityWarnings: [],
      },
    }
    const invoke = vi.fn().mockResolvedValue([report])

    window.__TAURI_INTERNALS__ = {
      invoke,
      transformCallback: vi.fn(),
      unregisterCallback: vi.fn(),
      callbacks: {},
      convertFileSrc: vi.fn(),
    }

    await expect(listContinuityReports('repo-alpha')).resolves.toEqual([report])
    expect(invoke).toHaveBeenCalledWith('list_continuity_reports', {
      repositoryId: 'repo-alpha',
    }, undefined)
  })
})
