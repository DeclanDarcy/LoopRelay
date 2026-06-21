import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ExecutionEventFeed } from '../../features/execution/ExecutionEventFeed'
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
  it('renders the same event identities as the execution feed for the same event stream', () => {
    const events: ExecutionEvent[] = [
      {
        sequence: 3,
        timestamp: '2026-06-21T16:15:00.000Z',
        type: 'stdout',
        message: 'First shared event',
      },
      {
        sequence: 4,
        timestamp: '2026-06-21T16:16:00.000Z',
        type: 'stderr',
        message: 'Second shared event',
      },
    ]

    render(
      <>
        <WorkspaceLiveActivityPanel events={events} />
        <ExecutionEventFeed events={events} />
      </>,
    )

    const workspaceActivity = screen.getByRole('region', { name: 'Workspace live activity' })
    const executionOutput = screen.getByRole('region', { name: 'Execution output' })

    expect(within(workspaceActivity).getByRole('heading', { level: 4, name: '2 events' })).toBeInTheDocument()
    expect(within(executionOutput).getByRole('heading', { level: 4, name: '2 events' })).toBeInTheDocument()
    expect(eventIdentities(workspaceActivity)).toEqual(eventIdentities(executionOutput))
  })

  it('uses the supplied execution navigation callback without changing event rendering', () => {
    const onOpenExecutionActivity = vi.fn()

    render(
      <WorkspaceLiveActivityPanel
        events={[
          {
            sequence: 5,
            timestamp: '2026-06-21T16:20:00.000Z',
            type: 'stdout',
            message: 'Navigation event',
          },
        ]}
        onOpenExecutionActivity={onOpenExecutionActivity}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Open in Execution' }))

    expect(onOpenExecutionActivity).toHaveBeenCalledTimes(1)
    expect(screen.getByText('Navigation event')).toBeInTheDocument()
  })
})
