import { Button, StatusBadge } from '../design'
import { workflowProjectionStatus } from '../../lib/status'
import type { RepositoryDashboardProjection, WorkflowInstance } from '../../types'

type HeaderProps = {
  selectedRepository: RepositoryDashboardProjection | null
  workflow: WorkflowInstance | null
  isWorkspaceLoading: boolean
  isAddingRepository: boolean
  onRefreshRepositories: () => void
  onRefreshWorkspace: () => void
  onAddRepository: () => void
}

export function Header({
  selectedRepository,
  workflow,
  isWorkspaceLoading,
  isAddingRepository,
  onRefreshRepositories,
  onRefreshWorkspace,
  onAddRepository,
}: HeaderProps) {
  return (
    <header className="app-header">
      <div className="header-title">
        <p className="breadcrumb">Command Center / Repositories</p>
        <h1>Repositories</h1>
        <p>
          {selectedRepository
            ? `Selected ${selectedRepository.repository.name} - ${selectedRepository.repository.path}`
            : 'Select or register a repository.'}
        </p>
      </div>
      <div className="header-status">
        {selectedRepository ? (
          <StatusBadge status={workflowProjectionStatus(workflow)} />
        ) : null}
        <span
          className="notification-slot notification-slot-disabled"
          aria-label="Notifications unavailable"
          title="Notifications require a backend projection."
        >
          Notifications
        </span>
      </div>
      <div className="header-actions">
        <Button
          type="button"
          variant="secondary"
          className="secondary-action"
          onClick={selectedRepository ? onRefreshWorkspace : onRefreshRepositories}
          disabled={Boolean(selectedRepository) && isWorkspaceLoading}
        >
          {selectedRepository && isWorkspaceLoading ? 'Refreshing...' : 'Refresh'}
        </Button>
        <Button
          type="button"
          variant="primary"
          className="primary-action"
          onClick={onAddRepository}
          disabled={isAddingRepository}
        >
          {isAddingRepository ? 'Adding...' : 'Add Repository'}
        </Button>
      </div>
    </header>
  )
}
