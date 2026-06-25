import type {
  CertificationFindingExplanation,
} from '../../components/explainability'
import type {
  ExplanationConstraint,
  ExplanationDiagnostic,
  ExplanationEvidence,
  ExplanationTone,
  ExplanationUncertainty,
  ReasoningCertificationEvidence,
  ReasoningConceptMaterializationReview,
  ReasoningDiagnosticGroup,
  ReasoningMaterializationOutcome,
  ReasoningProvenance,
  ReasoningReconstruction,
  ReasoningReconstructionConfidence,
  ReasoningReconstructionEvidence,
  ReasoningReference,
  ReasoningTaxonomyMaterializationFinding,
} from '../../types'

export function reasoningReferenceToEvidence(
  reference: ReasoningReference,
  label: string = reference.kind,
): ExplanationEvidence {
  return {
    id: `${reference.kind}:${reference.id}`,
    label,
    detail: formatReasoningReference(reference),
    source: reference.relativePath,
    fingerprint: reference.fingerprint ?? undefined,
  }
}

export function reasoningProvenanceToEvidence(
  provenance: ReasoningProvenance,
  label = 'Provenance',
): ExplanationEvidence {
  return {
    label,
    detail: `${provenance.sourceKind} by ${provenance.capturedBy}`,
    source: provenance.relativePath,
    fingerprint: provenance.fingerprint ?? undefined,
  }
}

export function reasoningReconstructionEvidenceToEvidence(
  evidence: ReasoningReconstructionEvidence[],
): ExplanationEvidence[] {
  return evidence.map((item) => ({
    id: `${item.kind}:${item.id}`,
    label: `${item.kind} ${item.id}: ${item.title}`,
    detail: item.summary,
    source: item.reference?.relativePath ?? item.provenance?.relativePath,
    fingerprint: item.reference?.fingerprint ?? item.provenance?.fingerprint ?? undefined,
  }))
}

export function reasoningReconstructionConfidenceToDiagnostics(
  confidence: ReasoningReconstructionConfidence,
): ExplanationDiagnostic[] {
  return [
    {
      label: `${confidence.level} confidence`,
      detail: confidence.rationale,
      tone: toneFromConfidence(confidence.level),
    },
    {
      label: 'Event evidence',
      detail: confidence.eventEvidencePresent ? 'Present' : 'Not present',
      tone: confidence.eventEvidencePresent ? 'success' : 'warning',
    },
    {
      label: 'Relationship evidence',
      detail: confidence.relationshipEvidencePresent ? 'Present' : 'Not present',
      tone: confidence.relationshipEvidencePresent ? 'success' : 'warning',
    },
    {
      label: 'Trace diagnostics',
      detail: confidence.traceDiagnosticsPresent ? 'Present' : 'Not present',
      tone: confidence.traceDiagnosticsPresent ? 'warning' : 'success',
    },
  ]
}

export function reasoningReconstructionConfidenceToUncertainty(
  confidence: ReasoningReconstructionConfidence,
): ExplanationUncertainty[] {
  return [
    ...confidence.missingEvidence.map((item) => ({
      label: 'Missing evidence',
      detail: item,
      severity: 'warning' as const,
    })),
    ...confidence.whyNotHigher.map((item) => ({
      label: 'Why confidence was not higher',
      detail: item,
      severity: 'info' as const,
    })),
  ]
}

export function reasoningReconstructionScopeToEvidence(
  reconstruction: ReasoningReconstruction,
): ExplanationEvidence[] {
  return [
    reasoningReferenceToEvidence(reconstruction.scope.target, 'Target'),
    ...(reconstruction.scope.source
      ? [reasoningReferenceToEvidence(reconstruction.scope.source, 'Source')]
      : [{ label: 'Source', detail: 'Not reported' }]),
    {
      label: 'Historical cutoff',
      detail: reconstruction.scope.historicalCutoff ?? 'Current graph',
    },
    {
      label: 'Reachable evidence',
      detail: `${reconstruction.scope.reachableEvidence.length} item(s)`,
    },
    {
      label: 'Known unreachable evidence',
      detail: `${reconstruction.scope.unreachableEvidence.length} item(s)`,
    },
  ]
}

export function reasoningDiagnosticsToExplanation(
  diagnostics: string[],
  label = 'Reasoning diagnostic',
  tone: ExplanationTone = 'neutral',
): ExplanationDiagnostic[] {
  return diagnostics.map((diagnostic) => ({
    label,
    detail: diagnostic,
    tone,
  }))
}

export function reasoningDiagnosticGroupsToExplanation(
  groups: ReasoningDiagnosticGroup[] | null | undefined,
): ExplanationDiagnostic[] {
  return (groups ?? []).flatMap((group) =>
    group.diagnostics.map((diagnostic) => ({
      label: group.title ?? group.category,
      detail: diagnostic,
      tone: toneFromDiagnosticGroup(group.category),
      evidence: [
        {
          label: 'Diagnostic category',
          detail: group.category,
        },
      ],
    })),
  )
}

export function reasoningMaterializationConceptToEvidence(
  concept: ReasoningConceptMaterializationReview,
): ExplanationEvidence[] {
  return [
    {
      label: `${concept.concept} branch reason`,
      detail: concept.branchReason,
    },
    ...concept.evidence.map((item) => ({
      label: `${concept.concept} evidence`,
      detail: item,
    })),
    ...concept.elevatedRiskSignals.map((item) => ({
      label: `${concept.concept} elevated risk signal`,
      detail: item,
    })),
  ]
}

export function reasoningMaterializationConceptToConstraints(
  concept: ReasoningConceptMaterializationReview,
): ExplanationConstraint[] {
  return [
    {
      label: 'Failed scenario threshold',
      detail: `${concept.failedScenarioCount}/${concept.failedScenarioThreshold}`,
      satisfied: concept.failedScenarioCount >= concept.failedScenarioThreshold,
    },
    {
      label: 'Repeated workflow threshold',
      detail: `${concept.repeatedWorkflowCount}/${concept.repeatedWorkflowThreshold}`,
      satisfied: concept.repeatedWorkflowCount >= concept.repeatedWorkflowThreshold,
    },
    {
      label: 'Recommended outcome',
      detail: concept.recommendation,
      satisfied: concept.recommendation !== 'RejectConcept',
    },
  ]
}

export function reasoningMaterializationConceptToDiagnostics(
  concept: ReasoningConceptMaterializationReview,
): ExplanationDiagnostic[] {
  return [
    {
      label: `${concept.concept} recommendation`,
      detail: concept.summary,
      tone: toneFromMaterializationOutcome(concept.recommendation),
    },
    ...concept.risks.map((risk) => ({
      label: `${concept.concept} risk`,
      detail: risk,
      tone: 'warning' as const,
    })),
  ]
}

export function reasoningTaxonomyFindingsToDiagnostics(
  findings: ReasoningTaxonomyMaterializationFinding[],
): ExplanationDiagnostic[] {
  return findings.map((finding) => ({
    label: `${finding.family} taxonomy finding`,
    detail: `${finding.summary} (${finding.eventTypeCount}/${finding.eventTypeThreshold} event types; terminal event types ${finding.terminalEventTypePresent ? finding.terminalEventTypes.join(', ') : 'absent'}). ${finding.riskReason}`,
    tone: finding.lifecycleRisk ? 'warning' : 'neutral',
    evidence: finding.evidence.map((item) => ({
      label: `${finding.family} taxonomy evidence`,
      detail: item,
    })),
  }))
}

export function reasoningCertificationEvidenceToFindings(
  evidence: ReasoningCertificationEvidence[],
): CertificationFindingExplanation[] {
  return evidence.map((item) => ({
    id: item.id,
    title: item.scenario,
    category: item.passed ? 'Passed' : 'Failed',
    passed: item.passed,
    detail: item.summary,
    evidence: item.references.map((reference) => reasoningReferenceToEvidence(reference)),
    diagnostics: item.details.map((detail) => ({
      label: item.passed ? 'Certification detail' : 'Certification failure detail',
      detail,
      tone: item.passed ? 'success' : 'danger',
    })),
  }))
}

export function formatReasoningReference(reference: ReasoningReference) {
  const qualifiers = [reference.relativePath, reference.section].filter(Boolean)
  return qualifiers.length > 0
    ? `${reference.kind} ${reference.id} (${qualifiers.join(' - ')})`
    : `${reference.kind} ${reference.id}`
}

function toneFromConfidence(level: string): ExplanationTone {
  const normalized = level.toLowerCase()
  if (normalized.includes('high')) {
    return 'success'
  }
  if (normalized.includes('limited') || normalized.includes('low')) {
    return 'warning'
  }
  return 'neutral'
}

function toneFromDiagnosticGroup(category: string): ExplanationTone {
  const normalized = category.toLowerCase()
  if (normalized.includes('risk') || normalized.includes('boundary') || normalized.includes('validation')) {
    return 'warning'
  }
  if (normalized.includes('confidence') || normalized.includes('evidence')) {
    return 'info'
  }
  return 'neutral'
}

function toneFromMaterializationOutcome(outcome: ReasoningMaterializationOutcome): ExplanationTone {
  switch (outcome) {
    case 'RemainDerived':
      return 'success'
    case 'RejectConcept':
      return 'warning'
    case 'PromoteToFirstClassEntity':
      return 'danger'
    default:
      return 'info'
  }
}
