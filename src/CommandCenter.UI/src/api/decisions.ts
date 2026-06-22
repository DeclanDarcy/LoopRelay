import type {
  DecisionCandidate,
  DecisionContextSnapshot,
  DecisionEvidenceInspection,
  DecisionOptionComparison,
  DecisionProposal,
  DecisionProposalBrowserItem,
  DecisionProposalState,
  DecisionReviewWorkspace,
  DecisionSourceAttribution,
} from '../types'
import { invokeCommand } from './tauri'

export function getDecisionContext(repositoryId: string) {
  return invokeCommand<DecisionContextSnapshot>('get_decision_context', { repositoryId })
}

export function buildDecisionContext(repositoryId: string) {
  return invokeCommand<DecisionContextSnapshot>('build_decision_context', { repositoryId })
}

export function listDecisionCandidates(repositoryId: string) {
  return invokeCommand<DecisionCandidate[]>('list_decision_candidates', { repositoryId })
}

export function listDecisionProposals(repositoryId: string) {
  return invokeCommand<DecisionProposal[]>('list_decision_proposals', { repositoryId })
}

export function listDecisionProposalBrowser(
  repositoryId: string,
  states: DecisionProposalState[] = [],
) {
  return invokeCommand<DecisionProposalBrowserItem[]>('list_decision_proposal_browser', {
    repositoryId,
    states,
  })
}

export function getDecisionProposal(repositoryId: string, proposalId: string) {
  return invokeCommand<DecisionProposal>('get_decision_proposal', { repositoryId, proposalId })
}

export function getDecisionProposalReview(repositoryId: string, proposalId: string) {
  return invokeCommand<DecisionReviewWorkspace>('get_decision_proposal_review', {
    repositoryId,
    proposalId,
  })
}

export function getDecisionOptionComparison(repositoryId: string, proposalId: string) {
  return invokeCommand<DecisionOptionComparison>('get_decision_option_comparison', {
    repositoryId,
    proposalId,
  })
}

export function getDecisionEvidenceInspection(repositoryId: string, proposalId: string) {
  return invokeCommand<DecisionEvidenceInspection>('get_decision_evidence_inspection', {
    repositoryId,
    proposalId,
  })
}

export function listDecisionSourceAttributions(repositoryId: string, proposalId: string) {
  return invokeCommand<DecisionSourceAttribution[]>('list_decision_source_attributions', {
    repositoryId,
    proposalId,
  })
}
