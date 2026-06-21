import type { ArtifactCategory, ArtifactInventory } from '../types'

export function getArtifactCategories(inventory: ArtifactInventory): ArtifactCategory[] {
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
      label: 'Historical Operational Contexts',
      missingLabel: 'No historical operational contexts found.',
      artifacts: inventory.historicalOperationalContexts,
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

export function getAvailableArtifactPaths(inventory: ArtifactInventory) {
  return getArtifactCategories(inventory)
    .flatMap((category) => category.artifacts)
    .map((artifact) => artifact.relativePath)
}
