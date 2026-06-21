import { Button, StatusBadge } from '../design'
import { repositoryExecutionStatus } from '../../lib/status'
import type { RepositoryDashboardProjection } from '../../types'

type SidebarProps = {
  repositories: RepositoryDashboardProjection[]
  selectedRepositoryId: string | null
  isLoading: boolean
  onOpenPalette: () => void
  onSelectRepository: (repositoryId: string) => void
}

const globalNavigationItems = ['Overview', 'Repositories', 'Executions', 'Insights']

export function Sidebar({
  repositories,
  selectedRepositoryId,
  isLoading,
  onOpenPalette,
  onSelectRepository,
}: SidebarProps) {
  return (
    <aside className="app-sidebar" aria-label="Command Center navigation">
      <div className="sidebar-brand">
        <span>Kernritsu</span>
        <strong>Compass</strong>
        <small>Command Center</small>
      </div>

      <Button
        type="button"
        variant="secondary"
        className="command-launcher"
        onClick={onOpenPalette}
      >
        <span>Command</span>
        <kbd>Ctrl K</kbd>
      </Button>

      <nav className="global-nav" aria-label="Global navigation">
        {globalNavigationItems.map((item) => (
          <button
            type="button"
            key={item}
            className={`global-nav-item${item === 'Repositories' ? ' selected' : ''}`}
            disabled={item !== 'Repositories'}
          >
            {item}
          </button>
        ))}
      </nav>

      <section className="sidebar-repositories" aria-label="Registered repositories">
        <div className="sidebar-section-header">
          <span>Repositories</span>
          <small>{repositories.length}</small>
        </div>

        {isLoading ? (
          <p className="sidebar-empty">Loading repositories...</p>
        ) : repositories.length === 0 ? (
          <p className="sidebar-empty">No repositories registered.</p>
        ) : (
          <div className="sidebar-repository-list">
            {repositories.map((entry) => {
              const isSelected = entry.repository.id === selectedRepositoryId

              return (
                <button
                  type="button"
                  key={entry.repository.id}
                  className={`sidebar-repository-item${isSelected ? ' selected' : ''}`}
                  aria-current={isSelected ? 'page' : undefined}
                  onClick={() => onSelectRepository(entry.repository.id)}
                >
                  <span className="sidebar-repository-name">{entry.repository.name}</span>
                  <StatusBadge status={repositoryExecutionStatus[entry.executionState]} />
                  {entry.continuitySummary.pendingProposalExists ? (
                    <span className="proposal-indicator">Proposal</span>
                  ) : null}
                </button>
              )
            })}
          </div>
        )}
      </section>
    </aside>
  )
}
