import { act, renderHook, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  useArtifactContent,
  useExecutionContextPreview,
  useRepositories,
  useRepositoryWorkspace,
} from '../../hooks'
import type {
  ExecutionContextPreview,
  RepositoryDashboardProjection,
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

function installInvokeMock(invoke: unknown) {
  window.__TAURI_INTERNALS__ = {
    invoke: invoke as (cmd: string, args?: Record<string, unknown>) => Promise<unknown>,
    transformCallback: vi.fn(),
    unregisterCallback: vi.fn(),
    callbacks: {},
    convertFileSrc: vi.fn(),
  }
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
})
