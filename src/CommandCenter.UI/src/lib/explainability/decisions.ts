import type {
  CertificationFindingExplanation,
} from '../../components/explainability'
import type {
  DecisionCertificationEvidence,
  DecisionEvidence,
  DecisionEvidenceInspectionItem,
  DecisionGenerationExecutiveReport,
  DecisionGenerationCertificationFinding,
  DecisionGenerationDiagnostics,
  DecisionGovernanceFinding,
  DecisionInfluenceStatement,
  DecisionLifecycleEntityEligibility,
  DecisionOption,
  DecisionProjectionDecisionDiagnostic,
  DecisionQualityAssessment,
  DecisionQualitySignal,
  DecisionQualitySignalContribution,
  DecisionRecommendation,
  RecommendationEvidence,
  HumanAuthoringBurdenExplanation,
  HumanAuthoringBurdenSignal,
  RefinementPlan,
  DecisionSourceAttribution,
  DecisionSourceReference,
  Explanation,
  ExplanationAlternative,
  ExplanationAction,
  ExplanationConstraint,
  ExplanationDiagnostic,
  ExplanationEvidence,
  ExplanationTone,
  ExplanationUncertainty,
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

export function decisionEvidenceToEvidence(
  evidence: DecisionEvidence[],
  label = 'Decision Evidence',
): ExplanationEvidence[] {
  return evidence.flatMap((item, index) => [
    {
      id: `${label}-${index}`,
      label,
      detail: item.summary,
    },
    ...decisionSourceReferencesToEvidence(item.sources),
  ])
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

export function decisionGenerationExecutiveReportToEvidence(
  report: DecisionGenerationExecutiveReport,
): ExplanationEvidence[] {
  return report.evidence.map((item, index) => ({
    id: `generation-executive-evidence-${index}`,
    label: 'Executive readiness evidence',
    detail: item,
  }))
}

export function decisionGenerationExecutiveReportToDiagnostics(
  report: DecisionGenerationExecutiveReport,
): ExplanationDiagnostic[] {
  return [
    ...report.blockingGaps.map((gap) => ({
      label: 'Executive readiness blocking gap',
      detail: gap,
      tone: 'warning' as const,
    })),
    ...decisionDiagnosticsToExplanation(report.diagnostics, 'Executive readiness diagnostic'),
  ]
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

export function decisionQualitySignalsToDiagnostics(
  signals: DecisionQualitySignal[],
): ExplanationDiagnostic[] {
  return signals.map((signal) => ({
    label: `${signal.category} / ${signal.direction} / ${signal.severity}`,
    detail: `${signal.summary}: ${signal.detail}`,
    tone: toneFromSeverity(signal.severity),
    evidence: decisionSourceReferencesToEvidence(signal.sources),
  }))
}

export function decisionQualityContributionsToDiagnostics(
  contributions: DecisionQualitySignalContribution[],
): ExplanationDiagnostic[] {
  return contributions.map((contribution) => ({
    label: `${formatSignedNumber(contribution.scoreContribution)} score | ${contribution.signalId}`,
    detail: contribution.summary,
    tone: toneFromSeverity(contribution.severity),
    evidence: [
      {
        label: `${contribution.category} / ${contribution.direction} / ${contribution.severity}`,
        detail: 'Quality signal contribution',
      },
    ],
  }))
}

export function decisionQualityAssessmentToExplanation(
  assessment: DecisionQualityAssessment,
): Explanation {
  const explanation = assessment.qualityExplanation

  return {
    id: assessment.id,
    domain: 'Decision Quality',
    title: `${assessment.decisionId}: ${assessment.rating}`,
    summary: `Score ${assessment.score}`,
    why: explanation
      ? `${explanation.threshold.reason}${explanation.overrideReason ? ` Override: ${explanation.overrideReason}` : ''}`
      : 'No quality explanation is projected.',
    evidence: [],
    constraints: explanation
      ? [
          {
            label: `Threshold: ${explanation.threshold.rating}`,
            detail: `${explanation.threshold.minimumScore ?? 'none'}-${explanation.threshold.maximumScore ?? 'none'}`,
            satisfied: true,
          },
          {
            label: 'Base score',
            detail: `${explanation.baseScore}`,
            satisfied: true,
          },
          {
            label: 'Raw score',
            detail: `${explanation.rawScore}`,
            satisfied: true,
          },
          {
            label: 'Clamped score',
            detail: `${explanation.clampedScore}`,
            satisfied: true,
          },
        ]
      : [],
    diagnostics: [
      ...(explanation ? decisionQualityContributionsToDiagnostics(explanation.signalContributions) : []),
      ...decisionDiagnosticsToExplanation(explanation?.diagnostics ?? assessment.diagnostics, 'Quality Diagnostic'),
    ],
  }
}

export function humanAuthoringBurdenSignalToEvidence(
  signal: HumanAuthoringBurdenSignal,
): ExplanationEvidence[] {
  return [
    {
      id: signal.id,
      label: `${signal.burden} / ${signal.sourceKind}`,
      detail: signal.summary,
    },
    ...decisionSourceReferencesToEvidence(signal.sources),
  ]
}

export function humanAuthoringBurdenExplanationToDiagnostics(
  explanation: HumanAuthoringBurdenExplanation,
): ExplanationDiagnostic[] {
  return [
    {
      label: 'Selection rule',
      detail: explanation.selectionRule,
      evidence: explanation.winningSignal ? humanAuthoringBurdenSignalToEvidence(explanation.winningSignal) : [],
    },
    {
      label: 'Effective burden',
      detail: `${explanation.effectiveBurden}${explanation.isUnknown ? ' | Unknown' : ' | Known'}${explanation.isInferred ? ' | Inferred' : ' | Signal-backed'}`,
      evidence: explanation.winningSignal ? humanAuthoringBurdenSignalToEvidence(explanation.winningSignal) : [],
    },
    ...decisionDiagnosticsToExplanation(explanation.diagnostics, 'Burden Diagnostic'),
  ]
}

export function humanAuthoringBurdenExplanationToExplanation(
  explanation: HumanAuthoringBurdenExplanation,
): Explanation {
  const winningSignalEvidence = explanation.winningSignal
    ? humanAuthoringBurdenSignalToEvidence(explanation.winningSignal)
    : []

  return {
    domain: 'Human Authoring Burden',
    title: explanation.decisionId,
    summary: `Effective burden: ${explanation.effectiveBurden}`,
    why: explanation.selectionRule,
    evidence: explanation.winningSignal
      ? [
          {
            id: `${explanation.winningSignal.id}-winning-signal`,
            label: `Winning signal: ${explanation.winningSignal.id}`,
            detail: explanation.winningSignal.summary,
          },
          ...winningSignalEvidence,
        ]
      : [],
    constraints: [
      {
        label: explanation.isUnknown ? 'Unknown burden' : 'Known burden',
        detail: explanation.isUnknown
          ? 'No authoritative burden signal was projected.'
          : 'An authoritative burden classification was projected.',
        satisfied: !explanation.isUnknown,
      },
      {
        label: explanation.isInferred ? 'Inferred' : 'Signal-backed',
        detail: explanation.isInferred
          ? 'The burden was inferred from available quality evidence.'
          : 'The burden was selected from projected burden signals.',
        satisfied: !explanation.isInferred,
      },
    ],
    diagnostics: humanAuthoringBurdenExplanationToDiagnostics(explanation),
  }
}

export function decisionRecommendationToExplanation(
  recommendation: DecisionRecommendation,
): Explanation {
  return {
    domain: 'Decision Recommendation',
    title: recommendation.optionId,
    summary: recommendation.summary ?? recommendation.rationale,
    why: recommendation.rationale,
    evidence: [
      ...decisionEvidenceToEvidence(recommendation.evidence, 'Recommendation Evidence'),
      ...decisionRecommendationEvidenceToEvidence(recommendation.recommendationEvidence ?? []),
    ],
    constraints: (recommendation.concerns ?? []).map((concern) => ({
      label: 'Concern',
      detail: concern,
      satisfied: null,
    })),
    assumptions: (recommendation.assumptions ?? []).map((assumption) => ({
      label: 'Recommendation assumption',
      detail: assumption,
    })),
    alternatives: (recommendation.alternativeExplanations ?? []).map((alternative) => ({
      label: 'Alternative explanation',
      detail: alternative,
    })),
    recommendations: (recommendation.supportingFactors ?? []).map((factor) => ({
      label: 'Supporting factor',
      detail: factor,
    })),
    diagnostics: [
      ...(recommendation.mode ? [{ label: 'Recommendation mode', detail: recommendation.mode }] : []),
      ...(recommendation.supportingFactors ?? []).map((factor) => ({
        label: 'Supporting factor',
        detail: factor,
      })),
    ],
  }
}

export function decisionRecommendationEvidenceToEvidence(
  evidence: RecommendationEvidence[],
): ExplanationEvidence[] {
  return evidence.flatMap((item) => [
    {
      id: `${item.type}-${item.optionId}-${item.summary}`,
      label: `${item.type}: ${item.optionId}`,
      detail: item.summary,
    },
    ...decisionEvidenceToEvidence(item.evidence, `${item.type} Evidence`),
  ])
}

export function decisionOptionsToAlternatives(
  options: DecisionOption[],
  label: string,
): ExplanationAlternative[] {
  return options.map((option) => ({
    label: `${label}: ${option.id}`,
    detail: `${option.title}: ${option.description}`,
    evidence: decisionEvidenceToEvidence(option.evidence, `${option.id} Evidence`),
  }))
}

export function decisionGenerationDiagnosticsToRejectedOptionDiagnostics(
  diagnostics: DecisionGenerationDiagnostics,
): ExplanationDiagnostic[] {
  return diagnostics.optionValidationResults
    .filter((result) => !result.isValid)
    .flatMap((result) =>
      result.issues.map((issue) => ({
        label: `Invalid option ${result.optionId}`,
        detail: `${issue.type}: ${issue.message}`,
        tone: 'warning' as const,
      })),
    )
}

export function refinementPlanToConstraints(plan: RefinementPlan): ExplanationConstraint[] {
  return [
    ...plan.appliedConstraints.map((constraint) => ({
      label: 'Applied constraint',
      detail: constraint,
      satisfied: true,
    })),
    ...plan.directives.map((directive) => ({
      label: `${directive.type}: ${directive.targetField ?? 'Proposal'}`,
      detail: directive.instruction ?? directive.summary,
      satisfied: true,
      evidence: directive.sources ? decisionSourceReferencesToEvidence(directive.sources) : [],
    })),
  ]
}

export function refinementPlanToDiagnostics(plan: RefinementPlan): ExplanationDiagnostic[] {
  const scope = [
    plan.regenerateOptions ? 'Options' : null,
    plan.reevaluateTradeoffs ? 'Tradeoffs' : null,
    plan.reevaluateRecommendation ? 'Recommendation' : null,
    plan.fullRegeneration ? 'Full regeneration' : null,
  ].filter(Boolean)

  return [
    {
      label: 'Plan scope',
      detail: scope.length > 0 ? scope.join(', ') : 'No mutation scope detected',
    },
    ...decisionDiagnosticsToExplanation(plan.diagnostics, 'Refinement Diagnostic'),
  ]
}

export function decisionProjectionDiagnosticsToExplanation(
  decisions: DecisionProjectionDecisionDiagnostic[],
  label: string,
): ExplanationDiagnostic[] {
  return decisions.map((decision) => ({
    label: `${label}: ${decision.decisionId}`,
    detail: decision.reason,
    evidence: [
      {
        label: decision.title,
        detail: `${decision.state} | ${decision.outcome ?? 'No outcome'} | ${decision.classification}`,
      },
      {
        label: 'Projected statement count',
        detail: `${decision.projectedStatementIds.length}`,
      },
      ...decision.projectedStatementIds.map((statementId) => ({
        label: 'Projected statement',
        detail: statementId,
      })),
    ],
  }))
}

export function decisionInfluenceStatementsToEvidence(
  statements: DecisionInfluenceStatement[],
  label: string,
): ExplanationEvidence[] {
  return statements.flatMap((statement) => [
    {
      id: statement.statementId,
      label: `${label}: ${statement.decisionId}`,
      detail: statement.statement,
    },
    {
      id: `${statement.statementId}-metadata`,
      label: statement.title,
      detail: `${statement.classification} | ${statement.projectionKind} | ${statement.promptSection}`,
    },
    ...(statement.priorityRank
      ? [
          {
            id: `${statement.statementId}-rank`,
            label: `Rank ${statement.priorityRank}`,
            detail: 'Priority rank',
          },
        ]
      : []),
    ...decisionSourceReferencesToEvidence(statement.sources),
  ])
}

export function decisionInfluenceStatementAdherenceToDiagnostics(
  statements: DecisionInfluenceStatement[],
): ExplanationDiagnostic[] {
  return statements.flatMap((statement) =>
    statement.adherenceObservations.map((observation) => ({
      label: `Adherence: ${statement.statementId}`,
      detail: `${observation.observer} at ${observation.observedAt}: ${observation.observation}`,
      evidence: [
        {
          label: statement.decisionId,
          detail: statement.title,
        },
      ],
    })),
  )
}

export function decisionInfluenceMissingStatementUncertainty(
  statements: DecisionInfluenceStatement[],
  label: string,
): ExplanationUncertainty[] {
  return statements.length > 0
    ? []
    : [
        {
          label,
          detail: 'No projected statements were recorded for this category.',
          severity: 'info',
        },
      ]
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

function formatSignedNumber(value: number) {
  return value > 0 ? `+${value}` : `${value}`
}
