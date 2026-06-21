import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { WorkspaceMilestonesPanel } from '../../features/workspace/WorkspaceMilestonesPanel'
import type { Artifact } from '../../types'

afterEach(() => {
  cleanup()
})

const milestones: Artifact[] = [
  {
    relativePath: '.agents/milestones/m3-workspace-migration.md',
    name: 'm3-workspace-migration.md',
    type: 'Milestone',
    family: 'Milestone',
    versionKind: 'Current',
  },
  {
    relativePath: '.agents/milestones/m4-execution-workspace.md',
    name: 'm4-execution-workspace.md',
    type: 'Milestone',
    family: 'Milestone',
    versionKind: 'Current',
  },
]

describe('workspace milestones panel characterization', () => {
  it('renders milestone artifact names and paths with selected state only', () => {
    render(
      <WorkspaceMilestonesPanel
        milestones={milestones}
        selectedMilestonePath=".agents/milestones/m4-execution-workspace.md"
        onSelectMilestone={vi.fn()}
      />,
    )

    const panel = screen.getByRole('region', { name: 'Workspace milestones' })

    expect(
      within(panel).getByRole('heading', { level: 4, name: 'm4-execution-workspace.md' }),
    ).toBeInTheDocument()
    expect(within(panel).getByText('.agents/milestones/m3-workspace-migration.md')).toBeInTheDocument()
    expect(within(panel).getByText('.agents/milestones/m4-execution-workspace.md')).toBeInTheDocument()
    expect(
      within(panel).getByRole('button', { name: /m4-execution-workspace\.md/ }),
    ).toHaveAttribute('aria-current', 'true')
    expect(panel).not.toHaveTextContent(/progress|criteria|complete/i)
  })

  it('selects milestones through the supplied navigation callback', () => {
    const onSelectMilestone = vi.fn()

    render(
      <WorkspaceMilestonesPanel
        milestones={milestones}
        selectedMilestonePath=".agents/milestones/m3-workspace-migration.md"
        onSelectMilestone={onSelectMilestone}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: /m4-execution-workspace\.md/ }))

    expect(onSelectMilestone).toHaveBeenCalledTimes(1)
    expect(onSelectMilestone).toHaveBeenCalledWith('.agents/milestones/m4-execution-workspace.md')
  })

  it('renders the empty inventory state without a synthetic selected milestone', () => {
    render(
      <WorkspaceMilestonesPanel
        milestones={[]}
        selectedMilestonePath={null}
        onSelectMilestone={vi.fn()}
      />,
    )

    const panel = screen.getByRole('region', { name: 'Workspace milestones' })

    expect(within(panel).getByRole('heading', { level: 4, name: 'No milestone selected' })).toBeInTheDocument()
    expect(within(panel).getByText('No milestone files found.')).toBeInTheDocument()
    expect(within(panel).queryByRole('button')).not.toBeInTheDocument()
  })
})
