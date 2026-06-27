import { describe, expect, it } from 'vitest'
import { initialPlanAuthoringState, planAuthoringReducer } from './planAuthoringMachine'
import type { PlanAuthoringState } from '../../types'

function drive(state: PlanAuthoringState, actions: Parameters<typeof planAuthoringReducer>[1][]) {
  return actions.reduce(planAuthoringReducer, state)
}

describe('planAuthoringReducer', () => {
  it('starts in Authoring with no plan', () => {
    expect(initialPlanAuthoringState.status).toBe('Authoring')
    expect(initialPlanAuthoringState.plan).toBeNull()
    expect(initialPlanAuthoringState.streamedText).toBe('')
  })

  it('moves to Planning when a write is submitted', () => {
    const next = planAuthoringReducer(initialPlanAuthoringState, { kind: 'write-submitted' })

    expect(next.status).toBe('Planning')
    expect(next.streamedText).toBe('')
    expect(next.failure).toBeNull()
  })

  it('accumulates delta text while planning and completes into PlanReady', () => {
    const next = drive(initialPlanAuthoringState, [
      { kind: 'write-submitted' },
      { kind: 'event', event: { type: 'turn-started', phase: 'WritePlan' } },
      { kind: 'event', event: { type: 'delta', text: '# Plan\n' } },
      { kind: 'event', event: { type: 'delta', text: '1. Do the thing' } },
      {
        kind: 'event',
        event: { type: 'completed', plan: '# Plan\n1. Do the thing', promptTokens: 12, outputTokens: 34 },
      },
    ])

    expect(next.status).toBe('PlanReady')
    expect(next.streamedText).toBe('# Plan\n1. Do the thing')
    expect(next.plan).toBe('# Plan\n1. Do the thing')
    expect(next.tokens).toEqual({ promptTokens: 12, outputTokens: 34 })
    expect(next.turnPhase).toBeNull()
  })

  it('records the turn phase from turn-started so the header can label it', () => {
    const next = drive(initialPlanAuthoringState, [
      { kind: 'write-submitted' },
      { kind: 'event', event: { type: 'turn-started', phase: 'WritePlan' } },
    ])

    expect(next.status).toBe('Planning')
    expect(next.turnPhase).toBe('WritePlan')
  })

  it('revises an existing plan: Revising, re-streams, then PlanReady with the new plan', () => {
    const ready = drive(initialPlanAuthoringState, [
      { kind: 'write-submitted' },
      { kind: 'event', event: { type: 'completed', plan: 'v1', promptTokens: 1, outputTokens: 1 } },
    ])

    const revised = drive(ready, [
      { kind: 'revise-submitted' },
      { kind: 'event', event: { type: 'turn-started', phase: 'RevisePlan' } },
      { kind: 'event', event: { type: 'delta', text: 'v2 streaming' } },
      { kind: 'event', event: { type: 'completed', plan: 'v2', promptTokens: 5, outputTokens: 6 } },
    ])

    expect(revised.status).toBe('PlanReady')
    expect(revised.plan).toBe('v2')
    expect(revised.tokens).toEqual({ promptTokens: 5, outputTokens: 6 })
  })

  it('clears the previous streamed text when a revise turn starts', () => {
    const ready = drive(initialPlanAuthoringState, [
      { kind: 'write-submitted' },
      { kind: 'event', event: { type: 'delta', text: 'old stream' } },
      { kind: 'event', event: { type: 'completed', plan: 'v1', promptTokens: 1, outputTokens: 1 } },
    ])

    const revising = drive(ready, [{ kind: 'revise-submitted' }])

    expect(revising.status).toBe('Revising')
    expect(revising.streamedText).toBe('')
    expect(revising.plan).toBe('v1')
  })

  it('moves to Executing when execute is submitted from PlanReady', () => {
    const ready = drive(initialPlanAuthoringState, [
      { kind: 'write-submitted' },
      { kind: 'event', event: { type: 'completed', plan: 'v1', promptTokens: 1, outputTokens: 1 } },
    ])

    const executing = planAuthoringReducer(ready, { kind: 'execute-submitted' })

    expect(executing.status).toBe('Executing')
    expect(executing.plan).toBe('v1')
  })

  it('surfaces a failed event with reason and detail', () => {
    const failed = drive(initialPlanAuthoringState, [
      { kind: 'write-submitted' },
      { kind: 'event', event: { type: 'failed', reason: 'Agent crashed', detail: 'stack trace' } },
    ])

    expect(failed.status).toBe('Failed')
    expect(failed.failure).toEqual({ reason: 'Agent crashed', detail: 'stack trace' })
  })

  it('surfaces a command failure (e.g. 409) as a failure without losing an existing plan', () => {
    const ready = drive(initialPlanAuthoringState, [
      { kind: 'write-submitted' },
      { kind: 'event', event: { type: 'completed', plan: 'v1', promptTokens: 1, outputTokens: 1 } },
    ])

    const failed = planAuthoringReducer(ready, {
      kind: 'command-failed',
      reason: 'A planning turn is already running.',
    })

    expect(failed.status).toBe('Failed')
    expect(failed.failure).toEqual({ reason: 'A planning turn is already running.', detail: null })
    expect(failed.plan).toBe('v1')
  })

  it('returns from a failure to Authoring on reset', () => {
    const failed = drive(initialPlanAuthoringState, [
      { kind: 'write-submitted' },
      { kind: 'event', event: { type: 'failed', reason: 'boom' } },
    ])

    const reset = planAuthoringReducer(failed, { kind: 'reset' })

    expect(reset.status).toBe('Authoring')
    expect(reset.failure).toBeNull()
    expect(reset.streamedText).toBe('')
  })
})
