import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
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
})
