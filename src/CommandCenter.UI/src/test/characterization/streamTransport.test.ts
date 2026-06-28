import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { subscribeToExecutionRunEvents, subscribeToPlanEvents } from '../../api/planning'
import { subscribeToDecisionRunEvents } from '../../api/decisionRuntime'

// A minimal fake EventSource that captures listeners and lets the test drive frames, lastEventId,
// and readyState transitions. The real api subscribers attach addEventListener handlers and an
// onerror handler, which we exercise here without a network.
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
  }

  // Drive a named frame with a JSON payload and an explicit lastEventId (the SSE `id:` field).
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
  // @ts-expect-error - swap in the fake for the test
  globalThis.EventSource = FakeEventSource
})

afterEach(() => {
  globalThis.EventSource = originalEventSource
  vi.restoreAllMocks()
})

describe('execution stream transport', () => {
  it('dedupes replayed frames by sequence id', () => {
    const onEvent = vi.fn()
    subscribeToExecutionRunEvents('http://backend', 'repo-1', onEvent)
    const source = FakeEventSource.instances[0]

    source.emit('milestones-extracted', { count: 3 }, '5')
    // A replayed frame at or below the last seen sequence is skipped.
    source.emit('milestones-extracted', { count: 3 }, '5')
    source.emit('milestones-extracted', { count: 3 }, '4')
    // A higher sequence is delivered.
    source.emit('committed', { commitSha: 'abc', pushed: true }, '6')

    expect(onEvent).toHaveBeenCalledTimes(2)
    expect(onEvent).toHaveBeenNthCalledWith(1, { type: 'milestones-extracted', count: 3 })
    expect(onEvent).toHaveBeenNthCalledWith(2, {
      type: 'committed',
      commitSha: 'abc',
      pushed: true,
    })
  })

  it('signals reconnecting while the browser retries, and transport failure when it gives up', () => {
    const onReconnecting = vi.fn()
    const onError = vi.fn()
    subscribeToExecutionRunEvents('http://backend', 'repo-1', vi.fn(), {
      onReconnecting,
      onError,
    })
    const source = FakeEventSource.instances[0]

    source.fireError(FakeEventSource.CONNECTING)
    expect(onReconnecting).toHaveBeenCalledTimes(1)
    expect(onError).not.toHaveBeenCalled()

    source.fireError(FakeEventSource.CLOSED)
    expect(onError).toHaveBeenCalledTimes(1)
  })

  it('does not dedupe frames that carry no sequence id', () => {
    const onEvent = vi.fn()
    subscribeToExecutionRunEvents('http://backend', 'repo-1', onEvent)
    const source = FakeEventSource.instances[0]

    source.emit('run-started', {}, '')
    source.emit('phase', { phase: 'ExtractMilestones' }, '')

    expect(onEvent).toHaveBeenCalledTimes(2)
  })
})

describe('decision stream transport', () => {
  it('dedupes replayed frames and surfaces reconnect/transport callbacks', () => {
    const onEvent = vi.fn()
    const onReconnecting = vi.fn()
    const onError = vi.fn()
    subscribeToDecisionRunEvents('http://backend', 'repo-1', onEvent, {
      onReconnecting,
      onError,
    })
    const source = FakeEventSource.instances[0]

    source.emit('diagnostics', { sandbox: 'read-only', approvals: 'never', seeded: true }, '2')
    source.emit('diagnostics', { sandbox: 'read-only', approvals: 'never', seeded: true }, '2')
    expect(onEvent).toHaveBeenCalledTimes(1)

    source.fireError(FakeEventSource.CONNECTING)
    expect(onReconnecting).toHaveBeenCalledTimes(1)
    source.fireError(FakeEventSource.CLOSED)
    expect(onError).toHaveBeenCalledTimes(1)
  })
})

describe('plan stream transport', () => {
  it('dedupes replayed frames and surfaces reconnect/transport callbacks', () => {
    const onEvent = vi.fn()
    const onReconnecting = vi.fn()
    const onError = vi.fn()
    subscribeToPlanEvents('http://backend', 'repo-1', onEvent, {
      onReconnecting,
      onError,
    })
    const source = FakeEventSource.instances[0]

    source.emit('delta', { text: 'a' }, '1')
    source.emit('delta', { text: 'a' }, '1')
    expect(onEvent).toHaveBeenCalledTimes(1)

    source.fireError(FakeEventSource.CONNECTING)
    expect(onReconnecting).toHaveBeenCalledTimes(1)
    source.fireError(FakeEventSource.CLOSED)
    expect(onError).toHaveBeenCalledTimes(1)
  })

  it('clears reconnecting on the next successful frame', () => {
    const onReconnecting = vi.fn()
    const onActive = vi.fn()
    subscribeToPlanEvents('http://backend', 'repo-1', vi.fn(), {
      onReconnecting,
      onActive,
    })
    const source = FakeEventSource.instances[0]

    source.fireError(FakeEventSource.CONNECTING)
    expect(onReconnecting).toHaveBeenCalledTimes(1)

    // Any successfully received frame clears the reconnecting flag.
    source.emit('delta', { text: 'a' }, '1')
    expect(onActive).toHaveBeenCalled()
  })
})

describe('bounded reconnect window', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('escalates a stuck CONNECTING stream to a transport failure after the window expires', () => {
    const onReconnecting = vi.fn()
    const onError = vi.fn()
    subscribeToPlanEvents('http://backend', 'repo-1', vi.fn(), {
      onReconnecting,
      onError,
    })
    const source = FakeEventSource.instances[0]

    // A mid-stream drop keeps the browser in CONNECTING and auto-reconnects forever; without a
    // bounded window the user would be stuck on "Reconnecting…" indefinitely.
    source.fireError(FakeEventSource.CONNECTING)
    expect(onReconnecting).toHaveBeenCalledTimes(1)
    expect(onError).not.toHaveBeenCalled()

    // The window has not elapsed yet.
    vi.advanceTimersByTime(5_000)
    expect(onError).not.toHaveBeenCalled()

    // Once the bounded window expires with no frame, the stream is closed and the failure surfaces.
    vi.advanceTimersByTime(20_000)
    expect(source.closed).toBe(true)
    expect(onError).toHaveBeenCalledTimes(1)
  })

  it('arms the window once and does not let repeated CONNECTING errors push it forever', () => {
    const onError = vi.fn()
    subscribeToPlanEvents('http://backend', 'repo-1', vi.fn(), { onError })
    const source = FakeEventSource.instances[0]

    // The browser retries every few seconds; each retry fires another CONNECTING onerror. The
    // window must be armed on the first one and NOT refreshed, so continuous retries cannot defer
    // the escalation indefinitely.
    source.fireError(FakeEventSource.CONNECTING)
    vi.advanceTimersByTime(4_000)
    source.fireError(FakeEventSource.CONNECTING)
    vi.advanceTimersByTime(4_000)
    source.fireError(FakeEventSource.CONNECTING)
    vi.advanceTimersByTime(4_000)
    source.fireError(FakeEventSource.CONNECTING)

    // ~16s of continuous retries have elapsed; the once-armed window must already have fired.
    expect(onError).toHaveBeenCalledTimes(1)
  })

  it('clears the window when a frame arrives, so a recovered stream never escalates', () => {
    const onError = vi.fn()
    const onActive = vi.fn()
    subscribeToPlanEvents('http://backend', 'repo-1', vi.fn(), { onError, onActive })
    const source = FakeEventSource.instances[0]

    source.fireError(FakeEventSource.CONNECTING)
    // The stream recovers: a frame arrives before the window elapses.
    vi.advanceTimersByTime(2_000)
    source.emit('delta', { text: 'a' }, '1')
    expect(onActive).toHaveBeenCalled()

    // Even well past the original window, no failure escalates because recovery cleared the timer.
    vi.advanceTimersByTime(60_000)
    expect(onError).not.toHaveBeenCalled()
  })

  it('does not arm the window on a healthy stream that never errors', () => {
    const onError = vi.fn()
    subscribeToPlanEvents('http://backend', 'repo-1', vi.fn(), { onError })
    const source = FakeEventSource.instances[0]

    // A healthy-but-quiet stream (frames, no errors) must never escalate to a transport failure.
    source.emit('delta', { text: 'a' }, '1')
    vi.advanceTimersByTime(60_000)
    expect(onError).not.toHaveBeenCalled()
  })

  it('clears the window on subscription teardown', () => {
    const onError = vi.fn()
    const subscription = subscribeToPlanEvents('http://backend', 'repo-1', vi.fn(), { onError })
    const source = FakeEventSource.instances[0]

    source.fireError(FakeEventSource.CONNECTING)
    subscription.close()
    expect(source.closed).toBe(true)

    // After teardown the armed timer must not fire onError on a stream we have already abandoned.
    vi.advanceTimersByTime(60_000)
    expect(onError).not.toHaveBeenCalled()
  })
})
