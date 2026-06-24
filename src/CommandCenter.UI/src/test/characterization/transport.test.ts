import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  archiveDecision,
  discoverDecisions,
  dismissDecisionCandidate,
  discardDecisionProposal,
  executeDecisionSessionTransfer,
  expireDecisionCandidate,
  expireDecisionProposal,
  generateDecisionProposal,
  getDecisionLifecycleEligibility,
  getDecisionSessionLifecycleProjection,
  getWorkflowProjection,
  listContinuityReports,
  markDecisionCandidateDuplicate,
  markDecisionProposalNeedsRefinement,
  markDecisionProposalReadyForResolution,
  markDecisionProposalViewed,
  promoteDecisionCandidate,
  refreshRepositoryWorkspace,
  recoverDecisionSession,
  subscribeToExecutionEvents,
  supersedeDecision,
} from '../../api'
import type {
  DecisionSessionLifecycleProjection,
  DecisionSessionRecoveryResult,
  DecisionSessionTransferResult,
  DecisionCandidate,
  DecisionDiscoveryResult,
  DecisionLifecycleEligibilityProjection,
  DecisionProposal,
  DecisionReviewStatus,
  ExecutionEvent,
  RepositoryWorkspaceProjection,
  WorkflowInstance,
} from '../../types'

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
      decisionSessionSummary: {
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

  it('preserves workflow projection command request and response handling', async () => {
    const workflow = {
      repositoryId: 'repo-alpha',
      currentStage: 'Execution',
      progressState: 'Active',
      blockingGate: 'None',
      requiredHumanAction: '',
    } as WorkflowInstance
    const invoke = vi.fn().mockResolvedValue(workflow)

    window.__TAURI_INTERNALS__ = {
      invoke,
      transformCallback: vi.fn(),
      unregisterCallback: vi.fn(),
      callbacks: {},
      convertFileSrc: vi.fn(),
    }

    await expect(getWorkflowProjection('repo-alpha')).resolves.toBe(workflow)
    expect(invoke).toHaveBeenCalledWith('get_workflow_projection', {
      repositoryId: 'repo-alpha',
    }, undefined)
  })

  it('preserves decision-session governance command request and response handling', async () => {
    const lifecycleProjection = {
      repositoryId: 'repo-alpha',
      activeSession: null,
      sessions: [],
      metrics: null,
      size: null,
      economics: null,
      coherence: null,
      policy: null,
      transferEligibility: null,
      currentContinuityArtifact: null,
      continuityArtifacts: [],
      recentTransfers: [],
      recentTransferEvents: [],
      transferEvents: [],
      recentRecoveryResults: [],
      diagnostics: {
        repositoryId: 'repo-alpha',
        isValid: true,
        sessionCount: 0,
        activeSessionCount: 0,
        errors: [],
        warnings: [],
        generatedAt: '2026-06-24T00:00:00Z',
      },
      generatedAt: '2026-06-24T00:00:00Z',
    } satisfies DecisionSessionLifecycleProjection
    const transfer = { succeeded: false } as DecisionSessionTransferResult
    const recovery = { recoveryId: 'recovery.1' } as DecisionSessionRecoveryResult
    const invoke = vi.fn()
      .mockResolvedValueOnce(lifecycleProjection)
      .mockResolvedValueOnce(transfer)
      .mockResolvedValueOnce(recovery)

    window.__TAURI_INTERNALS__ = {
      invoke,
      transformCallback: vi.fn(),
      unregisterCallback: vi.fn(),
      callbacks: {},
      convertFileSrc: vi.fn(),
    }

    await expect(getDecisionSessionLifecycleProjection('repo-alpha')).resolves.toBe(lifecycleProjection)
    await expect(executeDecisionSessionTransfer('repo-alpha')).resolves.toBe(transfer)
    await expect(recoverDecisionSession('repo-alpha')).resolves.toBe(recovery)
    expect(invoke).toHaveBeenNthCalledWith(1, 'get_decision_session_lifecycle_projection', {
      repositoryId: 'repo-alpha',
    }, undefined)
    expect(invoke).toHaveBeenNthCalledWith(2, 'execute_decision_session_transfer', {
      repositoryId: 'repo-alpha',
    }, undefined)
    expect(invoke).toHaveBeenNthCalledWith(3, 'recover_decision_session', {
      repositoryId: 'repo-alpha',
    }, undefined)
  })

  it('preserves core decision lifecycle command request and response handling', async () => {
    const candidate = { id: 'CAND-0001' } as DecisionCandidate
    const discovery = { candidates: [candidate] } as DecisionDiscoveryResult
    const proposal = { id: 'PROP-0001' } as DecisionProposal
    const review = { proposalId: 'PROP-0001', state: 'Viewed' } as DecisionReviewStatus
    const decision = { id: 'DEC-0001' }
    const eligibility = {
      repositoryId: 'repo-alpha',
      candidates: [],
      proposals: [],
      decisions: [],
      diagnostics: [],
    } satisfies DecisionLifecycleEligibilityProjection
    const invoke = vi.fn()
      .mockResolvedValueOnce(discovery)
      .mockResolvedValueOnce(eligibility)
      .mockResolvedValueOnce(candidate)
      .mockResolvedValueOnce(candidate)
      .mockResolvedValueOnce(candidate)
      .mockResolvedValueOnce(candidate)
      .mockResolvedValueOnce(proposal)
      .mockResolvedValueOnce(review)
      .mockResolvedValueOnce(review)
      .mockResolvedValueOnce(review)
      .mockResolvedValueOnce(proposal)
      .mockResolvedValueOnce(proposal)
      .mockResolvedValueOnce(decision)
      .mockResolvedValueOnce(decision)

    window.__TAURI_INTERNALS__ = {
      invoke,
      transformCallback: vi.fn(),
      unregisterCallback: vi.fn(),
      callbacks: {},
      convertFileSrc: vi.fn(),
    }

    await discoverDecisions('repo-alpha')
    await getDecisionLifecycleEligibility('repo-alpha')
    await promoteDecisionCandidate('repo-alpha', 'CAND-0001', { reason: 'ready' })
    await dismissDecisionCandidate('repo-alpha', 'CAND-0001')
    await expireDecisionCandidate('repo-alpha', 'CAND-0001', { reason: 'stale' })
    await markDecisionCandidateDuplicate('repo-alpha', 'CAND-0001', {
      duplicateOfCandidateId: 'CAND-0000',
      reason: 'same source',
    })
    await generateDecisionProposal('repo-alpha', 'CAND-0001')
    await markDecisionProposalViewed('repo-alpha', 'PROP-0001')
    await markDecisionProposalNeedsRefinement('repo-alpha', 'PROP-0001', { reason: 'needs more evidence' })
    await markDecisionProposalReadyForResolution('repo-alpha', 'PROP-0001')
    await expireDecisionProposal('repo-alpha', 'PROP-0001')
    await discardDecisionProposal('repo-alpha', 'PROP-0001', { reason: 'wrong scope' })
    await supersedeDecision('repo-alpha', 'DEC-0001', {
      replacementDecisionId: 'DEC-0002',
      rationale: 'replacement accepted',
      resolver: 'Reviewer',
    })
    await archiveDecision('repo-alpha', 'DEC-0001', {
      rationale: 'no longer relevant',
      resolver: 'Reviewer',
    })

    expect(invoke).toHaveBeenNthCalledWith(1, 'discover_decisions', {
      repositoryId: 'repo-alpha',
    }, undefined)
    expect(invoke).toHaveBeenNthCalledWith(2, 'get_decision_lifecycle_eligibility', {
      repositoryId: 'repo-alpha',
    }, undefined)
    expect(invoke).toHaveBeenNthCalledWith(3, 'promote_decision_candidate', {
      repositoryId: 'repo-alpha',
      candidateId: 'CAND-0001',
      reason: 'ready',
    }, undefined)
    expect(invoke).toHaveBeenNthCalledWith(4, 'dismiss_decision_candidate', {
      repositoryId: 'repo-alpha',
      candidateId: 'CAND-0001',
      reason: null,
    }, undefined)
    expect(invoke).toHaveBeenNthCalledWith(5, 'expire_decision_candidate', {
      repositoryId: 'repo-alpha',
      candidateId: 'CAND-0001',
      reason: 'stale',
    }, undefined)
    expect(invoke).toHaveBeenNthCalledWith(6, 'mark_decision_candidate_duplicate', {
      repositoryId: 'repo-alpha',
      candidateId: 'CAND-0001',
      duplicateOfCandidateId: 'CAND-0000',
      reason: 'same source',
    }, undefined)
    expect(invoke).toHaveBeenNthCalledWith(7, 'generate_decision_proposal', {
      repositoryId: 'repo-alpha',
      candidateId: 'CAND-0001',
    }, undefined)
    expect(invoke).toHaveBeenNthCalledWith(8, 'mark_decision_proposal_viewed', {
      repositoryId: 'repo-alpha',
      proposalId: 'PROP-0001',
      reason: null,
    }, undefined)
    expect(invoke).toHaveBeenNthCalledWith(9, 'mark_decision_proposal_needs_refinement', {
      repositoryId: 'repo-alpha',
      proposalId: 'PROP-0001',
      reason: 'needs more evidence',
    }, undefined)
    expect(invoke).toHaveBeenNthCalledWith(10, 'mark_decision_proposal_ready_for_resolution', {
      repositoryId: 'repo-alpha',
      proposalId: 'PROP-0001',
      reason: null,
    }, undefined)
    expect(invoke).toHaveBeenNthCalledWith(11, 'expire_decision_proposal', {
      repositoryId: 'repo-alpha',
      proposalId: 'PROP-0001',
      reason: null,
    }, undefined)
    expect(invoke).toHaveBeenNthCalledWith(12, 'discard_decision_proposal', {
      repositoryId: 'repo-alpha',
      proposalId: 'PROP-0001',
      reason: 'wrong scope',
    }, undefined)
    expect(invoke).toHaveBeenNthCalledWith(13, 'supersede_decision', {
      repositoryId: 'repo-alpha',
      decisionId: 'DEC-0001',
      request: {
        replacementDecisionId: 'DEC-0002',
        rationale: 'replacement accepted',
        resolver: 'Reviewer',
      },
    }, undefined)
    expect(invoke).toHaveBeenNthCalledWith(14, 'archive_decision', {
      repositoryId: 'repo-alpha',
      decisionId: 'DEC-0001',
      request: {
        rationale: 'no longer relevant',
        resolver: 'Reviewer',
      },
    }, undefined)
  })
})
