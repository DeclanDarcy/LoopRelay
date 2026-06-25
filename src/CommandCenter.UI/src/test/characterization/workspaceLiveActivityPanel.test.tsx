import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { WorkspaceLiveActivityPanel } from '../../features/workspace/WorkspaceLiveActivityPanel'
import type { ExecutionEvent } from '../../types'

afterEach(() => {
  cleanup()
})

function eventIdentities(region: HTMLElement) {
  return Array.from(region.querySelectorAll('.execution-event-row')).map((row) => ({
    sequence: row.getAttribute('data-event-sequence'),
    type: row.getAttribute('data-event-type'),
    timestamp: row.getAttribute('data-event-timestamp'),
    message: within(row as HTMLElement).getByText(/event$/).textContent,
  }))
}

describe('workspace live activity rendering characterization', () => {
  it('renders a contextual event summary without duplicating the execution event feed', () => {
    const events: ExecutionEvent[] = [
      {
        sequence: 3,
        timestamp: '2026-06-21T16:15:00.000Z',
        type: 'stdout',
        category: 'Provider',
        consequence: 'Provider emitted standard output.',
        message: 'First shared event',
      },
      {
        sequence: 4,
        timestamp: '2026-06-21T16:16:00.000Z',
        type: 'stderr',
        category: 'Failure',
        consequence: 'Provider emitted standard error output.',
        message: 'Second shared event',
      },
    ]

    render(<WorkspaceLiveActivityPanel events={events} />)

    const workspaceActivity = screen.getByRole('region', { name: 'Workspace live activity' })

    expect(within(workspaceActivity).getByRole('heading', { level: 4, name: '2 events' })).toBeInTheDocument()
    expect(within(workspaceActivity).getByText('Latest: stderr')).toBeInTheDocument()
    expect(within(workspaceActivity).getByText('Categories: Provider, Failure')).toBeInTheDocument()
    expect(within(workspaceActivity).getByText('Consequence: Provider emitted standard error output.')).toBeInTheDocument()
    expect(workspaceActivity.querySelectorAll('.execution-event-row')).toHaveLength(0)
    expect(eventIdentities(workspaceActivity)).toEqual([])
  })

  it('uses the supplied execution navigation callback without rendering primary feed rows', () => {
    const onOpenExecutionActivity = vi.fn()

    render(
      <WorkspaceLiveActivityPanel
        events={[
          {
            sequence: 5,
            timestamp: '2026-06-21T16:20:00.000Z',
            type: 'stdout',
            consequence: 'Navigation event consequence.',
            message: 'Navigation event',
          },
        ]}
        onOpenExecutionActivity={onOpenExecutionActivity}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Open in Execution' }))

    expect(onOpenExecutionActivity).toHaveBeenCalledTimes(1)
    expect(screen.getByText('Latest: stdout')).toBeInTheDocument()
    expect(screen.queryByText('Navigation event')).not.toBeInTheDocument()
    expect(document.querySelectorAll('.execution-event-row')).toHaveLength(0)
  })
})
