import type {
  CertificationFindingExplanation,
} from '../../components/explainability'
import type {
  DecisionCertificationEvidence,
  DecisionEvidenceInspectionItem,
  DecisionGenerationCertificationFinding,
  DecisionGovernanceFinding,
  DecisionLifecycleEntityEligibility,
  DecisionSourceAttribution,
  DecisionSourceReference,
  ExplanationAction,
  ExplanationDiagnostic,
  ExplanationEvidence,
  ExplanationTone,
} from '../../types'

function toneFromSeverity(severity: string): ExplanationTone {
  const normalized = severity.toLowerCase()

  if (normalized.includes('blocking') || normalized.includes('critical') || normalized.includes('error')) {
    return 'danger'
  }

  if (normalized.includes('warning') || normalized.includes('failed')) {
    return 'warning'
  }

  if (normalized.includes('info') || normalized.includes('passed')) {
    return 'info'
  }

  return 'neutral'
}

function relatedEvidence(
  relatedDecisionIds: string[],
  relatedCandidateIds: string[],
  relatedProposalIds: string[],
): ExplanationEvidence[] {
  return [
    ...relatedDecisionIds.map((id) => ({ label: `Decision ${id}`, detail: 'Related decision' })),
    ...relatedCandidateIds.map((id) => ({ label: `Candidate ${id}`, detail: 'Related candidate' })),
    ...relatedProposalIds.map((id) => ({ label: `Proposal ${id}`, detail: 'Related proposal' })),
  ]
}

export function decisionSourceReferencesToEvidence(
  sources: DecisionSourceReference[],
): ExplanationEvidence[] {
  return sources.map((source, index) => ({
    id: `${source.sourceKind}-${source.relativePath ?? 'none'}-${source.itemId ?? index}`,
    label: source.sourceKind,
    detail: [
      source.section,
      source.excerpt,
      source.itemId ? `Item ${source.itemId}` : null,
      source.decisionId ? `Decision ${source.decisionId}` : null,
      source.proposalId ? `Proposal ${source.proposalId}` : null,
      source.candidateId ? `Candidate ${source.candidateId}` : null,
    ].filter(Boolean).join(' | '),
    source: source.relativePath,
  }))
}

export function decisionSourceAttributionsToEvidence(
  sources: DecisionSourceAttribution[],
): ExplanationEvidence[] {
  return sources.map((source, index) => ({
    id: `${source.appliesToKind}-${source.relativePath ?? 'none'}-${source.itemId ?? index}`,
    label: source.sourceKind,
    detail: [
      source.appliesToKind,
      source.itemId,
      source.section,
      source.excerpt,
    ].filter(Boolean).join(' | '),
    source: source.relativePath,
  }))
}

export function decisionCertificationEvidenceToFindings(
  evidence: DecisionCertificationEvidence[],
): CertificationFindingExplanation[] {
  return evidence.map((item) => ({
    id: item.id,
    title: `${item.area}: ${item.id}`,
    category: item.area,
    passed: item.passed,
    detail: item.detail,
    evidence: [
      ...decisionSourceReferencesToEvidence(item.sources),
      ...relatedEvidence(item.relatedDecisionIds, item.relatedCandidateIds, item.relatedProposalIds),
    ],
    diagnostics: [],
  }))
}

export function decisionGovernanceFindingsToCertificationFindings(
  findings: DecisionGovernanceFinding[],
): CertificationFindingExplanation[] {
  return findings.map((finding) => ({
    id: finding.id,
    title: finding.title,
    category: `${finding.severity} / ${finding.category}`,
    passed: false,
    detail: finding.detail,
    evidence: [
      ...decisionSourceReferencesToEvidence(finding.sources),
      ...relatedEvidence(finding.relatedDecisionIds, finding.relatedCandidateIds, finding.relatedProposalIds),
      {
        label: finding.blocksExecutionProjection ? 'Blocks execution projection' : 'Advisory',
        detail: 'Governance projection impact',
      },
    ],
    diagnostics: [],
  }))
}

export function decisionGenerationCertificationFindingsToExplanation(
  findings: DecisionGenerationCertificationFinding[],
): CertificationFindingExplanation[] {
  return findings.map((finding) => ({
    id: finding.id,
    title: finding.summary,
    category: finding.category,
    passed: finding.passed,
    detail: finding.detail,
    evidence: [
      ...decisionSourceReferencesToEvidence(finding.sources),
      ...relatedEvidence(finding.relatedDecisionIds, finding.relatedCandidateIds, finding.relatedProposalIds),
    ],
    diagnostics: [],
  }))
}

export function decisionDiagnosticsToExplanation(
  diagnostics: string[],
  label = 'Decision Diagnostic',
): ExplanationDiagnostic[] {
  return diagnostics.map((diagnostic) => ({
    label,
    detail: diagnostic,
  }))
}

export function decisionGovernanceFindingsToDiagnostics(
  findings: DecisionGovernanceFinding[],
): ExplanationDiagnostic[] {
  return findings.map((finding) => ({
    label: `${finding.severity}: ${finding.category}`,
    detail: `${finding.title}: ${finding.detail}`,
    tone: toneFromSeverity(finding.severity),
    evidence: [
      ...decisionSourceReferencesToEvidence(finding.sources),
      ...relatedEvidence(finding.relatedDecisionIds, finding.relatedCandidateIds, finding.relatedProposalIds),
      {
        label: finding.blocksExecutionProjection ? 'Blocks execution projection' : 'Advisory',
        detail: 'Governance projection impact',
      },
    ],
  }))
}

export function decisionEvidenceInspectionItemsToEvidence(
  items: DecisionEvidenceInspectionItem[],
): ExplanationEvidence[] {
  return items.map((item, index) => ({
    id: `${item.appliesToKind}-${item.itemId ?? 'proposal'}-${index}`,
    label: item.summary,
    detail: `${item.appliesToKind}${item.itemId ? ` | ${item.itemId}` : ''}`,
  }))
}

export function decisionLifecycleEligibilityToActions(
  eligibility: DecisionLifecycleEntityEligibility,
): ExplanationAction[] {
  return [...eligibility.allowedActions, ...eligibility.blockedActions].map((action) => ({
    label: action.displayName,
    detail: `${eligibility.entityKind} ${eligibility.entityId}: ${eligibility.currentState} -> ${action.targetState}.`,
    eligible: action.isAllowed,
    reason: action.reason,
    command: action.commandName,
    constraints: [
      {
        label: action.governingRule,
        detail: action.reason ?? 'Allowed by backend lifecycle rules.',
        satisfied: action.isAllowed,
      },
      ...action.requiredInputs.map((input) => ({
        label: `Required input: ${input}`,
        detail: `${input} must be supplied by the command caller.`,
        satisfied: action.isAllowed,
      })),
    ],
  }))
}
