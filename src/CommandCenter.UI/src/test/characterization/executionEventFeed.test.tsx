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
        category: 'Provider',
        consequence: 'Provider emitted standard error output.',
        message: 'Second event',
      },
      {
        sequence: 7,
        timestamp: '2026-06-21T16:16:00.000Z',
        type: 'stdout',
        category: 'Provider',
        consequence: 'Provider emitted standard output.',
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
    expect(screen.getByLabelText('Provider execution events')).toBeInTheDocument()
    expect(within(screen.getByLabelText('Provider execution events')).getByText('2 events')).toBeInTheDocument()
    expect(screen.getAllByText('Provider emitted standard error output.')).not.toHaveLength(0)
    expect(screen.getAllByText('Provider emitted standard output.')).not.toHaveLength(0)
    expect(screen.getByText('Provider Consequences')).toBeInTheDocument()
    expect(Array.from(document.querySelectorAll('pre')).map((node) => node.textContent)).toEqual([
      'Second event',
      'Seventh event',
    ])
  })

  it('groups events by backend semantic category in first-seen order', () => {
    const events: ExecutionEvent[] = [
      {
        sequence: 1,
        timestamp: '2026-06-21T16:15:00.000Z',
        type: 'ProviderStarted',
        category: 'Launch',
        consequence: 'Provider execution began.',
        message: 'Provider process started.',
      },
      {
        sequence: 2,
        timestamp: '2026-06-21T16:16:00.000Z',
        type: 'Failure',
        category: 'Failure',
        consequence: 'Execution is failed or requires recovery before normal progression can continue.',
        message: 'Provider process exited with code 2.',
      },
      {
        sequence: 3,
        timestamp: '2026-06-21T16:17:00.000Z',
        type: 'HandoffValidated',
        category: 'Handoff',
        consequence: 'Handoff passed validation and the repository is awaiting acceptance.',
        message: 'Current handoff validated for review.',
      },
    ]

    render(<ExecutionEventFeed events={events} />)

    expect(screen.getByLabelText('Launch execution events')).toBeInTheDocument()
    expect(screen.getByLabelText('Failure execution events')).toBeInTheDocument()
    expect(screen.getByLabelText('Handoff execution events')).toBeInTheDocument()
    expect(screen.getAllByText('Provider execution began.').at(-1)).toHaveClass('execution-event-consequence')
    expect(screen.getAllByText('Execution is failed or requires recovery before normal progression can continue.').at(-1)).toHaveClass(
      'execution-event-consequence',
    )
    expect(screen.getAllByText('Handoff passed validation and the repository is awaiting acceptance.').at(-1)).toHaveClass(
      'execution-event-consequence',
    )
    expect(screen.getByText('Launch Consequences')).toBeInTheDocument()
    expect(screen.getByText('Failure Consequences')).toBeInTheDocument()
    expect(screen.getByText('Handoff Consequences')).toBeInTheDocument()
    expect(Array.from(document.querySelectorAll('.execution-event-row')).map((row) => row.getAttribute('data-event-category'))).toEqual([
      'Launch',
      'Failure',
      'Handoff',
    ])
  })

  it('keeps older events visible when semantic fields are absent', () => {
    render(
      <ExecutionEventFeed
        events={[
          {
            sequence: 9,
            timestamp: '2026-06-21T16:18:00.000Z',
            type: 'Info',
            message: 'Older event',
          },
        ]}
      />,
    )

    expect(screen.getByLabelText('Monitoring execution events')).toBeInTheDocument()
    expect(screen.getAllByText('Execution monitoring recorded activity.')).not.toHaveLength(0)
    expect(document.querySelector('pre')).toHaveTextContent('Older event')
  })
})
