import type { ContinuityDiagnostics, ContinuityReport } from '../types'
import { invokeCommand } from './tauri'

export function getContinuityDiagnostics(repositoryId: string) {
  return invokeCommand<ContinuityDiagnostics>('get_continuity_diagnostics', { repositoryId })
}

export function generateContinuityReport(repositoryId: string) {
  return invokeCommand<ContinuityReport>('generate_continuity_report', { repositoryId })
}

export function listContinuityReports(repositoryId: string) {
  return invokeCommand<ContinuityReport[]>('list_continuity_reports', { repositoryId })
}
