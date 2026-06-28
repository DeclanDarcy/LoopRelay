import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import {
  PhaseTimeline,
  StreamFailurePanel,
  StreamOutputPanel,
} from '../../features/streams'

afterEach(() => {
  cleanup()
})

describe('StreamOutputPanel', () => {
  it('renders a polite live region with the streamed text and eyebrow', () => {
    render(
      <StreamOutputPanel
        ariaLabel="Execution output"
        eyebrow="Output"
        streamedText="Working the milestone…"
        live
      />,
    )

    const region = screen.getByRole('region', { name: 'Execution output' })
    expect(region).toHaveTextContent('Working the milestone…')
    expect(region).toHaveTextContent('Output')

    const stream = region.querySelector('pre')
    expect(stream).not.toBeNull()
    expect(stream).toHaveAttribute('aria-live', 'polite')
  })

  it('shows the phase label in the pill while live', () => {
    render(
      <StreamOutputPanel
        ariaLabel="Execution output"
        streamedText=""
        live
        phaseLabel="Extracting milestones"
      />,
    )

    expect(screen.getByText('Extracting milestones…')).toBeInTheDocument()
  })

  it('shows the token summary instead of a pill when not live', () => {
    render(
      <StreamOutputPanel
        ariaLabel="Execution output"
        streamedText="done"
        live={false}
        tokens={{ promptTokens: 4200, outputTokens: 1850 }}
      />,
    )

    expect(screen.getByText(/4,200 in/)).toBeInTheDocument()
    expect(screen.getByText(/1,850 out/)).toBeInTheDocument()
  })
})

describe('PhaseTimeline', () => {
  it('renders an ordered step rail and marks the active step with aria-current', () => {
    render(
      <PhaseTimeline
        ariaLabel="Execution phases"
        steps={[
          { key: 'one', label: 'Extract milestones', state: 'done', note: '3 extracted' },
          { key: 'two', label: 'Commit & push', state: 'active', note: null },
          { key: 'three', label: 'Start execution', state: 'pending', note: null },
        ]}
      />,
    )

    const list = screen.getByRole('list', { name: 'Execution phases' })
    expect(list).toBeInTheDocument()

    const activeItem = screen.getByText('Commit & push').closest('li')
    expect(activeItem).toHaveAttribute('aria-current', 'step')

    const doneItem = screen.getByText('Extract milestones').closest('li')
    expect(doneItem).not.toHaveAttribute('aria-current')
    expect(doneItem).toHaveTextContent('3 extracted')
  })
})

describe('StreamFailurePanel', () => {
  it('renders an alert with the eyebrow, reason, detail, and dismiss button', () => {
    let dismissed = 0
    render(
      <StreamFailurePanel
        ariaLabel="Execution failed"
        eyebrow="Execution failed: StartExecution"
        reason="Agent process exited"
        detail="exit 1"
        onDismiss={() => {
          dismissed += 1
        }}
        dismissLabel="Back to plan"
      />,
    )

    const alert = screen.getByRole('alert', { name: 'Execution failed' })
    expect(alert).toHaveTextContent('Execution failed: StartExecution')
    expect(alert).toHaveTextContent('Agent process exited')
    expect(alert).toHaveTextContent('exit 1')

    const button = screen.getByRole('button', { name: 'Back to plan' })
    button.click()
    expect(dismissed).toBe(1)
  })

  it('omits the dismiss button when no handler is given', () => {
    render(<StreamFailurePanel eyebrow="Failed" reason="boom" />)

    expect(screen.getByRole('alert')).toHaveTextContent('boom')
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })
})
