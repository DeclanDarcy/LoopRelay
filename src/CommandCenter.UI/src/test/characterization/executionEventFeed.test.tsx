import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ExecutionEventFeed } from '../../features/execution/ExecutionEventFeed'
import type { ExecutionEvent } from '../../types'

afterEach(() => {
  cleanup()
})

describe('execution event feed rendering characterization', () => {
  it('renders the current empty event state', () => {
    render(<ExecutionEventFeed events={[]} />)

    expect(screen.getByRole('region', { name: 'Execution output' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 4, name: '0 events' })).toBeInTheDocument()
    expect(screen.getByText('No execution events recorded.')).toHaveClass('empty-state')
  })

  it('renders event rows in provided order', () => {
    const events: ExecutionEvent[] = [
      {
        sequence: 2,
        timestamp: '2026-06-21T16:15:00.000Z',
        type: 'stderr',
        message: 'Second event',
      },
      {
        sequence: 7,
        timestamp: '2026-06-21T16:16:00.000Z',
        type: 'stdout',
        message: 'Seventh event',
      },
    ]

    render(<ExecutionEventFeed events={events} />)

    expect(screen.getByRole('heading', { level: 4, name: '2 events' })).toBeInTheDocument()
    const rows = document.querySelectorAll('.execution-event-row')
    expect(Array.from(rows).map((row) => within(row as HTMLElement).getByText(/^#/).textContent)).toEqual([
      '#2',
      '#7',
    ])
    expect(screen.getByText('stderr')).toBeInTheDocument()
    expect(screen.getByText('stdout')).toBeInTheDocument()
    expect(screen.getByText('Second event').tagName).toBe('PRE')
    expect(screen.getByText('Seventh event').tagName).toBe('PRE')
  })
})
