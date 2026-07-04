import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { PlanAuthoringScreen } from '../../features/planning/PlanAuthoringScreen'

// Drive the real transport seam end-to-end: install a FakeEventSource and a non-mock backend URL so
// PlanAuthoringScreen runs the real subscribeToPlanEvents branch (the mock-bridge branch drops the
// transport callbacks). This exercises onerror -> setTransportFailed/isReconnecting -> the rendered
// panel, which streamReconnect.test.tsx covers only by hard-coding props, and also drives R3's
// bounded-window escalation through to the DOM.
class FakeEventSource {
  static instances: FakeEventSource[] = []
  static readonly CONNECTING = 0
  static readonly OPEN = 1
  static readonly CLOSED = 2

  url: string
  readyState = FakeEventSource.CONNECTING
  onerror: (() => void) | null = null
  private listeners: Record<string, ((event: MessageEvent) => void)[]> = {}
  closed = false

  constructor(url: string) {
    this.url = url
    FakeEventSource.instances.push(this)
  }

  addEventListener(type: string, handler: (event: MessageEvent) => void) {
    ;(this.listeners[type] ??= []).push(handler)
  }

  close() {
    this.closed = true
    this.readyState = FakeEventSource.CLOSED
  }

  emit(type: string, data: unknown, lastEventId = '') {
    const event = { data: JSON.stringify(data), lastEventId } as MessageEvent
    ;(this.listeners[type] ?? []).forEach((handler) => handler(event))
  }

  fireError(readyState: number) {
    this.readyState = readyState
    this.onerror?.()
  }
}

const originalEventSource = globalThis.EventSource

beforeEach(() => {
  FakeEventSource.instances = []
  vi.useFakeTimers()
  // @ts-expect-error - swap in the fake for the test
  globalThis.EventSource = FakeEventSource
  // A non-mock backend URL routes the hooks through the real EventSource subscriber. get_plan_status
  // returns a written plan so Execute is reachable; we never need the stream's own completion here.
  window.__TAURI_INTERNALS__ = {
    invoke: async (cmd: string) => {
      if (cmd === 'get_backend_url') {
        return 'http://backend'
      }

      if (cmd === 'get_plan_status') {
        return { planExists: true, state: 'PlanReady' }
      }

      return { phase: 'WritePlan' }
    },
  } as unknown as Window['__TAURI_INTERNALS__']
})

afterEach(() => {
  cleanup()
  vi.useRealTimers()
  globalThis.EventSource = originalEventSource
  delete window.__TAURI_INTERNALS__
  vi.restoreAllMocks()
})

// Resolve the async getBackendUrl()/getPlanStatus() promises and the post-Write subscription so the
// plan EventSource exists before the test drives frames on it.
async function flushAsync() {
  await act(async () => {
    await Promise.resolve()
    await Promise.resolve()
  })
}

async function startPlanningTurn() {
  render(<PlanAuthoringScreen repositoryId="repo-1" repositoryName="Repo" />)
  await flushAsync()

  fireEvent.change(screen.getByRole('textbox', { name: 'Epic' }), {
    target: { value: 'Ship the planning workflow.' },
  })
  fireEvent.click(screen.getByRole('button', { name: 'Write Plan' }))
  await flushAsync()

  // The plan stream is the only active EventSource during the Planning turn.
  const source = FakeEventSource.instances.at(-1)
  if (!source) {
    throw new Error('plan EventSource was not opened')
  }

  return source
}

describe('plan stream transport failure (end-to-end through the hooks)', () => {
  it('shows Reconnecting, then escalates a stuck stream to a recoverable failure, then recovers', async () => {
    const source = await startPlanningTurn()

    // A mid-stream drop keeps the browser in CONNECTING: the live pill reads "Reconnecting…".
    await act(async () => {
      source.fireError(FakeEventSource.CONNECTING)
    })
    expect(screen.getByText('Reconnecting…')).toBeInTheDocument()
    expect(screen.queryByRole('alert', { name: 'Planning connection lost' })).not.toBeInTheDocument()

    // The bounded reconnect window expires with no frame: the failure surfaces with a recovery exit,
    // and the live output region is gone.
    await act(async () => {
      vi.advanceTimersByTime(15_000)
    })
    const alert = screen.getByRole('alert', { name: 'Planning connection lost' })
    expect(alert).toHaveTextContent(/connection/i)
    expect(screen.getByRole('button', { name: 'Back to plan' })).toBeInTheDocument()
    expect(document.querySelector('pre[aria-live="polite"]')).toBeNull()
  })

  it('clears the failure when a frame arrives before the window expires', async () => {
    const source = await startPlanningTurn()

    await act(async () => {
      source.fireError(FakeEventSource.CONNECTING)
    })
    expect(screen.getByText('Reconnecting…')).toBeInTheDocument()

    // A delta frame arrives before the window expires: the stream recovered, no failure escalates
    // even after the original window would have elapsed.
    await act(async () => {
      source.emit('delta', { text: 'planning…' }, '1')
    })
    await act(async () => {
      vi.advanceTimersByTime(30_000)
    })

    expect(screen.queryByRole('alert', { name: 'Planning connection lost' })).not.toBeInTheDocument()
    expect(document.querySelector('pre[aria-live="polite"]')).not.toBeNull()
  })
})
