import type {
  CommitPreparation,
  ExecutionContextPreview,
  ExecutionGitActionEligibility,
  ExecutionPromptManifest,
  ExecutionSessionSummary,
  ExecutionSessionTransparency,
  ExecutionStatus,
  PushAttemptResult,
} from '../types'
import { invokeCommand } from './tauri'

const fallbackBackendUrl = 'http://127.0.0.1:5000'

export async function getBackendUrl() {
  try {
    const url = await invokeCommand<string>('get_backend_url')
    return normalizeBackendUrl(url)
  } catch {
    return fallbackBackendUrl
  }
}

export function normalizeBackendUrl(url: string) {
  return url.replace(/\/$/, '')
}

export async function getExecutionStatus(backendUrl: string, sessionId: string) {
  const response = await fetch(`${backendUrl}/api/execution-sessions/${sessionId}/status`)
  if (!response.ok) {
    throw new Error(`execution status lookup failed with status ${response.status}`)
  }

  return response.json() as Promise<ExecutionStatus>
}

export function previewExecutionContext(repositoryId: string) {
  return invokeCommand<ExecutionContextPreview>('preview_execution_context', {
    repositoryId,
  })
}

export function startExecution(repositoryId: string) {
  return invokeCommand<ExecutionSessionSummary>('start_execution', { repositoryId })
}

export function cancelExecution(repositoryId: string) {
  return invokeCommand<ExecutionSessionSummary>('cancel_execution', { repositoryId })
}

export function getExecutionPromptManifest(sessionId: string) {
  return invokeCommand<ExecutionPromptManifest>('get_execution_prompt_manifest', { sessionId })
}

export function getExecutionTransparency(sessionId: string) {
  return invokeCommand<ExecutionSessionTransparency>('get_execution_transparency', { sessionId })
}

export function acceptExecutionHandoff(sessionId: string) {
  return invokeCommand<ExecutionSessionSummary>('accept_execution_handoff', { sessionId })
}

export function rejectExecutionHandoff(sessionId: string) {
  return invokeCommand<ExecutionSessionSummary>('reject_execution_handoff', { sessionId })
}

export function prepareCommit(sessionId: string) {
  return invokeCommand<CommitPreparation>('prepare_commit', { sessionId })
}

export function getExecutionGitEligibility(
  sessionId: string,
  commitMessage: string | null,
  selectedPaths: string[],
) {
  return invokeCommand<ExecutionGitActionEligibility>('get_execution_git_eligibility', {
    sessionId,
    commitMessage,
    selectedPaths,
  })
}

export function commitExecution(
  sessionId: string,
  message: string,
  selectedPaths: string[],
  statusSnapshotId: string,
) {
  return invokeCommand<ExecutionSessionSummary>('commit_execution', {
    sessionId,
    message,
    selectedPaths,
    statusSnapshotId,
  })
}

export function pushExecution(sessionId: string) {
  return invokeCommand<PushAttemptResult>('push_execution', { sessionId })
}
