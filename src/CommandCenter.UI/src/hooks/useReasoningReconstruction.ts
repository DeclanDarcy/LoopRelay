import { useCallback, useEffect, useState } from 'react'
import { formatError, getBoundaryViolation, reconstructReasoning } from '../api'
import type { BoundaryViolationProjection, ReasoningQuery, ReasoningReconstruction } from '../types'

export function useReasoningReconstruction(repositoryId: string | null) {
  const [data, setData] = useState<ReasoningReconstruction | null>(null)
  const [isRunning, setIsRunning] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [boundaryViolation, setBoundaryViolation] = useState<BoundaryViolationProjection | null>(null)

  const run = useCallback(
    async (query: ReasoningQuery) => {
      if (!repositoryId) {
        setData(null)
        return null
      }

      setIsRunning(true)
      setError(null)
      setBoundaryViolation(null)
      try {
        const reconstruction = await reconstructReasoning(repositoryId, query)
        setData(reconstruction)
        return reconstruction
      } catch (reconstructionError) {
        const message = formatError(reconstructionError)
        setData(null)
        setError(message)
        setBoundaryViolation(getBoundaryViolation(reconstructionError))
        return null
      } finally {
        setIsRunning(false)
      }
    },
    [repositoryId],
  )

  useEffect(() => {
    if (!repositoryId) {
      const timeoutId = window.setTimeout(() => {
        setData(null)
        setIsRunning(false)
        setError(null)
        setBoundaryViolation(null)
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    return undefined
  }, [repositoryId])

  return {
    data,
    isRunning,
    error,
    boundaryViolation,
    run,
  }
}
