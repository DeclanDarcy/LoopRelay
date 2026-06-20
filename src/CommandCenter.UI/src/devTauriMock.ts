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
  milestoneCount: number
  hasCurrentHandoff: boolean
  hasCurrentDecisions: boolean
}

type MockState = {
  repositories: Repository[]
  workspaces: Record<string, Workspace>
  content: Record<string, string>
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
  }
}

function dashboardEntry(workspace: Workspace): DashboardEntry {
  return {
    repository: workspace.repository,
    availability: workspace.availability,
    readiness: workspace.readiness,
    milestoneCount: workspace.milestoneCount,
    hasCurrentHandoff: workspace.hasCurrentHandoff,
    hasCurrentDecisions: workspace.hasCurrentDecisions,
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
        case 'list_repositories':
          return clone(state.repositories.map((repository) => dashboardEntry(state.workspaces[repository.id])))
        case 'get_repository_workspace':
        case 'refresh_repository_workspace':
          return clone(state.workspaces[getStringArg(args, 'repositoryId')])
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
