import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { WorkspaceMilestonesPanel } from '../../features/workspace/WorkspaceMilestonesPanel'
import type { MilestoneProgressRollup } from '../../types'

afterEach(() => {
  cleanup()
})

const rollup: MilestoneProgressRollup = {
  completedMilestoneCount: 1,
  totalMilestoneCount: 3,
  milestones: [
    {
      relativePath: '.agents/milestones/m3-workspace-migration.md',
      name: 'm3-workspace-migration.md',
      completedTaskCount: 7,
      totalTaskCount: 7,
      isComplete: true,
    },
    {
      relativePath: '.agents/milestones/m4-execution-workspace.md',
      name: 'm4-execution-workspace.md',
      completedTaskCount: 3,
      totalTaskCount: 7,
      isComplete: false,
    },
    {
      relativePath: '.agents/milestones/m5-continuity.md',
      name: 'm5-continuity.md',
      completedTaskCount: 0,
      totalTaskCount: 5,
      isComplete: false,
    },
  ],
}

describe('workspace milestones panel characterization', () => {
  it('renders milestone names with a read-only progress readout', () => {
    render(<WorkspaceMilestonesPanel rollup={rollup} />)

    const panel = screen.getByRole('region', { name: 'Workspace milestones' })

    expect(within(panel).getByText('m3-workspace-migration.md')).toBeInTheDocument()
    expect(within(panel).getByText('m4-execution-workspace.md')).toBeInTheDocument()
    expect(within(panel).getByText('m5-continuity.md')).toBeInTheDocument()

    expect(within(panel).getByText('7/7 tasks')).toBeInTheDocument()
    expect(within(panel).getByText('3/7 tasks')).toBeInTheDocument()
    expect(within(panel).getByText('0/5 tasks')).toBeInTheDocument()
  })

  it('renders a status badge for each milestone progress state', () => {
    render(<WorkspaceMilestonesPanel rollup={rollup} />)

    const panel = screen.getByRole('region', { name: 'Workspace milestones' })

    expect(within(panel).getByText('Complete')).toBeInTheDocument()
    expect(within(panel).getByText('In progress')).toBeInTheDocument()
    expect(within(panel).getByText('Not started')).toBeInTheDocument()
  })

  it('exposes accessible progressbars carrying the task counts', () => {
    render(<WorkspaceMilestonesPanel rollup={rollup} />)

    const panel = screen.getByRole('region', { name: 'Workspace milestones' })
    const meters = within(panel).getAllByRole('progressbar')

    expect(meters).toHaveLength(3)
    expect(meters[0]).toHaveAttribute('aria-valuenow', '7')
    expect(meters[0]).toHaveAttribute('aria-valuemax', '7')
    expect(meters[0]).toHaveAttribute('aria-valuetext', '7/7 tasks')
    expect(meters[1]).toHaveAttribute('aria-valuenow', '3')
    expect(meters[1]).toHaveAttribute('aria-valuemax', '7')
    expect(meters[1]).toHaveAttribute('aria-valuetext', '3/7 tasks')
  })

  it('renders the overall milestone rollup in the header', () => {
    render(<WorkspaceMilestonesPanel rollup={rollup} />)

    const panel = screen.getByRole('region', { name: 'Workspace milestones' })

    expect(within(panel).getByText('1 of 3 milestones complete')).toBeInTheDocument()
  })

  it('is a passive display without selection affordances', () => {
    render(<WorkspaceMilestonesPanel rollup={rollup} />)

    const panel = screen.getByRole('region', { name: 'Workspace milestones' })

    expect(within(panel).queryByRole('button')).not.toBeInTheDocument()
    expect(within(panel).queryByRole('link')).not.toBeInTheDocument()
    expect(panel.querySelector('[aria-current]')).toBeNull()
  })

  it('renders the empty inventory state', () => {
    render(
      <WorkspaceMilestonesPanel
        rollup={{ completedMilestoneCount: 0, totalMilestoneCount: 0, milestones: [] }}
      />,
    )

    const panel = screen.getByRole('region', { name: 'Workspace milestones' })

    expect(within(panel).getByText('No milestone files found.')).toBeInTheDocument()
    expect(within(panel).queryByRole('progressbar')).not.toBeInTheDocument()
    expect(within(panel).getByText('0 of 0 milestones complete')).toBeInTheDocument()
  })
})
