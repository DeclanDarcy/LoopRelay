import { describe, expect, it } from 'vitest'
import { executionRunReducer, initialExecutionRunState } from './executionRunMachine'
import type { ExecutionRunState } from '../../types'

function drive(state: ExecutionRunState, actions: Parameters<typeof executionRunReducer>[1][]) {
  return actions.reduce(executionRunReducer, state)
}

describe('executionRunReducer', () => {
  it('starts Idle with no run output', () => {
    expect(initialExecutionRunState.status).toBe('Idle')
    expect(initialExecutionRunState.streamedText).toBe('')
    expect(initialExecutionRunState.completion).toBeNull()
    expect(initialExecutionRunState.failure).toBeNull()
  })

  it('moves to Running on run-started', () => {
    const next = executionRunReducer(initialExecutionRunState, {
      kind: 'event',
      event: { type: 'run-started', phase: 'ExecutePlan' },
    })

    expect(next.status).toBe('Running')
    expect(next.phase).toBe('ExecutePlan')
  })

  it('tracks the reported phase from phase events', () => {
    const next = drive(initialExecutionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'ExecutePlan' } },
      { kind: 'event', event: { type: 'phase', phase: 'ExtractMilestones' } },
    ])

    expect(next.phase).toBe('ExtractMilestones')
    expect(next.status).toBe('Running')
  })

  it('accumulates delta text across phases', () => {
    const next = drive(initialExecutionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'ExecutePlan' } },
      { kind: 'event', event: { type: 'delta', phase: 'ExtractMilestones', text: 'one ' } },
      { kind: 'event', event: { type: 'delta', phase: 'StartExecution', text: 'two' } },
    ])

    expect(next.streamedText).toBe('one two')
  })

  it('records the milestone count from milestones-extracted', () => {
    const next = drive(initialExecutionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'ExecutePlan' } },
      { kind: 'event', event: { type: 'milestones-extracted', count: 3 } },
    ])

    expect(next.milestoneCount).toBe(3)
  })

  it('records commit and push from committed', () => {
    const next = drive(initialExecutionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'ExecutePlan' } },
      { kind: 'event', event: { type: 'committed', commitSha: 'abc123', pushed: true } },
    ])

    expect(next.commit).toEqual({ commitSha: 'abc123', pushed: true })
  })

  it('keeps running through a lifecycle frame without losing the phase', () => {
    const next = drive(initialExecutionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'ExecutePlan' } },
      { kind: 'event', event: { type: 'phase', phase: 'StartExecution' } },
      { kind: 'event', event: { type: 'lifecycle', state: 'ExecutingPlan' } },
    ])

    expect(next.status).toBe('Running')
    expect(next.phase).toBe('StartExecution')
  })

  it('records the rotated handoff from handoff-rotated', () => {
    const next = drive(initialExecutionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'ExecutePlan' } },
      {
        kind: 'event',
        event: { type: 'handoff-rotated', sequence: 1, path: '.agents/handoffs/handoff.0001.md' },
      },
    ])

    expect(next.handoff).toEqual({ sequence: 1, path: '.agents/handoffs/handoff.0001.md' })
  })

  it('completes the run with milestone count, commit, and handoff results', () => {
    const next = drive(initialExecutionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'ExecutePlan' } },
      { kind: 'event', event: { type: 'phase', phase: 'ExtractMilestones' } },
      { kind: 'event', event: { type: 'milestones-extracted', count: 3 } },
      { kind: 'event', event: { type: 'committed', commitSha: 'abc123', pushed: true } },
      {
        kind: 'event',
        event: { type: 'handoff-rotated', sequence: 1, path: '.agents/handoffs/handoff.0001.md' },
      },
      {
        kind: 'event',
        event: {
          type: 'completed',
          commitSha: 'abc123',
          milestoneCount: 3,
          handoffPath: '.agents/handoffs/handoff.0001.md',
          promptTokens: 4200,
          outputTokens: 1850,
        },
      },
    ])

    expect(next.status).toBe('Completed')
    expect(next.phase).toBeNull()
    expect(next.completion).toEqual({
      commitSha: 'abc123',
      milestoneCount: 3,
      handoffPath: '.agents/handoffs/handoff.0001.md',
      promptTokens: 4200,
      outputTokens: 1850,
    })
  })

  it('completes even when no handoff-rotated event arrived first', () => {
    const next = drive(initialExecutionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'ExecutePlan' } },
      {
        kind: 'event',
        event: {
          type: 'completed',
          commitSha: null,
          milestoneCount: 2,
          handoffPath: '.agents/handoffs/handoff.0002.md',
          promptTokens: 10,
          outputTokens: 5,
        },
      },
    ])

    expect(next.status).toBe('Completed')
    expect(next.handoff).toEqual({ sequence: 0, path: '.agents/handoffs/handoff.0002.md' })
    expect(next.completion?.commitSha).toBeNull()
  })

  it('surfaces a failed event with phase, reason, and detail', () => {
    const next = drive(initialExecutionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'ExecutePlan' } },
      { kind: 'event', event: { type: 'phase', phase: 'StartExecution' } },
      {
        kind: 'event',
        event: { type: 'failed', phase: 'StartExecution', reason: 'Agent crashed', detail: 'trace' },
      },
    ])

    expect(next.status).toBe('Failed')
    expect(next.phase).toBeNull()
    expect(next.failure).toEqual({ phase: 'StartExecution', reason: 'Agent crashed', detail: 'trace' })
  })

  it('surfaces a failure with no phase or detail', () => {
    const next = drive(initialExecutionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'ExecutePlan' } },
      { kind: 'event', event: { type: 'failed', reason: 'boom' } },
    ])

    expect(next.failure).toEqual({ phase: null, reason: 'boom', detail: null })
  })

  it('is resilient to a delta arriving before run-started (out of order)', () => {
    const next = executionRunReducer(initialExecutionRunState, {
      kind: 'event',
      event: { type: 'delta', phase: 'ExtractMilestones', text: 'early chunk' },
    })

    expect(next.status).toBe('Running')
    expect(next.streamedText).toBe('early chunk')
  })

  it('does not overwrite a terminal status with a late non-terminal event', () => {
    const completed = drive(initialExecutionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'ExecutePlan' } },
      {
        kind: 'event',
        event: {
          type: 'completed',
          commitSha: 'abc',
          milestoneCount: 1,
          handoffPath: 'h',
          promptTokens: 1,
          outputTokens: 1,
        },
      },
    ])

    const afterLate = executionRunReducer(completed, {
      kind: 'event',
      event: { type: 'milestones-extracted', count: 9 },
    })

    expect(afterLate.status).toBe('Completed')
  })

  it('does not let a late failure clobber a completed run into Failed via non-terminal frames', () => {
    const failed = drive(initialExecutionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'ExecutePlan' } },
      { kind: 'event', event: { type: 'failed', reason: 'boom' } },
      { kind: 'event', event: { type: 'committed', commitSha: 'abc', pushed: false } },
    ])

    expect(failed.status).toBe('Failed')
  })

  it('clears all run state on reset', () => {
    const failed = drive(initialExecutionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'ExecutePlan' } },
      { kind: 'event', event: { type: 'failed', reason: 'boom' } },
    ])

    const reset = executionRunReducer(failed, { kind: 'reset' })

    expect(reset).toEqual(initialExecutionRunState)
  })
})
