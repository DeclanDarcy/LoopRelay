import { memo } from 'react'
import { EmptyState, Metric, Panel, SectionHeader, StatusBadge } from '../../components/design'
import { milestoneProgressStatus } from '../../lib/status'
import type { MilestoneProgressRollup } from '../../types'

type WorkspaceMilestonesPanelProps = {
  rollup: MilestoneProgressRollup
}

function WorkspaceMilestonesPanelImpl({ rollup }: WorkspaceMilestonesPanelProps) {
  const { milestones, completedMilestoneCount, totalMilestoneCount } = rollup

  return (
    <Panel id="workspace-milestones" className="workspace-milestones-panel" aria-label="Workspace milestones">
      <SectionHeader
        className="workspace-milestones-header"
        eyebrow="Milestones"
        title="Milestone progress"
        headingLevel={4}
        actions={
          <Metric
            className="workspace-milestones-rollup"
            label="Milestones"
            value={`${completedMilestoneCount} of ${totalMilestoneCount} milestones complete`}
          />
        }
      />

      {milestones.length === 0 ? (
        <EmptyState className="empty-state">No milestone files found.</EmptyState>
      ) : (
        <ul className="workspace-milestone-list">
          {milestones.map((milestone) => {
            const valueText = `${milestone.completedTaskCount}/${milestone.totalTaskCount} tasks`

            return (
              <li key={milestone.relativePath} className="workspace-milestone-item">
                <div className="workspace-milestone-head">
                  <span>{milestone.name}</span>
                  <StatusBadge status={milestoneProgressStatus(milestone)} />
                </div>
                <small>{milestone.relativePath}</small>
                <div className="workspace-milestone-meter">
                  <div
                    className="workspace-milestone-meter-progress"
                    role="progressbar"
                    aria-valuenow={milestone.completedTaskCount}
                    aria-valuemin={0}
                    aria-valuemax={milestone.totalTaskCount}
                    aria-valuetext={valueText}
                  >
                    <span
                      className="workspace-milestone-meter-fill"
                      style={{
                        width:
                          milestone.totalTaskCount > 0
                            ? `${Math.round((milestone.completedTaskCount / milestone.totalTaskCount) * 100)}%`
                            : '0%',
                      }}
                    />
                  </div>
                  <span className="workspace-milestone-meter-count">{valueText}</span>
                </div>
              </li>
            )
          })}
        </ul>
      )}
    </Panel>
  )
}

export const WorkspaceMilestonesPanel = memo(WorkspaceMilestonesPanelImpl)
