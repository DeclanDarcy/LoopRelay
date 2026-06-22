import type {
  CreateDecisionAssimilationRecommendationCommand,
  Decision,
  DecisionAssimilationRecommendation,
  DecisionCandidate,
  DecisionContextSnapshot,
  DecisionEvidenceInspection,
  DecisionGovernanceReport,
  ExecutionDecisionProjection,
  DecisionOptionComparison,
  DecisionProposal,
  DecisionProposalBrowserItem,
  DecisionProposalLineage,
  DecisionProposalState,
  DecisionRefinementRequest,
  DecisionReviewWorkspace,
  ResolveDecisionCommand,
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

export function getDecisionProposalLineage(repositoryId: string, proposalId: string) {
  return invokeCommand<DecisionProposalLineage>('get_decision_proposal_lineage', {
    repositoryId,
    proposalId,
  })
}

export function refineDecisionProposal(
  repositoryId: string,
  proposalId: string,
  request: DecisionRefinementRequest,
) {
  return invokeCommand<DecisionProposal>('refine_decision_proposal', {
    repositoryId,
    proposalId,
    request,
  })
}

export function resolveDecisionProposal(
  repositoryId: string,
  proposalId: string,
  request: ResolveDecisionCommand,
) {
  return invokeCommand<Decision>('resolve_decision_proposal', {
    repositoryId,
    proposalId,
    request,
  })
}

export function getDecisionAssimilationRecommendation(repositoryId: string, decisionId: string) {
  return invokeCommand<DecisionAssimilationRecommendation>('get_decision_assimilation_recommendation', {
    repositoryId,
    decisionId,
  })
}

export function proposeDecisionOperationalContextAssimilation(
  repositoryId: string,
  decisionId: string,
  request: CreateDecisionAssimilationRecommendationCommand,
) {
  return invokeCommand<DecisionAssimilationRecommendation>(
    'propose_decision_operational_context_assimilation',
    {
      repositoryId,
      decisionId,
      request,
    },
  )
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

export function getDecisionGovernance(repositoryId: string) {
  return invokeCommand<DecisionGovernanceReport>('get_decision_governance', { repositoryId })
}

export function generateDecisionGovernanceReport(repositoryId: string) {
  return invokeCommand<DecisionGovernanceReport>('generate_decision_governance_report', {
    repositoryId,
  })
}

export function listDecisionGovernanceReports(repositoryId: string) {
  return invokeCommand<DecisionGovernanceReport[]>('list_decision_governance_reports', {
    repositoryId,
  })
}

export function getExecutionDecisionProjection(repositoryId: string) {
  return invokeCommand<ExecutionDecisionProjection>('get_execution_decision_projection', {
    repositoryId,
  })
}
