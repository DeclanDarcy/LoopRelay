import type { OperationalContextProposal } from '../types'
import { invokeCommand } from './tauri'

export function generateOperationalContextProposal(repositoryId: string) {
  return invokeCommand<OperationalContextProposal>('generate_operational_context_proposal', {
    repositoryId,
  })
}

export function getOperationalContextProposal(repositoryId: string, proposalId: string) {
  return invokeCommand<OperationalContextProposal>('get_operational_context_proposal', {
    repositoryId,
    proposalId,
  })
}

export function editOperationalContextProposal(
  repositoryId: string,
  proposalId: string,
  content: string,
) {
  return invokeCommand<OperationalContextProposal>('edit_operational_context_proposal', {
    repositoryId,
    proposalId,
    content,
  })
}

export function acceptOperationalContextProposal(
  repositoryId: string,
  proposalId: string,
  reviewNote: string | null,
) {
  return invokeCommand<OperationalContextProposal>('accept_operational_context_proposal', {
    repositoryId,
    proposalId,
    reviewNote,
  })
}

export function rejectOperationalContextProposal(
  repositoryId: string,
  proposalId: string,
  reviewNote: string | null,
) {
  return invokeCommand<OperationalContextProposal>('reject_operational_context_proposal', {
    repositoryId,
    proposalId,
    reviewNote,
  })
}

export function promoteOperationalContextProposal(repositoryId: string, proposalId: string) {
  return invokeCommand<OperationalContextProposal>('promote_operational_context_proposal', {
    repositoryId,
    proposalId,
  })
}
