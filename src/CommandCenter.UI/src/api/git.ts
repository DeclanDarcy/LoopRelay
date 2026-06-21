import type { RepositoryGitStatus } from '../types'
import { invokeCommand } from './tauri'

export function getGitStatus(repositoryId: string) {
  return invokeCommand<RepositoryGitStatus>('get_git_status', { repositoryId })
}
