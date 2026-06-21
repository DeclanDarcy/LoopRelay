import { EmptyState, Panel, SectionHeader } from '../../components/design'
import type { Artifact } from '../../types'

type WorkspaceMilestonesPanelProps = {
  milestones: Artifact[]
  selectedMilestonePath: string | null
  onSelectMilestone: (milestonePath: string | null) => void
  onOpenExecutionContext?: (milestonePath: string) => void
}

export function WorkspaceMilestonesPanel({
  milestones,
  selectedMilestonePath,
  onSelectMilestone,
  onOpenExecutionContext,
}: WorkspaceMilestonesPanelProps) {
  const selectedMilestone = milestones.find(
    (milestone) => milestone.relativePath === selectedMilestonePath,
  )

  return (
    <Panel className="workspace-milestones-panel" aria-label="Workspace milestones">
      <SectionHeader
        className="workspace-milestones-header"
        eyebrow="Milestones"
        title={selectedMilestone ? selectedMilestone.name : 'No milestone selected'}
        headingLevel={4}
      />

      {milestones.length === 0 ? (
        <EmptyState className="empty-state">No milestone files found.</EmptyState>
      ) : (
        <div className="workspace-milestone-list">
          {milestones.map((milestone) => {
            const isSelected = milestone.relativePath === selectedMilestonePath

            return (
              <button
                type="button"
                key={milestone.relativePath}
                className={`workspace-milestone-item${isSelected ? ' selected' : ''}`}
                aria-current={isSelected ? 'true' : undefined}
                onClick={() => {
                  onSelectMilestone(milestone.relativePath)
                  onOpenExecutionContext?.(milestone.relativePath)
                }}
              >
                <span>{milestone.name}</span>
                <small>{milestone.relativePath}</small>
              </button>
            )
          })}
        </div>
      )}
    </Panel>
  )
}
