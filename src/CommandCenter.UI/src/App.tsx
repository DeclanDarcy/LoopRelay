import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  acceptExecutionHandoff,
  acceptOperationalContextProposal as acceptOperationalContextProposalCommand,
  commitExecution,
  editOperationalContextProposal,
  formatError,
  generateContinuityReport as generateContinuityReportCommand,
  generateOperationalContextProposal as generateOperationalContextProposalCommand,
  getOperationalContextProposal,
  loadArtifactContent,
  prepareCommit,
  promoteOperationalContextProposal as promoteOperationalContextProposalCommand,
  pushExecution as pushExecutionCommand,
  refreshRepositoryWorkspace,
  registerRepository,
  rejectExecutionHandoff,
  rejectOperationalContextProposal as rejectOperationalContextProposalCommand,
  removeRepository as removeRepositoryRegistration,
  rotateCurrentDecisions,
  rotateCurrentHandoff,
  saveArtifactContent,
  selectRepositoryDirectory,
  startExecution as startExecutionCommand,
} from './api'
import { ExecutionContextArtifactList } from './features/execution/ExecutionContextArtifactList'
import { ExecutionContextMissingOptionalList } from './features/execution/ExecutionContextMissingOptionalList'
import { ExecutionContextSummaryRows } from './features/execution/ExecutionContextSummaryRows'
import { ExecutionContextValidationList } from './features/execution/ExecutionContextValidationList'
import { ExecutionEventFeed } from './features/execution/ExecutionEventFeed'
import { ExecutionHistoryPanel } from './features/execution/ExecutionHistoryPanel'
import { ExecutionSessionPanel } from './features/execution/ExecutionSessionPanel'
import { GitPathBucket } from './features/execution/GitPathBucket'
import {
  useArtifactContent,
  useContinuityDiagnostics,
  useExecutionContextPreview,
  useExecutionEvents,
  useExecutionSession,
  useGitStatus,
  mergeExecutionEvents,
  useRepositories,
  useRepositoryWorkspace,
} from './hooks'
import {
  countDirtyPaths,
  formatDateTime,
  formatDuration,
  getArtifactCategories,
  getAvailableArtifactPaths,
  getExecutionWorkflowSteps,
  getOperationalContextSectionItems,
  renderMarkdown,
} from './lib'
import { useShellState } from './state/shellState'
import type {
  CommitPreparation,
  ExecutionReadiness,
  ExecutionSessionSummary,
  OperationalContextCompressionSummary,
  OperationalContextProposal,
  Repository,
  RepositoryAvailability,
  RepositoryExecutionState,
  RepositoryWorkspaceProjection,
} from './types'
import './App.css'

const availabilityLabels: Record<RepositoryAvailability, string> = {
  Available: 'Available',
  Missing: 'Missing',
  AccessDenied: 'Access denied',
}

const readinessLabels: Record<ExecutionReadiness, string> = {
  MissingPlan: 'Missing plan',
  MissingMilestones: 'Missing milestones',
  Ready: 'Ready',
}

const executionStateLabels: Record<RepositoryExecutionState, string> = {
  Ready: 'Ready',
  Executing: 'Executing',
  AwaitingAcceptance: 'Awaiting acceptance',
  Accepted: 'Accepted',
  AwaitingCommit: 'Awaiting commit',
  AwaitingPush: 'Awaiting push',
  Failed: 'Failed',
  Cancelled: 'Cancelled',
}

const decisionSemanticChangeTypes = new Set([
  'ImportantDecisionIntroduced',
  'DecisionRetired',
  'DecisionAdded',
  'DecisionRemoved',
  'RationaleChanged',
  'RationaleLostWarning',
  'OpenDecisionPreserved',
  'OpenDecisionResolved',
])

function getDecisionContinuityWarnings(summary: OperationalContextCompressionSummary) {
  return summary.warnings
    .concat(summary.stableUnderstandingRetentionWarnings)
    .filter((warning, index, warnings) =>
      warning.toLowerCase().includes('decision') && warnings.indexOf(warning) === index,
    )
}

function App() {
  const {
    selectedRepositoryId,
    selectedArtifactPath,
    selectedMilestonePath,
    selectRepository: selectRepositoryNavigation,
    reconcileRepositorySelection,
    selectArtifact: selectArtifactNavigation,
    reconcileSelectedArtifact: reconcileSelectedArtifactNavigation,
    selectMilestone,
    reconcileSelectedMilestone,
    clearRepositoryNavigation,
  } = useShellState()
  const [commitPreparation, setCommitPreparation] = useState<CommitPreparation | null>(null)
  const [selectedCommitPaths, setSelectedCommitPaths] = useState<Set<string>>(new Set())
  const [commitMessage, setCommitMessage] = useState('')
  const refreshedCompletedSessionIds = useRef<Set<string>>(new Set())
  const [draftContent, setDraftContent] = useState('')
  const [generatedHandoffContent, setGeneratedHandoffContent] = useState('')
  const [operationalContextProposal, setOperationalContextProposal] =
    useState<OperationalContextProposal | null>(null)
  const [operationalContextCurrentContent, setOperationalContextCurrentContent] = useState('')
  const [operationalContextProposalDraft, setOperationalContextProposalDraft] = useState('')
  const [operationalContextReviewNote, setOperationalContextReviewNote] = useState('')
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)
  const [isRotating, setIsRotating] = useState(false)
  const [isOperationalContextProposalLoading, setIsOperationalContextProposalLoading] =
    useState(false)
  const [isOperationalContextProposalSaving, setIsOperationalContextProposalSaving] =
    useState(false)
  const [isContinuityReportGenerating, setIsContinuityReportGenerating] = useState(false)
  const [isStartingExecution, setIsStartingExecution] = useState(false)
  const [isGeneratedHandoffLoading, setIsGeneratedHandoffLoading] = useState(false)
  const [isAcceptingHandoff, setIsAcceptingHandoff] = useState(false)
  const [isRejectingHandoff, setIsRejectingHandoff] = useState(false)
  const [isCommitPreparationLoading, setIsCommitPreparationLoading] = useState(false)
  const [isCommitting, setIsCommitting] = useState(false)
  const [isPushing, setIsPushing] = useState(false)
  const [isAdding, setIsAdding] = useState(false)
  const [removingRepositoryId, setRemovingRepositoryId] = useState<string | null>(null)
  const {
    data: repositories,
    setData: setRepositories,
    isLoading,
    error: repositoriesError,
    refresh: loadRepositories,
  } = useRepositories()

  const selectedRepository = useMemo(
    () =>
      repositories.find((entry) => entry.repository.id === selectedRepositoryId) ??
      repositories[0] ??
      null,
    [repositories, selectedRepositoryId],
  )
  const {
    data: workspace,
    setData: setWorkspace,
    isLoading: isWorkspaceLoading,
    error: workspaceError,
    load: loadWorkspaceProjection,
    refresh: refreshWorkspaceProjection,
  } = useRepositoryWorkspace(selectedRepository?.repository.id ?? null)
  const executionSummary =
    workspace?.executionSummary ??
    selectedRepository?.executionSummary ??
    selectedRepository?.activeExecutionSession ??
    null
  const selectedExecutionHistory =
    workspace?.executionHistory ??
    selectedRepository?.executionHistory ??
    (executionSummary ? [executionSummary] : [])
  const activeExecutionSummary =
    executionSummary?.repositoryState === 'Executing'
      ? executionSummary
      : selectedRepository?.activeExecutionSession ?? null
  const executionSessionId = executionSummary?.sessionId ?? null
  const {
    data: executionSessionStatus,
    setData: setExecutionSessionStatus,
    error: executionSessionError,
    refresh: refreshExecutionSessionStatus,
  } = useExecutionSession(selectedRepository?.repository.id ?? null, executionSessionId)
  const {
    data: streamedExecutionEvents,
    error: executionEventsError,
  } = useExecutionEvents(executionSessionId)
  const {
    data: artifactContent,
    setData: setArtifactContent,
    isLoading: isArtifactLoading,
    error: artifactError,
  } = useArtifactContent(selectedRepository?.repository.id ?? null, selectedArtifactPath)
  const {
    data: executionContext,
    setData: setExecutionContext,
    isLoading: isContextLoading,
    error: executionContextError,
    load: loadExecutionContextPreview,
  } = useExecutionContextPreview(selectedRepository?.repository.id ?? null, selectedMilestonePath)
  const {
    data: continuityDiagnostics,
    setData: setContinuityDiagnostics,
    isLoading: isContinuityDiagnosticsLoading,
    error: continuityDiagnosticsError,
    refresh: refreshContinuityDiagnostics,
  } = useContinuityDiagnostics(selectedRepository?.repository.id ?? null)

  const selectedArtifact = useMemo(() => {
    if (!workspace || !selectedArtifactPath) {
      return null
    }

    return (
      getArtifactCategories(workspace.artifactInventory)
        .flatMap((category) => category.artifacts)
        .find((artifact) => artifact.relativePath === selectedArtifactPath) ?? null
    )
  }, [selectedArtifactPath, workspace])

  const hasDraftChanges = artifactContent !== draftContent
  const canRotateSelectedArtifact =
    selectedArtifact?.versionKind === 'Current' &&
    (selectedArtifact.family === 'Handoff' || selectedArtifact.family === 'Decision')
  const operationalContextCandidateContent =
    operationalContextProposal?.editedContent ??
    operationalContextProposal?.generatedContent ??
    ''
  const isOperationalContextProposalReviewable =
    operationalContextProposal?.status === 'Pending' ||
    operationalContextProposal?.status === 'Edited'
  const isOperationalContextReviewBlocked =
    !operationalContextProposal ||
    !isOperationalContextProposalReviewable ||
    operationalContextProposal.review.reviewState === 'Stale'
  const canPromoteOperationalContextProposal =
    operationalContextProposal?.status === 'Accepted' &&
    operationalContextProposal.review.reviewState === 'Accepted' &&
    !operationalContextProposal.review.staleReason
  const hasOperationalContextProposalDraftChanges =
    operationalContextProposal !== null &&
    operationalContextProposalDraft !== operationalContextCandidateContent
  const proposedStableDecisions = getOperationalContextSectionItems(
    operationalContextProposalDraft,
    'Stable Decisions',
  )
  const proposedOpenDecisions = getOperationalContextSectionItems(
    operationalContextProposalDraft,
    'Open Questions',
  ).filter((item) => item.toLowerCase().startsWith('open decision:'))
  const proposedDecisionRationale = getOperationalContextSectionItems(
    operationalContextProposalDraft,
    'Decision Rationale',
  )
  const decisionSemanticChanges =
    operationalContextProposal?.semanticChanges.filter((change) =>
      decisionSemanticChangeTypes.has(change.type),
    ) ?? []
  const decisionContinuityWarnings = operationalContextProposal
    ? getDecisionContinuityWarnings(operationalContextProposal.compressionSummary)
    : []
  const milestoneOptions = workspace?.artifactInventory.milestones ?? []
  const selectedExecutionStatus = executionSummary
    ? executionSessionStatus?.sessionId === executionSessionId
      ? executionSessionStatus
      : null
    : null
  const selectedExecutionEvents = executionSummary
    ? mergeExecutionEvents(selectedExecutionStatus?.recentEvents ?? [], streamedExecutionEvents)
    : []
  const currentExecutionState = workspace?.executionState ?? selectedRepository?.executionState ?? 'Ready'
  const executionContextMatchesSelection =
    executionContext?.repositoryId === selectedRepository?.repository.id &&
    executionContext?.milestonePath === selectedMilestonePath
  const executionContextOperationalContext = executionContextMatchesSelection
    ? executionContext?.artifacts.find((artifact) => artifact.role === 'OperationalContext') ?? null
    : null
  const executionContextMissingOperationalContext = executionContextMatchesSelection
    ? executionContext?.diagnostics.missingOptionalArtifacts.includes('.agents/operational_context.md') ?? false
    : false
  const operationalContextExecutionStatus = !executionContext
    ? 'Preview not built'
    : !executionContextMatchesSelection
      ? 'Preview stale'
      : executionContextOperationalContext
        ? `Included (${executionContextOperationalContext.byteCount} bytes)`
        : executionContextMissingOperationalContext
          ? 'Not included: missing optional artifact'
          : 'Not included'
  const operationalContextReviewStatus =
    workspace?.operationalContext.latestReviewState ??
    workspace?.operationalContextProposalSummary.status ??
    'None'
  const canStartExecution =
    Boolean(selectedRepository && workspace && selectedMilestonePath && executionContextMatchesSelection) &&
    workspace?.readiness === 'Ready' &&
    currentExecutionState === 'Ready' &&
    !executionContext?.diagnostics.launchBlocked &&
    !executionContext?.diagnostics.hardLimitExceeded &&
    executionContext?.diagnostics.validationErrors.length === 0 &&
    !activeExecutionSummary &&
    !isStartingExecution
  const executionDisplay = executionSummary
    ? {
        sessionId: executionSummary.sessionId,
        milestonePath: executionSummary.milestonePath,
        state: selectedExecutionStatus?.state ?? executionSummary.state,
        repositoryState: selectedExecutionStatus?.repositoryState ?? executionSummary.repositoryState,
        startedAt: selectedExecutionStatus?.startedAt ?? executionSummary.startedAt,
        completedAt: selectedExecutionStatus?.completedAt ?? executionSummary.completedAt,
        duration: selectedExecutionStatus?.duration ?? executionSummary.duration,
        acceptedAt: selectedExecutionStatus?.acceptedAt ?? executionSummary.acceptedAt,
        rejectedAt: selectedExecutionStatus?.rejectedAt ?? executionSummary.rejectedAt,
        decisionNote: selectedExecutionStatus?.decisionNote ?? executionSummary.decisionNote,
        lastActivityAt: selectedExecutionStatus?.lastActivityAt ?? executionSummary.lastActivityAt,
        providerName: selectedExecutionStatus?.providerName ?? executionSummary.providerName,
        providerExecutablePath:
          selectedExecutionStatus?.providerExecutablePath ?? executionSummary.providerExecutablePath,
        providerProcessId: selectedExecutionStatus?.providerProcessId ?? executionSummary.providerProcessId,
        providerStartedAt: selectedExecutionStatus?.providerStartedAt ?? executionSummary.providerStartedAt,
        handoffPath: selectedExecutionStatus?.handoffPath ?? executionSummary.handoffPath,
        commitSha: executionSummary.commitSha,
        committedAt: executionSummary.committedAt,
        commitMessage: executionSummary.commitMessage,
        preparationSnapshotId: executionSummary.preparationSnapshotId,
        pushAttemptedAt: executionSummary.pushAttemptedAt,
        pushedAt: executionSummary.pushedAt,
        pushedCommitSha: executionSummary.pushedCommitSha,
        pushRemoteName: executionSummary.pushRemoteName,
        pushBranchName: executionSummary.pushBranchName,
        failureReason: selectedExecutionStatus?.failureReason ?? executionSummary.failureReason,
      }
    : null
  const canReviewGeneratedHandoff =
    executionDisplay?.repositoryState === 'AwaitingAcceptance' &&
    Boolean(executionDisplay.handoffPath)
  const isHandoffDecisionPending =
    canReviewGeneratedHandoff && !isAcceptingHandoff && !isRejectingHandoff
  const shouldShowGitWorkflow =
    currentExecutionState === 'Ready' ||
    currentExecutionState === 'AwaitingCommit' ||
    currentExecutionState === 'AwaitingPush'
  const {
    data: gitStatus,
    isLoading: isGitStatusLoading,
    error: gitStatusError,
    refresh: refreshGitStatus,
  } = useGitStatus(
    selectedRepository && shouldShowGitWorkflow && currentExecutionState !== 'AwaitingCommit'
      ? selectedRepository.repository.id
      : null,
  )
  const gitStatusPathCount = countDirtyPaths(gitStatus?.dirtyState ?? null)
  const selectedCommitScopeItems =
    commitPreparation?.scopeItems.filter((item) => selectedCommitPaths.has(item.path)) ?? []
  const isCommitPreparationCurrent =
    commitPreparation?.sessionId === executionSessionId &&
    currentExecutionState === 'AwaitingCommit'
  const canCommitPreparedScope =
    Boolean(isCommitPreparationCurrent && commitPreparation) &&
    selectedCommitScopeItems.length > 0 &&
    commitMessage.trim().length > 0 &&
    !isCommitting
  const canPushExecution =
    Boolean(executionSessionId) &&
    currentExecutionState === 'AwaitingPush' &&
    Boolean(executionDisplay?.commitSha) &&
    !isPushing
  const executionWorkflowSteps = getExecutionWorkflowSteps(
    currentExecutionState,
    executionContextMatchesSelection,
    Boolean(executionDisplay),
  )
  const executionContextSizeStatus = !executionContext
    ? 'Not available'
    : executionContext.diagnostics.hardLimitExceeded
      ? 'Hard limit'
      : executionContext.diagnostics.warningThresholdExceeded
        ? 'Warning'
        : 'Within limits'

  const startExecutionBlockedReason = useMemo(() => {
    if (!workspace) {
      return 'Workspace is loading.'
    }

    if (workspace.readiness !== 'Ready') {
      return `Repository readiness is ${readinessLabels[workspace.readiness]}.`
    }

    if (!selectedMilestonePath) {
      return 'Select a milestone.'
    }

    if (!executionContext || !executionContextMatchesSelection) {
      return 'Build an execution context for the selected milestone.'
    }

    if (activeExecutionSummary || currentExecutionState !== 'Ready') {
      return 'Repository already has an active execution session.'
    }

    if (executionContext.diagnostics.launchBlocked) {
      return 'Execution context validation blocks launch.'
    }

    if (executionContext.diagnostics.hardLimitExceeded) {
      return 'Execution context exceeds the hard size limit.'
    }

    if (executionContext.diagnostics.validationErrors.length > 0) {
      return executionContext.diagnostics.validationErrors[0]
    }

    return 'Ready to start execution.'
  }, [
    activeExecutionSummary,
    currentExecutionState,
    executionContext,
    executionContextMatchesSelection,
    selectedMilestonePath,
    workspace,
  ])

  const selectRepository = useCallback(
    (repositoryId: string) => {
      selectRepositoryNavigation(repositoryId)
      setOperationalContextProposal(null)
      setContinuityDiagnostics(null)
      setOperationalContextCurrentContent('')
      setOperationalContextProposalDraft('')
      setOperationalContextReviewNote('')
    },
    [selectRepositoryNavigation, setContinuityDiagnostics],
  )

  const selectArtifact = useCallback(
    (repositoryId: string, relativePath: string) => {
      selectArtifactNavigation(repositoryId, relativePath)
    },
    [selectArtifactNavigation],
  )

  const reconcileSelectedArtifact = useCallback(
    (repositoryId: string, nextWorkspace: RepositoryWorkspaceProjection) => {
      const artifactPaths = getAvailableArtifactPaths(nextWorkspace.artifactInventory)
      reconcileSelectedArtifactNavigation(repositoryId, artifactPaths)
    },
    [reconcileSelectedArtifactNavigation],
  )

  const loadWorkspace = useCallback(async (repositoryId: string) => {
    const nextWorkspace = await loadWorkspaceProjection()
    if (nextWorkspace && nextWorkspace.repository.id === repositoryId) {
      setExecutionContext(null)
      reconcileSelectedArtifact(repositoryId, nextWorkspace)
    }
  }, [loadWorkspaceProjection, reconcileSelectedArtifact, setExecutionContext])

  const loadCommitPreparation = useCallback(async (sessionId: string) => {
    setIsCommitPreparationLoading(true)
    try {
      const preparation = await prepareCommit(sessionId)
      setCommitPreparation(preparation)
      setCommitMessage(preparation.proposedMessage)
      setSelectedCommitPaths(
        new Set(
          preparation.scopeItems
            .filter((item) => item.isSelected)
            .map((item) => item.path),
        ),
      )
    } catch (prepareError) {
      setCommitPreparation(null)
      setSelectedCommitPaths(new Set())
      setCommitMessage('')
      setError(formatError(prepareError))
    } finally {
      setIsCommitPreparationLoading(false)
    }
  }, [])

  function setCommitPathSelection(path: string, isSelected: boolean) {
    setSelectedCommitPaths((currentPaths) => {
      const nextPaths = new Set(currentPaths)
      if (isSelected) {
        nextPaths.add(path)
      } else {
        nextPaths.delete(path)
      }

      return nextPaths
    })
  }

  function selectAllCommitPaths() {
    setSelectedCommitPaths(new Set(commitPreparation?.scopeItems.map((item) => item.path) ?? []))
  }

  function selectNoCommitPaths() {
    setSelectedCommitPaths(new Set())
  }

  async function commitPreparedScope() {
    if (!executionSessionId || !commitPreparation || !canCommitPreparedScope) {
      return
    }

    setIsCommitting(true)
    setError(null)
    setMessage(null)
    try {
      const summary = await commitExecution(
        executionSessionId,
        commitMessage.trim(),
        selectedCommitScopeItems.map((item) => item.path),
        commitPreparation.statusSnapshot.id,
      )
      setCommitPreparation(null)
      setSelectedCommitPaths(new Set())
      setCommitMessage('')
      setMessage(
        summary.commitSha
          ? `Committed ${summary.commitSha}. Push review is ready.`
          : 'Commit completed. Push review is ready.',
      )
      await loadRepositories()
      if (selectedRepository) {
        await loadWorkspace(selectedRepository.repository.id)
      }
    } catch (commitError) {
      setError(formatError(commitError))
    } finally {
      setIsCommitting(false)
    }
  }

  async function pushExecution() {
    if (!executionSessionId || !canPushExecution) {
      return
    }

    setIsPushing(true)
    setError(null)
    setMessage(null)
    try {
      const summary = await pushExecutionCommand(executionSessionId)
      setMessage(
        summary.pushedCommitSha
          ? `Pushed ${summary.pushedCommitSha}. Repository is ready.`
          : 'Push completed. Repository is ready.',
      )
      await loadRepositories()
      if (selectedRepository) {
        const nextWorkspace = await refreshRepositoryWorkspace(selectedRepository.repository.id)
        setWorkspace(nextWorkspace)
        setExecutionContext(null)
        reconcileSelectedArtifact(selectedRepository.repository.id, nextWorkspace)
        await refreshGitStatus()
      }
    } catch (pushError) {
      setError(formatError(pushError))
    } finally {
      setIsPushing(false)
    }
  }

  async function addRepository() {
    setIsAdding(true)
    setError(null)
    setMessage(null)
    try {
      const selectedPath = await selectRepositoryDirectory()

      if (!selectedPath) {
        return
      }

      await registerRepository(selectedPath)
      setMessage('Repository registered.')
      await loadRepositories()
    } catch (addError) {
      setError(formatError(addError))
    } finally {
      setIsAdding(false)
    }
  }

  async function removeRepository(repository: Repository) {
    const confirmed = window.confirm(
      `Remove ${repository.name} from Command Center?\n\nRepository files will not be deleted.`,
    )

    if (!confirmed) {
      return
    }

    setRemovingRepositoryId(repository.id)
    setError(null)
    setMessage(null)
    try {
      await removeRepositoryRegistration(repository.id)
      setMessage('Repository registration removed.')
      clearRepositoryNavigation(repository.id)
      setWorkspace(null)
      setExecutionContext(null)
      await loadRepositories()
    } catch (removeError) {
      setError(formatError(removeError))
    } finally {
      setRemovingRepositoryId(null)
    }
  }

  async function refreshWorkspace() {
    if (!selectedRepository) {
      return
    }

    setError(null)
    setMessage(null)
    try {
      const nextWorkspace = await refreshWorkspaceProjection()
      if (nextWorkspace) {
        setExecutionContext(null)
        reconcileSelectedArtifact(selectedRepository.repository.id, nextWorkspace)
        setMessage('Workspace refreshed.')
        await loadRepositories()
      }
    } catch (refreshError) {
      setError(formatError(refreshError))
    }
  }

  async function generateOperationalContextProposal() {
    if (!selectedRepository) {
      return
    }

    setIsOperationalContextProposalLoading(true)
    setError(null)
    setMessage(null)
    try {
      const proposal = await generateOperationalContextProposalCommand(
        selectedRepository.repository.id,
      )
      await setLoadedOperationalContextProposal(proposal)
      const nextWorkspace = await refreshRepositoryWorkspace(selectedRepository.repository.id)
      setWorkspace(nextWorkspace)
      setExecutionContext(null)
      reconcileSelectedArtifact(selectedRepository.repository.id, nextWorkspace)
      setMessage('Operational-context proposal generated.')
      await loadRepositories()
    } catch (proposalError) {
      setError(formatError(proposalError))
    } finally {
      setIsOperationalContextProposalLoading(false)
    }
  }

  async function loadOperationalContextCurrentContent(repositoryId: string) {
    const currentContext = workspace?.artifactInventory.operationalContext
    if (!currentContext) {
      setOperationalContextCurrentContent('')
      return
    }

    const content = await loadArtifactContent(repositoryId, currentContext.relativePath)
    setOperationalContextCurrentContent(content)
  }

  async function setLoadedOperationalContextProposal(proposal: OperationalContextProposal) {
    setOperationalContextProposal(proposal)
    setOperationalContextProposalDraft(proposal.editedContent ?? proposal.generatedContent ?? '')
    setOperationalContextReviewNote(proposal.review.reviewNote ?? '')
    if (selectedRepository) {
      await loadOperationalContextCurrentContent(selectedRepository.repository.id)
    }
  }

  async function loadLatestOperationalContextProposal() {
    if (!selectedRepository || !workspace?.operationalContextProposalSummary.latestProposalId) {
      return
    }

    setIsOperationalContextProposalLoading(true)
    setError(null)
    setMessage(null)
    try {
      const proposal = await getOperationalContextProposal(
        selectedRepository.repository.id,
        workspace.operationalContextProposalSummary.latestProposalId,
      )
      await setLoadedOperationalContextProposal(proposal)
      setMessage('Operational-context proposal loaded.')
    } catch (proposalError) {
      setError(formatError(proposalError))
    } finally {
      setIsOperationalContextProposalLoading(false)
    }
  }

  async function saveOperationalContextProposalEdit() {
    if (!selectedRepository || !operationalContextProposal) {
      return
    }

    setIsOperationalContextProposalSaving(true)
    setError(null)
    setMessage(null)
    try {
      const proposal = await editOperationalContextProposal(
        selectedRepository.repository.id,
        operationalContextProposal.proposalId,
        operationalContextProposalDraft,
      )
      await setLoadedOperationalContextProposal(proposal)
      const nextWorkspace = await refreshRepositoryWorkspace(selectedRepository.repository.id)
      setWorkspace(nextWorkspace)
      setMessage('Operational-context proposal edits saved.')
      await loadRepositories()
    } catch (proposalError) {
      setError(formatError(proposalError))
    } finally {
      setIsOperationalContextProposalSaving(false)
    }
  }

  async function acceptOperationalContextProposal() {
    if (!selectedRepository || !operationalContextProposal) {
      return
    }

    setIsOperationalContextProposalSaving(true)
    setError(null)
    setMessage(null)
    try {
      const proposal = await acceptOperationalContextProposalCommand(
        selectedRepository.repository.id,
        operationalContextProposal.proposalId,
        operationalContextReviewNote || null,
      )
      await setLoadedOperationalContextProposal(proposal)
      const nextWorkspace = await refreshRepositoryWorkspace(selectedRepository.repository.id)
      setWorkspace(nextWorkspace)
      setMessage('Operational-context proposal accepted for later promotion.')
      await loadRepositories()
    } catch (proposalError) {
      setError(formatError(proposalError))
    } finally {
      setIsOperationalContextProposalSaving(false)
    }
  }

  async function rejectOperationalContextProposal() {
    if (!selectedRepository || !operationalContextProposal) {
      return
    }

    setIsOperationalContextProposalSaving(true)
    setError(null)
    setMessage(null)
    try {
      const proposal = await rejectOperationalContextProposalCommand(
        selectedRepository.repository.id,
        operationalContextProposal.proposalId,
        operationalContextReviewNote || null,
      )
      await setLoadedOperationalContextProposal(proposal)
      const nextWorkspace = await refreshRepositoryWorkspace(selectedRepository.repository.id)
      setWorkspace(nextWorkspace)
      setMessage('Operational-context proposal rejected.')
      await loadRepositories()
    } catch (proposalError) {
      setError(formatError(proposalError))
    } finally {
      setIsOperationalContextProposalSaving(false)
    }
  }

  async function promoteOperationalContextProposal() {
    if (!selectedRepository || !operationalContextProposal) {
      return
    }

    setIsOperationalContextProposalSaving(true)
    setError(null)
    setMessage(null)
    try {
      const proposal = await promoteOperationalContextProposalCommand(
        selectedRepository.repository.id,
        operationalContextProposal.proposalId,
      )
      await setLoadedOperationalContextProposal(proposal)
      const nextWorkspace = await refreshRepositoryWorkspace(selectedRepository.repository.id)
      setWorkspace(nextWorkspace)
      reconcileSelectedArtifact(selectedRepository.repository.id, nextWorkspace)
      if (nextWorkspace.artifactInventory.operationalContext) {
        const content = await loadArtifactContent(
          selectedRepository.repository.id,
          nextWorkspace.artifactInventory.operationalContext.relativePath,
        )
        setOperationalContextCurrentContent(content)
      }
      setMessage('Operational-context proposal promoted.')
      await loadRepositories()
    } catch (proposalError) {
      setError(formatError(proposalError))
    } finally {
      setIsOperationalContextProposalSaving(false)
    }
  }

  async function generateContinuityReport() {
    if (!selectedRepository) {
      return
    }

    setIsContinuityReportGenerating(true)
    setError(null)
    setMessage(null)
    try {
      const report = await generateContinuityReportCommand(selectedRepository.repository.id)
      setContinuityDiagnostics(report.diagnostics)
      setMessage(`Continuity report generated: ${report.relativePath}`)
    } catch (reportError) {
      setError(formatError(reportError))
    } finally {
      setIsContinuityReportGenerating(false)
    }
  }

  async function saveArtifact() {
    if (!selectedRepository || !selectedArtifact) {
      return
    }

    setIsSaving(true)
    setError(null)
    setMessage(null)
    try {
      await saveArtifactContent(
        selectedRepository.repository.id,
        selectedArtifact.relativePath,
        draftContent,
      )
      setArtifactContent(draftContent)
      setMessage('Artifact saved.')
      await loadWorkspace(selectedRepository.repository.id)
      await loadRepositories()
    } catch (saveError) {
      setError(formatError(saveError))
    } finally {
      setIsSaving(false)
    }
  }

  async function rotateSelectedArtifact() {
    if (!selectedRepository || !selectedArtifact || !canRotateSelectedArtifact) {
      return
    }

    const artifactLabel = selectedArtifact.family === 'Handoff' ? 'current handoff' : 'current decisions'
    const confirmed = window.confirm(`Rotate ${artifactLabel}?`)

    if (!confirmed) {
      return
    }

    setIsRotating(true)
    setError(null)
    setMessage(null)
    try {
      const nextWorkspace =
        selectedArtifact.family === 'Handoff'
          ? await rotateCurrentHandoff(selectedRepository.repository.id)
          : await rotateCurrentDecisions(selectedRepository.repository.id)
      setWorkspace(nextWorkspace)
      setExecutionContext(null)
      reconcileSelectedArtifact(selectedRepository.repository.id, nextWorkspace)
      setMessage('Artifact rotated.')
      await loadRepositories()
    } catch (rotateError) {
      setError(formatError(rotateError))
    } finally {
      setIsRotating(false)
    }
  }

  async function buildExecutionContext() {
    if (!selectedRepository || !selectedMilestonePath) {
      return
    }

    setError(null)
    setMessage(null)
    const context = await loadExecutionContextPreview()
    if (context) {
      setMessage('Execution context built.')
    }
  }

  async function startExecution() {
    if (!selectedRepository || !selectedMilestonePath || !canStartExecution) {
      return
    }

    setIsStartingExecution(true)
    setError(null)
    setMessage(null)
    try {
      const session = await startExecutionCommand(
        selectedRepository.repository.id,
        selectedMilestonePath,
      )
      setWorkspace((currentWorkspace) =>
        currentWorkspace && currentWorkspace.repository.id === selectedRepository.repository.id
          ? {
              ...currentWorkspace,
              executionState: session.repositoryState,
              executionSummary: session,
            }
          : currentWorkspace,
      )
      setMessage(
        session.state === 'Failed'
          ? `Execution failed: ${session.sessionId}.`
          : `Execution started: ${session.sessionId}.`,
      )
      await loadRepositories()
      await loadWorkspace(selectedRepository.repository.id)
    } catch (startError) {
      setError(formatError(startError))
    } finally {
      setIsStartingExecution(false)
    }
  }

  async function refreshAfterHandoffDecision(summary: ExecutionSessionSummary, successMessage: string) {
    if (!selectedRepository) {
      return
    }

    setExecutionSessionStatus((currentStatus) =>
      currentStatus?.sessionId === summary.sessionId
        ? {
            ...currentStatus,
            state: summary.state,
            repositoryState: summary.repositoryState,
            completedAt: summary.completedAt,
            duration: summary.duration,
            acceptedAt: summary.acceptedAt,
            rejectedAt: summary.rejectedAt,
            decisionNote: summary.decisionNote,
            lastActivityAt: summary.lastActivityAt,
            handoffPath: summary.handoffPath,
            failureReason: summary.failureReason,
          }
        : currentStatus,
    )
    setMessage(successMessage)
    await loadRepositories()
    await loadWorkspace(selectedRepository.repository.id)
  }

  async function acceptGeneratedHandoff() {
    if (!executionDisplay || !isHandoffDecisionPending) {
      return
    }

    setIsAcceptingHandoff(true)
    setError(null)
    setMessage(null)
    try {
      const summary = await acceptExecutionHandoff(executionDisplay.sessionId)
      await refreshAfterHandoffDecision(
        summary,
        summary.repositoryState === 'AwaitingCommit'
          ? 'Handoff accepted. Commit review is ready.'
          : 'Handoff accepted. Repository is ready.',
      )
    } catch (acceptError) {
      setError(formatError(acceptError))
    } finally {
      setIsAcceptingHandoff(false)
    }
  }

  async function rejectGeneratedHandoff() {
    if (!executionDisplay || !isHandoffDecisionPending) {
      return
    }

    const confirmed = window.confirm(
      'Reject this execution handoff?\n\nThe handoff and session metadata will remain available for audit.',
    )

    if (!confirmed) {
      return
    }

    setIsRejectingHandoff(true)
    setError(null)
    setMessage(null)
    try {
      const summary = await rejectExecutionHandoff(executionDisplay.sessionId)
      await refreshAfterHandoffDecision(summary, 'Handoff rejected. Repository is ready.')
    } catch (rejectError) {
      setError(formatError(rejectError))
    } finally {
      setIsRejectingHandoff(false)
    }
  }

  useEffect(() => {
    if (!executionSessionId || streamedExecutionEvents.length === 0) {
      return
    }

    void refreshExecutionSessionStatus({ silent: true })
  }, [executionSessionId, refreshExecutionSessionStatus, streamedExecutionEvents])

  useEffect(() => {
    if (!selectedExecutionStatus || !executionSummary) {
      return
    }

    setWorkspace((currentWorkspace) =>
      currentWorkspace && currentWorkspace.executionSummary?.sessionId === selectedExecutionStatus.sessionId
        ? {
            ...currentWorkspace,
            executionState: selectedExecutionStatus.repositoryState,
            executionSummary: {
              ...currentWorkspace.executionSummary,
              state: selectedExecutionStatus.state,
              repositoryState: selectedExecutionStatus.repositoryState,
              completedAt: selectedExecutionStatus.completedAt,
              duration: selectedExecutionStatus.duration,
              acceptedAt: selectedExecutionStatus.acceptedAt,
              rejectedAt: selectedExecutionStatus.rejectedAt,
              decisionNote: selectedExecutionStatus.decisionNote,
              lastActivityAt: selectedExecutionStatus.lastActivityAt,
              providerName: selectedExecutionStatus.providerName,
              providerExecutablePath: selectedExecutionStatus.providerExecutablePath,
              providerProcessId: selectedExecutionStatus.providerProcessId,
              providerStartedAt: selectedExecutionStatus.providerStartedAt,
              handoffPath: selectedExecutionStatus.handoffPath,
              failureReason: selectedExecutionStatus.failureReason,
            },
          }
        : currentWorkspace,
    )
    setRepositories((currentRepositories) =>
      currentRepositories.map((entry) => {
        const summary = entry.executionSummary ?? entry.activeExecutionSession
        if (summary?.sessionId !== selectedExecutionStatus.sessionId) {
          return entry
        }

        const nextSummary = {
          ...summary,
          state: selectedExecutionStatus.state,
          repositoryState: selectedExecutionStatus.repositoryState,
          completedAt: selectedExecutionStatus.completedAt,
          duration: selectedExecutionStatus.duration,
          acceptedAt: selectedExecutionStatus.acceptedAt,
          rejectedAt: selectedExecutionStatus.rejectedAt,
          decisionNote: selectedExecutionStatus.decisionNote,
          lastActivityAt: selectedExecutionStatus.lastActivityAt,
          providerName: selectedExecutionStatus.providerName,
          providerExecutablePath: selectedExecutionStatus.providerExecutablePath,
          providerProcessId: selectedExecutionStatus.providerProcessId,
          providerStartedAt: selectedExecutionStatus.providerStartedAt,
          handoffPath: selectedExecutionStatus.handoffPath,
          failureReason: selectedExecutionStatus.failureReason,
        }

        return {
          ...entry,
          executionState: selectedExecutionStatus.repositoryState,
          activeExecutionSession:
            selectedExecutionStatus.repositoryState === 'Executing' ? nextSummary : null,
          executionSummary: nextSummary,
        }
      }),
    )
  }, [executionSummary, selectedExecutionStatus, setRepositories, setWorkspace])

  useEffect(() => {
    if (
      !selectedRepository ||
      !selectedExecutionStatus ||
      selectedExecutionStatus.repositoryState === 'Executing' ||
      refreshedCompletedSessionIds.current.has(selectedExecutionStatus.sessionId)
    ) {
      return
    }

    refreshedCompletedSessionIds.current.add(selectedExecutionStatus.sessionId)
    void loadRepositories()
    void loadWorkspace(selectedRepository.repository.id)
  }, [loadRepositories, loadWorkspace, selectedExecutionStatus, selectedRepository])

  useEffect(() => {
    reconcileRepositorySelection(repositories.map((entry) => entry.repository.id))
  }, [reconcileRepositorySelection, repositories])

  useEffect(() => {
    if (repositoriesError) {
      setError(repositoriesError)
    }
  }, [repositoriesError])

  useEffect(() => {
    if (workspaceError) {
      if (selectedRepositoryId) {
        selectArtifactNavigation(selectedRepositoryId, null)
        selectMilestone(selectedRepositoryId, null)
      }
      setExecutionContext(null)
      setError(workspaceError)
    }
  }, [
    selectArtifactNavigation,
    selectMilestone,
    selectedRepositoryId,
    setExecutionContext,
    workspaceError,
  ])

  useEffect(() => {
    if (artifactError) {
      setError(artifactError)
    }
  }, [artifactError])

  useEffect(() => {
    if (executionContextError) {
      setError(executionContextError)
    }
  }, [executionContextError])

  useEffect(() => {
    if (executionSessionError) {
      setError(executionSessionError)
    }
  }, [executionSessionError])

  useEffect(() => {
    if (executionEventsError) {
      setError(executionEventsError)
    }
  }, [executionEventsError])

  useEffect(() => {
    if (gitStatusError) {
      setError(gitStatusError)
    }
  }, [gitStatusError])

  useEffect(() => {
    if (continuityDiagnosticsError) {
      setError(continuityDiagnosticsError)
    }
  }, [continuityDiagnosticsError])

  useEffect(() => {
    if (!selectedRepository || !workspace) {
      return
    }

    if (workspace.repository.id === selectedRepository.repository.id) {
      reconcileSelectedArtifact(selectedRepository.repository.id, workspace)
    }
  }, [reconcileSelectedArtifact, selectedRepository, workspace])

  useEffect(() => {
    if (!selectedRepository || !shouldShowGitWorkflow) {
      setCommitPreparation(null)
      setSelectedCommitPaths(new Set())
      setCommitMessage('')
      return
    }

    const timeoutId = window.setTimeout(() => {
      if (currentExecutionState === 'AwaitingCommit' && executionSessionId) {
        void loadCommitPreparation(executionSessionId)
        return
      }

      setCommitPreparation(null)
      setSelectedCommitPaths(new Set())
      setCommitMessage('')
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [
    currentExecutionState,
    executionSessionId,
    loadCommitPreparation,
    selectedRepository,
    shouldShowGitWorkflow,
  ])

  useEffect(() => {
    setDraftContent(artifactContent)
  }, [artifactContent])

  useEffect(() => {
    if (!selectedRepository || !canReviewGeneratedHandoff || !executionDisplay?.handoffPath) {
      setGeneratedHandoffContent('')
      setIsGeneratedHandoffLoading(false)
      return
    }

    let isCurrent = true
    setIsGeneratedHandoffLoading(true)
    setError(null)
    loadArtifactContent(selectedRepository.repository.id, executionDisplay.handoffPath)
      .then((content) => {
        if (isCurrent) {
          setGeneratedHandoffContent(content)
        }
      })
      .catch((loadError) => {
        if (isCurrent) {
          setError(formatError(loadError))
        }
      })
      .finally(() => {
        if (isCurrent) {
          setIsGeneratedHandoffLoading(false)
        }
      })

    return () => {
      isCurrent = false
    }
  }, [canReviewGeneratedHandoff, executionDisplay?.handoffPath, selectedRepository])

  useEffect(() => {
    if (!workspace) {
      if (selectedRepositoryId) {
        selectMilestone(selectedRepositoryId, null)
      }
      return
    }

    const milestones = workspace.artifactInventory.milestones
    reconcileSelectedMilestone(
      workspace.repository.id,
      milestones.map((milestone) => milestone.relativePath),
    )
    setExecutionContext(null)
  }, [reconcileSelectedMilestone, selectMilestone, selectedRepositoryId, setExecutionContext, workspace])

  return (
    <main className="app-shell">
      <header className="app-header">
        <div>
          <p className="eyebrow">Command Center</p>
          <h1>Repositories</h1>
        </div>
        <div className="header-actions">
          <button type="button" className="secondary-action" onClick={loadRepositories}>
            Refresh
          </button>
          <button
            type="button"
            className="primary-action"
            onClick={addRepository}
            disabled={isAdding}
          >
            {isAdding ? 'Adding...' : 'Add Repository'}
          </button>
        </div>
      </header>

      {error ? <div className="notice error">{error}</div> : null}
      {message ? <div className="notice success">{message}</div> : null}

      <section className="workspace-grid" aria-label="Repository workspace">
        <section className="repository-list" aria-label="Registered repositories">
          <div className="section-heading">
            <h2>Dashboard</h2>
            <span>{repositories.length} registered</span>
          </div>

          {isLoading ? (
            <p className="empty-state">Loading repositories...</p>
          ) : repositories.length === 0 ? (
            <p className="empty-state">No repositories registered.</p>
          ) : (
            <div className="repository-items">
              {repositories.map((entry) => {
                const isSelected = entry.repository.id === selectedRepository?.repository.id

                return (
                  <button
                    type="button"
                    key={entry.repository.id}
                    className={`repository-item${isSelected ? ' selected' : ''}`}
                    onClick={() => selectRepository(entry.repository.id)}
                  >
                    <span className="repository-name">{entry.repository.name}</span>
                    <span className="repository-path">{entry.repository.path}</span>
                    <span
                      className={`availability availability-${entry.availability.toLowerCase()}`}
                    >
                      {availabilityLabels[entry.availability]}
                    </span>
                    <span className={`readiness readiness-${entry.readiness.toLowerCase()}`}>
                      {readinessLabels[entry.readiness]}
                    </span>
                    <span className={`execution-state execution-state-${entry.executionState.toLowerCase()}`}>
                      {executionStateLabels[entry.executionState]}
                    </span>
                    <span className="repository-metadata">
                      {entry.milestoneCount} milestones
                    </span>
                    {entry.executionSummary ? (
                      <>
                        <span className="repository-metadata">
                          Session {entry.executionSummary.sessionId}
                        </span>
                        <span className="repository-metadata">
                          State {entry.executionSummary.state}
                        </span>
                        {entry.executionSummary.lastActivityAt ? (
                          <span className="repository-metadata">
                            Activity {formatDateTime(entry.executionSummary.lastActivityAt)}
                          </span>
                        ) : null}
                        {entry.executionSummary.failureReason ? (
                          <span className="repository-metadata failure-metadata">
                            Failure {entry.executionSummary.failureReason}
                          </span>
                        ) : null}
                      </>
                    ) : null}
                    <span className="repository-metadata">
                      Handoff {entry.hasCurrentHandoff ? 'present' : 'missing'}
                    </span>
                    <span className="repository-metadata">
                      Decisions {entry.hasCurrentDecisions ? 'present' : 'missing'}
                    </span>
                    <span className="repository-metadata">
                      Context {entry.continuitySummary.operationalContextExists ? 'present' : 'missing'}
                    </span>
                    <span className="repository-metadata">
                      Updated {formatDateTime(entry.continuitySummary.operationalContextLastUpdatedAt)}
                    </span>
                    <span className="repository-metadata">
                      Revisions {entry.continuitySummary.operationalContextRevisionCount}
                    </span>
                    <span className="repository-metadata">
                      Questions {entry.continuitySummary.openQuestionCount}
                    </span>
                    <span className="repository-metadata">
                      Risks {entry.continuitySummary.activeRiskCount}
                    </span>
                  </button>
                )
              })}
            </div>
          )}
        </section>

        <section className="repository-details" aria-label="Repository details">
          <div className="section-heading">
            <h2>Workspace</h2>
            <div className="section-actions">
              <button
                type="button"
                className="secondary-action"
                onClick={() => void refreshWorkspace()}
                disabled={!selectedRepository || isWorkspaceLoading}
              >
                {isWorkspaceLoading ? 'Refreshing...' : 'Refresh Workspace'}
              </button>
            </div>
          </div>

          {selectedRepository ? (
            <div className="details-body">
              <div className="details-title-row">
                <div>
                  <p className="eyebrow">Selected repository</p>
                  <h3>{selectedRepository.repository.name}</h3>
                </div>
                <span
                  className={`availability availability-${selectedRepository.availability.toLowerCase()}`}
                >
                  {availabilityLabels[selectedRepository.availability]}
                </span>
              </div>

              <dl className="details-list">
                <div>
                  <dt>Path</dt>
                  <dd>{selectedRepository.repository.path}</dd>
                </div>
                <div>
                  <dt>Readiness</dt>
                  <dd>{readinessLabels[workspace?.readiness ?? selectedRepository.readiness]}</dd>
                </div>
                <div>
                  <dt>Execution</dt>
                  <dd>
                    {executionStateLabels[currentExecutionState]}
                  </dd>
                </div>
                {executionDisplay ? (
                  <>
                    <div>
                      <dt>Session</dt>
                      <dd>{executionDisplay.sessionId}</dd>
                    </div>
                    <div>
                      <dt>Provider</dt>
                      <dd>{executionDisplay.providerName || 'Unknown'}</dd>
                    </div>
                    <div>
                      <dt>Started</dt>
                      <dd>{formatDateTime(executionDisplay.startedAt)}</dd>
                    </div>
                    <div>
                      <dt>Last activity</dt>
                      <dd>{formatDateTime(executionDisplay.lastActivityAt)}</dd>
                    </div>
                    <div>
                      <dt>Duration</dt>
                      <dd>{formatDuration(executionDisplay.duration)}</dd>
                    </div>
                    <div>
                      <dt>Accepted</dt>
                      <dd>{formatDateTime(executionDisplay.acceptedAt)}</dd>
                    </div>
                    <div>
                      <dt>Rejected</dt>
                      <dd>{formatDateTime(executionDisplay.rejectedAt)}</dd>
                    </div>
                    <div>
                      <dt>Decision note</dt>
                      <dd>{executionDisplay.decisionNote || 'Not recorded'}</dd>
                    </div>
                    <div>
                      <dt>PID</dt>
                      <dd>{executionDisplay.providerProcessId ?? 'Not recorded'}</dd>
                    </div>
                    <div>
                      <dt>Executable</dt>
                      <dd>{executionDisplay.providerExecutablePath || 'Not recorded'}</dd>
                    </div>
                    <div>
                      <dt>Failure</dt>
                      <dd>{executionDisplay.failureReason || 'None'}</dd>
                    </div>
                    <div>
                      <dt>Handoff</dt>
                      <dd>{executionDisplay.handoffPath || 'Not recorded'}</dd>
                    </div>
                  </>
                ) : null}
                <div>
                  <dt>Milestones</dt>
                  <dd>{workspace?.milestoneCount ?? selectedRepository.milestoneCount}</dd>
                </div>
              </dl>

              <div className="summary-grid">
                <span>Plan: {workspace?.hasPlan ? 'Present' : 'Missing'}</span>
                <span>
                  Operational context:{' '}
                  {workspace?.hasOperationalContext ? 'Present' : 'Missing'}
                </span>
                <span>Handoff: {workspace?.hasCurrentHandoff ? 'Present' : 'Missing'}</span>
                <span>Decisions: {workspace?.hasCurrentDecisions ? 'Present' : 'Missing'}</span>
              </div>

              <section className="execution-context-panel" aria-label="Current understanding">
                <div className="context-toolbar">
                  <div>
                    <p className="eyebrow">Operational Context</p>
                    <h4>Current Understanding</h4>
                  </div>
                </div>

                {workspace?.operationalContext.exists ? (
                  <div className="context-artifact-previews">
                    <div className="context-summary">
                      <span>Path: {workspace.operationalContext.currentRelativePath}</span>
                      <span>Execution context: {operationalContextExecutionStatus}</span>
                      <span>Revisions: {workspace.operationalContext.revisionCount}</span>
                      <span>
                        Current revision: {workspace.operationalContext.currentRevisionNumber}
                      </span>
                      <span>
                        Updated: {formatDateTime(workspace.operationalContext.lastUpdatedAt)}
                      </span>
                      <span>
                        Last promoted:{' '}
                        {formatDateTime(workspace.operationalContext.lastPromotionAt)}
                      </span>
                      <span>Questions: {workspace.operationalContext.openQuestions.length}</span>
                      <span>Risks: {workspace.operationalContext.activeRisks.length}</span>
                      <span>
                        Review: {operationalContextReviewStatus}
                      </span>
                      <span>
                        Proposal:{' '}
                        {workspace.operationalContextProposalSummary.latestProposalId
                          ? workspace.operationalContextProposalSummary.status ?? 'Unknown'
                          : 'None'}
                      </span>
                    </div>

                    <div className="context-columns">
                      <div>
                        <h5>Current Model</h5>
                        {workspace.operationalContext.currentUnderstandingSummary.length > 0 ? (
                          <ul>
                            {workspace.operationalContext.currentUnderstandingSummary.map((item) => (
                              <li key={item}>{item}</li>
                            ))}
                          </ul>
                        ) : (
                          <p>No current model items recorded.</p>
                        )}
                      </div>
                      <div>
                        <h5>Stable Decisions</h5>
                        {workspace.operationalContext.stableDecisions.length > 0 ? (
                          <ul>
                            {workspace.operationalContext.stableDecisions.map((item) => (
                              <li key={item.id}>{item.text}</li>
                            ))}
                          </ul>
                        ) : (
                          <p>No stable decisions recorded.</p>
                        )}
                      </div>
                      <div>
                        <h5>Decision Rationale</h5>
                        {workspace.operationalContext.decisionRationale.length > 0 ? (
                          <ul>
                            {workspace.operationalContext.decisionRationale.map((item) => (
                              <li key={item.id}>{item.text}</li>
                            ))}
                          </ul>
                        ) : (
                          <p>No decision rationale recorded.</p>
                        )}
                      </div>
                      <div>
                        <h5>Architecture</h5>
                        {workspace.operationalContext.architecture.length > 0 ? (
                          <ul>
                            {workspace.operationalContext.architecture.map((item) => (
                              <li key={item.id}>{item.text}</li>
                            ))}
                          </ul>
                        ) : (
                          <p>No architecture items recorded.</p>
                        )}
                      </div>
                      <div>
                        <h5>Authority Boundaries</h5>
                        {workspace.operationalContext.authorityBoundaries.length > 0 ? (
                          <ul>
                            {workspace.operationalContext.authorityBoundaries.map((item) => (
                              <li key={item.id}>{item.text}</li>
                            ))}
                          </ul>
                        ) : (
                          <p>No authority boundaries recorded.</p>
                        )}
                      </div>
                      <div>
                        <h5>Constraints</h5>
                        {workspace.operationalContext.constraints.length > 0 ? (
                          <ul>
                            {workspace.operationalContext.constraints.map((item) => (
                              <li key={item.id}>{item.text}</li>
                            ))}
                          </ul>
                        ) : (
                          <p>No constraints recorded.</p>
                        )}
                      </div>
                      <div>
                        <h5>Open Questions</h5>
                        {workspace.operationalContext.openQuestions.length > 0 ? (
                          <ul>
                            {workspace.operationalContext.openQuestions.map((item) => (
                              <li key={item.id}>{item.text}</li>
                            ))}
                          </ul>
                        ) : (
                          <p>No open questions recorded.</p>
                        )}
                      </div>
                      <div>
                        <h5>Active Risks</h5>
                        {workspace.operationalContext.activeRisks.length > 0 ? (
                          <ul>
                            {workspace.operationalContext.activeRisks.map((item) => (
                              <li key={item.id}>{item.text}</li>
                            ))}
                          </ul>
                        ) : (
                          <p>No active risks recorded.</p>
                        )}
                      </div>
                      <div>
                        <h5>Recent Changes</h5>
                        {workspace.operationalContext.recentUnderstandingChanges.length > 0 ? (
                          <ul>
                            {workspace.operationalContext.recentUnderstandingChanges.map((item) => (
                              <li key={item.id}>{item.text}</li>
                            ))}
                          </ul>
                        ) : (
                          <p>No recent understanding changes recorded.</p>
                        )}
                      </div>
                      <div>
                        <h5>Continuity Warnings</h5>
                        {workspace.operationalContext.continuityWarnings.length > 0 ? (
                          <ul>
                            {workspace.operationalContext.continuityWarnings.map((warning) => (
                              <li key={warning}>{warning}</li>
                            ))}
                          </ul>
                        ) : (
                          <p>No continuity warnings recorded.</p>
                        )}
                      </div>
                    </div>
                  </div>
                ) : (
                  <div className="context-artifact-previews">
                    <div className="context-summary">
                      <span>Execution context: {operationalContextExecutionStatus}</span>
                      <span>Review: {operationalContextReviewStatus}</span>
                      <span>
                        Proposal:{' '}
                        {workspace?.operationalContextProposalSummary.latestProposalId
                          ? workspace.operationalContextProposalSummary.status ?? 'Unknown'
                          : 'None'}
                      </span>
                    </div>
                    <p className="empty-state">No current operational context exists.</p>
                  </div>
                )}
              </section>

              <section className="execution-context-panel" aria-label="Continuity diagnostics">
                <div className="context-toolbar">
                  <div>
                    <p className="eyebrow">Continuity</p>
                    <h4>Diagnostics</h4>
                  </div>
                  <div className="context-controls">
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={() => void refreshContinuityDiagnostics()}
                      disabled={!selectedRepository || isContinuityDiagnosticsLoading}
                    >
                      {isContinuityDiagnosticsLoading ? 'Loading...' : 'Refresh Diagnostics'}
                    </button>
                    <button
                      type="button"
                      onClick={() => void generateContinuityReport()}
                      disabled={!selectedRepository || isContinuityReportGenerating}
                    >
                      {isContinuityReportGenerating ? 'Generating...' : 'Generate Report'}
                    </button>
                  </div>
                </div>

                {continuityDiagnostics ? (
                  <div className="context-artifact-previews">
                    <div className="context-summary">
                      <span>Revisions: {continuityDiagnostics.revisionCount}</span>
                      <span>Current size: {continuityDiagnostics.currentContextByteCount} bytes</span>
                      <span>Growth: {continuityDiagnostics.contextByteGrowth} bytes</span>
                      <span>
                        Average:{' '}
                        {Math.round(continuityDiagnostics.averageBytesPerRevision)} bytes/revision
                      </span>
                      <span>
                        Questions resolved: {continuityDiagnostics.openQuestionTrend.resolvedCount}
                      </span>
                      <span>
                        Questions lost: {continuityDiagnostics.openQuestionTrend.lostCount}
                      </span>
                      <span>
                        Risks retired: {continuityDiagnostics.activeRiskTrend.resolvedCount}
                      </span>
                      <span>
                        Risks lost: {continuityDiagnostics.activeRiskTrend.lostCount}
                      </span>
                      <span>Decisions lost: {continuityDiagnostics.decisionTrend.lostCount}</span>
                      <span>Rationale lost: {continuityDiagnostics.rationaleTrend.lostCount}</span>
                    </div>

                    <div className="context-columns">
                      <div>
                        <h5>Preservation</h5>
                        <ul>
                          <li>Architecture lost: {continuityDiagnostics.architectureTrend.lostCount}</li>
                          <li>Constraints lost: {continuityDiagnostics.constraintTrend.lostCount}</li>
                          <li>Decisions added: {continuityDiagnostics.decisionTrend.addedCount}</li>
                          <li>Decisions removed: {continuityDiagnostics.decisionTrend.removedCount}</li>
                        </ul>
                      </div>
                      <div>
                        <h5>Compression</h5>
                        <ul>
                          <li>Proposals observed: {continuityDiagnostics.compressionTrend.proposalCount}</li>
                          <li>Items compressed: {continuityDiagnostics.compressionTrend.compressedItemCount}</li>
                          <li>Items removed: {continuityDiagnostics.compressionTrend.removedItemCount}</li>
                          <li>Warnings: {continuityDiagnostics.compressionTrend.warningCount}</li>
                        </ul>
                      </div>
                      <div>
                        <h5>Repeated Signals</h5>
                        {continuityDiagnostics.repeatedInvestigationIndicators.length +
                          continuityDiagnostics.repeatedQuestionIndicators.length +
                          continuityDiagnostics.decisionReworkIndicators.length >
                        0 ? (
                          <ul>
                            {continuityDiagnostics.repeatedInvestigationIndicators.map((indicator) => (
                              <li key={`investigation-${indicator}`}>{indicator}</li>
                            ))}
                            {continuityDiagnostics.repeatedQuestionIndicators.map((indicator) => (
                              <li key={`question-${indicator}`}>{indicator}</li>
                            ))}
                            {continuityDiagnostics.decisionReworkIndicators.map((indicator) => (
                              <li key={`decision-${indicator}`}>{indicator}</li>
                            ))}
                          </ul>
                        ) : (
                          <p>No repeated indicators recorded.</p>
                        )}
                      </div>
                      <div>
                        <h5>Warnings</h5>
                        {continuityDiagnostics.continuityWarnings.length > 0 ? (
                          <ul>
                            {continuityDiagnostics.continuityWarnings.map((warning) => (
                              <li key={warning}>{warning}</li>
                            ))}
                          </ul>
                        ) : (
                          <p>No continuity warnings recorded.</p>
                        )}
                      </div>
                    </div>
                  </div>
                ) : (
                  <p className="empty-state">
                    {isContinuityDiagnosticsLoading
                      ? 'Loading continuity diagnostics...'
                      : 'No continuity diagnostics loaded.'}
                  </p>
                )}
              </section>

              <section className="execution-context-panel" aria-label="Operational context proposals">
                <div className="context-toolbar">
                  <div>
                    <p className="eyebrow">Operational Context</p>
                    <h4>Proposal Review</h4>
                  </div>
                  <div className="context-controls">
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={() => void loadLatestOperationalContextProposal()}
                      disabled={
                        !workspace?.operationalContextProposalSummary.latestProposalId ||
                        isOperationalContextProposalLoading
                      }
                    >
                      Load Latest
                    </button>
                    <button
                      type="button"
                      onClick={() => void generateOperationalContextProposal()}
                      disabled={!selectedRepository || isOperationalContextProposalLoading}
                    >
                      {isOperationalContextProposalLoading ? 'Working...' : 'Generate Proposal'}
                    </button>
                  </div>
                </div>

                {workspace?.operationalContextProposalSummary.latestProposalId ? (
                  <div className="context-summary-grid">
                    <span>
                      Latest: {workspace.operationalContextProposalSummary.latestProposalId}
                    </span>
                    <span>
                      Status: {workspace.operationalContextProposalSummary.status ?? 'Unknown'}
                    </span>
                    <span>
                      Generated:{' '}
                      {formatDateTime(workspace.operationalContextProposalSummary.generatedAt)}
                    </span>
                    <span>
                      Inputs: {workspace.operationalContextProposalSummary.sourceInputCount}
                    </span>
                    <span>
                      Size: {workspace.operationalContextProposalSummary.contentByteCount} bytes
                    </span>
                    <span>
                      Current revisions:{' '}
                      {workspace.operationalContext.revisionCount}
                    </span>
                    <span>
                      Last promoted:{' '}
                      {formatDateTime(workspace.operationalContextProposalSummary.lastPromotedAt)}
                    </span>
                    <span>
                      Archived prior:{' '}
                      {workspace.operationalContextProposalSummary.lastArchivedRelativePath ?? 'None'}
                    </span>
                  </div>
                ) : (
                  <p className="empty-state">No operational-context proposal has been generated.</p>
                )}

                {operationalContextProposal ? (
                  <div className="context-artifact-previews">
                    <div className="context-summary-grid">
                      <span>Proposal: {operationalContextProposal.proposalId}</span>
                      <span>Status: {operationalContextProposal.status}</span>
                      <span>Review: {operationalContextProposal.review.reviewState}</span>
                      <span>
                        Reviewed: {formatDateTime(operationalContextProposal.review.reviewedAt)}
                      </span>
                      <span>
                        Promoted: {formatDateTime(operationalContextProposal.promotion.promotedAt)}
                      </span>
                      <span>
                        Archived: {operationalContextProposal.promotion.archivedRelativePath ?? 'None'}
                      </span>
                    </div>
                    {operationalContextProposal.review.staleReason ? (
                      <p className="empty-state">
                        Review blocked: {operationalContextProposal.review.staleReason}
                      </p>
                    ) : null}
                    {operationalContextProposal.promotion.archiveFailureReason ? (
                      <p className="empty-state">
                        Promotion archive failed: {operationalContextProposal.promotion.archiveFailureReason}
                      </p>
                    ) : null}
                    {operationalContextProposal.promotion.writeFailureReason ? (
                      <p className="empty-state">
                        Promotion write failed: {operationalContextProposal.promotion.writeFailureReason}
                      </p>
                    ) : null}
                    <div className="proposal-review-toolbar">
                      <button
                        type="button"
                        className="secondary-action"
                        onClick={() => void saveOperationalContextProposalEdit()}
                        disabled={
                          isOperationalContextReviewBlocked ||
                          !hasOperationalContextProposalDraftChanges ||
                          isOperationalContextProposalSaving
                        }
                      >
                        {isOperationalContextProposalSaving ? 'Saving...' : 'Save Edits'}
                      </button>
                      <button
                        type="button"
                        className="primary-action"
                        onClick={() => void acceptOperationalContextProposal()}
                        disabled={isOperationalContextReviewBlocked || isOperationalContextProposalSaving}
                      >
                        Accept
                      </button>
                      <button
                        type="button"
                        className="secondary-action"
                        onClick={() => void rejectOperationalContextProposal()}
                        disabled={isOperationalContextReviewBlocked || isOperationalContextProposalSaving}
                      >
                        Reject
                      </button>
                      <button
                        type="button"
                        className="primary-action"
                        onClick={() => void promoteOperationalContextProposal()}
                        disabled={!canPromoteOperationalContextProposal || isOperationalContextProposalSaving}
                      >
                        Promote
                      </button>
                    </div>
                    <label className="commit-message-editor">
                      <span>Review note</span>
                      <textarea
                        value={operationalContextReviewNote}
                        onChange={(event) => setOperationalContextReviewNote(event.target.value)}
                        spellCheck={false}
                      />
                    </label>
                    <label className="proposal-editor">
                      <span>Proposed markdown</span>
                      <textarea
                        value={operationalContextProposalDraft}
                        onChange={(event) => setOperationalContextProposalDraft(event.target.value)}
                        disabled={isOperationalContextReviewBlocked}
                        spellCheck={false}
                      />
                    </label>
                    <div className="proposal-warning-list proposal-decision-review">
                      <h5>Decision Continuity Review</h5>
                      <p>
                        Confirm important decisions, unresolved decisions, and rationale remain present before accepting.
                      </p>
                      <div className="proposal-decision-grid">
                        <div>
                          <h6>Stable Decisions</h6>
                          {proposedStableDecisions.length > 0 ? (
                            <ul>
                              {proposedStableDecisions.map((decision) => (
                                <li key={decision}>{decision}</li>
                              ))}
                            </ul>
                          ) : (
                            <p>No stable decisions in the proposal.</p>
                          )}
                        </div>
                        <div>
                          <h6>Open Decisions</h6>
                          {proposedOpenDecisions.length > 0 ? (
                            <ul>
                              {proposedOpenDecisions.map((decision) => (
                                <li key={decision}>{decision}</li>
                              ))}
                            </ul>
                          ) : (
                            <p>No open decisions in the proposal.</p>
                          )}
                        </div>
                        <div>
                          <h6>Decision Rationale</h6>
                          {proposedDecisionRationale.length > 0 ? (
                            <ul>
                              {proposedDecisionRationale.map((rationale) => (
                                <li key={rationale}>{rationale}</li>
                              ))}
                            </ul>
                          ) : (
                            <p>No decision rationale in the proposal.</p>
                          )}
                        </div>
                        <div>
                          <h6>Decision Changes</h6>
                          {decisionSemanticChanges.length > 0 ? (
                            <ul>
                              {decisionSemanticChanges.map((change, index) => (
                                <li key={`${change.type}-${change.itemId ?? index}`}>
                                  {change.type}: {change.description}
                                </li>
                              ))}
                            </ul>
                          ) : (
                            <p>No decision-specific semantic changes detected.</p>
                          )}
                        </div>
                      </div>
                      {decisionContinuityWarnings.length > 0 ? (
                        <>
                          <h6>Decision Warnings</h6>
                          <ul>
                            {decisionContinuityWarnings.map((warning) => (
                              <li key={warning}>{warning}</li>
                            ))}
                          </ul>
                        </>
                      ) : null}
                    </div>
                    <h5>Semantic Changes</h5>
                    {operationalContextProposal.semanticChanges.length === 0 ? (
                      <p>No coarse semantic changes detected.</p>
                    ) : (
                      <ul>
                        {operationalContextProposal.semanticChanges.map((change, index) => (
                          <li key={`${change.type}-${change.itemId ?? index}`}>
                            {change.type}: {change.description}
                          </li>
                        ))}
                      </ul>
                    )}
                    <h5>Compression Summary</h5>
                    <div className="context-summary-grid">
                      <span>Preserved: {operationalContextProposal.compressionSummary.preservedItemCount}</span>
                      <span>Added: {operationalContextProposal.compressionSummary.addedItemCount}</span>
                      <span>Removed: {operationalContextProposal.compressionSummary.removedItemCount}</span>
                      <span>Compressed: {operationalContextProposal.compressionSummary.compressedItemCount}</span>
                      <span>Permanent: {operationalContextProposal.compressionSummary.permanentUnderstandingItemCount}</span>
                      <span>Active: {operationalContextProposal.compressionSummary.activeUnderstandingItemCount}</span>
                      <span>Historical: {operationalContextProposal.compressionSummary.historicalUnderstandingItemCount}</span>
                      <span>Resolved: {operationalContextProposal.compressionSummary.resolvedQuestionCount}</span>
                      <span>Retired: {operationalContextProposal.compressionSummary.retiredRiskCount}</span>
                      <span>Warnings: {operationalContextProposal.compressionSummary.warningCount}</span>
                    </div>
                    {operationalContextProposal.compressionSummary.revisionSummary.length > 0 ? (
                      <div className="proposal-warning-list proposal-revision-summary">
                        <h5>Revision Summary</h5>
                        <ul>
                          {operationalContextProposal.compressionSummary.revisionSummary.map((summary) => (
                            <li key={summary}>{summary}</li>
                          ))}
                        </ul>
                      </div>
                    ) : null}
                    {operationalContextProposal.compressionSummary.stableUnderstandingRetentionWarnings.length > 0 ? (
                      <div className="proposal-warning-list">
                        <h5>Retention Warnings</h5>
                        <ul>
                          {operationalContextProposal.compressionSummary.stableUnderstandingRetentionWarnings.map((warning) => (
                            <li key={warning}>{warning}</li>
                          ))}
                        </ul>
                      </div>
                    ) : null}
                    {operationalContextProposal.compressionSummary.noiseRemovedIndicators.length > 0 ? (
                      <div className="proposal-warning-list">
                        <h5>Compressed Understanding</h5>
                        <ul>
                          {operationalContextProposal.compressionSummary.noiseRemovedIndicators.map((indicator) => (
                            <li key={indicator}>{indicator}</li>
                          ))}
                        </ul>
                      </div>
                    ) : null}
                    <div className="proposal-comparison-grid">
                      <div>
                        <h5>Current Understanding</h5>
                        <div className="markdown-preview context-artifact-content">
                          {operationalContextCurrentContent.trim()
                            ? renderMarkdown(operationalContextCurrentContent)
                            : <p>No current operational context.</p>}
                        </div>
                      </div>
                      <div>
                        <h5>Review Candidate</h5>
                        <div className="markdown-preview context-artifact-content">
                          {operationalContextProposalDraft.trim()
                            ? renderMarkdown(operationalContextProposalDraft)
                            : <p>Empty proposal.</p>}
                        </div>
                      </div>
                    </div>
                  </div>
                ) : null}
              </section>

              <section className="execution-workspace" aria-label="Execution workspace">
                <div className="execution-workspace-header">
                  <div>
                    <p className="eyebrow">Execution Workspace</p>
                    <h4>{executionDisplay?.milestonePath ?? selectedMilestonePath ?? 'Select a milestone'}</h4>
                  </div>
                  <span className={`execution-state execution-state-${currentExecutionState.toLowerCase()}`}>
                    {executionStateLabels[currentExecutionState]}
                  </span>
                </div>

                <div className="execution-workflow-rail" aria-label="Execution lifecycle">
                  {executionWorkflowSteps.map((step) => (
                    <div
                      className={`execution-workflow-step execution-workflow-step-${step.state}`}
                      key={step.key}
                    >
                      <span>{step.label}</span>
                      <small>{step.detail}</small>
                    </div>
                  ))}
                </div>

              <section className="execution-context-panel" aria-label="Execution context preview">
                <div className="context-toolbar">
                  <div>
                    <p className="eyebrow">Execution Context</p>
                    <h4>Preview Package</h4>
                  </div>
                  <div className="context-controls">
                    <select
                      value={selectedMilestonePath ?? ''}
                      onChange={(event) =>
                        selectMilestone(
                          selectedRepository.repository.id,
                          event.target.value || null,
                        )
                      }
                      disabled={milestoneOptions.length === 0 || isContextLoading}
                    >
                      {milestoneOptions.length === 0 ? (
                        <option value="">No milestones</option>
                      ) : (
                        milestoneOptions.map((milestone) => (
                          <option key={milestone.relativePath} value={milestone.relativePath}>
                            {milestone.name}
                          </option>
                        ))
                      )}
                    </select>
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={() => void buildExecutionContext()}
                      disabled={!selectedMilestonePath || isContextLoading}
                    >
                      {isContextLoading ? 'Building...' : 'Build Execution Context'}
                    </button>
                    <button
                      type="button"
                      className="primary-action"
                      onClick={() => void startExecution()}
                      disabled={!canStartExecution}
                      title={startExecutionBlockedReason}
                    >
                      {isStartingExecution ? 'Starting...' : 'Start Execution'}
                    </button>
                  </div>
                </div>

                {executionContext ? (
                  <div className="context-preview">
                    <ExecutionContextSummaryRows
                      executionContext={executionContext}
                      operationalContextStatus={operationalContextExecutionStatus}
                      launchStatus={canStartExecution ? 'Ready' : startExecutionBlockedReason}
                      sizeStatus={executionContextSizeStatus}
                    />

                    <div className="context-columns">
                      <div>
                        <h5>Artifacts</h5>
                        <ExecutionContextArtifactList artifacts={executionContext.artifacts} />
                      </div>
                      <div>
                        <h5>Missing Optional</h5>
                        <ExecutionContextMissingOptionalList
                          paths={executionContext.diagnostics.missingOptionalArtifacts}
                        />
                      </div>
                      <div>
                        <h5>Validation</h5>
                        <ExecutionContextValidationList
                          validationErrors={executionContext.diagnostics.validationErrors}
                        />
                      </div>
                    </div>

                    {executionContext.repositorySnapshot ? (
                      <div className="dirty-state">
                        <h5>Repository Snapshot</h5>
                        <div className="context-summary">
                          <span>Branch: {executionContext.repositorySnapshot.branch || '(detached)'}</span>
                          <span>
                            State:{' '}
                            {executionContext.repositorySnapshot.dirtyState.isClean ? 'Clean' : 'Dirty'}
                          </span>
                          <span>
                            Captured:{' '}
                            {new Date(executionContext.repositorySnapshot.capturedAt).toLocaleString()}
                          </span>
                        </div>
                        <div className="context-columns">
                          <GitPathBucket
                            label="Staged"
                            paths={executionContext.repositorySnapshot.dirtyState.stagedPaths}
                          />
                          <GitPathBucket
                            label="Modified"
                            paths={executionContext.repositorySnapshot.dirtyState.modifiedPaths}
                          />
                          <GitPathBucket
                            label="Added"
                            paths={executionContext.repositorySnapshot.dirtyState.addedPaths}
                          />
                          <GitPathBucket
                            label="Deleted"
                            paths={executionContext.repositorySnapshot.dirtyState.deletedPaths}
                          />
                          <GitPathBucket
                            label="Renamed"
                            paths={executionContext.repositorySnapshot.dirtyState.renamedPaths}
                          />
                          <GitPathBucket
                            label="Untracked"
                            paths={executionContext.repositorySnapshot.dirtyState.untrackedPaths}
                          />
                        </div>
                      </div>
                    ) : null}

                    <div className="artifact-diagnostics">
                      <h5>Artifact Sizes</h5>
                      <div className="diagnostic-list">
                        {executionContext.diagnostics.artifactDiagnostics.map((diagnostic) => (
                          <span key={diagnostic.relativePath}>
                            {diagnostic.relativePath}: {diagnostic.byteCount} bytes
                            {diagnostic.hardLimitExceeded
                              ? ' / hard limit'
                              : diagnostic.warningThresholdExceeded
                                ? ' / warning'
                                : ''}
                          </span>
                        ))}
                      </div>
                    </div>

                    <div className="context-artifact-previews">
                      <h5>Artifact Content</h5>
                      {executionContext.artifacts.map((artifact) => (
                        <details key={artifact.relativePath} open={artifact.role === 'OperationalContext'}>
                          <summary>
                            {artifact.role}: {artifact.relativePath} ({artifact.characterCount} characters)
                          </summary>
                          <div className="markdown-preview context-artifact-content">
                            {artifact.content.trim() ? renderMarkdown(artifact.content) : <p>Empty artifact.</p>}
                          </div>
                        </details>
                      ))}
                    </div>
                  </div>
                ) : (
                  <p className="empty-state">Build a context preview for a selected milestone.</p>
                )}
              </section>

              {shouldShowGitWorkflow ? (
                <section className="git-status-panel" aria-label="Git status">
                  <div className="git-status-header">
                    <div>
                      <p className="eyebrow">Git Workflow</p>
                      <h4>{executionStateLabels[currentExecutionState]}</h4>
                    </div>
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={() =>
                        currentExecutionState === 'AwaitingCommit' && executionSessionId
                          ? void loadCommitPreparation(executionSessionId)
                          : selectedRepository
                          ? void refreshGitStatus()
                          : undefined
                      }
                      disabled={
                        (!selectedRepository && !executionSessionId) ||
                        isGitStatusLoading ||
                        isCommitPreparationLoading
                      }
                    >
                      {isGitStatusLoading || isCommitPreparationLoading ? 'Refreshing...' : 'Refresh'}
                    </button>
                  </div>
                  {currentExecutionState === 'AwaitingCommit' ? (
                    isCommitPreparationCurrent && commitPreparation ? (
                      <div className="commit-review-panel">
                        <div className="context-summary">
                          <span>Preparation: {commitPreparation.id}</span>
                          <span>Snapshot: {commitPreparation.statusSnapshot.id}</span>
                          <span>Branch: {commitPreparation.statusSnapshot.branch || '(detached)'}</span>
                          <span>Generated: {formatDateTime(commitPreparation.generatedAt)}</span>
                          <span>Changed paths: {commitPreparation.scopeItems.length}</span>
                          <span>Selected: {selectedCommitScopeItems.length}</span>
                          <span>
                            Pre-existing:{' '}
                            {commitPreparation.hasPreExistingChanges ? 'Present' : 'None detected'}
                          </span>
                          <span>Captured: {formatDateTime(commitPreparation.statusSnapshot.capturedAt)}</span>
                        </div>
                        <label className="commit-message-editor">
                          <span>Commit message</span>
                          <textarea
                            value={commitMessage}
                            onChange={(event) => setCommitMessage(event.target.value)}
                            spellCheck={false}
                          />
                        </label>
                        <div className="commit-scope-toolbar">
                          <button
                            type="button"
                            className="secondary-action"
                            onClick={selectAllCommitPaths}
                            disabled={commitPreparation.scopeItems.length === 0}
                          >
                            Select All
                          </button>
                          <button
                            type="button"
                            className="secondary-action"
                            onClick={selectNoCommitPaths}
                            disabled={commitPreparation.scopeItems.length === 0}
                          >
                            Select None
                          </button>
                          <button
                            type="button"
                            className="primary-action"
                            onClick={() => void commitPreparedScope()}
                            disabled={!canCommitPreparedScope}
                          >
                            {isCommitting ? 'Committing...' : 'Commit Selected'}
                          </button>
                        </div>
                        {commitPreparation.scopeItems.length === 0 ? (
                          <p className="empty-state">No changed paths are available for commit.</p>
                        ) : (
                          <div className="commit-scope-list" aria-label="Commit scope">
                            {commitPreparation.scopeItems.map((item) => (
                              <label className="commit-scope-item" key={item.path}>
                                <input
                                  type="checkbox"
                                  checked={selectedCommitPaths.has(item.path)}
                                  onChange={(event) =>
                                    setCommitPathSelection(item.path, event.currentTarget.checked)
                                  }
                                />
                                <span>{item.path}</span>
                                <small>{item.changeType}</small>
                                <small>
                                  {item.origin === 'PreExisting'
                                    ? 'Pre-existing'
                                    : 'Execution generated'}
                                </small>
                              </label>
                            ))}
                          </div>
                        )}
                      </div>
                    ) : (
                      <p className="empty-state">
                        {isCommitPreparationLoading
                          ? 'Preparing commit review...'
                          : 'Commit preparation is not loaded.'}
                      </p>
                    )
                  ) : currentExecutionState === 'AwaitingPush' && executionDisplay?.commitSha ? (
                    <div className="commit-review-panel">
                      <div className="context-summary">
                        <span>Commit: {executionDisplay.commitSha}</span>
                        <span>Committed: {formatDateTime(executionDisplay.committedAt)}</span>
                        <span>Snapshot: {executionDisplay.preparationSnapshotId ?? 'Not recorded'}</span>
                        <span>Branch: {gitStatus?.branch || executionDisplay.pushBranchName || '(unknown)'}</span>
                        <span>Ahead: {gitStatus?.aheadCount ?? 'Not loaded'}</span>
                        <span>State: Awaiting push</span>
                      </div>
                      <div className="commit-scope-toolbar">
                        <button
                          type="button"
                          className="primary-action"
                          onClick={() => void pushExecution()}
                          disabled={!canPushExecution}
                        >
                          {isPushing ? 'Pushing...' : 'Push Commit'}
                        </button>
                      </div>
                    </div>
                  ) : gitStatus ? (
                    <>
                      <div className="context-summary">
                        <span>Branch: {gitStatus.branch || '(detached)'}</span>
                        <span>
                          State: {gitStatus.dirtyState.isClean ? 'Clean' : 'Dirty'}
                        </span>
                        <span>Ahead: {gitStatus.aheadCount}</span>
                        <span>Behind: {gitStatus.behindCount}</span>
                        <span>Changed paths: {gitStatusPathCount}</span>
                        <span>Captured: {formatDateTime(gitStatus.capturedAt)}</span>
                      </div>
                      <div className="context-columns">
                        <GitPathBucket label="Staged" paths={gitStatus.dirtyState.stagedPaths} />
                        <GitPathBucket label="Modified" paths={gitStatus.dirtyState.modifiedPaths} />
                        <GitPathBucket label="Added" paths={gitStatus.dirtyState.addedPaths} />
                        <GitPathBucket label="Deleted" paths={gitStatus.dirtyState.deletedPaths} />
                        <GitPathBucket label="Renamed" paths={gitStatus.dirtyState.renamedPaths} />
                        <GitPathBucket label="Untracked" paths={gitStatus.dirtyState.untrackedPaths} />
                      </div>
                    </>
                  ) : (
                    <p className="empty-state">
                      {isGitStatusLoading ? 'Loading Git status...' : 'Git status is not loaded.'}
                    </p>
                  )}
                </section>
              ) : null}

              {executionDisplay ? (
                <ExecutionSessionPanel
                  session={executionDisplay}
                  repositoryStateLabel={executionStateLabels[executionDisplay.repositoryState]}
                />
              ) : null}

              <ExecutionHistoryPanel
                sessions={selectedExecutionHistory}
                repositoryStateLabels={executionStateLabels}
              />

              {canReviewGeneratedHandoff && executionDisplay ? (
                <section className="handoff-review-panel" aria-label="Generated handoff review">
                  <div className="handoff-review-header">
                    <div>
                      <p className="eyebrow">Handoff Review</p>
                      <h4>{executionDisplay.handoffPath}</h4>
                    </div>
                    <div className="handoff-review-metadata">
                      <span>State: {executionStateLabels[executionDisplay.repositoryState]}</span>
                      <span>Completed: {formatDateTime(executionDisplay.completedAt)}</span>
                      <span>Duration: {formatDuration(executionDisplay.duration)}</span>
                      <span>Decision: Awaiting review</span>
                      <span>{generatedHandoffContent.length} characters</span>
                    </div>
                  </div>
                  <div className="handoff-review-actions">
                    <button
                      type="button"
                      className="primary-action"
                      onClick={() => void acceptGeneratedHandoff()}
                      disabled={!isHandoffDecisionPending}
                    >
                      {isAcceptingHandoff ? 'Accepting...' : 'Accept Handoff'}
                    </button>
                    <button
                      type="button"
                      className="danger-action"
                      onClick={() => void rejectGeneratedHandoff()}
                      disabled={!isHandoffDecisionPending}
                    >
                      {isRejectingHandoff ? 'Rejecting...' : 'Reject Handoff'}
                    </button>
                  </div>
                  <div className="markdown-preview handoff-review-content">
                    {isGeneratedHandoffLoading ? (
                      <p>Loading generated handoff...</p>
                    ) : generatedHandoffContent.trim() ? (
                      renderMarkdown(generatedHandoffContent)
                    ) : (
                      <p>Generated handoff is empty.</p>
                    )}
                  </div>
                </section>
              ) : null}

              {executionDisplay ? <ExecutionEventFeed events={selectedExecutionEvents} /> : null}
              </section>

              {workspace ? (
                <section className="artifact-workspace-shell" aria-label="Artifact workspace">
                  <div>
                    <p className="eyebrow">Repository Artifacts</p>
                    <h4>Explorer and Editor</h4>
                  </div>
                <div className="artifact-workspace">
                  <section className="artifact-explorer" aria-label="Artifact explorer">
                    {getArtifactCategories(workspace.artifactInventory).map((category) => (
                      <div className="artifact-category" key={category.label}>
                        <h4>{category.label}</h4>
                        {category.artifacts.length === 0 ? (
                          <p className="missing-artifact">{category.missingLabel}</p>
                        ) : (
                          <div className="artifact-list">
                            {category.artifacts.map((artifact) => (
                              <button
                                type="button"
                                key={artifact.relativePath}
                                className={`artifact-item${
                                  artifact.relativePath === selectedArtifactPath ? ' selected' : ''
                                }`}
                                onClick={() =>
                                  selectArtifact(
                                    selectedRepository.repository.id,
                                    artifact.relativePath,
                                  )
                                }
                              >
                                <span>{artifact.name}</span>
                                <span>{artifact.versionKind}</span>
                              </button>
                            ))}
                          </div>
                        )}
                      </div>
                    ))}
                  </section>

                  <section className="artifact-panel" aria-label="Artifact content">
                    {selectedArtifact ? (
                      <>
                        <div className="artifact-panel-header">
                          <div>
                            <p className="eyebrow">{selectedArtifact.family}</p>
                            <h4>{selectedArtifact.name}</h4>
                            <span>{selectedArtifact.relativePath}</span>
                          </div>
                          <div className="artifact-panel-actions">
                            {canRotateSelectedArtifact ? (
                              <button
                                type="button"
                                className="secondary-action"
                                onClick={() => void rotateSelectedArtifact()}
                                disabled={
                                  isRotating ||
                                  isArtifactLoading ||
                                  isSaving ||
                                  hasDraftChanges
                                }
                                title={
                                  hasDraftChanges
                                    ? 'Save changes before rotating.'
                                    : 'Archive the current artifact to the next historical file.'
                                }
                              >
                                {isRotating ? 'Rotating...' : 'Rotate'}
                              </button>
                            ) : null}
                            <button
                              type="button"
                              className="primary-action"
                              onClick={() => void saveArtifact()}
                              disabled={isSaving || isArtifactLoading || !hasDraftChanges}
                            >
                              {isSaving ? 'Saving...' : 'Save'}
                            </button>
                          </div>
                        </div>
                        <textarea
                          className="artifact-editor"
                          value={draftContent}
                          onChange={(event) => setDraftContent(event.target.value)}
                          spellCheck={false}
                          disabled={isArtifactLoading}
                        />
                        <div className="markdown-preview">
                          {isArtifactLoading ? (
                            <p>Loading artifact...</p>
                          ) : draftContent.trim() ? (
                            renderMarkdown(draftContent)
                          ) : (
                            <p>Empty artifact.</p>
                          )}
                        </div>
                      </>
                    ) : (
                      <p className="empty-state">No artifact selected.</p>
                    )}
                  </section>
                </div>
                </section>
              ) : (
                <p className="empty-state">Loading workspace...</p>
              )}

              <div className="details-actions">
                <button
                  type="button"
                  className="danger-action"
                  onClick={() => void removeRepository(selectedRepository.repository)}
                  disabled={removingRepositoryId === selectedRepository.repository.id}
                >
                  {removingRepositoryId === selectedRepository.repository.id
                    ? 'Removing...'
                    : 'Remove Registration'}
                </button>
              </div>
            </div>
          ) : (
            <p className="empty-state">Select or add a repository.</p>
          )}
        </section>
      </section>
    </main>
  )
}

export default App
