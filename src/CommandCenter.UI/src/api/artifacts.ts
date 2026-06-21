import type { RepositoryWorkspaceProjection } from '../types'
import { invokeCommand } from './tauri'

export function loadArtifactContent(repositoryId: string, relativePath: string) {
  return invokeCommand<string>('load_artifact_content', { repositoryId, relativePath })
}

export function saveArtifactContent(repositoryId: string, relativePath: string, content: string) {
  return invokeCommand<void>('save_artifact_content', { repositoryId, relativePath, content })
}

export function rotateCurrentHandoff(repositoryId: string) {
  return invokeCommand<RepositoryWorkspaceProjection>('rotate_current_handoff', { repositoryId })
}

export function rotateCurrentDecisions(repositoryId: string) {
  return invokeCommand<RepositoryWorkspaceProjection>('rotate_current_decisions', { repositoryId })
}
