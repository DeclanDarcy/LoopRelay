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

  it('keeps the message as each row primary content and moves every other detail into a tooltip', () => {
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

    const rows = Array.from(document.querySelectorAll('.execution-event-row')) as HTMLElement[]
    expect(rows).toHaveLength(2)

    // The message stays the row's primary, always-visible content, in provided order.
    expect(rows.map((row) => row.querySelector('.execution-event-message')?.textContent)).toEqual([
      'Second event',
      'Seventh event',
    ])

    // Every per-event detail now lives inside the row's tooltip — and the message does not.
    const firstTooltip = within(rows[0]).getByRole('tooltip')
    expect(within(firstTooltip).getByText('#2')).toBeInTheDocument()
    expect(within(firstTooltip).getByText('stderr')).toBeInTheDocument()
    expect(within(firstTooltip).getByText('Provider emitted standard error output.')).toHaveClass(
      'execution-event-consequence',
    )
    expect(within(firstTooltip).getByText('Provider not recorded')).toBeInTheDocument()
    expect(within(firstTooltip).getByText('Session not recorded')).toBeInTheDocument()
    expect(firstTooltip.querySelector('.execution-event-message')).toBeNull()

    // The details are reachable through a focusable, labelled trigger per row.
    expect(within(rows[0]).getByRole('button', { name: 'Event #2 details' })).toBeInTheDocument()
    expect(within(rows[1]).getByRole('button', { name: 'Event #7 details' })).toBeInTheDocument()
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
    // The consequence now renders exactly once per event — inside the row tooltip — now that the
    // duplicate, always-expanded per-group "Consequences" card list has been removed.
    expect(screen.getByText('Provider execution began.')).toHaveClass('execution-event-consequence')
    expect(screen.getByText('Execution is failed or requires recovery before normal progression can continue.')).toHaveClass(
      'execution-event-consequence',
    )
    expect(screen.getByText('Handoff passed validation and the repository is awaiting acceptance.')).toHaveClass(
      'execution-event-consequence',
    )
    // Regression guard: the per-group consequence card list (which re-rendered every event as a
    // stack of bordered cards above the rows) must not return — it duplicated the row detail.
    expect(screen.queryByText('Launch Consequences')).not.toBeInTheDocument()
    expect(screen.queryByText('Failure Consequences')).not.toBeInTheDocument()
    expect(screen.queryByText('Handoff Consequences')).not.toBeInTheDocument()
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
    // The message remains the visible primary content even with no semantic metadata.
    expect(document.querySelector('.execution-event-message')).toHaveTextContent('Older event')
    expect(within(screen.getByRole('region', { name: 'Execution output' })).getByRole('tooltip')).toBeInTheDocument()
  })
})
