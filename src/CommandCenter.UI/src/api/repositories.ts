import type { RepositoryDashboardProjection, RepositoryWorkspaceProjection } from '../types'
import { invokeCommand } from './tauri'

export function listRepositories() {
  return invokeCommand<RepositoryDashboardProjection[]>('list_repositories')
}

export function selectRepositoryDirectory() {
  return invokeCommand<string | null>('select_repository_directory')
}

export function registerRepository(path: string) {
  return invokeCommand<void>('register_repository', { path })
}

export function removeRepository(repositoryId: string) {
  return invokeCommand<void>('remove_repository', { repositoryId })
}

export function getRepositoryWorkspace(repositoryId: string) {
  return invokeCommand<RepositoryWorkspaceProjection>('get_repository_workspace', { repositoryId })
}

export function refreshRepositoryWorkspace(repositoryId: string) {
  return invokeCommand<RepositoryWorkspaceProjection>('refresh_repository_workspace', { repositoryId })
}
