import { type ReactNode, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { invoke } from '@tauri-apps/api/core'
import './App.css'

type RepositoryAvailability = 'Available' | 'Missing' | 'AccessDenied'
type ExecutionReadiness = 'MissingPlan' | 'MissingMilestones' | 'Ready'
type RepositoryExecutionState =
  | 'Ready'
  | 'Executing'
  | 'AwaitingAcceptance'
  | 'Accepted'
  | 'AwaitingCommit'
  | 'AwaitingPush'
  | 'Failed'
  | 'Cancelled'
type ExecutionSessionState = 'Created' | 'Executing' | 'Completed' | 'Failed' | 'Cancelled'
type ArtifactType = 'Plan' | 'OperationalContext' | 'Milestone' | 'Handoff' | 'Decision'
type ArtifactFamily = ArtifactType
type ArtifactVersionKind = 'Current' | 'Historical'

type Repository = {
  id: string
  name: string
  path: string
}

type Artifact = {
  relativePath: string
  name: string
  type: ArtifactType
  family: ArtifactFamily
  versionKind: ArtifactVersionKind
}

type ArtifactInventory = {
  plan: Artifact | null
  operationalContext: Artifact | null
  milestones: Artifact[]
  currentHandoff: Artifact | null
  historicalHandoffs: Artifact[]
  currentDecisions: Artifact | null
  historicalDecisions: Artifact[]
}

type ExecutionSessionSummary = {
  sessionId: string
  state: ExecutionSessionState
  repositoryState: RepositoryExecutionState
  milestonePath: string | null
  startedAt: string | null
  completedAt: string | null
  duration: string | null
  acceptedAt: string | null
  rejectedAt: string | null
  decisionNote: string | null
  lastActivityAt: string | null
  providerName: string
  providerExecutablePath: string | null
  providerProcessId: number | null
  providerStartedAt: string | null
  handoffPath: string | null
  commitSha: string | null
  committedAt: string | null
  commitMessage: string | null
  preparationSnapshotId: string | null
  pushAttemptedAt: string | null
  pushedAt: string | null
  pushedCommitSha: string | null
  pushRemoteName: string | null
  pushBranchName: string | null
  failureReason: string | null
}

type ExecutionEvent = {
  sequence: number
  timestamp: string
  type: string
  message: string
}

type ExecutionStatus = {
  sessionId: string
  state: ExecutionSessionState
  repositoryState: RepositoryExecutionState
  startedAt: string
  completedAt: string | null
  duration: string | null
  acceptedAt: string | null
  rejectedAt: string | null
  decisionNote: string | null
  lastActivityAt: string | null
  providerName: string
  providerExecutablePath: string | null
  providerProcessId: number | null
  providerStartedAt: string | null
  handoffPath: string | null
  failureReason: string | null
  recentEvents: ExecutionEvent[]
}

type ExecutionContextArtifact = {
  role: string
  relativePath: string
  name: string
  content: string
  byteCount: number
  characterCount: number
}

type ExecutionContextArtifactDiagnostic = {
  role: string
  relativePath: string
  byteCount: number
  characterCount: number
  warningThresholdBytes: number
  hardLimitBytes: number
  warningThresholdExceeded: boolean
  hardLimitExceeded: boolean
}

type RepositoryDirtyState = {
  stagedPaths: string[]
  modifiedPaths: string[]
  addedPaths: string[]
  deletedPaths: string[]
  renamedPaths: string[]
  untrackedPaths: string[]
  isClean: boolean
}

type ExecutionRepositorySnapshot = {
  branch: string
  dirtyState: RepositoryDirtyState
  capturedAt: string
}

type RepositoryGitStatus = {
  branch: string
  aheadCount: number
  behindCount: number
  dirtyState: RepositoryDirtyState
  capturedAt: string
}

type CommitChangeType = 'Staged' | 'Modified' | 'Added' | 'Deleted' | 'Renamed' | 'Untracked'
type CommitChangeOrigin = 'PreExisting' | 'ExecutionGenerated'

type CommitScopeItem = {
  path: string
  changeType: CommitChangeType
  origin: CommitChangeOrigin
  isSelected: boolean
}

type CommitStatusSnapshot = {
  id: string
  branch: string
  aheadCount: number
  behindCount: number
  dirtyState: RepositoryDirtyState
  capturedAt: string
}

type CommitPreparation = {
  id: string
  sessionId: string
  repositoryId: string
  repositoryPath: string
  proposedMessage: string
  scopeItems: CommitScopeItem[]
  statusSnapshot: CommitStatusSnapshot
  generatedAt: string
  hasPreExistingChanges: boolean
}

type ExecutionContextDiagnostics = {
  totalBytes: number
  totalCharacters: number
  warningThresholdBytes: number
  hardLimitBytes: number
  warningThresholdExceeded: boolean
  hardLimitExceeded: boolean
  artifactDiagnostics: ExecutionContextArtifactDiagnostic[]
  validationErrors: string[]
  missingOptionalArtifacts: string[]
  launchBlocked: boolean
}

type ExecutionContextPreview = {
  repositoryId: string
  repositoryName: string
  repositoryPath: string
  milestonePath: string
  generatedAt: string
  artifacts: ExecutionContextArtifact[]
  repositorySnapshot: ExecutionRepositorySnapshot | null
  diagnostics: ExecutionContextDiagnostics
}

type RepositoryDashboardProjection = {
  repository: Repository
  availability: RepositoryAvailability
  readiness: ExecutionReadiness
  executionState: RepositoryExecutionState
  activeExecutionSession: ExecutionSessionSummary | null
  executionSummary: ExecutionSessionSummary | null
  executionHistory: ExecutionSessionSummary[]
  milestoneCount: number
  hasCurrentHandoff: boolean
  hasCurrentDecisions: boolean
}

type RepositoryWorkspaceProjection = {
  repository: Repository
  availability: RepositoryAvailability
  readiness: ExecutionReadiness
  executionState: RepositoryExecutionState
  executionSummary: ExecutionSessionSummary | null
  executionHistory: ExecutionSessionSummary[]
  artifactInventory: ArtifactInventory
  milestoneCount: number
  hasPlan: boolean
  hasOperationalContext: boolean
  hasCurrentHandoff: boolean
  hasCurrentDecisions: boolean
}

type ArtifactCategory = {
  label: string
  missingLabel: string
  artifacts: Artifact[]
}

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

function formatError(error: unknown) {
  return error instanceof Error ? error.message : String(error)
}

function formatDateTime(value: string | null) {
  return value ? new Date(value).toLocaleString() : 'Not recorded'
}

function formatDuration(value: string | null) {
  return value ?? 'Not recorded'
}

function mergeExecutionEvents(currentEvents: ExecutionEvent[], incomingEvents: ExecutionEvent[]) {
  const eventsBySequence = new Map<number, ExecutionEvent>()
  currentEvents.forEach((event) => eventsBySequence.set(event.sequence, event))
  incomingEvents.forEach((event) => eventsBySequence.set(event.sequence, event))
  return Array.from(eventsBySequence.values()).sort((left, right) => left.sequence - right.sequence)
}

function getArtifactCategories(inventory: ArtifactInventory): ArtifactCategory[] {
  return [
    {
      label: 'Plan',
      missingLabel: 'plan.md is missing.',
      artifacts: inventory.plan ? [inventory.plan] : [],
    },
    {
      label: 'Operational Context',
      missingLabel: 'operational_context.md is missing.',
      artifacts: inventory.operationalContext ? [inventory.operationalContext] : [],
    },
    {
      label: 'Milestones',
      missingLabel: 'No milestone files found.',
      artifacts: inventory.milestones,
    },
    {
      label: 'Current Handoff',
      missingLabel: 'handoff.md is missing.',
      artifacts: inventory.currentHandoff ? [inventory.currentHandoff] : [],
    },
    {
      label: 'Historical Handoffs',
      missingLabel: 'No historical handoffs found.',
      artifacts: inventory.historicalHandoffs,
    },
    {
      label: 'Current Decisions',
      missingLabel: 'decisions.md is missing.',
      artifacts: inventory.currentDecisions ? [inventory.currentDecisions] : [],
    },
    {
      label: 'Historical Decisions',
      missingLabel: 'No historical decisions found.',
      artifacts: inventory.historicalDecisions,
    },
  ]
}

function renderMarkdown(content: string) {
  const nodes: ReactNode[] = []
  const lines = content.split(/\r?\n/)
  let codeLines: string[] = []
  let listItems: string[] = []
  let inCode = false

  function flushList(keyPrefix: string) {
    if (listItems.length === 0) {
      return
    }

    nodes.push(
      <ul key={`${keyPrefix}-list-${nodes.length}`}>
        {listItems.map((item, index) => (
          <li key={`${keyPrefix}-item-${index}`}>{item}</li>
        ))}
      </ul>,
    )
    listItems = []
  }

  lines.forEach((line, index) => {
    if (line.trim().startsWith('```')) {
      if (inCode) {
        nodes.push(
          <pre key={`code-${index}`}>
            <code>{codeLines.join('\n')}</code>
          </pre>,
        )
        codeLines = []
        inCode = false
      } else {
        flushList(`before-code-${index}`)
        inCode = true
      }
      return
    }

    if (inCode) {
      codeLines.push(line)
      return
    }

    const trimmed = line.trim()

    if (!trimmed) {
      flushList(`blank-${index}`)
      return
    }

    if (trimmed.startsWith('- ')) {
      listItems.push(trimmed.slice(2))
      return
    }

    flushList(`line-${index}`)

    if (trimmed.startsWith('### ')) {
      nodes.push(<h4 key={`h4-${index}`}>{trimmed.slice(4)}</h4>)
    } else if (trimmed.startsWith('## ')) {
      nodes.push(<h3 key={`h3-${index}`}>{trimmed.slice(3)}</h3>)
    } else if (trimmed.startsWith('# ')) {
      nodes.push(<h2 key={`h2-${index}`}>{trimmed.slice(2)}</h2>)
    } else {
      nodes.push(<p key={`p-${index}`}>{trimmed}</p>)
    }
  })

  if (inCode) {
    nodes.push(
      <pre key="code-tail">
        <code>{codeLines.join('\n')}</code>
      </pre>,
    )
  }

  flushList('tail')
  return nodes
}

function getAvailableArtifactPaths(inventory: ArtifactInventory) {
  return getArtifactCategories(inventory)
    .flatMap((category) => category.artifacts)
    .map((artifact) => artifact.relativePath)
}

function renderPathBucket(label: string, paths: string[]) {
  return (
    <div>
      <h5>{label}</h5>
      {paths.length === 0 ? (
        <p>None</p>
      ) : (
        <ul>
          {paths.map((path) => (
            <li key={path}>{path}</li>
          ))}
        </ul>
      )}
    </div>
  )
}

function countDirtyPaths(dirtyState: RepositoryDirtyState | null) {
  if (!dirtyState) {
    return 0
  }

  return (
    dirtyState.stagedPaths.length +
    dirtyState.modifiedPaths.length +
    dirtyState.addedPaths.length +
    dirtyState.deletedPaths.length +
    dirtyState.renamedPaths.length +
    dirtyState.untrackedPaths.length
  )
}

type ExecutionWorkflowStepState = 'complete' | 'current' | 'pending' | 'blocked'

type ExecutionWorkflowStep = {
  key: string
  label: string
  detail: string
  state: ExecutionWorkflowStepState
}

function getExecutionWorkflowSteps(
  repositoryState: RepositoryExecutionState,
  hasContextForSelection: boolean,
  hasExecutionSession: boolean,
): ExecutionWorkflowStep[] {
  const contextComplete = hasContextForSelection || hasExecutionSession || repositoryState !== 'Ready'
  const executionComplete =
    hasExecutionSession &&
    repositoryState !== 'Executing' &&
    repositoryState !== 'Failed' &&
    repositoryState !== 'Cancelled'
  const handoffComplete =
    repositoryState === 'AwaitingCommit' ||
    repositoryState === 'AwaitingPush' ||
    (repositoryState === 'Ready' && hasExecutionSession)
  const commitComplete =
    repositoryState === 'AwaitingPush' || (repositoryState === 'Ready' && hasExecutionSession)
  const pushComplete = repositoryState === 'Ready' && hasExecutionSession
  const isFailed = repositoryState === 'Failed' || repositoryState === 'Cancelled'

  return [
    {
      key: 'context',
      label: 'Context',
      detail: contextComplete ? 'Prepared' : 'Needs preview',
      state: contextComplete ? 'complete' : repositoryState === 'Ready' ? 'current' : 'pending',
    },
    {
      key: 'execution',
      label: 'Execution',
      detail:
        repositoryState === 'Executing'
          ? 'Running'
          : executionComplete
            ? 'Completed'
            : isFailed
              ? executionStateLabels[repositoryState]
              : 'Not started',
      state:
        repositoryState === 'Executing'
          ? 'current'
          : executionComplete
            ? 'complete'
            : isFailed
              ? 'blocked'
              : contextComplete
                ? 'current'
                : 'pending',
    },
    {
      key: 'handoff',
      label: 'Handoff',
      detail:
        repositoryState === 'AwaitingAcceptance'
          ? 'Awaiting review'
          : handoffComplete
            ? 'Accepted or closed'
            : isFailed
              ? 'Unavailable'
              : 'Pending execution',
      state:
        repositoryState === 'AwaitingAcceptance'
          ? 'current'
          : handoffComplete
            ? 'complete'
            : isFailed
              ? 'blocked'
              : 'pending',
    },
    {
      key: 'commit',
      label: 'Commit',
      detail:
        repositoryState === 'AwaitingCommit'
          ? 'Awaiting review'
          : commitComplete
            ? 'Committed'
            : 'Pending acceptance',
      state:
        repositoryState === 'AwaitingCommit'
          ? 'current'
          : commitComplete
            ? 'complete'
            : isFailed
              ? 'blocked'
              : 'pending',
    },
    {
      key: 'push',
      label: 'Push',
      detail:
        repositoryState === 'AwaitingPush'
          ? 'Awaiting push'
          : pushComplete
            ? 'Published'
            : 'Pending commit',
      state:
        repositoryState === 'AwaitingPush'
          ? 'current'
          : pushComplete
            ? 'complete'
            : isFailed
              ? 'blocked'
              : 'pending',
    },
  ]
}

function App() {
  const [repositories, setRepositories] = useState<RepositoryDashboardProjection[]>([])
  const [selectedRepositoryId, setSelectedRepositoryId] = useState<string | null>(null)
  const [workspace, setWorkspace] = useState<RepositoryWorkspaceProjection | null>(null)
  const [selectedArtifactPath, setSelectedArtifactPath] = useState<string | null>(null)
  const [selectedMilestonePath, setSelectedMilestonePath] = useState<string | null>(null)
  const [executionContext, setExecutionContext] = useState<ExecutionContextPreview | null>(null)
  const [backendUrl, setBackendUrl] = useState<string | null>(null)
  const [executionStatusesBySession, setExecutionStatusesBySession] = useState<Record<string, ExecutionStatus>>({})
  const [executionEventsBySession, setExecutionEventsBySession] = useState<Record<string, ExecutionEvent[]>>({})
  const [gitStatus, setGitStatus] = useState<RepositoryGitStatus | null>(null)
  const [commitPreparation, setCommitPreparation] = useState<CommitPreparation | null>(null)
  const [selectedCommitPaths, setSelectedCommitPaths] = useState<Set<string>>(new Set())
  const [commitMessage, setCommitMessage] = useState('')
  const selectedArtifactPathsByRepository = useRef<Record<string, string>>({})
  const refreshedCompletedSessionIds = useRef<Set<string>>(new Set())
  const [artifactContent, setArtifactContent] = useState('')
  const [draftContent, setDraftContent] = useState('')
  const [generatedHandoffContent, setGeneratedHandoffContent] = useState('')
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isWorkspaceLoading, setIsWorkspaceLoading] = useState(false)
  const [isArtifactLoading, setIsArtifactLoading] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [isRotating, setIsRotating] = useState(false)
  const [isContextLoading, setIsContextLoading] = useState(false)
  const [isStartingExecution, setIsStartingExecution] = useState(false)
  const [isGeneratedHandoffLoading, setIsGeneratedHandoffLoading] = useState(false)
  const [isAcceptingHandoff, setIsAcceptingHandoff] = useState(false)
  const [isRejectingHandoff, setIsRejectingHandoff] = useState(false)
  const [isGitStatusLoading, setIsGitStatusLoading] = useState(false)
  const [isCommitPreparationLoading, setIsCommitPreparationLoading] = useState(false)
  const [isCommitting, setIsCommitting] = useState(false)
  const [isPushing, setIsPushing] = useState(false)
  const [isAdding, setIsAdding] = useState(false)
  const [removingRepositoryId, setRemovingRepositoryId] = useState<string | null>(null)

  const selectedRepository = useMemo(
    () =>
      repositories.find((entry) => entry.repository.id === selectedRepositoryId) ??
      repositories[0] ??
      null,
    [repositories, selectedRepositoryId],
  )

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
  const milestoneOptions = workspace?.artifactInventory.milestones ?? []
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
  const selectedExecutionStatus = executionSummary
    ? executionStatusesBySession[executionSessionId ?? ''] ?? null
    : null
  const selectedExecutionEvents = executionSummary
    ? executionEventsBySession[executionSummary.sessionId] ??
      selectedExecutionStatus?.recentEvents ??
      []
    : []
  const currentExecutionState = workspace?.executionState ?? selectedRepository?.executionState ?? 'Ready'
  const executionContextMatchesSelection =
    executionContext?.repositoryId === selectedRepository?.repository.id &&
    executionContext?.milestonePath === selectedMilestonePath
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

  const selectRepository = useCallback((repositoryId: string) => {
    setSelectedRepositoryId(repositoryId)
    setSelectedArtifactPath(selectedArtifactPathsByRepository.current[repositoryId] ?? null)
  }, [])

  const selectArtifact = useCallback((repositoryId: string, relativePath: string) => {
    selectedArtifactPathsByRepository.current[repositoryId] = relativePath
    setSelectedArtifactPath(relativePath)
  }, [])

  const reconcileSelectedArtifact = useCallback(
    (repositoryId: string, nextWorkspace: RepositoryWorkspaceProjection) => {
      const artifactPaths = getAvailableArtifactPaths(nextWorkspace.artifactInventory)
      const rememberedPath = selectedArtifactPathsByRepository.current[repositoryId]

      if (rememberedPath && artifactPaths.includes(rememberedPath)) {
        setSelectedArtifactPath(rememberedPath)
        return
      }

      const nextPath = artifactPaths[0] ?? null
      setSelectedArtifactPath(nextPath)
      if (nextPath) {
        selectedArtifactPathsByRepository.current[repositoryId] = nextPath
      } else {
        delete selectedArtifactPathsByRepository.current[repositoryId]
      }
    },
    [],
  )

  const loadWorkspace = useCallback(async (repositoryId: string) => {
    setIsWorkspaceLoading(true)
    setError(null)
    try {
      const nextWorkspace = await invoke<RepositoryWorkspaceProjection>(
        'get_repository_workspace',
        { repositoryId },
      )
      setWorkspace(nextWorkspace)
      setExecutionContext(null)
      reconcileSelectedArtifact(repositoryId, nextWorkspace)
    } catch (loadError) {
      setWorkspace(null)
      setSelectedArtifactPath(null)
      setSelectedMilestonePath(null)
      setExecutionContext(null)
      setError(formatError(loadError))
    } finally {
      setIsWorkspaceLoading(false)
    }
  }, [reconcileSelectedArtifact])

  const loadGitStatus = useCallback(async (repositoryId: string) => {
    setIsGitStatusLoading(true)
    try {
      const status = await invoke<RepositoryGitStatus>('get_git_status', { repositoryId })
      setGitStatus(status)
    } catch (statusError) {
      setGitStatus(null)
      setError(formatError(statusError))
    } finally {
      setIsGitStatusLoading(false)
    }
  }, [])

  const loadCommitPreparation = useCallback(async (sessionId: string) => {
    setIsCommitPreparationLoading(true)
    try {
      const preparation = await invoke<CommitPreparation>('prepare_commit', { sessionId })
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
      const summary = await invoke<ExecutionSessionSummary>('commit_execution', {
        sessionId: executionSessionId,
        message: commitMessage.trim(),
        selectedPaths: selectedCommitScopeItems.map((item) => item.path),
        statusSnapshotId: commitPreparation.statusSnapshot.id,
      })
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
      const summary = await invoke<ExecutionSessionSummary>('push_execution', {
        sessionId: executionSessionId,
      })
      setMessage(
        summary.pushedCommitSha
          ? `Pushed ${summary.pushedCommitSha}. Repository is ready.`
          : 'Push completed. Repository is ready.',
      )
      await loadRepositories()
      if (selectedRepository) {
        const nextWorkspace = await invoke<RepositoryWorkspaceProjection>(
          'refresh_repository_workspace',
          { repositoryId: selectedRepository.repository.id },
        )
        setWorkspace(nextWorkspace)
        setExecutionContext(null)
        reconcileSelectedArtifact(selectedRepository.repository.id, nextWorkspace)
        await loadGitStatus(selectedRepository.repository.id)
      }
    } catch (pushError) {
      setError(formatError(pushError))
    } finally {
      setIsPushing(false)
    }
  }

  const loadRepositories = useCallback(async () => {
    setIsLoading(true)
    setError(null)
    try {
      const nextRepositories = await invoke<RepositoryDashboardProjection[]>(
        'list_repositories',
      )
      setRepositories(nextRepositories)
      setSelectedRepositoryId((currentId) => {
        if (nextRepositories.length === 0) {
          return null
        }

        if (
          currentId &&
          nextRepositories.some((entry) => entry.repository.id === currentId)
        ) {
          return currentId
        }

        return nextRepositories[0].repository.id
      })
    } catch (loadError) {
      setError(formatError(loadError))
    } finally {
      setIsLoading(false)
    }
  }, [])

  async function addRepository() {
    setIsAdding(true)
    setError(null)
    setMessage(null)
    try {
      const selectedPath = await invoke<string | null>('select_repository_directory')

      if (!selectedPath) {
        return
      }

      await invoke('register_repository', { path: selectedPath })
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
      await invoke('remove_repository', { repositoryId: repository.id })
      setMessage('Repository registration removed.')
      delete selectedArtifactPathsByRepository.current[repository.id]
      setWorkspace(null)
      setSelectedArtifactPath(null)
      setSelectedMilestonePath(null)
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

    setIsWorkspaceLoading(true)
    setError(null)
    setMessage(null)
    try {
      const nextWorkspace = await invoke<RepositoryWorkspaceProjection>(
        'refresh_repository_workspace',
        { repositoryId: selectedRepository.repository.id },
      )
      setWorkspace(nextWorkspace)
      setExecutionContext(null)
      reconcileSelectedArtifact(selectedRepository.repository.id, nextWorkspace)
      setMessage('Workspace refreshed.')
      await loadRepositories()
    } catch (refreshError) {
      setError(formatError(refreshError))
    } finally {
      setIsWorkspaceLoading(false)
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
      await invoke('save_artifact_content', {
        repositoryId: selectedRepository.repository.id,
        relativePath: selectedArtifact.relativePath,
        content: draftContent,
      })
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
      const command =
        selectedArtifact.family === 'Handoff'
          ? 'rotate_current_handoff'
          : 'rotate_current_decisions'
      const nextWorkspace = await invoke<RepositoryWorkspaceProjection>(command, {
        repositoryId: selectedRepository.repository.id,
      })
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

    setIsContextLoading(true)
    setError(null)
    setMessage(null)
    try {
      const context = await invoke<ExecutionContextPreview>('preview_execution_context', {
        repositoryId: selectedRepository.repository.id,
        milestonePath: selectedMilestonePath,
      })
      setExecutionContext(context)
      setMessage('Execution context built.')
    } catch (contextError) {
      setError(formatError(contextError))
    } finally {
      setIsContextLoading(false)
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
      const session = await invoke<ExecutionSessionSummary>('start_execution', {
        repositoryId: selectedRepository.repository.id,
        milestonePath: selectedMilestonePath,
      })
      setExecutionEventsBySession((currentEvents) => ({
        ...currentEvents,
        [session.sessionId]: currentEvents[session.sessionId] ?? [],
      }))
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

    setExecutionStatusesBySession((currentStatuses) => {
      const currentStatus = currentStatuses[summary.sessionId]
      if (!currentStatus) {
        return currentStatuses
      }

      return {
        ...currentStatuses,
        [summary.sessionId]: {
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
        },
      }
    })
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
      const summary = await invoke<ExecutionSessionSummary>('accept_execution_handoff', {
        sessionId: executionDisplay.sessionId,
      })
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
      const summary = await invoke<ExecutionSessionSummary>('reject_execution_handoff', {
        sessionId: executionDisplay.sessionId,
      })
      await refreshAfterHandoffDecision(summary, 'Handoff rejected. Repository is ready.')
    } catch (rejectError) {
      setError(formatError(rejectError))
    } finally {
      setIsRejectingHandoff(false)
    }
  }

  useEffect(() => {
    let isCurrent = true

    invoke<string>('get_backend_url')
      .then((url) => {
        if (isCurrent) {
          setBackendUrl(url.replace(/\/$/, ''))
        }
      })
      .catch(() => {
        if (isCurrent) {
          setBackendUrl('http://127.0.0.1:5000')
        }
      })

    return () => {
      isCurrent = false
    }
  }, [])

  useEffect(() => {
    if (!executionSessionId || !backendUrl || backendUrl === 'mock') {
      return
    }

    let isCurrent = true
    const statusUrl = `${backendUrl}/api/execution-sessions/${executionSessionId}/status`

    fetch(statusUrl)
      .then((response) => {
        if (!response.ok) {
          throw new Error(`execution status lookup failed with status ${response.status}`)
        }

        return response.json() as Promise<ExecutionStatus>
      })
      .then((status) => {
        if (!isCurrent) {
          return
        }

        setExecutionStatusesBySession((currentStatuses) => ({
          ...currentStatuses,
          [status.sessionId]: status,
        }))
        setExecutionEventsBySession((currentEvents) => ({
          ...currentEvents,
          [status.sessionId]: mergeExecutionEvents(
            currentEvents[status.sessionId] ?? [],
            status.recentEvents,
          ),
        }))
      })
      .catch((statusError) => {
        if (isCurrent) {
          setError(formatError(statusError))
        }
      })

    return () => {
      isCurrent = false
    }
  }, [backendUrl, executionSessionId])

  useEffect(() => {
    if (!executionSessionId || !backendUrl || backendUrl === 'mock') {
      return
    }

    const sessionId = executionSessionId
    const eventSource = new EventSource(
      `${backendUrl}/api/execution-sessions/${sessionId}/events/stream`,
    )

    eventSource.addEventListener('execution-event', (event) => {
      const executionEvent = JSON.parse(event.data) as ExecutionEvent
      setExecutionEventsBySession((currentEvents) => ({
        ...currentEvents,
        [sessionId]: mergeExecutionEvents(currentEvents[sessionId] ?? [], [executionEvent]),
      }))
      fetch(`${backendUrl}/api/execution-sessions/${sessionId}/status`)
        .then((response) => {
          if (!response.ok) {
            throw new Error(`execution status lookup failed with status ${response.status}`)
          }

          return response.json() as Promise<ExecutionStatus>
        })
        .then((status) => {
          setExecutionStatusesBySession((currentStatuses) => ({
            ...currentStatuses,
            [status.sessionId]: status,
          }))
          setExecutionEventsBySession((currentEvents) => ({
            ...currentEvents,
            [status.sessionId]: mergeExecutionEvents(
              currentEvents[status.sessionId] ?? [],
              status.recentEvents,
            ),
          }))
        })
        .catch(() => undefined)
    })

    eventSource.onerror = () => {
      if (eventSource.readyState === EventSource.CLOSED) {
        return
      }
    }

    return () => eventSource.close()
  }, [backendUrl, executionSessionId])

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
  }, [executionSummary, selectedExecutionStatus])

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
    const timeoutId = window.setTimeout(() => {
      void loadRepositories()
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [loadRepositories])

  useEffect(() => {
    if (!selectedRepository) {
      setGitStatus(null)
      return
    }

    const timeoutId = window.setTimeout(() => {
      void loadWorkspace(selectedRepository.repository.id)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [loadWorkspace, selectedRepository])

  useEffect(() => {
    if (!selectedRepository || !shouldShowGitWorkflow) {
      setGitStatus(null)
      setCommitPreparation(null)
      setSelectedCommitPaths(new Set())
      setCommitMessage('')
      return
    }

    const timeoutId = window.setTimeout(() => {
      if (currentExecutionState === 'AwaitingCommit' && executionSessionId) {
        setGitStatus(null)
        void loadCommitPreparation(executionSessionId)
        return
      }

      setCommitPreparation(null)
      setSelectedCommitPaths(new Set())
      setCommitMessage('')
      void loadGitStatus(selectedRepository.repository.id)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [
    currentExecutionState,
    executionSessionId,
    loadCommitPreparation,
    loadGitStatus,
    selectedRepository,
    shouldShowGitWorkflow,
  ])

  useEffect(() => {
    if (!selectedRepository || !selectedArtifactPath) {
      const timeoutId = window.setTimeout(() => {
        setArtifactContent('')
        setDraftContent('')
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    let isCurrent = true
    const timeoutId = window.setTimeout(() => {
      setIsArtifactLoading(true)
      setError(null)

      invoke<string>('load_artifact_content', {
        repositoryId: selectedRepository.repository.id,
        relativePath: selectedArtifactPath,
      })
        .then((content) => {
          if (!isCurrent) {
            return
          }

          setArtifactContent(content)
          setDraftContent(content)
        })
        .catch((loadError) => {
          if (isCurrent) {
            setError(formatError(loadError))
          }
        })
        .finally(() => {
          if (isCurrent) {
            setIsArtifactLoading(false)
          }
        })
    }, 0)

    return () => {
      isCurrent = false
      window.clearTimeout(timeoutId)
    }
  }, [selectedArtifactPath, selectedRepository])

  useEffect(() => {
    if (!selectedRepository || !canReviewGeneratedHandoff || !executionDisplay?.handoffPath) {
      setGeneratedHandoffContent('')
      setIsGeneratedHandoffLoading(false)
      return
    }

    let isCurrent = true
    setIsGeneratedHandoffLoading(true)
    setError(null)
    invoke<string>('load_artifact_content', {
      repositoryId: selectedRepository.repository.id,
      relativePath: executionDisplay.handoffPath,
    })
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
      setSelectedMilestonePath(null)
      return
    }

    const milestones = workspace.artifactInventory.milestones
    setSelectedMilestonePath((currentPath) => {
      if (currentPath && milestones.some((milestone) => milestone.relativePath === currentPath)) {
        return currentPath
      }

      return milestones[0]?.relativePath ?? null
    })
    setExecutionContext(null)
  }, [workspace])

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
                      onChange={(event) => setSelectedMilestonePath(event.target.value || null)}
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
                    <div className="context-summary">
                      <span>Generated: {new Date(executionContext.generatedAt).toLocaleString()}</span>
                      <span>Total: {executionContext.diagnostics.totalBytes} bytes</span>
                      <span>
                        Launch:{' '}
                        {canStartExecution ? 'Ready' : startExecutionBlockedReason}
                      </span>
                      <span>
                        Size:{' '}
                        {executionContext.diagnostics.hardLimitExceeded
                          ? 'Hard limit'
                          : executionContext.diagnostics.warningThresholdExceeded
                            ? 'Warning'
                            : 'Within limits'}
                      </span>
                    </div>

                    <div className="context-columns">
                      <div>
                        <h5>Artifacts</h5>
                        <ul>
                          {executionContext.artifacts.map((artifact) => (
                            <li key={artifact.relativePath}>
                              {artifact.role}: {artifact.relativePath} ({artifact.byteCount} bytes)
                            </li>
                          ))}
                        </ul>
                      </div>
                      <div>
                        <h5>Missing Optional</h5>
                        {executionContext.diagnostics.missingOptionalArtifacts.length === 0 ? (
                          <p>None</p>
                        ) : (
                          <ul>
                            {executionContext.diagnostics.missingOptionalArtifacts.map((path) => (
                              <li key={path}>{path}</li>
                            ))}
                          </ul>
                        )}
                      </div>
                      <div>
                        <h5>Validation</h5>
                        {executionContext.diagnostics.validationErrors.length === 0 ? (
                          <p>No validation errors</p>
                        ) : (
                          <ul>
                            {executionContext.diagnostics.validationErrors.map((validationError) => (
                              <li key={validationError}>{validationError}</li>
                            ))}
                          </ul>
                        )}
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
                          {renderPathBucket('Staged', executionContext.repositorySnapshot.dirtyState.stagedPaths)}
                          {renderPathBucket('Modified', executionContext.repositorySnapshot.dirtyState.modifiedPaths)}
                          {renderPathBucket('Added', executionContext.repositorySnapshot.dirtyState.addedPaths)}
                          {renderPathBucket('Deleted', executionContext.repositorySnapshot.dirtyState.deletedPaths)}
                          {renderPathBucket('Renamed', executionContext.repositorySnapshot.dirtyState.renamedPaths)}
                          {renderPathBucket('Untracked', executionContext.repositorySnapshot.dirtyState.untrackedPaths)}
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
                          ? void loadGitStatus(selectedRepository.repository.id)
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
                        {renderPathBucket('Staged', gitStatus.dirtyState.stagedPaths)}
                        {renderPathBucket('Modified', gitStatus.dirtyState.modifiedPaths)}
                        {renderPathBucket('Added', gitStatus.dirtyState.addedPaths)}
                        {renderPathBucket('Deleted', gitStatus.dirtyState.deletedPaths)}
                        {renderPathBucket('Renamed', gitStatus.dirtyState.renamedPaths)}
                        {renderPathBucket('Untracked', gitStatus.dirtyState.untrackedPaths)}
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
                <section className="execution-session-panel" aria-label="Execution session">
                  <div>
                    <p className="eyebrow">
                      {executionDisplay.repositoryState === 'Executing' ? 'Active Execution' : 'Execution Session'}
                    </p>
                    <h4>{executionDisplay.milestonePath ?? 'Selected milestone'}</h4>
                  </div>
                  <div className="execution-session-grid">
                    <span>Session: {executionDisplay.sessionId}</span>
                    <span>Provider: {executionDisplay.providerName || 'Unknown'}</span>
                    <span>State: {executionDisplay.state}</span>
                    <span>Repository state: {executionStateLabels[executionDisplay.repositoryState]}</span>
                    <span>Started: {formatDateTime(executionDisplay.startedAt)}</span>
                    <span>Completed: {formatDateTime(executionDisplay.completedAt)}</span>
                    <span>Duration: {formatDuration(executionDisplay.duration)}</span>
                    <span>Accepted: {formatDateTime(executionDisplay.acceptedAt)}</span>
                    <span>Rejected: {formatDateTime(executionDisplay.rejectedAt)}</span>
                    <span>Last activity: {formatDateTime(executionDisplay.lastActivityAt)}</span>
                    <span>Provider start: {formatDateTime(executionDisplay.providerStartedAt)}</span>
                    <span>PID: {executionDisplay.providerProcessId ?? 'Not recorded'}</span>
                    <span>Executable: {executionDisplay.providerExecutablePath || 'Not recorded'}</span>
                    <span>Handoff: {executionDisplay.handoffPath || 'Not recorded'}</span>
                    <span>Commit: {executionDisplay.commitSha || 'Not recorded'}</span>
                    <span>Committed: {formatDateTime(executionDisplay.committedAt)}</span>
                    <span>Pushed: {formatDateTime(executionDisplay.pushedAt)}</span>
                    <span>Pushed commit: {executionDisplay.pushedCommitSha || 'Not recorded'}</span>
                    {executionDisplay.failureReason ? (
                      <span className="execution-failure">Failure: {executionDisplay.failureReason}</span>
                    ) : null}
                  </div>
                </section>
              ) : null}

              {selectedExecutionHistory.length > 0 ? (
                <section className="execution-history-panel" aria-label="Execution history">
                  <div>
                    <p className="eyebrow">Session History</p>
                    <h4>{selectedExecutionHistory.length} recent sessions</h4>
                  </div>
                  <div className="execution-history-list">
                    {selectedExecutionHistory.map((session) => (
                      <div className="execution-history-row" key={session.sessionId}>
                        <span>{session.milestonePath ?? 'Milestone not recorded'}</span>
                        <small>{executionStateLabels[session.repositoryState]}</small>
                        <small>Started {formatDateTime(session.startedAt)}</small>
                        <small>Duration {formatDuration(session.duration)}</small>
                        <small>Commit {session.commitSha ?? 'Not recorded'}</small>
                        <small>Push {session.pushedAt ? formatDateTime(session.pushedAt) : 'Not recorded'}</small>
                      </div>
                    ))}
                  </div>
                </section>
              ) : null}

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

              {executionDisplay ? (
                <section className="execution-output-panel" aria-label="Execution output">
                  <div>
                    <p className="eyebrow">Execution Output</p>
                    <h4>{selectedExecutionEvents.length} events</h4>
                  </div>
                  <div className="execution-event-feed">
                    {selectedExecutionEvents.length === 0 ? (
                      <p className="empty-state">No execution events recorded.</p>
                    ) : (
                      selectedExecutionEvents.map((executionEvent) => (
                        <div className="execution-event-row" key={executionEvent.sequence}>
                          <span className="execution-event-sequence">#{executionEvent.sequence}</span>
                          <span className="execution-event-time">
                            {formatDateTime(executionEvent.timestamp)}
                          </span>
                          <span className="execution-event-type">{executionEvent.type}</span>
                          <pre>{executionEvent.message}</pre>
                        </div>
                      ))
                    )}
                  </div>
                </section>
              ) : null}
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
