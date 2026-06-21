import { useCallback, useEffect, useState } from 'react'
import { formatError, loadArtifactContent } from '../api'

export function useArtifactContent(repositoryId: string | null, relativePath: string | null) {
  const [data, setData] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId || !relativePath) {
      setData('')
      setIsLoading(false)
      return ''
    }

    setIsLoading(true)
    setError(null)
    try {
      const content = await loadArtifactContent(repositoryId, relativePath)
      setData(content)
      return content
    } catch (loadError) {
      const message = formatError(loadError)
      setError(message)
      return ''
    } finally {
      setIsLoading(false)
    }
  }, [relativePath, repositoryId])

  useEffect(() => {
    if (!repositoryId || !relativePath) {
      const timeoutId = window.setTimeout(() => {
        setData('')
        setIsLoading(false)
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    let isCurrent = true
    const timeoutId = window.setTimeout(() => {
      setIsLoading(true)
      setError(null)

      loadArtifactContent(repositoryId, relativePath)
        .then((content) => {
          if (isCurrent) {
            setData(content)
          }
        })
        .catch((loadError) => {
          if (isCurrent) {
            setError(formatError(loadError))
          }
        })
        .finally(() => {
          if (isCurrent) {
            setIsLoading(false)
          }
        })
    }, 0)

    return () => {
      isCurrent = false
      window.clearTimeout(timeoutId)
    }
  }, [relativePath, repositoryId])

  return { data, setData, isLoading, error, refresh }
}
