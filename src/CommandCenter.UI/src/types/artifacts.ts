export type ArtifactType = 'Plan' | 'OperationalContext' | 'Milestone' | 'Handoff' | 'Decision'

export type ArtifactFamily = ArtifactType

export type ArtifactVersionKind = 'Current' | 'Historical'

export type Artifact = {
  relativePath: string
  name: string
  type: ArtifactType
  family: ArtifactFamily
  versionKind: ArtifactVersionKind
}

export type ArtifactInventory = {
  plan: Artifact | null
  operationalContext: Artifact | null
  historicalOperationalContexts: Artifact[]
  milestones: Artifact[]
  currentHandoff: Artifact | null
  historicalHandoffs: Artifact[]
  currentDecisions: Artifact | null
  historicalDecisions: Artifact[]
}

export type ArtifactCategory = {
  label: string
  missingLabel: string
  artifacts: Artifact[]
}
