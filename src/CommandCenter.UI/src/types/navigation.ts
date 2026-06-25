import type { PrimaryWorkspaceTab } from '../state/shellState'

export type NavigationTargetKind =
  | 'repository'
  | 'workspace'
  | 'section'
  | 'milestone'
  | 'execution-session'
  | 'artifact'
  | 'discovery'

export type NavigationDestinationClassification =
  | 'primary'
  | 'contextual'
  | 'deprecated'
  | 'hidden'

export type NavigationTarget = {
  id: string
  kind: NavigationTargetKind
  group: string
  label: string
  description: string
  classification: NavigationDestinationClassification
  repositoryId: string | null
  tab: PrimaryWorkspaceTab | null
  sectionId: string | null
  artifactPath: string | null
  milestonePath: string | null
  searchText: string
}
