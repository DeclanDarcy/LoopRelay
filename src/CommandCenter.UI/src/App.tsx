import { useCallback, useEffect, useMemo, useState } from 'react'
import { invoke } from '@tauri-apps/api/core'
import './App.css'

type RepositoryAvailability = 'Available' | 'Missing' | 'AccessDenied'
type ExecutionReadiness = 'MissingPlan' | 'MissingMilestones' | 'Ready'

type Repository = {
  id: string
  name: string
  path: string
}

type RepositoryDashboardProjection = {
  repository: Repository
  availability: RepositoryAvailability
  readiness: ExecutionReadiness
  milestoneCount: number
  hasCurrentHandoff: boolean
  hasCurrentDecisions: boolean
}

const availabilityLabels: Record<RepositoryAvailability, string> = {
  Available: 'Available',
  Missing: 'Missing',
  AccessDenied: 'Access denied',
}

function formatError(error: unknown) {
  return error instanceof Error ? error.message : String(error)
}

function App() {
  const [repositories, setRepositories] = useState<RepositoryDashboardProjection[]>([])
  const [selectedRepositoryId, setSelectedRepositoryId] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isAdding, setIsAdding] = useState(false)
  const [removingRepositoryId, setRemovingRepositoryId] = useState<string | null>(null)

  const selectedRepository = useMemo(
    () =>
      repositories.find((entry) => entry.repository.id === selectedRepositoryId) ??
      repositories[0] ??
      null,
    [repositories, selectedRepositoryId],
  )

  const loadRepositories = useCallback(async () => {
    setIsLoading(true)
    setError(null)
    try {
      const nextRepositories = await invoke<RepositoryDashboardProjection[]>(
        'list_repositories',
      )
      setRepositories(nextRepositories)
      setSelectedRepositoryId((currentId) => {
        if (nextRepositories.length === 0) {
          return null
        }

        if (
          currentId &&
          nextRepositories.some((entry) => entry.repository.id === currentId)
        ) {
          return currentId
        }

        return nextRepositories[0].repository.id
      })
    } catch (loadError) {
      setError(formatError(loadError))
    } finally {
      setIsLoading(false)
    }
  }, [])

  async function addRepository() {
    setIsAdding(true)
    setError(null)
    setMessage(null)
    try {
      const selectedPath = await invoke<string | null>('select_repository_directory')

      if (!selectedPath) {
        return
      }

      await invoke('register_repository', { path: selectedPath })
      setMessage('Repository registered.')
      await loadRepositories()
    } catch (addError) {
      setError(formatError(addError))
    } finally {
      setIsAdding(false)
    }
  }

  async function removeRepository(repository: Repository) {
    const confirmed = window.confirm(
      `Remove ${repository.name} from Command Center?\n\nRepository files will not be deleted.`,
    )

    if (!confirmed) {
      return
    }

    setRemovingRepositoryId(repository.id)
    setError(null)
    setMessage(null)
    try {
      await invoke('remove_repository', { repositoryId: repository.id })
      setMessage('Repository registration removed.')
      await loadRepositories()
    } catch (removeError) {
      setError(formatError(removeError))
    } finally {
      setRemovingRepositoryId(null)
    }
  }

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void loadRepositories()
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [loadRepositories])

  return (
    <main className="app-shell">
      <header className="app-header">
        <div>
          <p className="eyebrow">Command Center</p>
          <h1>Repositories</h1>
        </div>
        <div className="header-actions">
          <button type="button" className="secondary-action" onClick={loadRepositories}>
            Refresh
          </button>
          <button
            type="button"
            className="primary-action"
            onClick={addRepository}
            disabled={isAdding}
          >
            {isAdding ? 'Adding...' : 'Add Repository'}
          </button>
        </div>
      </header>

      {error ? <div className="notice error">{error}</div> : null}
      {message ? <div className="notice success">{message}</div> : null}

      <section className="workspace-grid" aria-label="Repository workspace">
        <section className="repository-list" aria-label="Registered repositories">
          <div className="section-heading">
            <h2>Dashboard</h2>
            <span>{repositories.length} registered</span>
          </div>

          {isLoading ? (
            <p className="empty-state">Loading repositories...</p>
          ) : repositories.length === 0 ? (
            <p className="empty-state">No repositories registered.</p>
          ) : (
            <div className="repository-items">
              {repositories.map((entry) => {
                const isSelected = entry.repository.id === selectedRepository?.repository.id

                return (
                  <button
                    type="button"
                    key={entry.repository.id}
                    className={`repository-item${isSelected ? ' selected' : ''}`}
                    onClick={() => setSelectedRepositoryId(entry.repository.id)}
                  >
                    <span className="repository-name">{entry.repository.name}</span>
                    <span className="repository-path">{entry.repository.path}</span>
                    <span
                      className={`availability availability-${entry.availability.toLowerCase()}`}
                    >
                      {availabilityLabels[entry.availability]}
                    </span>
                  </button>
                )
              })}
            </div>
          )}
        </section>

        <section className="repository-details" aria-label="Repository details">
          <div className="section-heading">
            <h2>Details</h2>
          </div>

          {selectedRepository ? (
            <div className="details-body">
              <div className="details-title-row">
                <div>
                  <p className="eyebrow">Selected repository</p>
                  <h3>{selectedRepository.repository.name}</h3>
                </div>
                <span
                  className={`availability availability-${selectedRepository.availability.toLowerCase()}`}
                >
                  {availabilityLabels[selectedRepository.availability]}
                </span>
              </div>

              <dl className="details-list">
                <div>
                  <dt>Name</dt>
                  <dd>{selectedRepository.repository.name}</dd>
                </div>
                <div>
                  <dt>Path</dt>
                  <dd>{selectedRepository.repository.path}</dd>
                </div>
                <div>
                  <dt>Availability</dt>
                  <dd>{availabilityLabels[selectedRepository.availability]}</dd>
                </div>
              </dl>

              <div className="details-actions">
                <button
                  type="button"
                  className="danger-action"
                  onClick={() => void removeRepository(selectedRepository.repository)}
                  disabled={removingRepositoryId === selectedRepository.repository.id}
                >
                  {removingRepositoryId === selectedRepository.repository.id
                    ? 'Removing...'
                    : 'Remove Registration'}
                </button>
              </div>
            </div>
          ) : (
            <p className="empty-state">Select or add a repository.</p>
          )}
        </section>
      </section>
    </main>
  )
}

export default App
