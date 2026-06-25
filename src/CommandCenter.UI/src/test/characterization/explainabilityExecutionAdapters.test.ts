import { describe, expect, it } from 'vitest'
import {
  executionGitEligibilityToActions,
  executionGitEligibilityToDiagnostics,
  executionGovernedConflictsToDiagnostics,
  executionPromptManifestToDiagnostics,
  executionPromptManifestToEvidence,
  executionRepositorySnapshotToEvidence,
  executionSessionTransparencyToDiagnostics,
} from '../../lib/explainability'
import type {
  ExecutionGitActionEligibility,
  ExecutionGovernedConflictDiagnostic,
  ExecutionPromptManifest,
  ExecutionRepositorySnapshot,
  ExecutionSessionTransparency,
} from '../../types'

function promptManifest(): ExecutionPromptManifest {
  return {
    sessionId: 'session-alpha',
    generatedAt: '2026-06-21T16:00:30.000Z',
    promptText: 'Prompt text',
    promptArtifactPath: '.agents/execution/session-alpha/prompt.md',
    requestedArtifacts: [
      {
        role: 'Milestone',
        relativePath: '.agents/milestones/m8-explainability-layer.md',
        byteCount: 512,
        characterCount: 512,
        delivered: true,
      },
    ],
    requestedContextBytes: 512,
    requestedContextCharacters: 512,
    deliveredArtifacts: [
      {
        role: 'Milestone',
        relativePath: '.agents/milestones/m8-explainability-layer.md',
        byteCount: 512,
        characterCount: 512,
        delivered: true,
      },
    ],
    deliveredContextBytes: 512,
    deliveredContextCharacters: 512,
    dirtyRepositoryAtRequestTime: true,
    dirtyRepositoryAtDeliveryTime: false,
    governedDecisionCountRequested: 4,
    governedDecisionCountDelivered: 3,
    operationalContextSourceRequested: '.agents/operational_context.md',
    operationalContextSourceDelivered: '.agents/operational_context.md',
    handoffSourceRequested: '.agents/handoffs/handoff.md',
    handoffSourceDelivered: '.agents/handoffs/handoff.md',
    milestoneSourceRequested: '.agents/milestones/m8-explainability-layer.md',
    milestoneSourceDelivered: '.agents/milestones/m8-explainability-layer.md',
    providerDeliveryStatus: 'DeliveredWithAdjustment',
    providerAdjustments: ['Provider normalized line endings before delivery.'],
    divergenceReason: 'Governed decision count changed between request and delivery.',
    diagnostics: ['PromptManifestCaptured'],
  }
}

function gitEligibility(): ExecutionGitActionEligibility {
  return {
    sessionId: 'session-alpha',
    sessionExists: true,
    repositoryState: 'AwaitingPush',
    commitPreparationLoaded: true,
    commitPreparationCurrent: false,
    commitPreparationId: 'prep-alpha',
    preparedStatusSnapshotId: 'snapshot-before',
    currentStatusSnapshotId: 'snapshot-after',
    selectedPathCount: 1,
    preparedPathCount: 2,
    unknownSelectedPaths: ['unknown.ts'],
    commitMessagePresent: false,
    repositoryAllowsCommit: false,
    awaitingPush: true,
    commitShaExists: true,
    commitSha: 'abc123',
    previousPushAttemptedAt: '2026-06-21T16:30:00.000Z',
    previousPushFailure: 'git push failed: rejected by remote',
    remoteBranchState: {
      branch: 'main',
      aheadCount: 1,
      behindCount: 1,
      hasUnpushedChanges: true,
      hasRemoteDivergence: true,
      capturedAt: '2026-06-21T16:31:00.000Z',
    },
    canCommit: false,
    canPush: false,
    commitDisabledReasons: ['Commit preparation is stale.', 'Commit message is required.'],
    pushDisabledReasons: ['Remote branch has new commits; review branch state before pushing.'],
    diagnostics: ['Commit status snapshot unavailable: git status failed'],
  }
}

describe('execution explainability adapters', () => {
  it('preserves prompt manifest requested and delivered context facts as evidence', () => {
    const evidence = executionPromptManifestToEvidence(promptManifest())

    expect(evidence).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          label: 'Prompt artifact',
          detail: 'DeliveredWithAdjustment',
          source: '.agents/execution/session-alpha/prompt.md',
        }),
        expect.objectContaining({
          label: 'Requested context',
          detail: '512 bytes | 512 chars | 4 governed decisions',
        }),
        expect.objectContaining({
          label: 'Delivered context',
          detail: '512 bytes | 512 chars | 3 governed decisions',
        }),
        expect.objectContaining({
          label: 'Requested Milestone',
          source: '.agents/milestones/m8-explainability-layer.md',
        }),
      ]),
    )
  })

  it('preserves prompt manifest divergence, provider adjustment, and diagnostics', () => {
    const diagnostics = executionPromptManifestToDiagnostics(promptManifest())

    expect(diagnostics).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          label: 'Prompt divergence',
          detail: 'Governed decision count changed between request and delivery.',
          tone: 'warning',
        }),
        expect.objectContaining({
          label: 'Provider adjustment',
          detail: 'Provider normalized line endings before delivery.',
        }),
        expect.objectContaining({
          label: 'Prompt diagnostic',
          detail: 'PromptManifestCaptured',
        }),
      ]),
    )
  })

  it('preserves repository snapshot path evidence by change bucket', () => {
    const snapshot: ExecutionRepositorySnapshot = {
      branch: 'main',
      capturedAt: '2026-06-21T17:00:00.000Z',
      dirtyState: {
        stagedPaths: ['staged.ts'],
        modifiedPaths: ['modified.ts'],
        addedPaths: ['added.ts'],
        deletedPaths: ['deleted.ts'],
        renamedPaths: ['old.ts -> new.ts'],
        untrackedPaths: ['scratch.md'],
        isClean: false,
      },
    }

    expect(executionRepositorySnapshotToEvidence(snapshot)).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ label: 'Repository snapshot', detail: 'main | Dirty | 2026-06-21T17:00:00.000Z' }),
        expect.objectContaining({ label: 'Staged path', source: 'staged.ts' }),
        expect.objectContaining({ label: 'Modified path', source: 'modified.ts' }),
        expect.objectContaining({ label: 'Added path', source: 'added.ts' }),
        expect.objectContaining({ label: 'Deleted path', source: 'deleted.ts' }),
        expect.objectContaining({ label: 'Renamed path', source: 'old.ts -> new.ts' }),
        expect.objectContaining({ label: 'Untracked path', source: 'scratch.md' }),
      ]),
    )
  })

  it('preserves backend-owned git eligibility actions, constraints, and diagnostics', () => {
    const eligibility = gitEligibility()

    expect(executionGitEligibilityToActions(eligibility)).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          label: 'Commit execution changes',
          eligible: false,
          reason: 'Commit preparation is stale. Commit message is required.',
          command: 'executionCommit',
        }),
        expect.objectContaining({
          label: 'Push execution commit',
          eligible: false,
          reason: 'Remote branch has new commits; review branch state before pushing.',
          command: 'executionPush',
        }),
      ]),
    )
    expect(executionGitEligibilityToActions(eligibility)[0].constraints).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          label: 'Commit preparation current',
          detail: 'snapshot-before -> snapshot-after',
          satisfied: false,
        }),
        expect.objectContaining({
          label: 'Backend disabled reason',
          detail: 'Commit preparation is stale.',
          satisfied: false,
        }),
      ]),
    )
    expect(executionGitEligibilityToDiagnostics(eligibility)).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ label: 'Commit disabled reason', detail: 'Commit preparation is stale.' }),
        expect.objectContaining({ label: 'Push disabled reason', detail: 'Remote branch has new commits; review branch state before pushing.' }),
        expect.objectContaining({ label: 'Unknown selected path', detail: 'unknown.ts' }),
        expect.objectContaining({ label: 'Git eligibility diagnostic', detail: 'Commit status snapshot unavailable: git status failed' }),
      ]),
    )
  })

  it('preserves governed conflict facts and source evidence', () => {
    const conflict: ExecutionGovernedConflictDiagnostic = {
      id: 'conflict-1',
      decisionId: 'DEC-0007',
      title: 'Persistence authority',
      statement: 'Persistence mutations must remain backend-owned.',
      conflictingExcerpt: 'React recomputes persistence mutation eligibility.',
      conflictReason: 'Governed decision DEC-0007 conflicts with selected execution context.',
      affectedContext: '.agents/milestones/m8-explainability-layer.md',
      affectedPromptSection: 'Governed Decision Projection',
      recommendedResolution: 'Resolve or supersede the governed decision conflict before launching execution.',
      severity: 'Blocking',
      originatingAuthority: 'DecisionProjectionService',
      sources: [
        {
          sourceKind: 'Decision',
          relativePath: '.agents/decisions/decisions.md',
          section: 'Newly Authorized',
          excerpt: 'Persistence mutations must remain backend-owned.',
          itemId: 'DEC-0007',
          decisionId: 'DEC-0007',
          proposalId: null,
          candidateId: null,
        },
      ],
      evidence: ['Decision statement: Persistence mutations must remain backend-owned.'],
      diagnostics: ['Conflict was projected by the decisions authority and blocks launch.'],
    }

    expect(executionGovernedConflictsToDiagnostics([conflict])).toEqual([
      expect.objectContaining({
        label: 'Blocking: DEC-0007',
        detail: 'Governed decision DEC-0007 conflicts with selected execution context.',
        tone: 'danger',
        evidence: expect.arrayContaining([
          expect.objectContaining({ label: 'Persistence authority' }),
          expect.objectContaining({ label: 'Affected context', source: '.agents/milestones/m8-explainability-layer.md' }),
          expect.objectContaining({ label: 'Conflict evidence', detail: 'Decision statement: Persistence mutations must remain backend-owned.' }),
          expect.objectContaining({ label: 'Decision', source: '.agents/decisions/decisions.md' }),
        ]),
      }),
    ])
  })

  it('preserves recovery, monitoring, and handoff diagnostics from transparency projection', () => {
    const transparency: ExecutionSessionTransparency = {
      sessionId: 'session-alpha',
      promptMetadata: null,
      recovery: {
        recoveryRan: true,
        recoveryTrigger: 'StartupRecovery',
        reattachAttempted: true,
        reattachSucceeded: false,
        orphanedProviderState: true,
        sessionMarkedFailedByRecovery: true,
        recoveryEventTimestamp: '2026-06-21T16:13:00.000Z',
        recoveryMessage: 'Active provider process could not be reattached after backend restart.',
      },
      monitoring: {
        providerProcessState: 'Exited',
        exitCode: 2,
        lastActivityAt: '2026-06-21T16:13:00.000Z',
        staleActivity: false,
        retainedEventCount: 3,
        firstRetainedEventSequence: 1,
        lastRetainedEventSequence: 3,
        eventRetentionTrimmingDetected: false,
        monitoringWarnings: ['Provider exited with non-zero code 2.'],
      },
      handoffProcessing: {
        handoffProduced: true,
        handoffMissing: false,
        handoffArchived: true,
        archivePath: '.agents/handoffs/handoff.0005.md',
        archiveSequence: 5,
        archiveFailed: false,
        handoffValidated: true,
        validationFailure: null,
        resultingSessionState: 'Completed',
        resultingRepositoryState: 'AwaitingAcceptance',
        processedAt: '2026-06-21T16:13:30.000Z',
        providerFailureDistinctFromHandoffFailure: false,
        providerFailureReason: null,
        handoffFailureReason: null,
        diagnostics: ['PreviousHandoffArchived:.agents/handoffs/handoff.0005.md'],
      },
    }

    expect(executionSessionTransparencyToDiagnostics(transparency)).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          label: 'Recovery state',
          detail: 'Recovery ran | StartupRecovery | Active provider process could not be reattached after backend restart.',
          tone: 'warning',
        }),
        expect.objectContaining({
          label: 'Monitoring state',
          detail: 'Exited | retained events 3',
        }),
        expect.objectContaining({
          label: 'Monitoring warning',
          detail: 'Provider exited with non-zero code 2.',
        }),
        expect.objectContaining({
          label: 'Handoff processing',
          detail: 'Completed | AwaitingAcceptance',
        }),
        expect.objectContaining({
          label: 'Handoff diagnostic',
          detail: 'PreviousHandoffArchived:.agents/handoffs/handoff.0005.md',
        }),
      ]),
    )
  })
})
