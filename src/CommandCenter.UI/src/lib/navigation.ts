import type {
  ContinuityDiagnostics,
  ExecutionSessionSummary,
  NavigationDestinationClassification,
  NavigationTarget,
  OperationalContextItem,
  RepositoryDashboardProjection,
  RepositoryWorkspaceProjection,
} from '../types'
import type { PrimaryWorkspaceTab } from '../state/shellState'

export type WorkspaceTabDefinition = {
  id: PrimaryWorkspaceTab
  label: string
  classification: NavigationDestinationClassification
}

export type NavigationSectionTargetDefinition = Pick<
  NavigationTarget,
  'id' | 'kind' | 'group' | 'label' | 'description' | 'classification' | 'tab' | 'sectionId'
>

export type GlobalNavigationItemDefinition = {
  id: string
  label: string
  classification: NavigationDestinationClassification
}

export const workspaceTabDefinitions: WorkspaceTabDefinition[] = [
  { id: 'workspace', label: 'Workspace', classification: 'primary' },
  { id: 'execution', label: 'Execution', classification: 'primary' },
  { id: 'operational-context', label: 'Operational Context', classification: 'primary' },
  { id: 'governance', label: 'Governance', classification: 'primary' },
  { id: 'decisions', label: 'Decisions', classification: 'primary' },
  { id: 'reasoning', label: 'Reasoning', classification: 'primary' },
  { id: 'continuity', label: 'Continuity', classification: 'primary' },
]

export const globalNavigationItems: GlobalNavigationItemDefinition[] = [
  {
    id: 'repositories',
    label: 'Repositories',
    classification: 'primary',
  },
]

export const navigationSectionTargets: NavigationSectionTargetDefinition[] = [
  {
    id: 'section-artifacts',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Repository Artifacts',
    description: 'Open the artifact explorer and editor.',
    classification: 'contextual',
    tab: 'workspace',
    sectionId: 'artifact-workspace',
  },
  {
    id: 'section-workspace-execution-context',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Workspace Execution Context',
    description: 'Open the workspace execution context panel.',
    classification: 'contextual',
    tab: 'workspace',
    sectionId: 'workspace-execution-context',
  },
  {
    id: 'section-workspace-milestones',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Workspace Milestones',
    description: 'Open the milestone selector.',
    classification: 'contextual',
    tab: 'workspace',
    sectionId: 'workspace-milestones',
  },
  {
    id: 'section-workspace-inspector',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Workspace Inspector',
    description: 'Open the repository inspector rail.',
    classification: 'contextual',
    tab: 'workspace',
    sectionId: 'workspace-inspector',
  },
  {
    id: 'section-execution-events',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Execution Stream',
    description: 'Open the full execution event stream.',
    classification: 'contextual',
    tab: 'execution',
    sectionId: 'execution-events',
  },
  {
    id: 'section-execution-context',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Execution Context Diagnostics',
    description: 'Open the execution workspace context diagnostics.',
    classification: 'contextual',
    tab: 'execution',
    sectionId: 'execution-context',
  },
  {
    id: 'section-generated-handoff-review',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Generated Handoff Review',
    description: 'Open the handoff review panel when available.',
    classification: 'contextual',
    tab: 'execution',
    sectionId: 'generated-handoff-review',
  },
  {
    id: 'section-git-workflow',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Git Workflow',
    description: 'Open commit and push evidence.',
    classification: 'contextual',
    tab: 'execution',
    sectionId: 'git-workflow',
  },
  {
    id: 'section-operational-current',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Current Understanding',
    description: 'Open the operational context summary.',
    classification: 'contextual',
    tab: 'operational-context',
    sectionId: 'operational-current',
  },
  {
    id: 'section-proposal-review',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Proposal Review',
    description: 'Open operational-context proposal review.',
    classification: 'contextual',
    tab: 'operational-context',
    sectionId: 'proposal-review',
  },
  {
    id: 'section-operational-open-questions',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Open Questions',
    description: 'Open operational-context open questions.',
    classification: 'contextual',
    tab: 'operational-context',
    sectionId: 'operational-open-questions',
  },
  {
    id: 'section-operational-architecture',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Architecture',
    description: 'Open operational-context architecture.',
    classification: 'contextual',
    tab: 'operational-context',
    sectionId: 'operational-architecture',
  },
  {
    id: 'section-operational-constraints',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Constraints',
    description: 'Open operational-context constraints.',
    classification: 'contextual',
    tab: 'operational-context',
    sectionId: 'operational-constraints',
  },
  {
    id: 'section-operational-active-risks',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Active Risks',
    description: 'Open operational-context active risks.',
    classification: 'contextual',
    tab: 'operational-context',
    sectionId: 'operational-active-risks',
  },
  {
    id: 'section-operational-decision-rationale',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Decision Rationale',
    description: 'Open operational-context decision rationale.',
    classification: 'contextual',
    tab: 'operational-context',
    sectionId: 'operational-decision-rationale',
  },
  {
    id: 'section-operational-stable-decisions',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Stable Decisions',
    description: 'Open operational-context stable decisions.',
    classification: 'contextual',
    tab: 'operational-context',
    sectionId: 'operational-stable-decisions',
  },
  {
    id: 'section-governance-lifecycle',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Governance Lifecycle',
    description: 'Open governance lifecycle, eligibility, and recovery.',
    classification: 'contextual',
    tab: 'governance',
    sectionId: 'governance-workspace',
  },
  {
    id: 'section-decision-lifecycle',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Decision Lifecycle',
    description: 'Open the decision review workspace.',
    classification: 'contextual',
    tab: 'decisions',
    sectionId: 'decision-lifecycle',
  },
  {
    id: 'section-reasoning-trajectory',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Reasoning Trajectory',
    description: 'Open reasoning trajectory history.',
    classification: 'contextual',
    tab: 'reasoning',
    sectionId: 'reasoning-trajectory',
  },
  {
    id: 'section-reasoning-event-feed',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Reasoning Event Feed',
    description: 'Open immutable reasoning events.',
    classification: 'contextual',
    tab: 'reasoning',
    sectionId: 'reasoning-event-feed',
  },
  {
    id: 'section-reasoning-thread-view',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Reasoning Threads',
    description: 'Open reasoning thread navigation.',
    classification: 'contextual',
    tab: 'reasoning',
    sectionId: 'reasoning-thread-view',
  },
  {
    id: 'section-reasoning-materialization-review',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Reasoning Materialization Review',
    description: 'Open reasoning materialization review.',
    classification: 'contextual',
    tab: 'reasoning',
    sectionId: 'reasoning-materialization-review',
  },
  {
    id: 'section-continuity-diagnostics',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Continuity Diagnostics',
    description: 'Open continuity diagnostics.',
    classification: 'contextual',
    tab: 'continuity',
    sectionId: 'continuity-diagnostics',
  },
  {
    id: 'section-continuity-warnings',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Continuity Warnings',
    description: 'Open continuity warnings.',
    classification: 'contextual',
    tab: 'continuity',
    sectionId: 'continuity-warnings',
  },
  {
    id: 'section-continuity-compression',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Compression Trend',
    description: 'Open continuity compression trend.',
    classification: 'contextual',
    tab: 'continuity',
    sectionId: 'continuity-compression',
  },
  {
    id: 'section-continuity-decision-retention',
    kind: 'section',
    group: 'Inspector Sections',
    label: 'Decision Retention',
    description: 'Open continuity decision retention.',
    classification: 'contextual',
    tab: 'continuity',
    sectionId: 'continuity-decision-retention',
  },
]

type BuildNavigationTargetsInput = {
  repositories: RepositoryDashboardProjection[]
  selectedRepositoryId: string | null
  workspace: RepositoryWorkspaceProjection | null
  executionHistory: ExecutionSessionSummary[]
  continuityDiagnostics: ContinuityDiagnostics | null
}

type NavigationTargetInput = {
  id: string
  kind: NavigationTarget['kind']
  group: string
  label: string
  description: string
  classification?: NavigationDestinationClassification
  repositoryId?: string | null
  tab?: PrimaryWorkspaceTab | null
  sectionId?: string | null
  artifactPath?: string | null
  milestonePath?: string | null
}

export function getTabForSection(sectionId: string): PrimaryWorkspaceTab {
  return navigationSectionTargets.find((target) => target.sectionId === sectionId)?.tab ?? 'workspace'
}

export function buildNavigationTargets({
  repositories,
  selectedRepositoryId,
  workspace,
  executionHistory,
  continuityDiagnostics,
}: BuildNavigationTargetsInput): NavigationTarget[] {
  const targets: NavigationTarget[] = []
  const selectedRepository =
    repositories.find((entry) => entry.repository.id === selectedRepositoryId) ?? null

  repositories.forEach((entry) => {
    targets.push(
      createTarget({
        id: `repository-${entry.repository.id}`,
        kind: 'repository',
        group: 'Repositories',
        label: entry.repository.name,
        description: entry.repository.path,
        classification: 'primary',
        repositoryId: entry.repository.id,
      }),
    )

    workspaceTabDefinitions.forEach((tab) => {
      targets.push(
        createTarget({
          id: `repository-${entry.repository.id}-${tab.id}`,
          kind: 'workspace',
          group: 'Repository Workspaces',
          label: `${entry.repository.name} ${tab.label}`,
          description: `Open ${tab.label} for ${entry.repository.name}.`,
          classification: tab.classification,
          repositoryId: entry.repository.id,
          tab: tab.id,
        }),
      )
    })
  })

  if (!workspace || !selectedRepository) {
    return targets
  }

  workspaceTabDefinitions.forEach((tab) => {
    targets.push(
      createTarget({
        id: `current-repository-${tab.id}`,
        kind: 'workspace',
        group: 'Current Repository Workspaces',
        label: `${tab.label} tab`,
        description: `Open ${tab.label} for ${workspace.repository.name}.`,
        classification: tab.classification,
        repositoryId: workspace.repository.id,
        tab: tab.id,
      }),
    )
  })

  navigationSectionTargets.forEach((target) => {
    targets.push(
      createTarget({
        ...target,
        repositoryId: workspace.repository.id,
      }),
    )
  })

  workspace.artifactInventory.milestones.forEach((milestone) => {
    targets.push(
      createTarget({
        id: `milestone-${milestone.relativePath}`,
        kind: 'milestone',
        group: 'Milestones',
        label: milestone.name,
        description: milestone.relativePath,
        repositoryId: workspace.repository.id,
        tab: 'workspace',
        sectionId: 'workspace-execution-context',
        artifactPath: milestone.relativePath,
        milestonePath: milestone.relativePath,
      }),
    )
  })

  executionHistory.forEach((session) => {
    targets.push(
      createTarget({
        id: `execution-session-${session.sessionId}`,
        kind: 'execution-session',
        group: 'Execution Sessions',
        label: session.milestonePath ?? session.sessionId,
        description: `${session.repositoryState} / ${session.state}`,
        repositoryId: workspace.repository.id,
        tab: 'execution',
        sectionId: 'execution-events',
        milestonePath: session.milestonePath,
      }),
    )
  })

  const context = workspace.operationalContext
  addContextItemTargets(targets, workspace.repository.id, 'Stable Decisions', 'operational-stable-decisions', context.stableDecisions)
  addContextItemTargets(targets, workspace.repository.id, 'Open Questions', 'operational-open-questions', context.openQuestions)
  addContextItemTargets(targets, workspace.repository.id, 'Active Risks', 'operational-active-risks', context.activeRisks)

  if (workspace.operationalContextProposalSummary.pendingProposalExists) {
    targets.push(
      createTarget({
        id: 'discovery-pending-proposal',
        kind: 'discovery',
        group: 'Discovery',
        label: 'Pending proposal',
        description: workspace.operationalContextProposalSummary.latestProposalId ?? 'Awaiting review',
        repositoryId: workspace.repository.id,
        tab: 'operational-context',
        sectionId: 'proposal-review',
      }),
    )
  }

  const executionState = workspace.executionState
  if (workspace.executionSummary) {
    targets.push(
      createTarget({
        id: 'discovery-current-execution',
        kind: 'discovery',
        group: 'Discovery',
        label: 'Current execution',
        description: workspace.executionSummary.milestonePath ?? workspace.executionSummary.sessionId,
        repositoryId: workspace.repository.id,
        tab: 'execution',
        sectionId: 'execution-events',
        milestonePath: workspace.executionSummary.milestonePath,
      }),
    )
  }

  if (executionState === 'AwaitingAcceptance') {
    targets.push(
      createTarget({
        id: 'discovery-awaiting-handoff',
        kind: 'discovery',
        group: 'Discovery',
        label: 'Awaiting handoff review',
        description: workspace.executionSummary?.handoffPath ?? 'Generated handoff',
        repositoryId: workspace.repository.id,
        tab: 'execution',
        sectionId: 'generated-handoff-review',
        artifactPath: workspace.executionSummary?.handoffPath ?? null,
      }),
    )
  }

  if (executionState === 'AwaitingCommit' || executionState === 'AwaitingPush') {
    targets.push(
      createTarget({
        id: `discovery-${executionState}`,
        kind: 'discovery',
        group: 'Discovery',
        label: executionState === 'AwaitingCommit' ? 'Awaiting commit' : 'Awaiting push',
        description: workspace.executionSummary?.commitSha ?? 'Git workflow evidence',
        repositoryId: workspace.repository.id,
        tab: 'execution',
        sectionId: 'git-workflow',
      }),
    )
  }

  const warnings = [
    ...context.continuityWarnings,
    ...(continuityDiagnostics?.continuityWarnings ?? []),
  ].filter((warning, index, allWarnings) => allWarnings.indexOf(warning) === index)

  warnings.forEach((warning, index) => {
    targets.push(
      createTarget({
        id: `continuity-warning-${index}`,
        kind: 'discovery',
        group: 'Continuity Warnings',
        label: warning,
        description: 'Continuity warning',
        repositoryId: workspace.repository.id,
        tab: 'continuity',
        sectionId: 'continuity-warnings',
      }),
    )
  })

  return targets
}

function addContextItemTargets(
  targets: NavigationTarget[],
  repositoryId: string,
  group: string,
  sectionId: string,
  items: OperationalContextItem[],
) {
  items.forEach((item) => {
    targets.push(
      createTarget({
        id: `${sectionId}-${item.id}`,
        kind: 'discovery',
        group,
        label: item.text,
        description: item.rationale ?? group,
        repositoryId,
        tab: 'operational-context',
        sectionId,
      }),
    )
  })
}

function createTarget(target: NavigationTargetInput): NavigationTarget {
  const nextTarget = {
    classification: 'contextual' as NavigationDestinationClassification,
    repositoryId: null,
    tab: null,
    sectionId: null,
    artifactPath: null,
    milestonePath: null,
    ...target,
  }
  const searchText = [
    nextTarget.group,
    nextTarget.label,
    nextTarget.description,
    nextTarget.kind,
    nextTarget.artifactPath,
    nextTarget.milestonePath,
  ]
    .filter(Boolean)
    .join(' ')
    .toLowerCase()

  return { ...nextTarget, searchText }
}
