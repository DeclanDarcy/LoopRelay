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
import { ArtifactMarkdownPreview } from './features/artifacts/ArtifactMarkdownPreview'
import { ArtifactMetadata } from './features/artifacts/ArtifactMetadata'
import { Button, EmptyState, Panel, SectionHeader } from './components/design'
import { AppShell, CommandPalette, Header, Sidebar, WorkspaceTabs } from './components/shell'
import { ContinuityTab } from './features/continuity/ContinuityTab'
import { ExecutionTab } from './features/execution/ExecutionTab'
import { GeneratedHandoffContent } from './features/execution/GeneratedHandoffContent'
import {
  CommitPreparationSummary,
  GitStatusDetails,
  PushReviewSummary,
} from './features/execution/GitWorkflowEvidence'
import { OperationalContextTab } from './features/operational-context/OperationalContextTab'
import { SelectedRepositorySummary } from './features/repositories/SelectedRepositorySummary'
import { ExecutionContextPanel } from './features/workspace/ExecutionContextPanel'
import { WorkspaceLiveActivityPanel } from './features/workspace/WorkspaceLiveActivityPanel'
import { WorkspaceMilestonesPanel } from './features/workspace/WorkspaceMilestonesPanel'
import { WorkspaceInspectorRail } from './features/workspace/WorkspaceInspectorRail'
import { WorkspaceTab } from './features/workspace/WorkspaceTab'
import {
  useArtifactContent,
  useContinuityDiagnostics,
  useContinuityReports,
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
} from './lib'
import { executionReadinessStatus, repositoryExecutionStatus } from './lib/status'
import { useShellState } from './state/shellState'
import type {
  CommitPreparation,
  ExecutionSessionSummary,
  OperationalContextProposal,
  Repository,
  RepositoryWorkspaceProjection,
} from './types'
import './App.css'

function App() {
  const {
    selectedRepositoryId,
    selectedArtifactPath,
    selectedMilestonePath,
    activePrimaryTab,
    isCommandPaletteOpen,
    sectionTarget,
    selectRepository: selectRepositoryNavigation,
    reconcileRepositorySelection,
    selectArtifact: selectArtifactNavigation,
    reconcileSelectedArtifact: reconcileSelectedArtifactNavigation,
    selectMilestone,
    reconcileSelectedMilestone,
    clearRepositoryNavigation,
    setActivePrimaryTab,
    setIsCommandPaletteOpen,
    setSectionTarget,
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
  const {
    data: continuityReports,
    setData: setContinuityReports,
    isLoading: isContinuityReportsLoading,
    error: continuityReportsError,
  } = useContinuityReports(selectedRepository?.repository.id ?? null)

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
  const commitPreparationSessionId = commitPreparation?.sessionId ?? null
  const isCommitPreparationCurrent =
    commitPreparationSessionId === executionSessionId &&
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
      return `Repository readiness is ${executionReadinessStatus[workspace.readiness].label}.`
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
      setContinuityReports([])
      setOperationalContextCurrentContent('')
      setOperationalContextProposalDraft('')
      setOperationalContextReviewNote('')
    },
    [selectRepositoryNavigation, setContinuityDiagnostics, setContinuityReports],
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
      setContinuityReports((reports) => [
        report,
        ...reports.filter((item) => item.reportId !== report.reportId),
      ])
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
    if (continuityReportsError) {
      setError(continuityReportsError)
    }
  }, [continuityReportsError])

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

    if (commitPreparationSessionId && commitPreparationSessionId !== executionSessionId) {
      setCommitPreparation(null)
      setSelectedCommitPaths(new Set())
      setCommitMessage('')
    }
  }, [
    commitPreparationSessionId,
    executionSessionId,
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

  useEffect(() => {
    if (!sectionTarget) {
      return
    }

    document.getElementById(sectionTarget)?.scrollIntoView({ block: 'start' })
    setSectionTarget(null)
  }, [sectionTarget, setSectionTarget])

  const renderExecutionContextPanel = (panelId?: string) =>
    selectedRepository ? (
      <ExecutionContextPanel
        id={panelId}
        executionContext={executionContext}
        milestoneOptions={milestoneOptions}
        selectedMilestonePath={selectedMilestonePath}
        isContextLoading={isContextLoading}
        canStartExecution={canStartExecution}
        isStartingExecution={isStartingExecution}
        startExecutionBlockedReason={startExecutionBlockedReason}
        operationalContextExecutionStatus={operationalContextExecutionStatus}
        executionContextSizeStatus={executionContextSizeStatus}
        onSelectMilestone={(milestonePath) =>
          selectMilestone(selectedRepository.repository.id, milestonePath)
        }
        onBuildExecutionContext={() => void buildExecutionContext()}
        onStartExecution={() => void startExecution()}
      />
    ) : null

  const openExecutionSection = (sectionId = 'execution-context') => {
    setActivePrimaryTab('execution')
    setSectionTarget(sectionId)
  }

  const openOperationalContextSection = (sectionId: string) => {
    setActivePrimaryTab('operational-context')
    setSectionTarget(sectionId)
  }

  const openContinuitySection = (sectionId: string) => {
    setActivePrimaryTab('continuity')
    setSectionTarget(sectionId)
  }

  const openContinuityWarnings = () => {
    openContinuitySection('continuity-warnings')
  }

  const openWorkspaceExecutionContext = (milestonePath: string) => {
    if (selectedRepository) {
      selectMilestone(selectedRepository.repository.id, milestonePath)
    }

    setActivePrimaryTab('workspace')
    setSectionTarget('workspace-execution-context')
  }

  const openWorkspaceGit = () => {
    setActivePrimaryTab('workspace')
    setSectionTarget('workspace-inspector')
  }

  const openHandoffArtifact = (handoffPath: string) => {
    if (selectedRepository) {
      selectArtifact(selectedRepository.repository.id, handoffPath)
    }

    setActivePrimaryTab('workspace')
    setSectionTarget('artifact-workspace')
  }

  const openWorkspaceArtifact = (relativePath: string) => {
    if (selectedRepository) {
      selectArtifact(selectedRepository.repository.id, relativePath)
    }

    setActivePrimaryTab('workspace')
    setSectionTarget('artifact-workspace')
  }

  const renderGitWorkflowPanel = () =>
    shouldShowGitWorkflow ? (
      <Panel className="git-status-panel" aria-label="Git status">
        <SectionHeader
          className="git-status-header"
          eyebrow="Git Workflow"
          title={repositoryExecutionStatus[currentExecutionState].label}
          headingLevel={4}
          actions={
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
          }
        />
        {currentExecutionState === 'AwaitingCommit' ? (
          isCommitPreparationCurrent && commitPreparation ? (
            <div className="commit-review-panel">
              <CommitPreparationSummary
                preparation={commitPreparation}
                selectedPathCount={selectedCommitScopeItems.length}
              />
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
                <EmptyState className="empty-state">No changed paths are available for commit.</EmptyState>
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
                      <small>{item.origin === 'PreExisting' ? 'Pre-existing' : 'Execution generated'}</small>
                    </label>
                  ))}
                </div>
              )}
            </div>
          ) : (
            <EmptyState className="empty-state">
              {isCommitPreparationLoading
                ? 'Preparing commit review...'
                : 'Commit preparation is not loaded.'}
            </EmptyState>
          )
        ) : currentExecutionState === 'AwaitingPush' && executionDisplay?.commitSha ? (
          <div className="commit-review-panel">
            <PushReviewSummary execution={executionDisplay} gitStatus={gitStatus} />
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
          <GitStatusDetails gitStatus={gitStatus} changedPathCount={gitStatusPathCount} />
        ) : (
          <EmptyState className="empty-state">
            {isGitStatusLoading ? 'Loading Git status...' : 'Git status is not loaded.'}
          </EmptyState>
        )}
      </Panel>
    ) : null

  const renderHandoffReviewPanel = () =>
    canReviewGeneratedHandoff && executionDisplay ? (
      <Panel className="handoff-review-panel" aria-label="Generated handoff review">
        <div className="handoff-review-header">
          <SectionHeader
            eyebrow="Handoff Review"
            title={executionDisplay.handoffPath ?? 'Generated handoff'}
            headingLevel={4}
          />
          <div className="handoff-review-metadata">
            <span>State: {repositoryExecutionStatus[executionDisplay.repositoryState].label}</span>
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
        <GeneratedHandoffContent
          content={generatedHandoffContent}
          isLoading={isGeneratedHandoffLoading}
        />
      </Panel>
    ) : null

  return (
    <AppShell
      sidebar={
        <Sidebar
          repositories={repositories}
          selectedRepositoryId={selectedRepository?.repository.id ?? selectedRepositoryId}
          isLoading={isLoading}
          onOpenPalette={() => setIsCommandPaletteOpen(true)}
          onSelectRepository={selectRepository}
        />
      }
      header={
        <Header
          selectedRepository={selectedRepository}
          currentExecutionState={currentExecutionState}
          isWorkspaceLoading={isWorkspaceLoading}
          isAddingRepository={isAdding}
          onRefreshRepositories={loadRepositories}
          onRefreshWorkspace={() => void refreshWorkspace()}
          onAddRepository={addRepository}
        />
      }
      tabs={
        <WorkspaceTabs
          activeTab={activePrimaryTab}
          onSelectTab={setActivePrimaryTab}
        />
      }
      palette={
        <CommandPalette
          isOpen={isCommandPaletteOpen}
          repositories={repositories}
          onClose={() => setIsCommandPaletteOpen(false)}
          onOpen={() => setIsCommandPaletteOpen(true)}
          onSelectRepository={selectRepository}
          onSelectSection={(sectionId) => {
            if (sectionId === 'execution-context') {
              setActivePrimaryTab('execution')
            }

            if (
              sectionId === 'operational-current' ||
              sectionId === 'operational-open-questions' ||
              sectionId === 'operational-active-risks' ||
              sectionId === 'proposal-review'
            ) {
              setActivePrimaryTab('operational-context')
            }

            if (
              sectionId === 'continuity-diagnostics' ||
              sectionId === 'continuity-warnings' ||
              sectionId === 'continuity-compression' ||
              sectionId === 'continuity-decision-retention'
            ) {
              setActivePrimaryTab('continuity')
            }

            if (sectionId === 'artifacts') {
              setActivePrimaryTab('workspace')
              setSectionTarget('artifact-workspace')
              return
            }

            setSectionTarget(sectionId)
          }}
          onSelectTab={setActivePrimaryTab}
        />
      }
    >

      {error ? <div className="notice error">{error}</div> : null}
      {message ? <div className="notice success">{message}</div> : null}

      <section className="workspace-grid" aria-label="Repository workspace">
        <Panel className="repository-details" aria-label="Repository details">
          <SectionHeader
            className="section-heading"
            title="Workspace"
            headingLevel={3}
            actions={
              <div className="section-actions">
                <Button
                  type="button"
                  variant="secondary"
                  className="secondary-action"
                  onClick={() => void refreshWorkspace()}
                  disabled={!selectedRepository || isWorkspaceLoading}
                >
                  {isWorkspaceLoading ? 'Refreshing...' : 'Refresh Workspace'}
                </Button>
              </div>
            }
          />

          {selectedRepository ? (
            <div className="details-body" data-active-tab={activePrimaryTab}>
              <WorkspaceTab
                hidden={activePrimaryTab !== 'workspace'}
                workflowSteps={executionWorkflowSteps}
                summary={
                  <SelectedRepositorySummary
                    repository={selectedRepository}
                    workspace={workspace}
                    executionDisplay={executionDisplay}
                    currentExecutionState={currentExecutionState}
                  />
                }
                executionContext={renderExecutionContextPanel('workspace-execution-context')}
                liveActivity={
                  executionDisplay ? (
                    <WorkspaceLiveActivityPanel
                      events={selectedExecutionEvents}
                      onOpenExecutionActivity={() => openExecutionSection('execution-events')}
                    />
                  ) : null
                }
                milestones={
                  workspace ? (
                    <WorkspaceMilestonesPanel
                      milestones={milestoneOptions}
                      selectedMilestonePath={selectedMilestonePath}
                      onSelectMilestone={(milestonePath) =>
                        selectMilestone(selectedRepository.repository.id, milestonePath)
                      }
                      onOpenExecutionContext={openWorkspaceExecutionContext}
                    />
                  ) : null
                }
                artifactWorkspace={
                  workspace ? (
                    <Panel
                      id="artifact-workspace"
                      className="artifact-workspace-shell"
                      aria-label="Artifact workspace"
                    >
                      <SectionHeader
                        eyebrow="Repository Artifacts"
                        title="Explorer and Editor"
                        headingLevel={4}
                      />
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
                                <ArtifactMetadata artifact={selectedArtifact} />
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
                              <ArtifactMarkdownPreview
                                content={draftContent}
                                isLoading={isArtifactLoading}
                              />
                            </>
                          ) : (
                            <EmptyState className="empty-state">No artifact selected.</EmptyState>
                          )}
                        </section>
                      </div>
                    </Panel>
                  ) : (
                    <EmptyState className="empty-state">Loading workspace...</EmptyState>
                  )
                }
                inspector={
                  <WorkspaceInspectorRail
                    currentExecutionState={currentExecutionState}
                    gitStatus={gitStatus}
                    gitStatusPathCount={gitStatusPathCount}
                    isGitStatusLoading={isGitStatusLoading}
                    gitStatusError={gitStatusError}
                    commitPreparation={commitPreparation}
                    isCommitPreparationCurrent={isCommitPreparationCurrent}
                    selectedCommitPathCount={selectedCommitScopeItems.length}
                    execution={executionDisplay}
                    operationalContext={workspace?.operationalContext ?? null}
                    proposalSummary={workspace?.operationalContextProposalSummary ?? null}
                    executionHistory={selectedExecutionHistory}
                    onOpenOperationalContext={openOperationalContextSection}
                    onOpenContinuityWarnings={openContinuityWarnings}
                    onOpenExecutionSession={() => openExecutionSection('execution-context')}
                  />
                }
              />

              <OperationalContextTab
                workspace={workspace}
                proposal={operationalContextProposal}
                currentContent={operationalContextCurrentContent}
                proposalDraft={operationalContextProposalDraft}
                reviewNote={operationalContextReviewNote}
                executionStatus={operationalContextExecutionStatus}
                reviewStatus={operationalContextReviewStatus}
                isProposalLoading={isOperationalContextProposalLoading}
                isProposalSaving={isOperationalContextProposalSaving}
                isReviewBlocked={isOperationalContextReviewBlocked}
                canPromoteProposal={canPromoteOperationalContextProposal}
                hasProposalDraftChanges={hasOperationalContextProposalDraftChanges}
                hasSelectedRepository={Boolean(selectedRepository)}
                onLoadLatestProposal={() => void loadLatestOperationalContextProposal()}
                onGenerateProposal={() => void generateOperationalContextProposal()}
                onSaveProposalEdit={() => void saveOperationalContextProposalEdit()}
                onAcceptProposal={() => void acceptOperationalContextProposal()}
                onRejectProposal={() => void rejectOperationalContextProposal()}
                onPromoteProposal={() => void promoteOperationalContextProposal()}
                onProposalDraftChange={setOperationalContextProposalDraft}
                onReviewNoteChange={setOperationalContextReviewNote}
                onOpenOperationalContextSection={openOperationalContextSection}
                onOpenContinuityWarnings={openContinuityWarnings}
                onOpenContinuityCompression={() => openContinuitySection('continuity-compression')}
                onOpenContinuityDecisionRetention={() =>
                  openContinuitySection('continuity-decision-retention')
                }
                onOpenArtifact={openWorkspaceArtifact}
              />

              <ContinuityTab
                diagnostics={continuityDiagnostics}
                reports={continuityReports}
                hasSelectedRepository={Boolean(selectedRepository)}
                isDiagnosticsLoading={isContinuityDiagnosticsLoading || isContinuityReportsLoading}
                isReportGenerating={isContinuityReportGenerating}
                onRefreshDiagnostics={() => void refreshContinuityDiagnostics()}
                onGenerateReport={() => void generateContinuityReport()}
                onOpenOperationalContextSection={openOperationalContextSection}
                onOpenReport={openWorkspaceArtifact}
              />

              <ExecutionTab
                execution={executionDisplay}
                executionContext={executionContextMatchesSelection ? executionContext : null}
                executionEvents={selectedExecutionEvents}
                executionHistory={selectedExecutionHistory}
                workflowSteps={executionWorkflowSteps}
                currentExecutionState={currentExecutionState}
                selectedMilestonePath={selectedMilestonePath}
                contextPanel={activePrimaryTab === 'execution' ? renderExecutionContextPanel() : null}
                gitWorkflow={renderGitWorkflowPanel()}
                handoffReview={renderHandoffReviewPanel()}
                launchReadiness={startExecutionBlockedReason}
                onOpenWorkspaceMilestone={openWorkspaceExecutionContext}
                onOpenWorkspaceExecutionContext={() => {
                  setActivePrimaryTab('workspace')
                  setSectionTarget('workspace-execution-context')
                }}
                onOpenHandoffArtifact={openHandoffArtifact}
                onOpenWorkspaceGit={openWorkspaceGit}
              />

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
            <EmptyState className="empty-state">Select or add a repository.</EmptyState>
          )}
        </Panel>
      </section>
    </AppShell>
  )
}

export default App
