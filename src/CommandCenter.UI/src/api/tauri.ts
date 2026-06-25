import { invoke } from '@tauri-apps/api/core'
import type { BoundaryViolationProjection } from '../types'

export type TransportErrorPayload = {
  error: string
  boundaryViolation?: BoundaryViolationProjection | null
}

export class TransportError extends Error {
  readonly payload: TransportErrorPayload

  readonly boundaryViolation: BoundaryViolationProjection | null

  constructor(payload: TransportErrorPayload, cause?: unknown) {
    super(payload.error, { cause })
    this.name = 'TransportError'
    this.payload = payload
    this.boundaryViolation = payload.boundaryViolation ?? null
  }
}

export function formatError(error: unknown) {
  return error instanceof Error ? error.message : String(error)
}

export function getBoundaryViolation(error: unknown) {
  if (error instanceof TransportError) {
    return error.boundaryViolation
  }

  const payload = parseTransportErrorPayload(error)
  return payload?.boundaryViolation ?? null
}

export async function invokeCommand<T>(command: string, args?: Record<string, unknown>) {
  try {
    return await invoke<T>(command, args)
  } catch (error) {
    const payload = parseTransportErrorPayload(error)
    if (payload) {
      throw new TransportError(payload, error)
    }

    throw new Error(formatError(error), { cause: error })
  }
}

function parseTransportErrorPayload(error: unknown): TransportErrorPayload | null {
  const raw = error instanceof Error ? error.message : typeof error === 'string' ? error : null
  if (!raw) {
    return null
  }

  try {
    const parsed = JSON.parse(raw) as Partial<TransportErrorPayload>
    if (typeof parsed.error !== 'string') {
      return null
    }

    return {
      error: parsed.error,
      boundaryViolation: isBoundaryViolationProjection(parsed.boundaryViolation)
        ? parsed.boundaryViolation
        : null,
    }
  } catch {
    return null
  }
}

function isBoundaryViolationProjection(value: unknown): value is BoundaryViolationProjection {
  if (!value || typeof value !== 'object') {
    return false
  }

  const candidate = value as Partial<BoundaryViolationProjection>
  return (
    typeof candidate.boundaryRule === 'string' &&
    typeof candidate.owningDomain === 'string' &&
    typeof candidate.rejectedAssertion === 'string' &&
    typeof candidate.allowedAlternative === 'string' &&
    typeof candidate.diagnosticDetail === 'string' &&
    typeof candidate.severity === 'string'
  )
}
