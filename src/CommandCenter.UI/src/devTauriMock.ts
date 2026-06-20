type InvokeArgs = Record<string, unknown> | undefined

type Repository = {
  id: string
  name: string
  path: string
}

type Artifact = {
  relativePath: string
  name: string
  type: string
  family: string
  versionKind: string
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

type Workspace = {
  repository: Repository
  availability: string
  readiness: string
  executionState: string
  executionSummary: ExecutionSessionSummary | null
  artifactInventory: ArtifactInventory
  milestoneCount: number
  hasPlan: boolean
  hasOperationalContext: boolean
  hasCurrentHandoff: boolean
  hasCurrentDecisions: boolean
}

type DashboardEntry = {
  repository: Repository
  availability: string
  readiness: string
  executionState: string
  activeExecutionSession: ExecutionSessionSummary | null
  executionSummary: ExecutionSessionSummary | null
  milestoneCount: number
  hasCurrentHandoff: boolean
  hasCurrentDecisions: boolean
}

type ExecutionSessionSummary = {
  sessionId: string
  state: string
  repositoryState: string
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
  failureReason: string | null
}

type ExecutionSession = ExecutionSessionSummary & {
  id: string
  repositoryId: string
  repositoryPath: string
}

type ExecutionContextPreview = {
  repositoryId: string
  repositoryName: string
  repositoryPath: string
  milestonePath: string
  generatedAt: string
  artifacts: Array<{
    role: string
    relativePath: string
    name: string
    content: string
    byteCount: number
    characterCount: number
  }>
  repositorySnapshot: {
    branch: string
    dirtyState: {
      stagedPaths: string[]
      modifiedPaths: string[]
      addedPaths: string[]
      deletedPaths: string[]
      renamedPaths: string[]
      untrackedPaths: string[]
      isClean: boolean
    }
    capturedAt: string
  }
  diagnostics: {
    totalBytes: number
    totalCharacters: number
    warningThresholdBytes: number
    hardLimitBytes: number
    warningThresholdExceeded: boolean
    hardLimitExceeded: boolean
    artifactDiagnostics: Array<{
      role: string
      relativePath: string
      byteCount: number
      characterCount: number
      warningThresholdBytes: number
      hardLimitBytes: number
      warningThresholdExceeded: boolean
      hardLimitExceeded: boolean
    }>
    validationErrors: string[]
    missingOptionalArtifacts: string[]
    launchBlocked: boolean
  }
}

type RepositoryGitStatus = {
  branch: string
  aheadCount: number
  behindCount: number
  dirtyState: {
    stagedPaths: string[]
    modifiedPaths: string[]
    addedPaths: string[]
    deletedPaths: string[]
    renamedPaths: string[]
    untrackedPaths: string[]
    isClean: boolean
  }
  capturedAt: string
}

type CommitPreparation = {
  id: string
  sessionId: string
  repositoryId: string
  repositoryPath: string
  proposedMessage: string
  scopeItems: Array<{
    path: string
    changeType: string
    origin: string
    isSelected: boolean
  }>
  statusSnapshot: RepositoryGitStatus & {
    id: string
  }
  generatedAt: string
  hasPreExistingChanges: boolean
}

type MockState = {
  repositories: Repository[]
  workspaces: Record<string, Workspace>
  content: Record<string, string>
  sessions: Record<string, ExecutionSession>
}

type TauriInternals = {
  invoke: (cmd: string, args?: InvokeArgs) => Promise<unknown>
  transformCallback: (callback: unknown) => number
  unregisterCallback: () => void
  callbacks: Record<string, unknown>
  convertFileSrc: (filePath: string) => string
}

declare global {
  interface Window {
    __TAURI_INTERNALS__?: TauriInternals
    __COMMAND_CENTER_MOCK_STATE__?: MockState
  }
}

const alphaRepository: Repository = {
  id: 'repo-alpha',
  name: 'AlphaRepo',
  path: 'C:\\workspace\\AlphaRepo',
}

const emptyRepository: Repository = {
  id: 'repo-empty',
  name: 'EmptyRepo',
  path: 'C:\\workspace\\EmptyRepo',
}

const planOnlyRepository: Repository = {
  id: 'repo-plan-only',
  name: 'PlanOnlyRepo',
  path: 'C:\\workspace\\PlanOnlyRepo',
}

const artifacts = {
  plan: artifact('.agents/plan.md', 'plan.md', 'Plan', 'Plan', 'Current'),
  context: artifact(
    '.agents/operational_context.md',
    'operational_context.md',
    'OperationalContext',
    'OperationalContext',
    'Current',
  ),
  milestone: artifact('.agents/milestones/m5.md', 'm5.md', 'Milestone', 'Milestone', 'Current'),
  handoff: artifact('.agents/handoffs/handoff.md', 'handoff.md', 'Handoff', 'Handoff', 'Current'),
  oldHandoff: artifact(
    '.agents/handoffs/handoff.0001.md',
    'handoff.0001.md',
    'Handoff',
    'Handoff',
    'Historical',
  ),
  decisions: artifact(
    '.agents/decisions/decisions.md',
    'decisions.md',
    'Decision',
    'Decision',
    'Current',
  ),
}

function artifact(
  relativePath: string,
  name: string,
  type: string,
  family: string,
  versionKind: string,
): Artifact {
  return { relativePath, name, type, family, versionKind }
}

function createWorkspace(repository: Repository, inventory: ArtifactInventory): Workspace {
  const readiness = inventory.plan
    ? inventory.milestones.length > 0
      ? 'Ready'
      : 'MissingMilestones'
    : 'MissingPlan'

  return {
    repository,
    availability: 'Available',
    readiness,
    executionState: 'Ready',
    executionSummary: null,
    artifactInventory: inventory,
    milestoneCount: inventory.milestones.length,
    hasPlan: inventory.plan !== null,
    hasOperationalContext: inventory.operationalContext !== null,
    hasCurrentHandoff: inventory.currentHandoff !== null,
    hasCurrentDecisions: inventory.currentDecisions !== null,
  }
}

function createInitialState(): MockState {
  return {
    repositories: [alphaRepository, emptyRepository, planOnlyRepository],
    workspaces: {
      [alphaRepository.id]: createWorkspace(alphaRepository, {
        plan: artifacts.plan,
        operationalContext: artifacts.context,
        milestones: [artifacts.milestone],
        currentHandoff: artifacts.handoff,
        historicalHandoffs: [artifacts.oldHandoff],
        currentDecisions: artifacts.decisions,
        historicalDecisions: [],
      }),
      [emptyRepository.id]: createWorkspace(emptyRepository, {
        plan: null,
        operationalContext: null,
        milestones: [],
        currentHandoff: null,
        historicalHandoffs: [],
        currentDecisions: null,
        historicalDecisions: [],
      }),
      [planOnlyRepository.id]: createWorkspace(planOnlyRepository, {
        plan: artifacts.plan,
        operationalContext: null,
        milestones: [],
        currentHandoff: null,
        historicalHandoffs: [],
        currentDecisions: null,
        historicalDecisions: [],
      }),
    },
    content: {
      [artifacts.plan.relativePath]: '# Plan\n\nInitial plan content.',
      [artifacts.context.relativePath]: '# Operational Context\n\nContext content.',
      [artifacts.milestone.relativePath]: '# M5\n\nWorkspace experience milestone.',
      [artifacts.handoff.relativePath]: '# Handoff\n\nCurrent handoff content.',
      [artifacts.oldHandoff.relativePath]: '# Historical Handoff\n\nArchived content.',
      [artifacts.decisions.relativePath]: '# Decisions\n\nCurrent decisions content.',
    },
    sessions: {},
  }
}

function dashboardEntry(workspace: Workspace): DashboardEntry {
  return {
    repository: workspace.repository,
    availability: workspace.availability,
    readiness: workspace.readiness,
    executionState: workspace.executionState,
    activeExecutionSession: workspace.executionSummary,
    executionSummary: workspace.executionSummary,
    milestoneCount: workspace.milestoneCount,
    hasCurrentHandoff: workspace.hasCurrentHandoff,
    hasCurrentDecisions: workspace.hasCurrentDecisions,
  }
}

function createContextPreview(state: MockState, repositoryId: string, milestonePath: string): ExecutionContextPreview {
  const workspace = state.workspaces[repositoryId]
  const artifactsForContext = [
    workspace.artifactInventory.plan,
    workspace.artifactInventory.milestones.find((milestone) => milestone.relativePath === milestonePath) ?? null,
    workspace.artifactInventory.currentHandoff,
    workspace.artifactInventory.currentDecisions,
  ].filter((artifact): artifact is Artifact => artifact !== null)
  const artifactsWithContent = artifactsForContext.map((artifact) => {
    const content = state.content[artifact.relativePath] ?? ''
    return {
      role: artifact.type,
      relativePath: artifact.relativePath,
      name: artifact.name,
      content,
      byteCount: content.length,
      characterCount: content.length,
    }
  })
  const totalBytes = artifactsWithContent.reduce((total, artifact) => total + artifact.byteCount, 0)

  return {
    repositoryId,
    repositoryName: workspace.repository.name,
    repositoryPath: workspace.repository.path,
    milestonePath,
    generatedAt: new Date().toISOString(),
    artifacts: artifactsWithContent,
    repositorySnapshot: {
      branch: 'main',
      dirtyState: {
        stagedPaths: [],
        modifiedPaths: [],
        addedPaths: [],
        deletedPaths: [],
        renamedPaths: [],
        untrackedPaths: [],
        isClean: true,
      },
      capturedAt: new Date().toISOString(),
    },
    diagnostics: {
      totalBytes,
      totalCharacters: totalBytes,
      warningThresholdBytes: 131072,
      hardLimitBytes: 524288,
      warningThresholdExceeded: false,
      hardLimitExceeded: false,
      artifactDiagnostics: artifactsWithContent.map((artifact) => ({
        role: artifact.role,
        relativePath: artifact.relativePath,
        byteCount: artifact.byteCount,
        characterCount: artifact.characterCount,
        warningThresholdBytes: 98304,
        hardLimitBytes: 262144,
        warningThresholdExceeded: false,
        hardLimitExceeded: false,
      })),
      validationErrors: workspace.readiness === 'Ready' ? [] : [`Repository planning readiness is ${workspace.readiness}.`],
      missingOptionalArtifacts: [
        workspace.artifactInventory.currentHandoff ? null : '.agents/handoffs/handoff.md',
        workspace.artifactInventory.currentDecisions ? null : '.agents/decisions/decisions.md',
      ].filter((path): path is string => path !== null),
      launchBlocked: workspace.readiness !== 'Ready',
    },
  }
}

function createGitStatus(state: MockState, repositoryId: string): RepositoryGitStatus {
  const workspace = state.workspaces[repositoryId]
  const hasAcceptedWork = workspace.executionState === 'AwaitingCommit'
  return {
    branch: 'main',
    aheadCount: workspace.executionState === 'AwaitingPush' ? 1 : 0,
    behindCount: 0,
    dirtyState: {
      stagedPaths: [],
      modifiedPaths: hasAcceptedWork ? ['src/CommandCenter.UI/src/App.tsx'] : [],
      addedPaths: [],
      deletedPaths: [],
      renamedPaths: [],
      untrackedPaths: hasAcceptedWork ? ['.agents/handoffs/handoff.md'] : [],
      isClean: !hasAcceptedWork,
    },
    capturedAt: new Date().toISOString(),
  }
}

function createCommitPreparation(state: MockState, sessionId: string): CommitPreparation {
  const session = state.sessions[sessionId]
  if (!session) {
    throw new Error('Execution session was not found.')
  }

  if (session.repositoryState !== 'AwaitingCommit') {
    throw new Error('Commit can only be prepared while awaiting commit.')
  }

  const status = createGitStatus(state, session.repositoryId)
  return {
    id: `prep-${sessionId}`,
    sessionId,
    repositoryId: session.repositoryId,
    repositoryPath: session.repositoryPath,
    proposedMessage: 'm5\n\n- 2 files changed',
    scopeItems: [
      {
        path: 'src/CommandCenter.UI/src/App.tsx',
        changeType: 'Modified',
        origin: 'ExecutionGenerated',
        isSelected: true,
      },
      {
        path: '.agents/handoffs/handoff.md',
        changeType: 'Untracked',
        origin: 'ExecutionGenerated',
        isSelected: true,
      },
    ],
    statusSnapshot: {
      ...status,
      id: `snapshot-${sessionId}`,
    },
    generatedAt: new Date().toISOString(),
    hasPreExistingChanges: false,
  }
}

function clone<T>(value: T): T {
  return structuredClone(value)
}

function getStringArg(args: InvokeArgs, name: string): string {
  const value = args?.[name]
  if (typeof value !== 'string') {
    throw new Error(`Missing string argument: ${name}`)
  }

  return value
}

function rotateCurrentArtifact(
  state: MockState,
  repositoryId: string,
  currentKey: 'currentHandoff' | 'currentDecisions',
  historicalKey: 'historicalHandoffs' | 'historicalDecisions',
  filePrefix: 'handoff' | 'decisions',
) {
  const workspace = state.workspaces[repositoryId]
  const currentArtifact = workspace.artifactInventory[currentKey]
  if (!currentArtifact) {
    throw new Error(`No current ${filePrefix} artifact exists.`)
  }

  const nextIndex = workspace.artifactInventory[historicalKey].length + 1
  const historicalArtifact = {
    ...currentArtifact,
    relativePath: currentArtifact.relativePath.replace(
      `${filePrefix}.md`,
      `${filePrefix}.${String(nextIndex).padStart(4, '0')}.md`,
    ),
    name: `${filePrefix}.${String(nextIndex).padStart(4, '0')}.md`,
    versionKind: 'Historical',
  }

  workspace.artifactInventory[historicalKey] = [
    historicalArtifact,
    ...workspace.artifactInventory[historicalKey],
  ]
  state.content[historicalArtifact.relativePath] = state.content[currentArtifact.relativePath]
}

function startExecution(state: MockState, repositoryId: string, milestonePath: string): ExecutionSessionSummary {
  const workspace = state.workspaces[repositoryId]
  if (!workspace) {
    throw new Error(`Repository was not found: ${repositoryId}`)
  }

  if (workspace.executionState === 'Executing' || workspace.executionSummary) {
    throw new Error('Repository already has an active execution session.')
  }

  const context = createContextPreview(state, repositoryId, milestonePath)
  if (context.diagnostics.launchBlocked) {
    throw new Error('Execution launch is blocked.')
  }

  const timestamp = new Date().toISOString()
  const sessionId = `session-${Object.keys(state.sessions).length + 1}`
  const summary: ExecutionSessionSummary = {
    sessionId,
    state: 'Completed',
    repositoryState: 'AwaitingAcceptance',
    milestonePath,
    startedAt: timestamp,
    completedAt: timestamp,
    duration: '00:00:01',
    acceptedAt: null,
    rejectedAt: null,
    decisionNote: null,
    lastActivityAt: timestamp,
    providerName: 'Fake',
    providerExecutablePath: 'fake-provider',
    providerProcessId: null,
    providerStartedAt: timestamp,
    handoffPath: artifacts.handoff.relativePath,
    failureReason: null,
  }
  state.content[artifacts.handoff.relativePath] = [
    '# Generated Handoff',
    '',
    'Mock execution completed and produced this handoff for review.',
  ].join('\n')
  state.sessions[sessionId] = {
    ...summary,
    id: sessionId,
    repositoryId,
    repositoryPath: workspace.repository.path,
  }
  workspace.executionState = 'AwaitingAcceptance'
  workspace.executionSummary = summary
  return summary
}

function decideHandoff(
  state: MockState,
  sessionId: string,
  decision: 'accept' | 'reject',
): ExecutionSessionSummary {
  const session = state.sessions[sessionId]
  if (!session) {
    throw new Error('Execution session was not found.')
  }

  if (session.repositoryState !== 'AwaitingAcceptance') {
    throw new Error('Execution can only be decided while awaiting acceptance.')
  }

  const workspace = state.workspaces[session.repositoryId]
  const timestamp = new Date().toISOString()
  const repositoryState = decision === 'accept' ? 'AwaitingCommit' : 'Ready'
  const summary: ExecutionSessionSummary = {
    ...session,
    repositoryState,
    acceptedAt: decision === 'accept' ? timestamp : null,
    rejectedAt: decision === 'reject' ? timestamp : null,
    decisionNote: null,
    lastActivityAt: timestamp,
  }

  state.sessions[sessionId] = {
    ...summary,
    id: session.id,
    repositoryId: session.repositoryId,
    repositoryPath: session.repositoryPath,
  }
  workspace.executionState = repositoryState
  workspace.executionSummary = summary
  return summary
}

export function installDevTauriMock() {
  const searchParams = new URLSearchParams(window.location.search)
  if (searchParams.get('mock') !== 'workspace-certification') {
    return
  }

  const state = createInitialState()
  window.__COMMAND_CENTER_MOCK_STATE__ = state
  window.__TAURI_INTERNALS__ = {
    callbacks: {},
    convertFileSrc: (filePath: string) => filePath,
    transformCallback: () => 0,
    unregisterCallback: () => undefined,
    invoke: async (cmd: string, args?: InvokeArgs) => {
      switch (cmd) {
        case 'get_backend_url':
          return 'mock'
        case 'list_repositories':
          return clone(state.repositories.map((repository) => dashboardEntry(state.workspaces[repository.id])))
        case 'get_repository_workspace':
        case 'refresh_repository_workspace':
          return clone(state.workspaces[getStringArg(args, 'repositoryId')])
        case 'preview_execution_context':
          return clone(
            createContextPreview(
              state,
              getStringArg(args, 'repositoryId'),
              getStringArg(args, 'milestonePath'),
            ),
          )
        case 'start_execution':
          return clone(
            startExecution(
              state,
              getStringArg(args, 'repositoryId'),
              getStringArg(args, 'milestonePath'),
            ),
          )
        case 'get_active_execution': {
          const workspace = state.workspaces[getStringArg(args, 'repositoryId')]
          if (!workspace?.executionSummary) {
            throw new Error('No active execution session.')
          }

          return clone(workspace.executionSummary)
        }
        case 'get_git_status':
          return clone(createGitStatus(state, getStringArg(args, 'repositoryId')))
        case 'prepare_commit':
          return clone(createCommitPreparation(state, getStringArg(args, 'sessionId')))
        case 'get_execution_session': {
          const session = state.sessions[getStringArg(args, 'sessionId')]
          if (!session) {
            throw new Error('Execution session was not found.')
          }

          return clone(session)
        }
        case 'accept_execution_handoff':
          return clone(decideHandoff(state, getStringArg(args, 'sessionId'), 'accept'))
        case 'reject_execution_handoff':
          return clone(decideHandoff(state, getStringArg(args, 'sessionId'), 'reject'))
        case 'load_artifact_content':
          return state.content[getStringArg(args, 'relativePath')] ?? ''
        case 'save_artifact_content':
          state.content[getStringArg(args, 'relativePath')] = getStringArg(args, 'content')
          return undefined
        case 'rotate_current_handoff': {
          const repositoryId = getStringArg(args, 'repositoryId')
          rotateCurrentArtifact(state, repositoryId, 'currentHandoff', 'historicalHandoffs', 'handoff')
          return clone(state.workspaces[repositoryId])
        }
        case 'rotate_current_decisions': {
          const repositoryId = getStringArg(args, 'repositoryId')
          rotateCurrentArtifact(
            state,
            repositoryId,
            'currentDecisions',
            'historicalDecisions',
            'decisions',
          )
          return clone(state.workspaces[repositoryId])
        }
        case 'remove_repository': {
          const repositoryId = getStringArg(args, 'repositoryId')
          state.repositories = state.repositories.filter((repository) => repository.id !== repositoryId)
          return undefined
        }
        default:
          throw new Error(`Unhandled mock command: ${cmd}`)
      }
    },
  }
}
