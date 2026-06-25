import type {
  Artifact,
  ArtifactInventory,
  ArtifactType,
  ArtifactVersionKind,
  CommitPreparation,
  ContinuityDiagnostics,
  ContinuityReport,
  ContinuityTrend,
  Decision,
  DecisionAssimilationRecommendation,
  DecisionCandidate,
  DecisionCertificationReport,
  DecisionContextSnapshot,
  DecisionEvidenceInspection,
  DecisionGenerationCertificationReport,
  DecisionGovernanceReport,
  DecisionInfluenceTrace,
  DecisionLifecycleActionEligibility,
  DecisionLifecycleEligibilityProjection,
  DecisionLifecycleEntityEligibility,
  DecisionOutcome,
  DecisionPackageRegenerationRequest,
  DecisionPackageRegenerationResult,
  DecisionProposal,
  DecisionOptionComparison,
  DecisionProposalBrowserItem,
  DecisionProposalLineage,
  DecisionQualityAssessment,
  DecisionQualityReport,
  DecisionQualityTrend,
  DecisionRefinementAnalysisRequest,
  DecisionRefinementRequest,
  DecisionReviewWorkspace,
  DecisionSourceAttribution,
  RefinementPlan,
  ExecutionDecisionProjection,
  ExecutionContextPreview,
  ExecutionEvent,
  ExecutionGitActionEligibility,
  ExecutionPromptManifest,
  ExecutionSessionState,
  ExecutionSession,
  ExecutionSessionSummary,
  ExecutionSessionTransparency,
  PushAttemptResult,
  ManualReasoningCaptureCommand,
  ManualReasoningCaptureTemplate,
  OperationalContextItem,
  OperationalContextProjection,
  OperationalContextProposal,
  OperationalContextProposalSummary,
  ReasoningEvent,
  ReasoningCertificationReport,
  ReasoningGraph,
  ReasoningGraphNode,
  ReasoningGraphRelationship,
  ReasoningMaterializationReviewReport,
  ReasoningQuery,
  ReasoningQueryResult,
  ReasoningReconstruction,
  ReasoningReconstructionEvidence,
  ReasoningRelationship,
  ReasoningTrace,
  ReasoningThread,
  Repository,
  RepositoryDashboardProjection as DashboardEntry,
  RepositoryExecutionState,
  RepositoryGitStatus,
  RepositoryWorkspaceProjection as Workspace,
} from './types'

type InvokeArgs = Record<string, unknown> | undefined

type MockState = {
  repositories: Repository[]
  workspaces: Record<string, Workspace>
  content: Record<string, string>
  sessions: Record<string, ExecutionSession>
  operationalContextProposals: Record<string, OperationalContextProposal[]>
  continuityReports: Record<string, ContinuityReport[]>
  decisionContexts: Record<string, DecisionContextSnapshot>
  decisionCandidates: Record<string, DecisionCandidate[]>
  decisionProposalBrowserItems: Record<string, DecisionProposalBrowserItem[]>
  decisionProposalReviewWorkspaces: Record<string, Record<string, DecisionReviewWorkspace>>
  decisions: Record<string, Record<string, Decision>>
  decisionAssimilationRecommendations: Record<string, Record<string, DecisionAssimilationRecommendation>>
  decisionGovernanceReports: Record<string, DecisionGovernanceReport[]>
  decisionCertificationReports: Record<string, DecisionCertificationReport[]>
  decisionGenerationCertificationReports: Record<string, DecisionGenerationCertificationReport[]>
  decisionQualityAssessments: Record<string, DecisionQualityAssessment[]>
  decisionQualityReports: Record<string, DecisionQualityReport[]>
  decisionQualityTrends: Record<string, DecisionQualityTrend[]>
  reasoningEvents: Record<string, ReasoningEvent[]>
  reasoningThreads: Record<string, ReasoningThread[]>
  reasoningRelationships: Record<string, ReasoningRelationship[]>
  reasoningCertificationReports: Record<string, ReasoningCertificationReport[]>
  commandCalls: Record<string, number>
}

type TauriInternals = {
  invoke: (cmd: string, args?: InvokeArgs) => Promise<unknown>
  transformCallback: (callback: unknown) => number
  unregisterCallback: () => void
  callbacks: Record<string, unknown>
  convertFileSrc: (filePath: string) => string
}

declare global {
  interface Window {
    __TAURI_INTERNALS__?: TauriInternals
    __COMMAND_CENTER_MOCK_STATE__?: MockState
  }
}

const alphaRepository: Repository = {
  id: 'repo-alpha',
  name: 'AlphaRepo',
  path: 'C:\\workspace\\AlphaRepo',
}

const emptyRepository: Repository = {
  id: 'repo-empty',
  name: 'EmptyRepo',
  path: 'C:\\workspace\\EmptyRepo',
}

const planOnlyRepository: Repository = {
  id: 'repo-plan-only',
  name: 'PlanOnlyRepo',
  path: 'C:\\workspace\\PlanOnlyRepo',
}

const certificationRepositories: Repository[] = [
  {
    id: 'repo-cert-executing',
    name: 'CertificationExecuting',
    path: 'C:\\workspace\\CertificationExecuting',
  },
  {
    id: 'repo-cert-awaiting-acceptance',
    name: 'CertificationAwaitingAcceptance',
    path: 'C:\\workspace\\CertificationAwaitingAcceptance',
  },
  {
    id: 'repo-cert-awaiting-commit',
    name: 'CertificationAwaitingCommit',
    path: 'C:\\workspace\\CertificationAwaitingCommit',
  },
  {
    id: 'repo-cert-awaiting-push',
    name: 'CertificationAwaitingPush',
    path: 'C:\\workspace\\CertificationAwaitingPush',
  },
  {
    id: 'repo-cert-failed',
    name: 'CertificationFailed',
    path: 'C:\\workspace\\CertificationFailed',
  },
  {
    id: 'repo-cert-cancelled',
    name: 'CertificationCancelled',
    path: 'C:\\workspace\\CertificationCancelled',
  },
]

const artifacts = {
  plan: artifact('.agents/plan.md', 'plan.md', 'Plan', 'Plan', 'Current'),
  context: artifact(
    '.agents/operational_context.md',
    'operational_context.md',
    'OperationalContext',
    'OperationalContext',
    'Current',
  ),
  milestone: artifact('.agents/milestones/m5.md', 'm5.md', 'Milestone', 'Milestone', 'Current'),
  handoff: artifact('.agents/handoffs/handoff.md', 'handoff.md', 'Handoff', 'Handoff', 'Current'),
  oldHandoff: artifact(
    '.agents/handoffs/handoff.0001.md',
    'handoff.0001.md',
    'Handoff',
    'Handoff',
    'Historical',
  ),
  decisions: artifact(
    '.agents/decisions/decisions.md',
    'decisions.md',
    'Decision',
    'Decision',
    'Current',
  ),
}

function artifact(
  relativePath: string,
  name: string,
  type: ArtifactType,
  family: ArtifactType,
  versionKind: ArtifactVersionKind,
): Artifact {
  return { relativePath, name, type, family, versionKind }
}

function createWorkspace(repository: Repository, inventory: ArtifactInventory): Workspace {
  const readiness = inventory.plan
    ? inventory.milestones.length > 0
      ? 'Ready'
      : 'MissingMilestones'
    : 'MissingPlan'

  const operationalContextProposalSummary = {
    pendingProposalExists: false,
    latestProposalId: null,
    generatedAt: null,
    status: null,
    sourceInputCount: 0,
    contentByteCount: 0,
    contentCharacterCount: 0,
    lastPromotedAt: null,
    lastArchivedRelativePath: null,
  }

  return {
    repository,
    availability: 'Available',
    readiness,
    executionState: 'Ready',
    executionSummary: null,
    executionHistory: [],
    artifactInventory: inventory,
    milestoneCount: inventory.milestones.length,
    hasPlan: inventory.plan !== null,
    hasOperationalContext: inventory.operationalContext !== null,
    hasCurrentHandoff: inventory.currentHandoff !== null,
    hasCurrentDecisions: inventory.currentDecisions !== null,
    operationalContextProposalSummary,
    operationalContext: createOperationalContextProjection(inventory, operationalContextProposalSummary),
    reasoningSummary: createEmptyReasoningSummary(),
    decisionSessionSummary: createEmptyDecisionSessionSummary(),
  }
}

function createEmptyReasoningSummary(): Workspace['reasoningSummary'] {
  return {
    eventCount: 0,
    threadCount: 0,
    relationshipCount: 0,
    hypothesisEventCount: 0,
    alternativeEventCount: 0,
    contradictionEventCount: 0,
    directionEventCount: 0,
    decisionEvolutionEventCount: 0,
    assumptionEvolutionEventCount: 0,
    constraintEvolutionEventCount: 0,
    evidenceEventCount: 0,
    lastEventAt: null,
    lastThreadActivityAt: null,
    lastRelationshipAt: null,
    lastActivityAt: null,
    lastReconstructionAt: null,
    lastCertificationAt: null,
    certificationResult: null,
  }
}

function createEmptyDecisionSessionSummary(): Workspace['decisionSessionSummary'] {
  return {
    decisionSessionId: null,
    state: null,
    lifecycleDecision: null,
    transferEligibilityStatus: null,
    estimatedTokenCount: null,
    estimatedCacheTtl: null,
    cacheMissRisk: null,
    coherenceScore: null,
    transferPressure: null,
    healthDimensions: [],
    recentTransferLineage: [],
    diagnostics: [],
    generatedAt: null,
  }
}

function refreshReasoningSummary(state: MockState, repositoryId: string) {
  const workspace = state.workspaces[repositoryId]
  if (!workspace) {
    return
  }

  const events = state.reasoningEvents[repositoryId] ?? []
  const threads = state.reasoningThreads[repositoryId] ?? []
  const relationships = state.reasoningRelationships[repositoryId] ?? []
  const lastEventAt = latestTimestamp(events.map((event) => event.createdAt))
  const lastThreadActivityAt = latestTimestamp(threads.map((thread) => thread.updatedAt))
  const lastRelationshipAt = latestTimestamp(relationships.map((relationship) => relationship.createdAt))

  workspace.reasoningSummary = {
    eventCount: events.length,
    threadCount: threads.length,
    relationshipCount: relationships.length,
    hypothesisEventCount: events.filter((event) => event.family === 'Hypothesis').length,
    alternativeEventCount: events.filter((event) => event.family === 'Alternative').length,
    contradictionEventCount: events.filter((event) => event.family === 'Contradiction').length,
    directionEventCount: events.filter((event) => event.family === 'Direction').length,
    decisionEvolutionEventCount: events.filter((event) => event.family === 'DecisionEvolution').length,
    assumptionEvolutionEventCount: events.filter((event) => event.family === 'AssumptionEvolution').length,
    constraintEvolutionEventCount: events.filter((event) => event.family === 'ConstraintEvolution').length,
    evidenceEventCount: events.filter((event) => event.family === 'Evidence').length,
    lastEventAt,
    lastThreadActivityAt,
    lastRelationshipAt,
    lastActivityAt: latestTimestamp([lastEventAt, lastThreadActivityAt, lastRelationshipAt]),
    lastReconstructionAt: null,
    lastCertificationAt: null,
    certificationResult: null,
  }
}

function latestTimestamp(values: Array<string | null | undefined>): string | null {
  return values
    .filter((value): value is string => typeof value === 'string' && value.length > 0)
    .sort()
    .at(-1) ?? null
}

function createOperationalContextProjection(
  inventory: ArtifactInventory,
  proposalSummary: OperationalContextProposalSummary,
): OperationalContextProjection {
  if (!inventory.operationalContext) {
    return {
      exists: false,
      currentRelativePath: null,
      revisionCount: inventory.historicalOperationalContexts.length,
      currentRevisionNumber: 0,
      lastUpdatedAt: null,
      lastPromotionAt: proposalSummary.lastPromotedAt,
      currentUnderstandingSummary: [],
      architecture: [],
      authorityBoundaries: [],
      constraints: [],
      stableDecisions: [],
      decisionRationale: [],
      openQuestions: [],
      activeRisks: [],
      recentUnderstandingChanges: [],
      pendingProposalSummary: proposalSummary,
      latestReviewState: null,
      continuityWarnings: [],
    }
  }

  return {
    exists: true,
    currentRelativePath: inventory.operationalContext.relativePath,
    revisionCount: inventory.historicalOperationalContexts.length + 1,
    currentRevisionNumber: inventory.historicalOperationalContexts.length + 1,
    lastUpdatedAt: new Date().toISOString(),
    lastPromotionAt: proposalSummary.lastPromotedAt,
    currentUnderstandingSummary: [
      'Repository-owned artifacts carry current project understanding.',
    ],
    architecture: [
      operationalContextItem('architecture', 'Backend projections feed the workspace surface.'),
    ],
    authorityBoundaries: [
      operationalContextItem('authority', 'The UI displays continuity state without computing it.'),
    ],
    constraints: [
      operationalContextItem('constraint', 'Operational-context changes require review before promotion.'),
    ],
    stableDecisions: [
      operationalContextItem('decision', 'Execution sessions remain disposable.'),
    ],
    decisionRationale: [
      operationalContextItem('rationale', 'Repository artifacts survive restarts.'),
    ],
    openQuestions: [
      operationalContextItem('question', 'Which warning categories should be shown first?'),
    ],
    activeRisks: [
      operationalContextItem('risk', 'Projection drift could confuse review state.'),
    ],
    recentUnderstandingChanges: [
      operationalContextItem('recent-change', 'M7 mock data exposes current understanding.'),
    ],
    pendingProposalSummary: proposalSummary,
    latestReviewState: null,
    continuityWarnings: [],
  }
}

function operationalContextItem(kind: string, text: string): OperationalContextItem {
  return {
    id: `${kind}-${text.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '')}`,
    kind,
    text,
    rationale: null,
    sourceRelativePath: null,
  }
}

function decisionSource(relativePath: string, excerpt: string) {
  return {
    sourceKind: 'CurrentDecisionMarkdown',
    relativePath,
    section: null,
    itemId: null,
    decisionId: null,
    proposalId: null,
    candidateId: null,
    excerpt,
  }
}

function createDecisionContext(repository: Repository): DecisionContextSnapshot {
  const source = decisionSource('.agents/decisions/decisions.md', 'Decision lifecycle UI requires backend-owned state.')
  return {
    snapshotId: `context-${repository.id}`,
    repositoryId: repository.id,
    createdAt: new Date().toISOString(),
    fingerprint: `mock-decision-context-${repository.id}`,
    context: {
      repositoryId: repository.id,
      fingerprint: `mock-decision-context-${repository.id}`,
      items: [
        {
          id: 'current-decisions',
          kind: 'CurrentDecisionMarkdown',
          title: 'Current decisions',
          content: 'Decision lifecycle UI requires backend-owned state.',
          required: false,
          fingerprint: 'mock-current-decisions',
          sources: [source],
        },
      ],
      diagnostics: {
        sources: [
          {
            name: 'CurrentDecisionMarkdown',
            relativePath: '.agents/decisions/decisions.md',
            required: false,
            status: 'Loaded',
            message: null,
            byteCount: 64,
            characterCount: 64,
            fingerprint: 'mock-current-decisions',
          },
        ],
        warnings: [],
      },
      validation: {
        isValid: true,
        errors: [],
        warnings: [],
      },
    },
    diagnostics: {
      sources: [
        {
          name: 'CurrentDecisionMarkdown',
          relativePath: '.agents/decisions/decisions.md',
          required: false,
          status: 'Loaded',
          message: null,
          byteCount: 64,
          characterCount: 64,
          fingerprint: 'mock-current-decisions',
        },
      ],
      warnings: [],
    },
    validation: {
      isValid: true,
      errors: [],
      warnings: [],
    },
  }
}

function createDecisionCandidates(repository: Repository): DecisionCandidate[] {
  const source = decisionSource('.agents/plan.md', 'The review workspace should expose proposals without resolution.')
  const createCandidate = (
    id: string,
    state: DecisionCandidate['state'],
    priority: DecisionCandidate['priority'],
    classification: DecisionCandidate['classification'],
    title: string,
    summary: string,
  ): DecisionCandidate => ({
    id,
    repositoryId: repository.id,
    state,
    priority,
    classification,
    title,
    summary,
    sourceFingerprint: `mock-candidate-source-${id}`,
    signals: [
      {
        kind: 'OpenDecision',
        summary: 'UI must observe backend-owned lifecycle state.',
        classification,
        priority,
        evidence: [{ summary: 'Authorized UI phase is observational.', sources: [source] }],
      },
    ],
    evidence: [{ summary: 'Backend read models are stable enough for UI shell wiring.', sources: [source] }],
    sources: [source],
    diagnostics: [],
    history: [],
  })

  return [
    createCandidate(
      'CAND-0001',
      'Promoted',
      'High',
      'Architectural',
      'Decision review workspace UI boundary',
      'The Decisions UI should consume backend read models without reconstructing lifecycle authority.',
    ),
    createCandidate(
      'CAND-0002',
      'Discovered',
      'Medium',
      'Tactical',
      'Candidate browser navigation',
      'Reviewers need upstream candidate context before selecting a proposal.',
    ),
    createCandidate(
      'CAND-0003',
      'Dismissed',
      'Low',
      'Operational',
      'Inline mutation controls',
      'Mutation controls remain out of scope for the current inspection workspace.',
    ),
    createCandidate(
      'CAND-0004',
      'Expired',
      'Medium',
      'Strategic',
      'Outdated review shortcut',
      'This candidate no longer matches the active M4 repository state.',
    ),
    createCandidate(
      'CAND-0005',
      'Duplicate',
      'Low',
      'Tactical',
      'Duplicate proposal browser work',
      'A promoted candidate already covers the same proposal inspection need.',
    ),
  ]
}

function createDecisionProposalBrowserItems(): DecisionProposalBrowserItem[] {
  const timestamp = new Date().toISOString()
  return [
    {
      proposalId: 'PROP-0001',
      candidateId: 'CAND-0001',
      state: 'Generated',
      title: 'Use backend-owned review read models',
      classification: 'Architectural',
      priority: 'High',
      createdAt: timestamp,
      updatedAt: timestamp,
      reviewState: 'NotStarted',
      reviewUpdatedAt: timestamp,
      isResolved: false,
    },
    {
      proposalId: 'PROP-0002',
      candidateId: 'CAND-0001',
      state: 'Viewed',
      title: 'Keep proposal selection in React presentation state',
      classification: 'Tactical',
      priority: 'Medium',
      createdAt: timestamp,
      updatedAt: timestamp,
      reviewState: 'Viewed',
      reviewUpdatedAt: timestamp,
      isResolved: false,
    },
    {
      proposalId: 'PROP-0003',
      candidateId: 'CAND-0001',
      state: 'ReadyForResolution',
      title: 'Defer mutation controls until evidence navigation exists',
      classification: 'Operational',
      priority: 'Medium',
      createdAt: timestamp,
      updatedAt: timestamp,
      reviewState: 'ReadyForResolution',
      reviewUpdatedAt: timestamp,
      isResolved: false,
    },
  ]
}

function createDecisionProposalReviewWorkspaces(
  repository: Repository,
  proposals: DecisionProposalBrowserItem[],
): Record<string, DecisionReviewWorkspace> {
  const timestamp = new Date().toISOString()
  return Object.fromEntries(
    proposals.map((proposal, index) => {
      const proposalSource = {
        sourceKind: 'DecisionProposal',
        relativePath: `.agents/decisions/proposals/${proposal.proposalId}/proposal.json`,
        section: null,
        itemId: null,
        decisionId: null,
        proposalId: proposal.proposalId,
        candidateId: proposal.candidateId,
        excerpt: 'The proposal review workspace is loaded from a backend-owned read model.',
      }
      const planSource = {
        sourceKind: 'Plan',
        relativePath: '.agents/plan.md',
        section: 'Milestone 4',
        itemId: null,
        decisionId: null,
        proposalId: proposal.proposalId,
        candidateId: proposal.candidateId,
        excerpt: 'Provide full proposal inspection before refinement or resolution.',
      }
      const optionIds = [`${proposal.proposalId}-OPT-A`, `${proposal.proposalId}-OPT-B`]
      const workspace: DecisionReviewWorkspace = {
        proposal: {
          id: proposal.proposalId,
          repositoryId: repository.id,
          candidateId: proposal.candidateId,
          state: proposal.state,
          title: proposal.title,
          context:
            'The review workspace must expose proposal context, supported options, tradeoffs, recommendation, assumptions, notes, revisions, diagnostics, and source attribution before mutation controls are introduced.',
          options: [
            {
              id: optionIds[0],
              title: 'Render a read-only backend workspace',
              description: 'Load the selected proposal review workspace and display proposal details without allowing lifecycle mutation.',
              evidence: [
                {
                  summary: 'The current slice authorizes inspection-only review UI.',
                  sources: [planSource],
                },
              ],
            },
            {
              id: optionIds[1],
              title: 'Delay full inspection',
              description: 'Keep only the proposal browser until mutation controls are ready.',
              evidence: [
                {
                  summary: 'Deferring inspection would leave reviewers without enough context.',
                  sources: [proposalSource],
                },
              ],
            },
          ],
          tradeoffs: [
            {
              optionId: optionIds[0],
              benefit: 'Reviewers can inspect the proposal before refinement or resolution.',
              cost: 'The UI needs a larger read-only surface.',
              evidence: [
                {
                  summary: 'M4 exit criteria require full proposal inspection.',
                  sources: [planSource],
                },
              ],
            },
            {
              optionId: optionIds[1],
              benefit: 'Smaller UI change.',
              cost: 'Evidence and review state remain hidden from the reviewer.',
              evidence: [
                {
                  summary: 'Review, refinement, and resolution controls are deferred until inspection exists.',
                  sources: [proposalSource],
                },
              ],
            },
          ],
          recommendation: {
            optionId: optionIds[0],
            rationale:
              'Use the backend review workspace as the source of truth and keep all controls observational in this slice.',
            evidence: [
              {
                summary: 'Human authority should see evidence before any decision mutation path is added.',
                sources: [planSource, proposalSource],
              },
            ],
          },
          assumptions: [
            {
              id: `${proposal.proposalId}-ASM-1`,
              statement: 'The backend review workspace already contains notes, revisions, and diagnostics.',
              evidence: [
                {
                  summary: 'The Tauri command and API wrapper already expose get_decision_proposal_review.',
                  sources: [proposalSource],
                },
              ],
            },
          ],
          evidence: [
            {
              summary: 'Proposal-level evidence remains visible near the proposal summary.',
              sources: [planSource],
            },
          ],
          history: [
            {
              at: timestamp,
              actor: 'system',
              action: 'Generate',
              fromState: null,
              toState: proposal.state,
              reason: 'Mock proposal seeded for review workspace characterization.',
              sources: [proposalSource],
            },
          ],
        },
        review: {
          repositoryId: repository.id,
          proposalId: proposal.proposalId,
          state: proposal.reviewState,
          updatedAt: proposal.reviewUpdatedAt,
          reason: proposal.reviewState === 'NotStarted' ? null : 'Mock review state mirrors proposal browser item.',
          sources: [proposalSource],
        },
        notes:
          index === 0
            ? [
                {
                  id: 'NOTE-0001',
                  repositoryId: repository.id,
                  proposalId: proposal.proposalId,
                  createdAt: timestamp,
                  reviewer: 'reviewer',
                  body: 'Keep mutation controls out until evidence and attribution are visible.',
                  sources: [planSource],
                },
              ]
            : [],
        revisions:
          index === 0
            ? [
                {
                  id: 'REV-0001',
                  repositoryId: repository.id,
                  proposalId: proposal.proposalId,
                  createdAt: timestamp,
                  reason: 'Initial generated review workspace revision.',
                  changedFields: ['context', 'options', 'recommendation'],
                  sourceProposalFingerprint: `mock-fingerprint-${proposal.proposalId}`,
                  sources: [proposalSource],
                  requestedBy: 'mock-reviewer',
                  acceptedChanges: ['Seeded mock revision for lineage display.'],
                  rejectedChanges: [],
                  diagnostics: [],
                  previousOptions: [],
                  retiredOptions: [],
                  previousAssumptions: [],
                  retiredAssumptions: [],
                  previousRecommendationRationale: null,
                  revisedRecommendationRationale: null,
                  previousContext: null,
                  revisedContext: null,
                  revisedOptions: [],
                  previousTradeoffs: [],
                  revisedTradeoffs: [],
                  revisedAssumptions: [],
                  humanAuthoringBurden: 'MinorEdit',
                },
              ]
            : [],
        diagnostics: {
          hasRecommendation: true,
          hasEvidence: true,
          optionCount: 2,
          tradeoffCount: 2,
          assumptionCount: 1,
          noteCount: index === 0 ? 1 : 0,
          warnings: [],
        },
        authority: {
          proposalFingerprint: `mock-current-fingerprint-${proposal.proposalId}`,
          packageId: `PKG-${String(index + 1).padStart(4, '0')}`,
          packageFingerprint: `mock-package-fingerprint-${proposal.proposalId}`,
          packageVersionCreatedAt: timestamp,
          packageSourceProposalFingerprint: `mock-current-fingerprint-${proposal.proposalId}`,
          isPackageCurrentForProposalContent: true,
        },
      }
      return [proposal.proposalId, workspace]
    }),
  )
}

function filterDecisionProposalBrowserItems(
  proposals: DecisionProposalBrowserItem[],
  states: string[],
) {
  if (states.length === 0) {
    return proposals
  }

  const selectedStates = new Set(states)
  return proposals.filter((proposal) => selectedStates.has(proposal.state))
}

function createDecisionLifecycleEligibility(
  state: MockState,
  repositoryId: string,
): DecisionLifecycleEligibilityProjection {
  return {
    repositoryId,
    candidates: (state.decisionCandidates[repositoryId] ?? []).map((candidate) =>
      createLifecycleEntity(
        'Candidate',
        candidate.id,
        candidate.state,
        [
          createLifecycleAction(
            'promote_decision_candidate',
            'Promote',
            'Promoted',
            candidate.state === 'Discovered',
            'DecisionLifecycleRules.ValidateCandidateTransition',
          ),
          createLifecycleAction(
            'dismiss_decision_candidate',
            'Dismiss',
            'Dismissed',
            candidate.state === 'Discovered',
            'DecisionLifecycleRules.ValidateCandidateTransition',
          ),
          createLifecycleAction(
            'expire_decision_candidate',
            'Expire',
            'Expired',
            candidate.state === 'Discovered',
            'DecisionLifecycleRules.ValidateCandidateTransition',
          ),
          createLifecycleAction(
            'mark_decision_candidate_duplicate',
            'Mark duplicate',
            'Duplicate',
            candidate.state === 'Discovered',
            'DecisionLifecycleRules.ValidateCandidateTransition',
          ),
          createLifecycleAction(
            'generate_decision_proposal',
            'Generate proposal',
            'Generated',
            candidate.state === 'Promoted' &&
              !(state.decisionProposalBrowserItems[repositoryId] ?? []).some(
                (proposal) =>
                  proposal.candidateId === candidate.id &&
                  proposal.state !== 'Expired' &&
                  proposal.state !== 'Discarded',
              ),
            'DecisionGenerationService.GenerateProposalAsync',
          ),
        ],
      ),
    ),
    proposals: (state.decisionProposalBrowserItems[repositoryId] ?? []).map((proposal) =>
      createLifecycleEntity(
        'Proposal',
        proposal.proposalId,
        proposal.state,
        [
          createLifecycleAction(
            'mark_decision_proposal_viewed',
            'Mark viewed',
            'Viewed',
            proposal.state === 'Generated',
            'DecisionLifecycleRules.ValidateProposalTransition',
          ),
          createLifecycleAction(
            'mark_decision_proposal_needs_refinement',
            'Needs refinement',
            'NeedsRefinement',
            proposal.state === 'Viewed' || proposal.state === 'ReadyForResolution',
            'DecisionLifecycleRules.ValidateProposalTransition',
          ),
          createLifecycleAction(
            'mark_decision_proposal_ready_for_resolution',
            'Ready for resolution',
            'ReadyForResolution',
            proposal.state === 'Viewed' || proposal.state === 'NeedsRefinement' || proposal.state === 'Refined',
            'DecisionLifecycleRules.ValidateProposalTransition',
          ),
          createLifecycleAction(
            'resolve_decision_proposal',
            'Resolve',
            'Resolved',
            proposal.state === 'ReadyForResolution',
            'DecisionLifecycleRules.ValidateProposalTransition',
          ),
          createLifecycleAction(
            'expire_decision_proposal',
            'Expire',
            'Expired',
            proposal.state !== 'Resolved' && proposal.state !== 'Expired' && proposal.state !== 'Discarded',
            'DecisionLifecycleRules.ValidateProposalTransition',
          ),
          createLifecycleAction(
            'discard_decision_proposal',
            'Discard',
            'Discarded',
            proposal.state !== 'Resolved' && proposal.state !== 'Expired' && proposal.state !== 'Discarded',
            'DecisionLifecycleRules.ValidateProposalTransition',
          ),
        ],
      ),
    ),
    decisions: Object.values(state.decisions[repositoryId] ?? {}).map((decision) =>
      createLifecycleEntity(
        'Decision',
        decision.id,
        decision.state,
        [
          createLifecycleAction(
            'supersede_decision',
            'Supersede',
            'Superseded',
            decision.state === 'Resolved',
            'DecisionLifecycleRules.ValidateDecisionTransition',
          ),
          createLifecycleAction(
            'archive_decision',
            'Archive',
            'Archived',
            decision.state === 'Resolved' || decision.state === 'Superseded',
            'DecisionLifecycleRules.ValidateDecisionTransition',
          ),
        ],
      ),
    ),
    diagnostics: [],
  }
}

function createLifecycleEntity(
  entityKind: string,
  entityId: string,
  currentState: string,
  actions: DecisionLifecycleActionEligibility[],
): DecisionLifecycleEntityEligibility {
  const allowedActions = actions.filter((action) => action.isAllowed)
  const blockedActions = actions.filter((action) => !action.isAllowed)

  return {
    entityKind,
    entityId,
    currentState,
    allowedActions,
    blockedActions,
    allowedNextStates: allowedActions.map((action) => action.targetState),
    blockedNextStates: blockedActions.map((action) => ({
      state: action.targetState,
      reason: action.reason ?? `Transition from ${currentState} to ${action.targetState} is not currently allowed.`,
      governingRule: action.governingRule,
    })),
    diagnostics: [],
  }
}

function createLifecycleAction(
  commandName: string,
  displayName: string,
  targetState: string,
  isAllowed: boolean,
  governingRule: string,
): DecisionLifecycleActionEligibility {
  return {
    commandName,
    displayName,
    targetState,
    isAllowed,
    requiredInputs: ['reason'],
    reason: isAllowed ? null : `Transition to ${targetState} is not currently allowed in the mock lifecycle.`,
    governingRule,
  }
}

function getDecisionReviewWorkspace(
  state: MockState,
  repositoryId: string,
  proposalId: string,
): DecisionReviewWorkspace {
  const workspace = state.decisionProposalReviewWorkspaces[repositoryId]?.[proposalId]
  if (!workspace) {
    throw new Error('Decision proposal review workspace was not found.')
  }

  return workspace
}

function refineDecisionProposalMock(
  state: MockState,
  repositoryId: string,
  proposalId: string,
  request: DecisionRefinementRequest,
): DecisionProposal {
  const workspace = getDecisionReviewWorkspace(state, repositoryId, proposalId)
  if (workspace.proposal.state !== 'NeedsRefinement') {
    throw new Error(`Proposal transition from ${workspace.proposal.state} to Refined is not allowed.`)
  }

  if (!request.reason?.trim()) {
    throw new Error('Refinement reason is required.')
  }

  const nextContext = request.context?.trim() || workspace.proposal.context
  const nextRecommendation = request.recommendation ?? workspace.proposal.recommendation
  if (
    nextContext === workspace.proposal.context &&
    nextRecommendation?.rationale === workspace.proposal.recommendation?.rationale &&
    (request.rejectedChanges?.length ?? 0) === 0
  ) {
    throw new Error('Refinement must change proposal content.')
  }

  const timestamp = new Date().toISOString()
  const revisionId = `REV-${String(workspace.revisions.length + 1).padStart(4, '0')}`
  const source = {
    sourceKind: 'DecisionProposal',
    relativePath: `.agents/decisions/proposals/${proposalId}/proposal.json`,
    section: null,
    itemId: null,
    decisionId: null,
    proposalId,
    candidateId: workspace.proposal.candidateId,
    excerpt: 'Mock refinement request submitted through backend-shaped command.',
  }
  const changedFields = [
    ...(nextContext !== workspace.proposal.context ? ['context'] : []),
    ...(nextRecommendation?.rationale !== workspace.proposal.recommendation?.rationale ? ['recommendation'] : []),
    ...((request.rejectedChanges?.length ?? 0) > 0 ? ['rejectedChanges'] : []),
  ]
  const revision = {
    id: revisionId,
    repositoryId,
    proposalId,
    createdAt: timestamp,
    reason: request.reason.trim(),
    changedFields,
    sourceProposalFingerprint: request.baseProposalFingerprint ?? `mock-current-fingerprint-${proposalId}`,
    sources: [source],
    requestedBy: request.requestedBy?.trim() || null,
    acceptedChanges: changedFields,
    rejectedChanges: request.rejectedChanges ?? [],
    diagnostics: request.baseProposalFingerprint ? [] : ['No base proposal fingerprint supplied.'],
    previousOptions: workspace.proposal.options,
    retiredOptions: [],
    previousAssumptions: workspace.proposal.assumptions,
    retiredAssumptions: [],
    previousRecommendationRationale: workspace.proposal.recommendation?.rationale ?? null,
    revisedRecommendationRationale: nextRecommendation?.rationale ?? null,
    previousContext: workspace.proposal.context,
    revisedContext: nextContext,
    revisedOptions: workspace.proposal.options,
    previousTradeoffs: workspace.proposal.tradeoffs,
    revisedTradeoffs: workspace.proposal.tradeoffs,
    revisedAssumptions: workspace.proposal.assumptions,
    humanAuthoringBurden: 'MinorEdit' as const,
  }
  const refinedProposal: DecisionProposal = {
    ...workspace.proposal,
    state: 'Refined',
    context: nextContext,
    recommendation: nextRecommendation,
    history: [
      ...workspace.proposal.history,
      {
        at: timestamp,
        actor: request.requestedBy?.trim() || 'reviewer',
        action: 'Refined',
        fromState: workspace.proposal.state,
        toState: 'Refined',
        reason: request.reason.trim(),
        sources: [source],
      },
    ],
  }

  workspace.proposal = refinedProposal
  workspace.revisions = [revision, ...workspace.revisions]
  workspace.review = {
    ...workspace.review,
    state: 'Viewed',
    updatedAt: timestamp,
    reason: 'Mock refinement returned proposal to non-authoritative review state.',
  }

  state.decisionProposalBrowserItems[repositoryId] =
    state.decisionProposalBrowserItems[repositoryId]?.map((item) =>
      item.proposalId === proposalId
        ? {
            ...item,
            state: 'Refined',
            reviewState: 'Viewed',
            updatedAt: timestamp,
            reviewUpdatedAt: timestamp,
          }
        : item,
    ) ?? []

  return refinedProposal
}

function analyzeDecisionRefinementMock(
  state: MockState,
  repositoryId: string,
  proposalId: string,
  request: DecisionRefinementAnalysisRequest,
): RefinementPlan {
  const workspace = getDecisionReviewWorkspace(state, repositoryId, proposalId)
  const guidance = request.guidance?.trim() ?? ''
  if (!guidance) {
    throw new Error('Refinement guidance is required.')
  }

  const lowered = guidance.toLowerCase()
  const directives: RefinementPlan['directives'] = []
  const addDirective = (type: RefinementPlan['directives'][number]['type'], summary: string, targetField: string) => {
    directives.push({
      id: `DIR-${String(directives.length + 1).padStart(4, '0')}`,
      type,
      summary,
      targetOptionId: null,
      targetField,
      instruction: guidance,
      sources: [],
    })
  }

  if (lowered.includes('constraint') || lowered.includes('must') || lowered.includes('required')) {
    addDirective('AddConstraint', 'Add or tighten a review constraint.', 'Constraints')
  }
  if (lowered.includes('remove constraint') || lowered.includes('drop constraint')) {
    addDirective('RemoveConstraint', 'Remove or relax a stated constraint.', 'Constraints')
  }
  if (lowered.includes('priority') || lowered.includes('urgent') || lowered.includes('blocking')) {
    addDirective('IncreasePriority', 'Increase priority during option evaluation.', 'Priority')
  }
  if (lowered.includes('alternative') || lowered.includes('another option') || lowered.includes('explore')) {
    addDirective('ExploreAlternative', 'Explore an additional or revised option.', 'Options')
  }
  if (lowered.includes('risk') || lowered.includes('failure')) {
    addDirective('ReevaluateRisk', 'Reevaluate risk analysis.', 'Risks')
  }
  if (lowered.includes('cost') || lowered.includes('effort')) {
    addDirective('ReevaluateCost', 'Reevaluate cost analysis.', 'Costs')
  }
  if (lowered.includes('recommend')) {
    addDirective('ReevaluateRecommendation', 'Reevaluate the recommendation.', 'Recommendation')
  }
  if (lowered.includes('goal') || lowered.includes('scope') || lowered.includes('clarify')) {
    addDirective('ClarifyGoal', 'Clarify the decision goal or scope.', 'Context')
  }
  if (directives.length === 0) {
    addDirective('ClarifyGoal', 'Clarify the reviewer guidance before regeneration.', 'Context')
  }

  return {
    repositoryId,
    proposalId,
    analyzedAt: new Date().toISOString(),
    baseProposalFingerprint:
      request.baseProposalFingerprint ?? workspace.authority.proposalFingerprint,
    directives,
    regenerateOptions: directives.some((directive) =>
      ['ExploreAlternative', 'ClarifyGoal'].includes(directive.type),
    ),
    reevaluateTradeoffs: directives.some((directive) =>
      [
        'AddConstraint',
        'RemoveConstraint',
        'IncreasePriority',
        'DecreasePriority',
        'ReevaluateRisk',
        'ReevaluateCost',
        'ClarifyGoal',
      ].includes(directive.type),
    ),
    reevaluateRecommendation: directives.some((directive) =>
      [
        'AddConstraint',
        'RemoveConstraint',
        'IncreasePriority',
        'DecreasePriority',
        'ReevaluateRecommendation',
        'ClarifyGoal',
      ].includes(directive.type),
    ),
    fullRegeneration: directives.some((directive) => directive.type === 'ClarifyGoal'),
    appliedConstraints: directives.some((directive) => directive.type === 'AddConstraint') ? [guidance] : [],
    diagnostics: [`Analyzed ${directives.length} directive(s) from reviewer guidance.`],
  }
}

function regenerateDecisionRefinementMock(
  state: MockState,
  repositoryId: string,
  proposalId: string,
  request: DecisionPackageRegenerationRequest,
): DecisionPackageRegenerationResult {
  const workspace = getDecisionReviewWorkspace(state, repositoryId, proposalId)
  if (request.basePackageFingerprint !== workspace.authority.packageFingerprint) {
    throw new Error('Base package fingerprint is stale.')
  }

  const timestamp = new Date().toISOString()
  const basePackageVersion = createDecisionPackageVersion(
    workspace,
    request.basePackageId,
    request.basePackageFingerprint,
    workspace.proposal.recommendation,
    timestamp,
  )
  const regeneratedRecommendation = workspace.proposal.recommendation
    ? {
        ...workspace.proposal.recommendation,
        rationale: `${workspace.proposal.recommendation.rationale} Refined by directive guidance.`,
      }
    : null
  const regeneratedPackageId = `PKG-${String(Number.parseInt(request.basePackageId.replace(/\D/g, ''), 10) + 1 || 2).padStart(4, '0')}`
  const regeneratedPackageFingerprint = `mock-regenerated-${proposalId}-${Date.now()}`
  const regeneratedPackageVersion = createDecisionPackageVersion(
    workspace,
    regeneratedPackageId,
    regeneratedPackageFingerprint,
    regeneratedRecommendation,
    timestamp,
  )
  const comparison = {
    proposalId,
    leftPackageId: request.basePackageId,
    rightPackageId: regeneratedPackageId,
    repositoryId,
    leftPackageFingerprint: request.basePackageFingerprint,
    rightPackageFingerprint: regeneratedPackageFingerprint,
    recommendationChanged: true,
    optionsChanged: request.plan.regenerateOptions,
    evidenceChanged: request.plan.directives.length > 0,
    risksChanged: request.plan.reevaluateTradeoffs,
    contextFingerprintChanged: request.plan.fullRegeneration,
    fieldComparisons: [
      {
        field: 'recommendation.rationale',
        changeType: 'Changed',
        previousValue: workspace.proposal.recommendation?.rationale ?? null,
        revisedValue: regeneratedRecommendation?.rationale ?? null,
      },
    ],
    addedOptions: [],
    removedOptions: [],
    modifiedOptions: request.plan.regenerateOptions ? workspace.proposal.options.slice(0, 1) : [],
    addedEvidence: request.plan.appliedConstraints,
    removedEvidence: [],
    addedRisks: request.plan.reevaluateTradeoffs ? ['Directive guidance changed risk or cost analysis.'] : [],
    removedRisks: [],
    diagnostics: ['Mock package comparison generated from refinement directives.'],
  }
  const artifact = {
    id: `REF-${String(workspace.revisions.length + 1).padStart(4, '0')}`,
    repositoryId,
    proposalId,
    createdAt: timestamp,
    request,
    directives: request.plan.directives,
    plan: request.plan,
    basePackageId: request.basePackageId,
    basePackageFingerprint: request.basePackageFingerprint,
    regeneratedPackageId,
    regeneratedPackageFingerprint,
    comparison,
    humanAuthoringBurden: 'MajorRefinement' as const,
    diagnostics: ['Mock refinement artifact persisted for package regeneration.'],
  }

  workspace.authority = {
    ...workspace.authority,
    packageId: regeneratedPackageId,
    packageFingerprint: regeneratedPackageFingerprint,
    packageVersionCreatedAt: timestamp,
  }

  return {
    repositoryId,
    proposalId,
    plan: request.plan,
    basePackageVersion,
    regeneratedPackageVersion,
    comparison,
    humanAuthoringBurden: 'MajorRefinement',
    diagnostics: ['Package regenerated from directive plan.'],
    refinementArtifact: artifact,
  }
}

function createDecisionPackageVersion(
  workspace: DecisionReviewWorkspace,
  packageId: string,
  packageFingerprint: string,
  recommendation: DecisionProposal['recommendation'],
  timestamp: string,
) {
  return {
    id: packageId,
    repositoryId: workspace.proposal.repositoryId,
    proposalId: workspace.proposal.id,
    candidateId: workspace.proposal.candidateId,
    createdAt: timestamp,
    packageFingerprint,
    package: {
      id: packageId,
      repositoryId: workspace.proposal.repositoryId,
      proposalId: workspace.proposal.id,
      candidateId: workspace.proposal.candidateId,
      title: workspace.proposal.title,
      decisionSummary: workspace.proposal.context,
      options: workspace.proposal.options,
      tradeoffs: workspace.proposal.tradeoffs,
      recommendation,
      assumptions: workspace.proposal.assumptions,
      openConcerns: [],
      evidence: workspace.proposal.evidence,
      metadata: {
        contextFingerprint: `mock-context-${workspace.proposal.id}`,
        generatorVersion: 'mock',
        candidateId: workspace.proposal.candidateId,
        repositoryStateFingerprint: `mock-repository-state-${workspace.proposal.id}`,
        milestoneId: 'mock',
        milestonePath: '.agents/milestones/mock.md',
        sourceProposalId: workspace.proposal.id,
        sourceProposalFingerprint: workspace.authority.proposalFingerprint,
      },
      generatedAt: timestamp,
    },
  }
}

function resolveDecisionProposalMock(
  state: MockState,
  repositoryId: string,
  proposalId: string,
  request: {
    rationale?: unknown
    resolver?: unknown
    selectedOptionId?: unknown
    outcome?: unknown
  },
): Decision {
  const workspace = getDecisionReviewWorkspace(state, repositoryId, proposalId)
  if (workspace.proposal.state !== 'ReadyForResolution') {
    throw new Error(`Proposal transition from ${workspace.proposal.state} to Resolved is not allowed.`)
  }

  const rationale = typeof request.rationale === 'string' ? request.rationale.trim() : ''
  const resolver = typeof request.resolver === 'string' ? request.resolver.trim() : ''
  const selectedOptionId =
    typeof request.selectedOptionId === 'string' ? request.selectedOptionId.trim() : ''
  const outcome = isDecisionOutcome(request.outcome) ? request.outcome : 'Accepted'
  if (!rationale) {
    throw new Error('Resolution rationale is required.')
  }
  if (!resolver) {
    throw new Error('Resolver metadata is required.')
  }
  if (!selectedOptionId) {
    throw new Error('Selected option id is required.')
  }

  const selectedOption = workspace.proposal.options.find((option) => option.id === selectedOptionId)
  if (!selectedOption) {
    throw new Error(`Selected option was not found: ${selectedOptionId}`)
  }

  const timestamp = new Date().toISOString()
  const decisionId = `DEC-${String(Object.keys(state.decisions[repositoryId] ?? {}).length + 1).padStart(4, '0')}`
  const proposalSource = {
    sourceKind: 'DecisionProposal',
    relativePath: `.agents/decisions/proposals/${proposalId}/proposal.json`,
    section: null,
    itemId: null,
    decisionId,
    proposalId,
    candidateId: workspace.proposal.candidateId,
    excerpt: 'Mock proposal resolved through explicit resolution command.',
  }
  const optionSource = {
    sourceKind: 'DecisionOption',
    relativePath: `.agents/decisions/proposals/${proposalId}/proposal.json`,
    section: 'Options',
    itemId: selectedOptionId,
    decisionId,
    proposalId,
    candidateId: workspace.proposal.candidateId,
    excerpt: selectedOption.title,
  }
  const decisionState = targetDecisionStateForOutcome(outcome)
  const decision: Decision = {
    id: decisionId,
    state: decisionState,
    classification: 'Architectural',
    title: workspace.proposal.title,
    context: workspace.proposal.context,
    metadata: {
      repositoryId,
      createdAt: timestamp,
      updatedAt: timestamp,
      schemaVersion: '1',
    },
    resolution: {
      outcome,
      selectedOptionId,
      rationale,
      resolvedBy: resolver,
      recommendationDiverged:
        Boolean(workspace.proposal.recommendation) &&
        workspace.proposal.recommendation?.optionId !== selectedOptionId,
      resolvedAt: timestamp,
      sources: [proposalSource, optionSource],
      sourceProposalSnapshot: {
        proposalId,
        candidateId: workspace.proposal.candidateId,
        proposalFingerprint: `mock-current-fingerprint-${proposalId}`,
        proposalState: workspace.proposal.state,
        title: workspace.proposal.title,
        context: workspace.proposal.context,
        options: workspace.proposal.options,
        tradeoffs: workspace.proposal.tradeoffs,
        recommendation: workspace.proposal.recommendation,
        assumptions: workspace.proposal.assumptions,
        evidence: workspace.proposal.evidence,
        history: workspace.proposal.history,
        revisions: workspace.revisions,
      },
    },
    relationships: [],
    evidence: workspace.proposal.evidence,
    history: [
      {
        at: timestamp,
        actor: resolver,
        action: 'Resolved',
        fromState: 'Open',
        toState: decisionState,
        reason: rationale,
        sources: [proposalSource, optionSource],
      },
    ],
  }

  state.decisions[repositoryId] = {
    ...(state.decisions[repositoryId] ?? {}),
    [decisionId]: decision,
  }

  workspace.proposal = {
    ...workspace.proposal,
    state: 'Resolved',
    history: [
      ...workspace.proposal.history,
      {
        at: timestamp,
        actor: resolver,
        action: 'Resolved',
        fromState: workspace.proposal.state,
        toState: 'Resolved',
        reason: rationale,
        sources: [proposalSource],
      },
    ],
  }
  workspace.review = {
    ...workspace.review,
    state: 'Closed',
    updatedAt: timestamp,
    reason: 'Mock proposal was resolved through explicit human action.',
  }
  state.decisionProposalBrowserItems[repositoryId] =
    state.decisionProposalBrowserItems[repositoryId]?.map((item) =>
      item.proposalId === proposalId
        ? {
            ...item,
            state: 'Resolved',
            reviewState: 'Closed',
            isResolved: true,
            updatedAt: timestamp,
            reviewUpdatedAt: timestamp,
          }
        : item,
    ) ?? []

  return decision
}

function getDecisionAssimilationRecommendationMock(
  state: MockState,
  repositoryId: string,
  decisionId: string,
): DecisionAssimilationRecommendation {
  const recommendation = state.decisionAssimilationRecommendations[repositoryId]?.[decisionId]
  if (!recommendation) {
    throw new Error(`Decision assimilation recommendation was not found: ${decisionId}`)
  }

  return recommendation
}

function proposeDecisionOperationalContextAssimilationMock(
  state: MockState,
  repositoryId: string,
  decisionId: string,
  request: { requestedBy?: unknown; notes?: unknown },
): DecisionAssimilationRecommendation {
  const decision = state.decisions[repositoryId]?.[decisionId]
  if (!decision) {
    throw new Error(`Decision was not found: ${decisionId}`)
  }
  if (decision.state !== 'Resolved') {
    throw new Error('Only resolved decisions can produce operational-context assimilation recommendations.')
  }

  const timestamp = new Date().toISOString()
  const contextSnapshot = state.decisionContexts[repositoryId]
  const recommendation: DecisionAssimilationRecommendation = {
    decisionId,
    repositoryId,
    createdAt: timestamp,
    decisionFingerprint: `mock-decision-fingerprint-${decisionId}`,
    contextSnapshotId: contextSnapshot.snapshotId,
    contextFingerprint: contextSnapshot.fingerprint,
    sourceDecision: decision,
    contextSnapshot,
    projectedStableDecision: `${decision.title}: ${decision.resolution?.rationale ?? decision.context}`,
    rationale: decision.resolution?.rationale ?? decision.context,
    requestedBy: typeof request.requestedBy === 'string' && request.requestedBy.trim()
      ? request.requestedBy.trim()
      : null,
    notes: typeof request.notes === 'string' && request.notes.trim() ? request.notes.trim() : null,
    evidence: decision.evidence,
    sources: decision.resolution?.sources ?? [],
    diagnostics: [
      'Mock package is advisory and does not mutate .agents/operational_context.md.',
    ],
  }

  state.decisionAssimilationRecommendations[repositoryId] = {
    ...(state.decisionAssimilationRecommendations[repositoryId] ?? {}),
    [decisionId]: recommendation,
  }

  return recommendation
}

function createDecisionGovernanceReport(
  state: MockState,
  repositoryId: string,
  persist: boolean,
): DecisionGovernanceReport {
  const timestamp = new Date().toISOString()
  const candidates = state.decisionCandidates[repositoryId] ?? []
  const proposals = state.decisionProposalBrowserItems[repositoryId] ?? []
  const decisions = Object.values(state.decisions[repositoryId] ?? {})
  const recommendations = Object.values(state.decisionAssimilationRecommendations[repositoryId] ?? {})
  const activeCandidates = candidates.filter((candidate) =>
    candidate.state === 'Discovered' || candidate.state === 'Promoted',
  )
  const activeProposals = proposals.filter((proposal) =>
    proposal.state !== 'Resolved' && proposal.state !== 'Expired' && proposal.state !== 'Discarded',
  )
  const findings = [
    {
      id: 'GOV-MOCK-001',
      category: 'DecisionCoverage' as const,
      severity: 'Warning' as const,
      blocksExecutionProjection: false,
      title: 'Promoted candidates still need resolution',
      detail:
        'Mock governance reports promoted candidates that have not yet produced governed resolved decisions.',
      sources: [decisionSource('.agents/decisions/candidates/CAND-0001/candidate.json', 'Candidate remains promoted.')],
      relatedDecisionIds: [],
      relatedCandidateIds: activeCandidates.map((candidate) => candidate.id),
      relatedProposalIds: activeProposals.map((proposal) => proposal.proposalId),
    },
    ...(decisions.some((decision) => decision.state === 'Resolved') && recommendations.length === 0
      ? [
          {
            id: 'GOV-MOCK-002',
            category: 'ExecutionProjectionReadiness' as const,
            severity: 'Blocking' as const,
            blocksExecutionProjection: true,
            title: 'Resolved decision lacks assimilation package',
            detail:
              'A resolved decision is visible, but no operational-context assimilation recommendation has been generated.',
            sources: [
              decisionSource(
                '.agents/decisions/records/DEC-mock/decision.json',
                'Resolved decision requires governance visibility before execution projection.',
              ),
            ],
            relatedDecisionIds: decisions
              .filter((decision) => decision.state === 'Resolved')
              .map((decision) => decision.id),
            relatedCandidateIds: [],
            relatedProposalIds: [],
          },
        ]
      : []),
  ]

  const report: DecisionGovernanceReport = {
    id: persist ? `governance.${Date.now().toString().padStart(21, '0')}` : 'governance.current',
    repositoryId,
    generatedAt: timestamp,
    inputFingerprint: `mock-governance-${repositoryId}-${decisions.length}-${proposals.length}`,
    health: findings.some((finding) => finding.blocksExecutionProjection)
      ? 'Blocked'
      : findings.length > 0
        ? 'AdvisoryFindings'
        : 'Healthy',
    summary: {
      decisionCount: decisions.length,
      resolvedDecisionCount: decisions.filter((decision) => decision.state === 'Resolved').length,
      activeCandidateCount: activeCandidates.length,
      activeProposalCount: activeProposals.length,
      assimilationRecommendationCount: recommendations.length,
      findingCount: findings.length,
      blockingFindingCount: findings.filter((finding) => finding.blocksExecutionProjection).length,
    },
    findings,
    diagnostics: persist
      ? ['Mock governance report was persisted to generated report history.']
      : ['Mock current governance is an inspection and is not persisted.'],
  }

  if (persist) {
    state.decisionGovernanceReports[repositoryId] = [
      report,
      ...(state.decisionGovernanceReports[repositoryId] ?? []),
    ]
  }

  return report
}

function createDecisionCertificationReport(
  state: MockState,
  repositoryId: string,
  persist: boolean,
): DecisionCertificationReport {
  const governance = createDecisionGovernanceReport(state, repositoryId, false)
  const decisions = Object.values(state.decisions[repositoryId] ?? {})
  const candidates = state.decisionCandidates[repositoryId] ?? []
  const proposals = state.decisionProposalBrowserItems[repositoryId] ?? []
  const timestamp = new Date().toISOString()
  const evidence = [
    {
      id: 'context-resolution',
      area: 'Context',
      passed: true,
      detail: 'Decision context can be rebuilt from repository artifacts.',
      sources: [decisionSource('.agents/plan.md', 'Repository files remain authoritative.')],
      relatedDecisionIds: [],
      relatedCandidateIds: [],
      relatedProposalIds: [],
    },
    {
      id: 'discovery',
      area: 'Discovery',
      passed: candidates.length > 0,
      detail: candidates.length > 0
        ? 'Candidate discovery produced source-linked candidates.'
        : 'No candidates were available to certify discovery coverage.',
      sources: [decisionSource('.agents/decisions/candidates/CAND-0001/candidate.json', 'Candidate evidence remains source-linked.')],
      relatedDecisionIds: [],
      relatedCandidateIds: candidates.map((candidate) => candidate.id),
      relatedProposalIds: [],
    },
    {
      id: 'proposal-lifecycle',
      area: 'Proposal',
      passed: proposals.length > 0,
      detail: proposals.length > 0
        ? 'Proposal lifecycle state is readable for review and refinement.'
        : 'No proposals were available to certify proposal lifecycle coverage.',
      sources: [decisionSource('.agents/decisions/proposals/PROP-0001/proposal.json', 'Proposal state remains structured.')],
      relatedDecisionIds: [],
      relatedCandidateIds: [],
      relatedProposalIds: proposals.map((proposal) => proposal.proposalId),
    },
    {
      id: 'authority-boundaries',
      area: 'Authority',
      passed: decisions.every((decision) =>
        !decision.resolution || !/governance|certification|execution/i.test(decision.resolution.resolvedBy),
      ),
      detail: 'Certification, governance, and execution remain inspection surfaces rather than resolution authority.',
      sources: [decisionSource('.agents/decisions/records/DEC-mock/decision.json', 'Resolution authority remains human.')],
      relatedDecisionIds: decisions.map((decision) => decision.id),
      relatedCandidateIds: [],
      relatedProposalIds: [],
    },
  ]
  const failedEvidenceCount = evidence.filter((item) => !item.passed).length
  const passedEvidenceCount = evidence.length - failedEvidenceCount
  const findings = failedEvidenceCount > 0 ? governance.findings : []
  const report: DecisionCertificationReport = {
    id: persist ? `certification.${Date.now().toString().padStart(21, '0')}` : 'certification.current',
    repositoryId,
    generatedAt: timestamp,
    inputFingerprint: `mock-certification-${repositoryId}-${decisions.length}-${candidates.length}-${proposals.length}`,
    result: {
      kind: failedEvidenceCount > 0 ? 'Failed' : 'Passed',
      passedEvidenceCount,
      failedEvidenceCount,
    },
    health: governance.health,
    evidence,
    findings,
    diagnostics: persist
      ? ['Mock certification report was persisted to generated report history.']
      : ['Mock current certification is an inspection and is not persisted.'],
  }

  if (persist) {
    state.decisionCertificationReports[repositoryId] = [
      report,
      ...(state.decisionCertificationReports[repositoryId] ?? []),
    ]
  }

  return report
}

function createDecisionGenerationCertificationReport(
  state: MockState,
  repositoryId: string,
  persist: boolean,
): DecisionGenerationCertificationReport {
  const decisions = Object.values(state.decisions[repositoryId] ?? {})
  const candidates = state.decisionCandidates[repositoryId] ?? []
  const proposals = state.decisionProposalBrowserItems[repositoryId] ?? []
  const qualityAssessments = state.decisionQualityAssessments[repositoryId] ?? []
  const generatedResolvedDecisionCount = decisions.filter((decision) =>
    Boolean(decision.resolution?.sourceProposalSnapshot),
  ).length
  const reviewOnlyCount = generatedResolvedDecisionCount
  const generationBypassedCount = Math.max(0, decisions.length - generatedResolvedDecisionCount)
  const generationCertified = candidates.length > 0 && proposals.length > 0
  const governanceCertified = decisions.every((decision) =>
    !decision.resolution || !/governance|certification|execution/i.test(decision.resolution.resolvedBy),
  )
  const qualityCertified = qualityAssessments.length > 0 || generatedResolvedDecisionCount === 0
  const consumptionCertified = generatedResolvedDecisionCount === 0 || decisions.length > 0
  const workflowReplacementCertified = generatedResolvedDecisionCount > 0
    ? reviewOnlyCount >= generatedResolvedDecisionCount
    : false
  const findings = [
    {
      id: 'generation-capability',
      category: 'GenerationCapability',
      passed: generationCertified,
      summary: 'Generated decision artifacts are available',
      detail: generationCertified
        ? 'Candidates and proposals are available for review.'
        : 'Candidate and proposal evidence is not yet available.',
      sources: [decisionSource('.agents/decisions/proposals/PROP-0001/proposal.json', 'Generated proposal content remains reviewable.')],
      relatedDecisionIds: [],
      relatedCandidateIds: candidates.map((candidate) => candidate.id),
      relatedProposalIds: proposals.map((proposal) => proposal.proposalId),
    },
    {
      id: 'workflow-replacement',
      category: 'WorkflowReplacement',
      passed: workflowReplacementCertified,
      summary: 'Humans govern generated decisions',
      detail: workflowReplacementCertified
        ? 'Generated decisions reached resolution without full rewrite evidence.'
        : 'No generated resolved decision is available to prove workflow replacement.',
      sources: [decisionSource('.agents/decisions/records/DEC-mock/decision.json', 'Human resolution remains authoritative.')],
      relatedDecisionIds: decisions.map((decision) => decision.id),
      relatedCandidateIds: [],
      relatedProposalIds: [],
    },
  ]
  const failures = findings.filter((finding) => !finding.passed).map((finding) => finding.detail)
  const timestamp = new Date().toISOString()
  const report: DecisionGenerationCertificationReport = {
    id: persist
      ? `generation-certification.${Date.now().toString().padStart(21, '0')}`
      : 'generation-certification.current',
    repositoryId,
    generatedAt: timestamp,
    inputFingerprint: `mock-generation-certification-${repositoryId}-${decisions.length}-${candidates.length}-${proposals.length}`,
    result: {
      generationCertified,
      governanceCertified,
      throughputCertified: proposals.length > 0,
      qualityCertified,
      consumptionCertified,
      workflowReplacementCertified,
      findings,
      failures,
      certified: failures.length === 0 && generationCertified && governanceCertified && qualityCertified && consumptionCertified,
    },
    candidateCount: candidates.length,
    generatedProposalCount: proposals.length,
    generatedPackageCount: proposals.length,
    generatedResolvedDecisionCount,
    executionInfluenceTraceCount: generatedResolvedDecisionCount,
    humanAuthoringBurden: {
      repositoryId,
      decisionCount: decisions.length,
      reviewOnlyCount,
      minorEditCount: 0,
      majorRefinementCount: 0,
      fullRewriteCount: 0,
      generationBypassedCount,
      unknownCount: 0,
      signals: decisions.map((decision) => ({
        id: `burden-${decision.id}`,
        repositoryId,
        decisionId: decision.id,
        burden: decision.resolution?.sourceProposalSnapshot ? 'ReviewOnly' : 'GenerationBypassed',
        sourceKind: 'MockDecisionResolution',
        summary: decision.resolution?.sourceProposalSnapshot
          ? 'Mock decision was resolved from generated proposal content.'
          : 'Mock decision bypassed generated proposal content.',
        sources: [decisionSource(`.agents/decisions/records/${decision.id}/decision.json`, decision.title)],
      })),
    },
    qualityAssessments,
    repositoryReport: {
      candidateCount: candidates.length,
      automaticallyDiscoveredCandidateCount: candidates.length,
      generatedProposalCount: proposals.length,
      generatedPackageCount: proposals.length,
      generatedResolvedDecisionCount,
      qualityAssessmentCount: qualityAssessments.length,
      executionInfluenceTraceCount: generatedResolvedDecisionCount,
      manualBypassCount: generationBypassedCount,
      diagnostics: ['Mock repository report is derived from in-memory decision artifacts.'],
    },
    workflowReport: {
      generatedResolvedDecisionCount,
      humanResolvedGeneratedDecisionCount: generatedResolvedDecisionCount,
      systemResolvedGeneratedDecisionCount: 0,
      preservedHistoryDecisionCount: generatedResolvedDecisionCount,
      recommendationDivergenceCount: 0,
      recommendationDivergenceRate: 0,
      executionInfluenceCoveredDecisionCount: generatedResolvedDecisionCount,
      executionInfluenceCoverageRate: generatedResolvedDecisionCount > 0 ? 1 : 0,
      diagnostics: ['Mock workflow report keeps generation and human governance separate.'],
    },
    humanAuthoringBurdenSummary: {
      decisionCount: decisions.length,
      reviewOnlyCount,
      reviewOnlyRate: rate(reviewOnlyCount, decisions.length),
      minorEditCount: 0,
      minorEditRate: 0,
      majorRefinementCount: 0,
      majorRefinementRate: 0,
      fullRewriteCount: 0,
      fullRewriteRate: 0,
      generationBypassedCount,
      generationBypassedRate: rate(generationBypassedCount, decisions.length),
      primaryAuthoringReplaced: reviewOnlyCount > generationBypassedCount,
      diagnostics: ['Mock burden summary is derived from generated proposal resolution evidence.'],
    },
    executiveReport: {
      replacementReady: failures.length === 0 && workflowReplacementCertified,
      answer: workflowReplacementCertified
        ? 'System generation has replaced primary human decision production for the certified evidence set; humans remain governance authorities.'
        : 'System generation has not yet replaced primary human decision production for the certified evidence set.',
      summary: workflowReplacementCertified
        ? 'Generated decisions reached human resolution without rewrite evidence.'
        : 'Generated resolved decision evidence is incomplete.',
      evidence: [
        `Generated decisions resolved: ${generatedResolvedDecisionCount}.`,
        `ReviewOnly rate: ${Math.round(rate(reviewOnlyCount, decisions.length) * 100)}%.`,
        `GenerationBypassed rate: ${Math.round(rate(generationBypassedCount, decisions.length) * 100)}%.`,
        `Execution influence coverage: ${generatedResolvedDecisionCount > 0 ? 100 : 0}%.`,
      ],
      blockingGaps: failures,
      diagnostics: ['Mock executive readiness avoids an opaque numeric score.'],
    },
    diagnostics: persist
      ? ['Mock generation certification report was persisted to generated report history.']
      : ['Mock current generation certification is advisory and is not persisted.'],
  }

  if (persist) {
    state.decisionGenerationCertificationReports[repositoryId] = [
      report,
      ...(state.decisionGenerationCertificationReports[repositoryId] ?? []),
    ]
  }

  return report
}

function assessDecisionQualityMock(
  state: MockState,
  repositoryId: string,
  proposalId: string,
): DecisionQualityAssessment {
  const decision = Object.values(state.decisions[repositoryId] ?? {}).find(
    (item) => item.resolution?.sourceProposalSnapshot?.proposalId === proposalId,
  )
  if (!decision) {
    throw new Error(`Decision proposal has not been resolved: ${proposalId}`)
  }

  const existing = state.decisionQualityAssessments[repositoryId] ?? []
  const assessment = createDecisionQualityAssessment(repositoryId, decision, existing.length + 1)
  state.decisionQualityAssessments[repositoryId] = [assessment, ...existing]
  return assessment
}

function createDecisionQualityReport(
  state: MockState,
  repositoryId: string,
  persist: boolean,
): DecisionQualityReport {
  const decisions = Object.values(state.decisions[repositoryId] ?? {})
  const assessments = state.decisionQualityAssessments[repositoryId] ?? []
  const generatedPackageCount = decisions.filter(
    (decision) => decision.resolution?.sourceProposalSnapshot?.packageId,
  ).length
  const acceptedCount = decisions.filter((decision) => decision.resolution?.outcome === 'Accepted').length
  const rejectedCount = decisions.filter((decision) => decision.resolution?.outcome === 'Rejected').length
  const recommendationDivergenceCount = decisions.filter(
    (decision) => decision.resolution?.recommendationDiverged,
  ).length
  const alternativeUtilizationCount = recommendationDivergenceCount
  const reviewOnlyCount = assessments.filter((assessment) =>
    assessment.humanAuthoringBurdenSignals.some((signal) => signal.burden === 'ReviewOnly'),
  ).length
  const minorEditCount = assessments.filter((assessment) =>
    assessment.humanAuthoringBurdenSignals.some((signal) => signal.burden === 'MinorEdit'),
  ).length
  const majorRefinementCount = assessments.filter((assessment) =>
    assessment.humanAuthoringBurdenSignals.some((signal) => signal.burden === 'MajorRefinement'),
  ).length
  const fullRewriteCount = assessments.filter((assessment) =>
    assessment.humanAuthoringBurdenSignals.some((signal) => signal.burden === 'FullRewrite'),
  ).length
  const generationBypassedCount = decisions.filter(
    (decision) => !decision.resolution?.sourceProposalSnapshot,
  ).length
  const modifiedCount = minorEditCount + majorRefinementCount + fullRewriteCount
  const rating = fullRewriteCount > 0 || generationBypassedCount > 0
    ? 'Mixed'
    : acceptedCount > 0 && recommendationDivergenceCount === 0
      ? 'Good'
      : 'Unknown'
  const report: DecisionQualityReport = {
    id: persist ? `quality.${Date.now().toString().padStart(21, '0')}` : 'quality.current',
    repositoryId,
    generatedAt: new Date().toISOString(),
    decisionCount: decisions.length,
    generatedPackageCount,
    acceptedCount,
    acceptedRate: rate(acceptedCount, decisions.length),
    modifiedCount,
    modifiedRate: rate(modifiedCount, decisions.length),
    rejectedCount,
    rejectedRate: rate(rejectedCount, decisions.length),
    supersededCount: decisions.filter((decision) => decision.state === 'Superseded').length,
    supersededRate: rate(decisions.filter((decision) => decision.state === 'Superseded').length, decisions.length),
    recommendationDivergenceCount,
    recommendationDivergenceRate: rate(recommendationDivergenceCount, decisions.length),
    alternativeUtilizationCount,
    alternativeUtilizationRate: rate(alternativeUtilizationCount, decisions.length),
    reviewOnlyCount,
    reviewOnlyRate: rate(reviewOnlyCount, assessments.length),
    minorEditCount,
    minorEditRate: rate(minorEditCount, assessments.length),
    majorRefinementCount,
    majorRefinementRate: rate(majorRefinementCount, assessments.length),
    fullRewriteCount,
    fullRewriteRate: rate(fullRewriteCount, assessments.length),
    generationBypassedCount,
    generationBypassedRate: rate(generationBypassedCount, decisions.length),
    rating,
    assessments,
    diagnostics: persist
      ? ['Mock quality report was persisted to generated report history.']
      : ['Mock current quality report is an inspection and is not persisted.'],
  }

  if (persist) {
    state.decisionQualityReports[repositoryId] = [
      report,
      ...(state.decisionQualityReports[repositoryId] ?? []),
    ]
  }

  return report
}

function createDecisionQualityTrend(
  state: MockState,
  repositoryId: string,
  persist: boolean,
): DecisionQualityTrend {
  const assessments = state.decisionQualityAssessments[repositoryId] ?? []
  const currentAverageScore = average(assessments.map((assessment) => assessment.score))
  const previousAverageScore = average(assessments.slice(1).map((assessment) => assessment.score))
  const currentRating = assessments[0]?.rating ?? 'Unknown'
  const previousRating = assessments[1]?.rating ?? 'Unknown'
  const trend: DecisionQualityTrend = {
    id: persist ? `trend.${Date.now().toString().padStart(21, '0')}` : 'trend.current',
    repositoryId,
    generatedAt: new Date().toISOString(),
    assessmentCount: assessments.length,
    currentRating,
    previousRating,
    currentAverageScore,
    previousAverageScore,
    direction: currentAverageScore > previousAverageScore
      ? 'Positive'
      : currentAverageScore < previousAverageScore
        ? 'Negative'
        : 'Neutral',
    diagnostics: persist
      ? ['Mock quality trend was persisted to generated trend history.']
      : ['Mock current quality trend is calculated from saved assessments.'],
  }

  if (persist) {
    state.decisionQualityTrends[repositoryId] = [
      trend,
      ...(state.decisionQualityTrends[repositoryId] ?? []),
    ]
  }

  return trend
}

function createDecisionQualityAssessment(
  repositoryId: string,
  decision: Decision,
  sequence: number,
): DecisionQualityAssessment {
  const recommendationDiverged = decision.resolution?.recommendationDiverged ?? false
  const burden = decision.resolution?.sourceProposalSnapshot?.revisions.length
    ? 'MajorRefinement'
    : decision.resolution?.sourceProposalSnapshot
      ? 'ReviewOnly'
      : 'GenerationBypassed'
  const rating = decision.resolution?.outcome === 'Accepted' && !recommendationDiverged
    ? 'Good'
    : decision.resolution?.outcome === 'Rejected'
      ? 'Mixed'
      : 'Unknown'
  const source = decisionSource(
    `.agents/decisions/records/${decision.id}/decision.json`,
    'Human resolution remains the quality assessment boundary.',
  )

  return {
    id: `assessment.${String(sequence).padStart(4, '0')}`,
    repositoryId,
    decisionId: decision.id,
    assessedAt: new Date().toISOString(),
    rating,
    score: rating === 'Good' ? 80 : rating === 'Mixed' ? 55 : 0,
    signals: [
      {
        id: `QS-${String(sequence).padStart(4, '0')}-burden`,
        repositoryId,
        decisionId: decision.id,
        category: 'HumanAuthoringBurden',
        direction: burden === 'ReviewOnly' ? 'Positive' : 'Negative',
        severity: burden === 'GenerationBypassed' ? 'High' : 'Info',
        summary: `Burden classified as ${burden}.`,
        detail: 'Mock assessment derives burden from resolution and revision evidence.',
        sources: [source],
      },
      {
        id: `QS-${String(sequence).padStart(4, '0')}-recommendation`,
        repositoryId,
        decisionId: decision.id,
        category: 'RecommendationStability',
        direction: recommendationDiverged ? 'Negative' : 'Positive',
        severity: recommendationDiverged ? 'Medium' : 'Info',
        summary: recommendationDiverged
          ? 'Human selected an alternative to the recommendation.'
          : 'Human selected the recommended option.',
        detail: 'Mock quality keeps recommendation stability visible without making it authoritative.',
        sources: [source],
      },
      {
        id: `QS-${String(sequence).padStart(4, '0')}-tradeoff`,
        repositoryId,
        decisionId: decision.id,
        category: 'TradeoffQuality',
        direction: decision.resolution?.sourceProposalSnapshot?.tradeoffs.length ? 'Positive' : 'Negative',
        severity: decision.resolution?.sourceProposalSnapshot?.tradeoffs.length ? 'Info' : 'Medium',
        summary: 'Resolved proposal tradeoffs remain inspectable.',
        detail: 'Tradeoff signals come from the frozen source proposal snapshot.',
        sources: [source],
      },
      {
        id: `QS-${String(sequence).padStart(4, '0')}-context`,
        repositoryId,
        decisionId: decision.id,
        category: 'ContextQuality',
        direction: decision.context ? 'Positive' : 'Negative',
        severity: decision.context ? 'Info' : 'Medium',
        summary: 'Decision context is available for assessment.',
        detail: 'Context quality remains observational and does not alter the decision.',
        sources: [source],
      },
      {
        id: `QS-${String(sequence).padStart(4, '0')}-constraint`,
        repositoryId,
        decisionId: decision.id,
        category: 'ConstraintQuality',
        direction: 'Neutral',
        severity: 'Info',
        summary: 'Constraint quality is visible as a separate signal.',
        detail: 'Mock data keeps constraint quality distinct from overall rating.',
        sources: [source],
      },
    ],
    humanAuthoringBurdenSignals: [
      {
        id: `HAB-${String(sequence).padStart(4, '0')}`,
        repositoryId,
        decisionId: decision.id,
        burden,
        sourceKind: 'ResolutionSnapshot',
        summary: `Human authoring burden classified as ${burden}.`,
        sources: [source],
      },
    ],
    diagnostics: ['Mock assessment is advisory and did not mutate decision authority.'],
  }
}

function rate(count: number, total: number) {
  return total > 0 ? count / total : 0
}

function average(values: number[]) {
  return values.length > 0 ? values.reduce((sum, value) => sum + value, 0) / values.length : 0
}

function createExecutionDecisionProjection(
  state: MockState,
  repositoryId: string,
): ExecutionDecisionProjection {
  const decisions = Object.values(state.decisions[repositoryId] ?? {})
  const governance = createDecisionGovernanceReport(state, repositoryId, false)
  const blockedDecisionIds = new Set(
    governance.findings
      .filter((finding) => finding.blocksExecutionProjection)
      .flatMap((finding) => finding.relatedDecisionIds),
  )
  const constraints: ExecutionDecisionProjection['constraints'] = []
  const directives: ExecutionDecisionProjection['directives'] = []
  const priorities: ExecutionDecisionProjection['priorities'] = []
  const architectureRules: ExecutionDecisionProjection['architectureRules'] = []
  const diagnostics: string[] = []

  for (const decision of decisions.sort((left, right) => left.id.localeCompare(right.id))) {
    if (decision.state !== 'Resolved' || decision.resolution?.outcome !== 'Accepted') {
      continue
    }

    if (blockedDecisionIds.has(decision.id)) {
      diagnostics.push(`${decision.id} excluded by blocking governance finding.`)
      continue
    }

    const selectedOption = decision.resolution.sourceProposalSnapshot?.options.find(
      (option) => option.id === decision.resolution?.selectedOptionId,
    )
    const statement = selectedOption
      ? selectedOption.description.trim()
        ? `${selectedOption.title}: ${selectedOption.description}`
        : selectedOption.title
      : decision.resolution.rationale.trim() || decision.context
    const projectionKind = classifyMockProjectionKind(decision, statement)
    const item = {
      id: '',
      decisionId: decision.id,
      title: decision.title,
      statement,
      classification: decision.classification,
      projectionKind,
      sources: decision.resolution.sources.length > 0
        ? decision.resolution.sources
        : [decisionSource(`.agents/decisions/records/${decision.id}/decision.json`, statement)],
    }

    if (
      projectionKind === 'ArchitecturalConstraint' ||
      projectionKind === 'TechnologyChoice' ||
      projectionKind === 'RepositoryConvention'
    ) {
      constraints.push({ ...item, id: `ECON-${String(constraints.length + 1).padStart(4, '0')}` })
      architectureRules.push({ ...item, id: `EARC-${String(architectureRules.length + 1).padStart(4, '0')}` })
    } else {
      directives.push({ ...item, id: `EDIR-${String(directives.length + 1).padStart(4, '0')}` })
      const priorityText = `${decision.title} ${decision.context} ${statement}`.toLowerCase()
      if (
        decision.classification === 'Strategic' ||
        priorityText.includes('priority') ||
        priorityText.includes('prioritize') ||
        priorityText.includes('before ') ||
        priorityText.includes('first ')
      ) {
        priorities.push({
          ...item,
          id: `EPRI-${String(priorities.length + 1).padStart(4, '0')}`,
          rank: priorities.length + 1,
        })
      }
    }
  }

  const conflicts: ExecutionDecisionProjection['conflicts'] = []
  const projectedStatements: ExecutionDecisionProjection['projectedStatements'] = [
    ...constraints.map((constraint) => ({ ...constraint, projectionCategory: 'Constraint' })),
    ...directives.map((directive) => ({ ...directive, projectionCategory: 'Directive' })),
    ...priorities.map((priority) => ({ ...priority, projectionCategory: 'Priority' })),
    ...architectureRules.map((rule) => ({ ...rule, projectionCategory: 'ArchitectureRule' })),
  ]
  const context: ExecutionDecisionProjection['context'] = {
    constraints,
    directives,
    priorities,
    architectureRules,
    conflicts,
    diagnostics,
  }

  return {
    repositoryId,
    generatedAt: new Date().toISOString(),
    constraints,
    directives,
    priorities,
    architectureRules,
    conflicts,
    diagnostics,
    context,
    includedDecisions: [],
    excludedDecisions: [],
    supersededDecisions: [],
    conflictingDecisions: [],
    ignoredDecisions: [],
    blockedDecisions: [],
    projectedStatements,
    projectionFingerprint: `mock-${repositoryId}-${projectedStatements.length}`,
  }
}

function createDecisionInfluenceTrace(
  state: MockState,
  repositoryId: string,
  executionSessionId: string,
): DecisionInfluenceTrace {
  const projection = createExecutionDecisionProjection(state, repositoryId)
  const statements: DecisionInfluenceTrace['statements'] = [
    ...projection.constraints.map((constraint) => ({
      statementId: constraint.id,
      decisionId: constraint.decisionId,
      title: constraint.title,
      statement: constraint.statement,
      classification: constraint.classification,
      projectionKind: constraint.projectionKind,
      statementType: 'Constraint',
      promptSection: 'Decision Constraints',
      priorityRank: null,
      sources: constraint.sources,
      adherenceObservations: [],
    })),
    ...projection.directives.map((directive) => ({
      statementId: directive.id,
      decisionId: directive.decisionId,
      title: directive.title,
      statement: directive.statement,
      classification: directive.classification,
      projectionKind: directive.projectionKind,
      statementType: 'Directive',
      promptSection: 'Decision Directives',
      priorityRank: null,
      sources: directive.sources,
      adherenceObservations: [],
    })),
    ...projection.priorities.map((priority) => ({
      statementId: priority.id,
      decisionId: priority.decisionId,
      title: priority.title,
      statement: priority.statement,
      classification: priority.classification,
      projectionKind: priority.projectionKind,
      statementType: 'Priority',
      promptSection: 'Decision Priorities',
      priorityRank: priority.rank,
      sources: priority.sources,
      adherenceObservations: [],
    })),
    ...projection.architectureRules.map((rule) => ({
      statementId: rule.id,
      decisionId: rule.decisionId,
      title: rule.title,
      statement: rule.statement,
      classification: rule.classification,
      projectionKind: rule.projectionKind,
      statementType: 'ArchitectureRule',
      promptSection: 'Architecture Rules',
      priorityRank: null,
      sources: rule.sources,
      adherenceObservations: [],
    })),
  ]

  return {
    id: `execution-${executionSessionId.replace(/[^a-zA-Z0-9]/g, '').toLowerCase().padEnd(32, '0').slice(0, 32)}`,
    repositoryId,
    executionSessionId,
    recordedAt: projection.generatedAt,
    projectionGeneratedAt: projection.generatedAt,
    projectionFingerprint: projection.projectionFingerprint,
    statements,
    includedDecisions: projection.includedDecisions,
    excludedDecisions: projection.excludedDecisions,
    supersededDecisions: projection.supersededDecisions,
    conflictingDecisions: projection.conflictingDecisions,
    ignoredDecisions: projection.ignoredDecisions,
    blockedDecisions: projection.blockedDecisions,
    diagnostics: projection.diagnostics,
  }
}

function listDecisionInfluenceTraces(
  state: MockState,
  repositoryId: string,
  decisionId: string,
): DecisionInfluenceTrace[] {
  return (state.workspaces[repositoryId]?.executionHistory ?? [])
    .map((session) => createDecisionInfluenceTrace(state, repositoryId, session.sessionId))
    .filter((trace) => trace.statements.some((statement) => statement.decisionId === decisionId))
}

function classifyMockProjectionKind(
  decision: Decision,
  statement: string,
): ExecutionDecisionProjection['constraints'][number]['projectionKind'] {
  const searchable = `${decision.title} ${decision.context} ${statement}`.toLowerCase()
  if (/(technology|framework|library|package|dependency|provider|runtime|api|sdk|react|tauri|rust|typescript|\.net)/.test(searchable)) {
    return 'TechnologyChoice'
  }
  if (/(workflow|process|review|approval|promotion|governance|handoff|commit|push|rotation|certification)/.test(searchable)) {
    return 'WorkflowPolicy'
  }
  if (/(repository|repo|artifact|path|file|directory|folder|\.agents|markdown|json|projection|naming|convention)/.test(searchable)) {
    return 'RepositoryConvention'
  }

  if (decision.classification === 'Architectural') {
    return 'ArchitecturalConstraint'
  }
  if (decision.classification === 'Tactical') {
    return 'ImplementationDirective'
  }
  return 'WorkflowPolicy'
}

function isDecisionOutcome(value: unknown): value is DecisionOutcome {
  return value === 'Accepted' || value === 'Rejected' || value === 'Deferred'
}

function targetDecisionStateForOutcome(outcome: DecisionOutcome) {
  if (outcome === 'Rejected') {
    return 'Archived'
  }
  if (outcome === 'Deferred') {
    return 'UnderReview'
  }

  return 'Resolved'
}

function createDecisionProposalLineage(
  state: MockState,
  repositoryId: string,
  proposalId: string,
): DecisionProposalLineage {
  const workspace = getDecisionReviewWorkspace(state, repositoryId, proposalId)
  const events = [
    ...workspace.proposal.history.map((entry) => ({
      occurredAt: 'timestamp' in entry ? String(entry.timestamp) : String(entry.at),
      kind: 'ProposalHistory',
      itemId: workspace.proposal.id,
      summary: 'event' in entry ? String(entry.event) : String(entry.action),
      fromState: entry.fromState,
      toState: 'toState' in entry ? entry.toState : null,
      sources: entry.sources,
    })),
    ...workspace.revisions.map((revision) => ({
      occurredAt: revision.createdAt,
      kind: 'Revision',
      itemId: revision.id,
      summary: revision.reason,
      fromState: null,
      toState: 'Refined',
      sources: revision.sources,
    })),
    ...workspace.notes.map((note) => ({
      occurredAt: note.createdAt,
      kind: 'ReviewNote',
      itemId: note.id,
      summary: note.body,
      fromState: null,
      toState: null,
      sources: note.sources,
    })),
  ].sort((left, right) => left.occurredAt.localeCompare(right.occurredAt))

  return {
    repositoryId,
    proposalId,
    currentState: workspace.proposal.state,
    currentProposalFingerprint: `mock-current-fingerprint-${proposalId}`,
    currentProposal: workspace.proposal,
    review: workspace.review,
    events,
    revisions: workspace.revisions.map((revision) => ({
      revision,
      comparison: {
        proposalId,
        revisionId: revision.id,
        repositoryId,
        sourceProposalFingerprint: revision.sourceProposalFingerprint,
        currentProposalFingerprint: `mock-current-fingerprint-${proposalId}`,
        sourceMatchesCurrentProposal: false,
        changedFields: revision.changedFields,
        fieldComparisons: revision.changedFields.map((field) => ({
          field,
          changeType: 'Changed',
          previousValue: null,
          revisedValue: null,
        })),
        acceptedChanges: [],
        rejectedChanges: [],
        diagnostics: [],
        previousOptions: [],
        revisedOptions: workspace.proposal.options,
        retiredOptions: [],
        previousAssumptions: [],
        revisedAssumptions: workspace.proposal.assumptions,
        retiredAssumptions: [],
        previousTradeoffs: [],
        revisedTradeoffs: workspace.proposal.tradeoffs,
        humanAuthoringBurden: revision.humanAuthoringBurden,
        sources: revision.sources,
      },
      isCurrentProposal: false,
      authorityBoundary:
        'Historical revision is read-only explanatory history; currentProposal remains authoritative.',
    })),
    reviewNotes: workspace.notes,
    diagnostics: [
      'Current proposal is authoritative; revision snapshots are historical and read-only.',
      'Revision comparisons explain evolution and do not resolve decisions.',
    ],
  }
}

function createDecisionOptionComparison(
  state: MockState,
  repositoryId: string,
  proposalId: string,
): DecisionOptionComparison {
  const workspace = getDecisionReviewWorkspace(state, repositoryId, proposalId)
  const recommendedOptionId = workspace.proposal.recommendation?.optionId ?? null

  return {
    proposalId,
    recommendedOptionId,
    options: workspace.proposal.options.map((option) => {
      const tradeoffs = workspace.proposal.tradeoffs.filter((tradeoff) => tradeoff.optionId === option.id)
      return {
        optionId: option.id,
        title: option.title,
        description: option.description,
        isRecommended: option.id === recommendedOptionId,
        benefits: tradeoffs.map((tradeoff) => tradeoff.benefit),
        costs: tradeoffs.map((tradeoff) => tradeoff.cost),
        evidence: [
          ...option.evidence,
          ...tradeoffs.flatMap((tradeoff) => tradeoff.evidence),
          ...(option.id === recommendedOptionId ? workspace.proposal.recommendation?.evidence ?? [] : []),
        ],
      }
    }),
  }
}

function createDecisionEvidenceInspection(
  state: MockState,
  repositoryId: string,
  proposalId: string,
): DecisionEvidenceInspection {
  const workspace = getDecisionReviewWorkspace(state, repositoryId, proposalId)
  const proposal = workspace.proposal
  const items = [
    ...proposal.evidence.map((evidence) => ({
      appliesToKind: 'Proposal',
      itemId: proposal.id,
      summary: evidence.summary,
      sources: evidence.sources.map((source) => createDecisionSourceAttribution('Proposal', proposal.id, source)),
    })),
    ...proposal.options.flatMap((option) =>
      option.evidence.map((evidence) => ({
        appliesToKind: 'Option',
        itemId: option.id,
        summary: evidence.summary,
        sources: evidence.sources.map((source) => createDecisionSourceAttribution('Option', option.id, source)),
      })),
    ),
    ...proposal.tradeoffs.flatMap((tradeoff) =>
      tradeoff.evidence.map((evidence) => ({
        appliesToKind: 'Tradeoff',
        itemId: tradeoff.optionId,
        summary: evidence.summary,
        sources: evidence.sources.map((source) => createDecisionSourceAttribution('Tradeoff', tradeoff.optionId, source)),
      })),
    ),
    ...(proposal.recommendation?.evidence.map((evidence) => ({
      appliesToKind: 'Recommendation',
      itemId: proposal.recommendation?.optionId ?? null,
      summary: evidence.summary,
      sources: evidence.sources.map((source) =>
        createDecisionSourceAttribution('Recommendation', proposal.recommendation?.optionId ?? null, source),
      ),
    })) ?? []),
    ...proposal.assumptions.flatMap((assumption) =>
      assumption.evidence.map((evidence) => ({
        appliesToKind: 'Assumption',
        itemId: assumption.id,
        summary: evidence.summary,
        sources: evidence.sources.map((source) => createDecisionSourceAttribution('Assumption', assumption.id, source)),
      })),
    ),
  ]

  return {
    proposalId,
    candidateId: proposal.candidateId,
    items,
    diagnostics: workspace.diagnostics,
  }
}

function listDecisionSourceAttributionsForProposal(
  state: MockState,
  repositoryId: string,
  proposalId: string,
): DecisionSourceAttribution[] {
  return createDecisionEvidenceInspection(state, repositoryId, proposalId).items.flatMap((item) => item.sources)
}

function createDecisionSourceAttribution(
  appliesToKind: string,
  itemId: string | null,
  source: DecisionSourceAttribution['source'],
): DecisionSourceAttribution {
  return {
    appliesToKind,
    itemId,
    sourceKind: source.sourceKind,
    relativePath: source.relativePath,
    section: source.section,
    excerpt: source.excerpt,
    source,
  }
}

function createReadyInventory(): ArtifactInventory {
  return {
    plan: artifacts.plan,
    operationalContext: artifacts.context,
    historicalOperationalContexts: [],
    milestones: [artifacts.milestone],
    currentHandoff: artifacts.handoff,
    historicalHandoffs: [artifacts.oldHandoff],
    currentDecisions: artifacts.decisions,
    historicalDecisions: [],
  }
}

function createInitialState(): MockState {
  const state: MockState = {
    repositories: [alphaRepository, emptyRepository, planOnlyRepository, ...certificationRepositories],
    workspaces: {
      [alphaRepository.id]: createWorkspace(alphaRepository, createReadyInventory()),
      [emptyRepository.id]: createWorkspace(emptyRepository, {
        plan: null,
        operationalContext: null,
        historicalOperationalContexts: [],
        milestones: [],
        currentHandoff: null,
        historicalHandoffs: [],
        currentDecisions: null,
        historicalDecisions: [],
      }),
      [planOnlyRepository.id]: createWorkspace(planOnlyRepository, {
        plan: artifacts.plan,
        operationalContext: null,
        historicalOperationalContexts: [],
        milestones: [],
        currentHandoff: null,
        historicalHandoffs: [],
        currentDecisions: null,
        historicalDecisions: [],
      }),
      ...Object.fromEntries(
        certificationRepositories.map((repository) => [
          repository.id,
          createWorkspace(repository, createReadyInventory()),
        ]),
      ),
    },
    content: {
      [artifacts.plan.relativePath]: '# Plan\n\nInitial plan content.',
      [artifacts.context.relativePath]: '# Operational Context\n\nContext content.',
      [artifacts.milestone.relativePath]: '# M5\n\nWorkspace experience milestone.',
      [artifacts.handoff.relativePath]: '# Handoff\n\nCurrent handoff content.',
      [artifacts.oldHandoff.relativePath]: '# Historical Handoff\n\nArchived content.',
      [artifacts.decisions.relativePath]: '# Decisions\n\nCurrent decisions content.',
    },
    sessions: {},
    operationalContextProposals: {},
    continuityReports: {},
    decisionContexts: {},
    decisionCandidates: {},
    decisionProposalBrowserItems: {},
    decisionProposalReviewWorkspaces: {},
    decisions: {},
    decisionAssimilationRecommendations: {},
    decisionGovernanceReports: {},
    decisionCertificationReports: {},
    decisionGenerationCertificationReports: {},
    decisionQualityAssessments: {},
    decisionQualityReports: {},
    decisionQualityTrends: {},
    reasoningEvents: {},
    reasoningThreads: {},
    reasoningRelationships: {},
    reasoningCertificationReports: {},
    commandCalls: {},
  }

  state.repositories.forEach((repository) => {
    state.decisionContexts[repository.id] = createDecisionContext(repository)
    state.decisionCandidates[repository.id] = createDecisionCandidates(repository)
    state.decisionProposalBrowserItems[repository.id] = createDecisionProposalBrowserItems()
    state.decisionProposalReviewWorkspaces[repository.id] = createDecisionProposalReviewWorkspaces(
      repository,
      state.decisionProposalBrowserItems[repository.id],
    )
    state.decisions[repository.id] = {}
    state.decisionAssimilationRecommendations[repository.id] = {}
    state.decisionGovernanceReports[repository.id] = []
    state.decisionCertificationReports[repository.id] = []
    state.decisionGenerationCertificationReports[repository.id] = []
    state.decisionQualityAssessments[repository.id] = []
    state.decisionQualityReports[repository.id] = []
    state.decisionQualityTrends[repository.id] = []
    state.reasoningEvents[repository.id] = repository.id === alphaRepository.id
      ? createReasoningEvents(repository.id)
      : []
    state.reasoningThreads[repository.id] = repository.id === alphaRepository.id
      ? createReasoningThreads(repository.id)
      : []
    state.reasoningRelationships[repository.id] = repository.id === alphaRepository.id
      ? createReasoningRelationships(repository.id)
      : []
    state.reasoningCertificationReports[repository.id] = []
    refreshReasoningSummary(state, repository.id)
  })

  seedCertificationSession(state, certificationRepositories[0], 'Executing', 'Executing')
  seedCertificationSession(state, certificationRepositories[1], 'AwaitingAcceptance', 'Completed')
  seedCertificationSession(state, certificationRepositories[2], 'AwaitingCommit', 'Completed', {
    acceptedAt: new Date().toISOString(),
  })
  seedCertificationSession(state, certificationRepositories[3], 'AwaitingPush', 'Completed', {
    acceptedAt: new Date().toISOString(),
    commitSha: 'mock-certification-commit',
    committedAt: new Date().toISOString(),
    preparationSnapshotId: `snapshot-cert-${certificationRepositories[3].id}`,
  })
  seedCertificationSession(state, certificationRepositories[4], 'Failed', 'Failed', {
    failureReason:
      'Certification failure with a deliberately long diagnostic message to verify wrapping in the unified execution workspace.',
  })
  seedCertificationSession(state, certificationRepositories[5], 'Cancelled', 'Cancelled', {
    failureReason: 'Certification cancellation state.',
  })

  return state
}

function seedCertificationSession(
  state: MockState,
  repository: Repository,
  repositoryState: RepositoryExecutionState,
  sessionState: ExecutionSessionState,
  overrides: Partial<ExecutionSessionSummary> = {},
) {
  const workspace = state.workspaces[repository.id]
  const timestamp = new Date().toISOString()
  const sessionId = `cert-${repository.id}`
  const summary: ExecutionSessionSummary = {
    sessionId,
    state: sessionState,
    repositoryState,
    milestonePath:
      '.agents/milestones/extremely-long-certification-milestone-name-for-responsive-layout-validation.md',
    startedAt: timestamp,
    completedAt: repositoryState === 'Executing' ? null : timestamp,
    duration: repositoryState === 'Executing' ? null : '00:03:24',
    acceptedAt: null,
    rejectedAt: null,
    decisionNote: null,
    lastActivityAt: timestamp,
    providerName: 'Fake',
    providerExecutablePath:
      'C:\\tools\\command-center\\providers\\fake-provider-with-a-long-executable-path.exe',
    providerProcessId: repositoryState === 'Executing' ? 4242 : null,
    providerStartedAt: timestamp,
    handoffPath: repositoryState === 'Executing' ? null : artifacts.handoff.relativePath,
    commitSha: null,
    committedAt: null,
    commitMessage: null,
    preparationSnapshotId: null,
    pushAttemptedAt: null,
    pushedAt: null,
    pushedCommitSha: null,
    pushRemoteName: null,
    pushBranchName: null,
    failureReason: null,
    ...overrides,
  }

  state.sessions[sessionId] = {
    ...summary,
    id: sessionId,
    repositoryId: repository.id,
    repositoryPath: repository.path,
  }
  workspace.executionState = repositoryState
  workspace.executionSummary = summary
  workspace.executionHistory = [
    summary,
    ...workspace.executionHistory.filter((session) => session.sessionId !== sessionId),
  ]
}

function dashboardEntry(workspace: Workspace): DashboardEntry {
  return {
    repository: workspace.repository,
    availability: workspace.availability,
    readiness: workspace.readiness,
    executionState: workspace.executionState,
    activeExecutionSession:
      workspace.executionState === 'Executing' ? workspace.executionSummary : null,
    executionSummary: workspace.executionSummary,
    executionHistory: workspace.executionHistory,
    milestoneCount: workspace.milestoneCount,
    hasCurrentHandoff: workspace.hasCurrentHandoff,
    hasCurrentDecisions: workspace.hasCurrentDecisions,
    continuitySummary: {
      operationalContextExists: workspace.operationalContext.exists,
      operationalContextRevisionCount: workspace.operationalContext.revisionCount,
      operationalContextLastUpdatedAt: workspace.operationalContext.lastUpdatedAt,
      openQuestionCount: workspace.operationalContext.openQuestions.length,
      activeRiskCount: workspace.operationalContext.activeRisks.length,
      pendingProposalExists: workspace.operationalContextProposalSummary.pendingProposalExists,
    },
    reasoningSummary: workspace.reasoningSummary,
    decisionSessionSummary: workspace.decisionSessionSummary,
  }
}

function createReasoningEvents(repositoryId: string): ReasoningEvent[] {
  return [
    {
      id: 'EVT-0001',
      repositoryId,
      createdAt: '2026-06-22T16:00:00.0000000Z',
      family: 'Hypothesis',
      type: 'HypothesisRaised',
      title: 'Event substrate can stay narrow',
      narrative: {
        summary: 'Reasoning should begin as immutable events with provenance.',
        details: 'Specialized concepts remain reconstructed from event families and traces.',
      },
      references: [
        {
          kind: 'Artifact',
          id: '.agents/plan.md',
          relativePath: '.agents/plan.md',
          section: 'Milestone 1',
          excerpt: 'Preserve events first.',
        },
      ],
      provenance: {
        sourceKind: 'ManualCapture',
        capturedBy: 'codex',
        relativePath: '.agents/plan.md',
        section: 'Milestone 1',
        excerpt: 'Event substrate is operational.',
        fingerprint: 'mock-fingerprint-1',
      },
      threadIds: ['THR-0001'],
      tags: ['milestone-1'],
    },
    {
      id: 'EVT-0002',
      repositoryId,
      createdAt: '2026-06-22T16:05:00.0000000Z',
      family: 'Alternative',
      type: 'AlternativeRejected',
      title: 'Specialized entity storage deferred',
      narrative: {
        summary: 'Hypotheses, alternatives, contradictions, and direction stay derived.',
        details: 'The materialization gate must approve any future first-class entity.',
      },
      references: [],
      provenance: {
        sourceKind: 'ManualCapture',
        capturedBy: 'codex',
        relativePath: '.agents/decisions/decisions.md',
        section: 'Newly Authorized',
        excerpt: 'Do not add specialized endpoints before the materialization gate.',
        fingerprint: 'mock-fingerprint-2',
      },
      threadIds: ['THR-0001'],
      tags: ['derived-only'],
    },
  ]
}

function createManualReasoningCaptureTemplates(): ManualReasoningCaptureTemplate[] {
  return [
    ['AlternativeIntroduced', 'Alternative', 'AlternativeIntroduced', 'PathConsidered'],
    ['AlternativeRejected', 'Alternative', 'AlternativeRejected', 'PathConsidered'],
    ['AlternativeRevisited', 'Alternative', 'AlternativeRevisited', 'PathConsidered'],
    ['ContradictionIdentified', 'Contradiction', 'ContradictionIdentified', 'Conflict'],
    ['ContradictionResolved', 'Contradiction', 'ContradictionResolved', 'Conflict'],
    ['DirectionObserved', 'Direction', 'DirectionObserved', 'StrategicMovement'],
    ['DirectionShifted', 'Direction', 'DirectionShifted', 'StrategicMovement'],
    ['HypothesisRaised', 'Hypothesis', 'HypothesisRaised', 'BeliefUnderInvestigation'],
    ['HypothesisInvalidated', 'Hypothesis', 'HypothesisInvalidated', 'BeliefUnderInvestigation'],
    ['DecisionReconsidered', 'DecisionEvolution', 'DecisionReconsidered', 'DecisionEvolution'],
    ['AssumptionInvalidated', 'AssumptionEvolution', 'AssumptionInvalidated', 'AssumptionEvolution'],
    ['ConstraintModified', 'ConstraintEvolution', 'ConstraintModified', 'ConstraintEvolution'],
    ['EvidenceAdded', 'Evidence', 'EvidenceAdded', 'EvidenceTrail'],
  ].map(([kind, family, type, suggestedThreadTheme]) => ({
    kind,
    family,
    type,
    suggestedThreadTheme,
    provenanceSourceKind: 'UserSupplied',
    suggestedReferenceKinds: ['Artifact'],
  })) as ManualReasoningCaptureTemplate[]
}

function createReasoningThreads(repositoryId: string): ReasoningThread[] {
  return [
    {
      id: 'THR-0001',
      repositoryId,
      title: 'Milestone 1 ontology boundary',
      theme: 'DecisionEvolution',
      createdAt: '2026-06-22T16:00:00.0000000Z',
      updatedAt: '2026-06-22T16:05:00.0000000Z',
      summary: 'Tracks why the event substrate remains explanatory rather than authoritative.',
      eventIds: ['EVT-0001', 'EVT-0002'],
      tags: ['milestone-1'],
    },
  ]
}

function createReasoningRelationships(repositoryId: string): ReasoningRelationship[] {
  return [
    {
      id: 'REL-0001',
      repositoryId,
      createdAt: '2026-06-22T16:06:00.0000000Z',
      type: 'Supports',
      source: {
        kind: 'ReasoningEvent',
        id: 'EVT-0001',
        relativePath: null,
        section: null,
        excerpt: null,
      },
      target: {
        kind: 'ReasoningEvent',
        id: 'EVT-0002',
        relativePath: null,
        section: null,
        excerpt: null,
      },
      narrative: {
        summary: 'A narrow event substrate supports deferring specialized storage.',
        details: '',
      },
      provenance: {
        sourceKind: 'ManualCapture',
        capturedBy: 'codex',
        relativePath: '.agents/handoffs/handoff.md',
        section: 'Current Gaps',
        excerpt: 'Derived display status remains unimplemented.',
        fingerprint: 'mock-fingerprint-3',
      },
    },
  ]
}

function createReasoningGraph(state: MockState, repositoryId: string): ReasoningGraph {
  const events = state.reasoningEvents[repositoryId] ?? []
  const threads = state.reasoningThreads[repositoryId] ?? []
  const relationships = state.reasoningRelationships[repositoryId] ?? []
  const nodes: ReasoningGraphNode[] = [
    ...events.map((event) => ({
      id: `ReasoningEvent:${event.id}`,
      kind: 'ReasoningEvent' as const,
      referenceId: event.id,
      label: event.title,
      resolved: true,
      reference: {
        kind: 'ReasoningEvent' as const,
        id: event.id,
        relativePath: null,
        section: null,
        excerpt: event.narrative.summary,
      },
    })),
    ...threads.map((thread) => ({
      id: `ReasoningThread:${thread.id}`,
      kind: 'ReasoningThread' as const,
      referenceId: thread.id,
      label: thread.title,
      resolved: true,
      reference: {
        kind: 'ReasoningThread' as const,
        id: thread.id,
        relativePath: null,
        section: thread.theme,
        excerpt: thread.summary,
      },
    })),
  ]
  const graphRelationships: ReasoningGraphRelationship[] = [
    ...relationships.map((relationship) => ({
      id: `Relationship:${relationship.id}`,
      type: relationship.type,
      sourceNodeId: `${relationship.source.kind}:${relationship.source.id}`,
      targetNodeId: `${relationship.target.kind}:${relationship.target.id}`,
      label: relationship.narrative.summary,
      provenance: `${relationship.provenance.sourceKind} by ${relationship.provenance.capturedBy}`,
      relationshipId: relationship.id,
    })),
    ...events.flatMap((event) =>
      event.threadIds.map((threadId) => ({
        id: `ThreadMembership:${event.id}:${threadId}`,
        type: 'BelongsTo' as const,
        sourceNodeId: `ReasoningEvent:${event.id}`,
        targetNodeId: `ReasoningThread:${threadId}`,
        label: 'Event belongs to thread',
        provenance: 'ReasoningEvent.ThreadIds',
        relationshipId: null,
      })),
    ),
  ]

  return {
    repositoryId,
    generatedAt: new Date().toISOString(),
    nodes,
    relationships: graphRelationships,
    diagnostics: [],
  }
}

function createReasoningTrace(
  graph: ReasoningGraph,
  direction: ReasoningTrace['direction'],
  kind: ReasoningTrace['target']['kind'],
  id: string,
): ReasoningTrace {
  const targetNodeId = `${kind}:${id}`
  const relationshipSet = graph.relationships.filter((relationship) =>
    direction === 'Backward'
      ? relationship.targetNodeId === targetNodeId
      : relationship.sourceNodeId === targetNodeId,
  )
  const nodeIds = new Set<string>([targetNodeId])
  for (const relationship of relationshipSet) {
    nodeIds.add(relationship.sourceNodeId)
    nodeIds.add(relationship.targetNodeId)
  }

  return {
    repositoryId: graph.repositoryId,
    direction,
    target: {
      kind,
      id,
      relativePath: null,
      section: null,
      excerpt: null,
    },
    nodes: graph.nodes.filter((node) => nodeIds.has(node.id)),
    relationships: relationshipSet,
    diagnostics: graph.diagnostics,
  }
}

function createReasoningReconstruction(
  state: MockState,
  repositoryId: string,
  query: ReasoningQuery,
): ReasoningReconstruction {
  const graph = createReasoningGraph(state, repositoryId)
  const trace = createReasoningTrace(graph, query.direction, query.target.kind, query.target.id)
  const eventsById = new Map((state.reasoningEvents[repositoryId] ?? []).map((event) => [event.id, event]))
  const threadsById = new Map((state.reasoningThreads[repositoryId] ?? []).map((thread) => [thread.id, thread]))
  const relationshipsById = new Map(
    (state.reasoningRelationships[repositoryId] ?? []).map((relationship) => [relationship.id, relationship]),
  )
  const evidence: ReasoningReconstructionEvidence[] = [
    ...trace.nodes.map((node) => {
      const event = node.kind === 'ReasoningEvent' ? eventsById.get(node.referenceId) : null
      if (event) {
        return {
          kind: 'Event',
          id: event.id,
          title: `${event.type}: ${event.title}`,
          summary: event.narrative.summary,
          reference: {
            kind: 'ReasoningEvent' as const,
            id: event.id,
            relativePath: null,
            section: null,
            excerpt: null,
          },
          provenance: event.provenance,
        }
      }

      const thread = node.kind === 'ReasoningThread' ? threadsById.get(node.referenceId) : null
      if (thread) {
        return {
          kind: 'Thread',
          id: thread.id,
          title: `${thread.theme}: ${thread.title}`,
          summary: thread.summary,
          reference: {
            kind: 'ReasoningThread' as const,
            id: thread.id,
            relativePath: null,
            section: null,
            excerpt: null,
          },
          provenance: null,
        }
      }

      return {
        kind: 'Reference',
        id: node.referenceId,
        title: `${node.kind}: ${node.label}`,
        summary: node.reference?.excerpt ?? node.label,
        reference: node.reference,
        provenance: null,
      }
    }),
    ...trace.relationships.map((graphRelationship) => {
      const relationship = graphRelationship.relationshipId
        ? relationshipsById.get(graphRelationship.relationshipId)
        : null
      return relationship
        ? {
            kind: 'Relationship',
            id: relationship.id,
            title: relationship.type,
            summary: relationship.narrative.summary,
            reference: relationship.target,
            provenance: relationship.provenance,
          }
        : {
            kind: 'GraphRelationship',
            id: graphRelationship.id,
            title: graphRelationship.type,
            summary: graphRelationship.label,
            reference: null,
            provenance: null,
          }
    }),
  ]
  const relationshipEvidenceCount = evidence.filter((item) =>
    item.kind === 'Relationship' || item.kind === 'GraphRelationship',
  ).length
  const eventEvidenceCount = evidence.filter((item) => item.kind === 'Event').length

  return {
    repositoryId,
    generatedAt: new Date().toISOString(),
    query,
    narrative: {
      summary: `The ${query.category.toLowerCase()} question about ${query.target.kind} ${query.target.id} is reconstructed from ${eventEvidenceCount} event(s) and ${relationshipEvidenceCount} relationship edge(s).`,
      details: [
        `Question: ${query.question}`,
        `Target: ${query.target.kind} ${query.target.id}`,
        `Trace direction: ${query.direction}`,
        'Evidence:',
        ...evidence.map((item) => `- ${item.kind} ${item.id}: ${item.title} - ${item.summary}`),
      ].join('\n'),
    },
    confidence: eventEvidenceCount > 0 && relationshipEvidenceCount > 0 ? 'High' : evidence.length > 0 ? 'Medium' : 'Low',
    trace,
    evidence,
    diagnostics: trace.diagnostics,
  }
}

function createReasoningMaterializationReview(
  state: MockState,
  repositoryId: string,
): ReasoningMaterializationReviewReport {
  const events = state.reasoningEvents[repositoryId] ?? []
  const threads = state.reasoningThreads[repositoryId] ?? []
  const relationships = state.reasoningRelationships[repositoryId] ?? []
  const familyCounts = events.reduce<Record<string, number>>((counts, event) => {
    counts[event.family] = (counts[event.family] ?? 0) + 1
    return counts
  }, {})

  return {
    repositoryId,
    generatedAt: new Date().toISOString(),
    concepts: [
      {
        concept: 'Hypothesis',
        recommendation: 'RemainDerived',
        summary: 'Hypothesis remains reconstructable from reasoning events and trace evidence.',
        evidence: [`${familyCounts.Hypothesis ?? 0} hypothesis events`],
        risks: ['Promoting hypotheses would imply a lifecycle not owned by reasoning.'],
      },
      {
        concept: 'Alternative',
        recommendation: 'RemainDerived',
        summary: 'Alternatives remain explanatory classifications until failed reconstruction evidence appears.',
        evidence: [`${familyCounts.Alternative ?? 0} alternative events`],
        risks: ['Alternative status belongs in reconstruction, not mutation authority.'],
      },
      {
        concept: 'Contradiction',
        recommendation: 'RemainDerived',
        summary: 'Contradictions remain trace-derived unless repeated ambiguity is demonstrated.',
        evidence: [`${familyCounts.Contradiction ?? 0} contradiction events`],
        risks: ['A first-class contradiction object could overlap governance findings.'],
      },
      {
        concept: 'Direction',
        recommendation: 'RemainDerived',
        summary: 'Direction remains derived because direction events alone do not justify stronger persistence.',
        evidence: [`${familyCounts.Direction ?? 0} direction events`],
        risks: ['Direction persistence could imply strategic authority.'],
      },
      {
        concept: 'Thread',
        recommendation: 'RemainDerived',
        summary: 'Thread identity remains a grouping aid and not an authoritative artifact family.',
        evidence: [`${threads.length} threads`, `${relationships.length} relationships`],
        risks: ['Thread persistence must stay subject to future materialization review.'],
      },
    ],
    taxonomyFindings: Object.entries(familyCounts).map(([family, count]) => ({
      family: family as ReasoningMaterializationReviewReport['taxonomyFindings'][number]['family'],
      eventTypeCount: new Set(events.filter((event) => event.family === family).map((event) => event.type)).size,
      lifecycleRisk: false,
      summary: `${family} remains classification vocabulary in the mock review.`,
      evidence: [`${count} events`],
    })),
    diagnostics: [],
  }
}

function createReasoningCertificationReport(
  state: MockState,
  repositoryId: string,
  persist: boolean,
): ReasoningCertificationReport {
  const events = state.reasoningEvents[repositoryId] ?? []
  const threads = state.reasoningThreads[repositoryId] ?? []
  const relationships = state.reasoningRelationships[repositoryId] ?? []
  const missingProvenance = events.filter((event) => !event.provenance?.sourceKind || !event.provenance.capturedBy)
  const hasOutcomeCoverage =
    events.some((event) => event.type === 'AlternativeRejected') &&
    threads.some((thread) => thread.eventIds.length > 0)

  const evidence = [
    {
      id: 'CERT-000',
      scenario: 'Reasoning baseline',
      passed: events.length === 0 || hasOutcomeCoverage,
      summary:
        events.length === 0
          ? 'No reasoning has been captured yet; the empty baseline is valid.'
          : 'Reasoning records can answer at least one outcome-oriented scenario.',
      details:
        events.length === 0
          ? ['Certification found no reasoning artifacts to reconstruct.']
          : [
              `${events.length} event(s), ${threads.length} thread(s), and ${relationships.length} relationship(s) are available.`,
              hasOutcomeCoverage
                ? 'Alternative rejection and thread reconstruction are answerable.'
                : 'Outcome coverage is incomplete.',
            ],
      references: events.slice(0, 2).map((event) => ({
        kind: 'ReasoningEvent' as const,
        id: event.id,
        relativePath: null,
        section: event.type,
        excerpt: event.narrative.summary,
      })),
    },
    {
      id: 'CERT-010',
      scenario: 'Provenance completeness',
      passed: missingProvenance.length === 0,
      summary:
        missingProvenance.length === 0
          ? 'Every reasoning event has provenance.'
          : 'One or more reasoning events lack provenance.',
      details:
        missingProvenance.length === 0
          ? ['Events remain auditable to source context.']
          : missingProvenance.map((event) => `${event.id} is missing provenance.`),
      references: missingProvenance.map((event) => ({
        kind: 'ReasoningEvent' as const,
        id: event.id,
        relativePath: null,
        section: event.type,
        excerpt: event.title,
      })),
    },
    {
      id: 'CERT-040',
      scenario: 'Thread reconstruction',
      passed: events.length === 0 || threads.some((thread) => thread.eventIds.length > 0),
      summary:
        events.length === 0
          ? 'Thread reconstruction is not required for the empty baseline.'
          : 'At least one reasoning thread can be reconstructed from event membership.',
      details: [
        threads.length > 0
          ? `${threads.length} thread(s) are available for navigation.`
          : 'No reasoning threads are available.',
      ],
      references: threads.slice(0, 2).map((thread) => ({
        kind: 'ReasoningThread' as const,
        id: thread.id,
        relativePath: null,
        section: thread.theme,
        excerpt: thread.summary,
      })),
    },
  ]

  const failedEvidenceCount = evidence.filter((item) => !item.passed).length
  const report: ReasoningCertificationReport = {
    id: persist
      ? `certification.${new Date().toISOString().replace(/\D/g, '').slice(0, 21).padEnd(21, '0')}`
      : 'certification.current',
    repositoryId,
    generatedAt: new Date().toISOString(),
    result: {
      kind: failedEvidenceCount === 0 ? 'Passed' : 'Failed',
      summary:
        failedEvidenceCount === 0
          ? 'Reasoning remains reconstructable from repository artifacts.'
          : `${failedEvidenceCount} certification evidence item(s) failed.`,
    },
    evidence,
    diagnostics:
      events.length === 0
        ? ['No reasoning captured yet; certification is reporting the valid empty baseline.']
        : [],
  }

  if (persist) {
    state.reasoningCertificationReports[repositoryId] = [
      report,
      ...(state.reasoningCertificationReports[repositoryId] ?? []),
    ]
    const workspace = state.workspaces[repositoryId]
    if (workspace) {
      workspace.reasoningSummary = {
        ...workspace.reasoningSummary,
        lastCertificationAt: report.generatedAt,
        certificationResult: report.result.kind,
      }
    }
  }

  return report
}

function createReasoningQueryResult(
  state: MockState,
  repositoryId: string,
  query: ReasoningQuery,
): ReasoningQueryResult {
  const reconstruction = createReasoningReconstruction(state, repositoryId, query)
  return {
    repositoryId,
    generatedAt: reconstruction.generatedAt,
    query,
    reconstruction,
    diagnostics: reconstruction.diagnostics,
  }
}

function createContextPreview(state: MockState, repositoryId: string, milestonePath: string): ExecutionContextPreview {
  const workspace = state.workspaces[repositoryId]
  const artifactsForContext = [
    { role: 'Plan', artifact: workspace.artifactInventory.plan },
    {
      role: 'Milestone',
      artifact:
        workspace.artifactInventory.milestones.find((milestone) => milestone.relativePath === milestonePath) ??
        null,
    },
    { role: 'OperationalContext', artifact: workspace.artifactInventory.operationalContext },
    { role: 'CurrentHandoff', artifact: workspace.artifactInventory.currentHandoff },
    { role: 'CurrentDecisions', artifact: workspace.artifactInventory.currentDecisions },
  ].filter((entry): entry is { role: string; artifact: Artifact } => entry.artifact !== null)
  const artifactsWithContent = artifactsForContext.map(({ role, artifact }) => {
    const content = state.content[artifact.relativePath] ?? ''
    return {
      role,
      relativePath: artifact.relativePath,
      name: artifact.name,
      content,
      byteCount: content.length,
      characterCount: content.length,
    }
  })
  const totalBytes = artifactsWithContent.reduce((total, artifact) => total + artifact.byteCount, 0)

  return {
    repositoryId,
    repositoryName: workspace.repository.name,
    repositoryPath: workspace.repository.path,
    milestonePath,
    generatedAt: new Date().toISOString(),
    artifacts: artifactsWithContent,
    repositorySnapshot: {
      branch: 'main',
      dirtyState: {
        stagedPaths: [],
        modifiedPaths: [],
        addedPaths: [],
        deletedPaths: [],
        renamedPaths: [],
        untrackedPaths: [],
        isClean: true,
      },
      capturedAt: new Date().toISOString(),
    },
    diagnostics: {
      totalBytes,
      totalCharacters: totalBytes,
      warningThresholdBytes: 131072,
      hardLimitBytes: 524288,
      warningThresholdExceeded: false,
      hardLimitExceeded: false,
      artifactDiagnostics: artifactsWithContent.map((artifact) => ({
        role: artifact.role,
        relativePath: artifact.relativePath,
        byteCount: artifact.byteCount,
        characterCount: artifact.characterCount,
        warningThresholdBytes: 98304,
        hardLimitBytes: 262144,
        warningThresholdExceeded: false,
        hardLimitExceeded: false,
      })),
      validationErrors: workspace.readiness === 'Ready' ? [] : [`Repository planning readiness is ${workspace.readiness}.`],
      missingOptionalArtifacts: [
        workspace.artifactInventory.operationalContext ? null : '.agents/operational_context.md',
        workspace.artifactInventory.currentHandoff ? null : '.agents/handoffs/handoff.md',
        workspace.artifactInventory.currentDecisions ? null : '.agents/decisions/decisions.md',
      ].filter((path): path is string => path !== null),
      launchBlocked: workspace.readiness !== 'Ready',
    },
  }
}

function createGitStatus(state: MockState, repositoryId: string): RepositoryGitStatus {
  const workspace = state.workspaces[repositoryId]
  const hasAcceptedWork = workspace.executionState === 'AwaitingCommit'
  return {
    branch: 'main',
    aheadCount: workspace.executionState === 'AwaitingPush' ? 1 : 0,
    behindCount: 0,
    dirtyState: {
      stagedPaths: [],
      modifiedPaths: hasAcceptedWork ? ['src/CommandCenter.UI/src/App.tsx'] : [],
      addedPaths: [],
      deletedPaths: [],
      renamedPaths: [],
      untrackedPaths: hasAcceptedWork ? ['.agents/handoffs/handoff.md'] : [],
      isClean: !hasAcceptedWork,
    },
    capturedAt: new Date().toISOString(),
  }
}

function createCommitPreparation(state: MockState, sessionId: string): CommitPreparation {
  const session = state.sessions[sessionId]
  if (!session) {
    throw new Error('Execution session was not found.')
  }

  if (session.repositoryState !== 'AwaitingCommit') {
    throw new Error('Commit can only be prepared while awaiting commit.')
  }

  const status = createGitStatus(state, session.repositoryId)
  return {
    id: `prep-${sessionId}`,
    sessionId,
    repositoryId: session.repositoryId,
    repositoryPath: session.repositoryPath,
    proposedMessage: 'm5\n\n- 2 files changed',
    scopeItems: [
      {
        path: 'src/CommandCenter.UI/src/App.tsx',
        changeType: 'Modified',
        origin: 'ExecutionGenerated',
        isSelected: true,
      },
      {
        path: '.agents/handoffs/handoff.md',
        changeType: 'Untracked',
        origin: 'ExecutionGenerated',
        isSelected: true,
      },
    ],
    statusSnapshot: {
      ...status,
      id: `snapshot-${sessionId}`,
    },
    generatedAt: new Date().toISOString(),
    hasPreExistingChanges: false,
  }
}

function createExecutionGitEligibility(
  state: MockState,
  args: InvokeArgs,
): ExecutionGitActionEligibility {
  const sessionId = getStringArg(args, 'sessionId')
  const session = state.sessions[sessionId]
  if (!session) {
    throw new Error('Execution session was not found.')
  }

  const selectedPaths = getStringArrayArg(args, 'selectedPaths')
  const commitMessage = typeof args?.commitMessage === 'string' ? args.commitMessage : ''
  const preparation =
    session.repositoryState === 'AwaitingCommit' ? createCommitPreparation(state, sessionId) : null
  const preparedPaths = new Set(preparation?.scopeItems.map((item) => item.path) ?? [])
  const unknownSelectedPaths = selectedPaths.filter((path) => !preparedPaths.has(path))
  const status = createGitStatus(state, session.repositoryId)
  const commitDisabledReasons: string[] = []
  const pushDisabledReasons: string[] = []
  const repositoryAllowsCommit = session.repositoryState === 'AwaitingCommit'
  const commitPreparationLoaded = preparation !== null
  const commitPreparationCurrent = repositoryAllowsCommit && commitPreparationLoaded
  const commitMessagePresent = commitMessage.trim().length > 0
  const awaitingPush = session.repositoryState === 'AwaitingPush'
  const commitShaExists = Boolean(session.commitSha)

  if (!repositoryAllowsCommit) {
    commitDisabledReasons.push('Repository is not awaiting commit.')
  }
  if (!commitPreparationLoaded) {
    commitDisabledReasons.push('Commit preparation is not loaded.')
  }
  if (selectedPaths.length === 0) {
    commitDisabledReasons.push('At least one path must be selected for commit.')
  }
  if (unknownSelectedPaths.length > 0) {
    commitDisabledReasons.push('Selected paths include entries outside the prepared commit scope.')
  }
  if (!commitMessagePresent) {
    commitDisabledReasons.push('Commit message is required.')
  }
  if (!awaitingPush) {
    pushDisabledReasons.push('Repository is not awaiting push.')
  }
  if (!commitShaExists) {
    pushDisabledReasons.push('Committed execution SHA is not recorded.')
  }
  return {
    sessionId,
    sessionExists: true,
    repositoryState: session.repositoryState,
    commitPreparationLoaded,
    commitPreparationCurrent,
    commitPreparationId: preparation?.id ?? null,
    preparedStatusSnapshotId: preparation?.statusSnapshot.id ?? null,
    currentStatusSnapshotId: preparation?.statusSnapshot.id ?? null,
    selectedPathCount: selectedPaths.length,
    preparedPathCount: preparation?.scopeItems.length ?? 0,
    unknownSelectedPaths,
    commitMessagePresent,
    repositoryAllowsCommit,
    awaitingPush,
    commitShaExists,
    commitSha: session.commitSha,
    previousPushAttemptedAt: session.pushAttemptedAt,
    previousPushFailure: session.failureReason,
    remoteBranchState: {
      branch: status.branch,
      aheadCount: status.aheadCount,
      behindCount: status.behindCount,
      hasUnpushedChanges: status.aheadCount > 0,
      hasRemoteDivergence: status.behindCount > 0,
      capturedAt: status.capturedAt,
    },
    canCommit: commitDisabledReasons.length === 0,
    canPush: pushDisabledReasons.length === 0,
    commitDisabledReasons,
    pushDisabledReasons,
    diagnostics: [],
  }
}

function clone<T>(value: T): T {
  return structuredClone(value)
}

function getStringArg(args: InvokeArgs, name: string): string {
  const value = args?.[name]
  if (typeof value !== 'string') {
    throw new Error(`Missing string argument: ${name}`)
  }

  return value
}

function getStringArrayArg(args: InvokeArgs, name: string): string[] {
  const value = args?.[name]
  if (!Array.isArray(value)) {
    return []
  }

  return value.filter((item): item is string => typeof item === 'string')
}

function rotateCurrentArtifact(
  state: MockState,
  repositoryId: string,
  currentKey: 'currentHandoff' | 'currentDecisions',
  historicalKey: 'historicalHandoffs' | 'historicalDecisions',
  filePrefix: 'handoff' | 'decisions',
) {
  const workspace = state.workspaces[repositoryId]
  const currentArtifact = workspace.artifactInventory[currentKey]
  if (!currentArtifact) {
    throw new Error(`No current ${filePrefix} artifact exists.`)
  }

  const nextIndex = workspace.artifactInventory[historicalKey].length + 1
  const historicalArtifact: Artifact = {
    ...currentArtifact,
    relativePath: currentArtifact.relativePath.replace(
      `${filePrefix}.md`,
      `${filePrefix}.${String(nextIndex).padStart(4, '0')}.md`,
    ),
    name: `${filePrefix}.${String(nextIndex).padStart(4, '0')}.md`,
    versionKind: 'Historical',
  }

  workspace.artifactInventory[historicalKey] = [
    historicalArtifact,
    ...workspace.artifactInventory[historicalKey],
  ]
  state.content[historicalArtifact.relativePath] = state.content[currentArtifact.relativePath]
}

function startExecution(state: MockState, repositoryId: string, milestonePath: string): ExecutionSessionSummary {
  const workspace = state.workspaces[repositoryId]
  if (!workspace) {
    throw new Error(`Repository was not found: ${repositoryId}`)
  }

  if (workspace.executionState !== 'Ready') {
    throw new Error('Repository already has an active execution session.')
  }

  const context = createContextPreview(state, repositoryId, milestonePath)
  if (context.diagnostics.launchBlocked) {
    throw new Error('Execution launch is blocked.')
  }

  const timestamp = new Date().toISOString()
  const sessionId = `session-${Object.keys(state.sessions).length + 1}`
  const summary: ExecutionSessionSummary = {
    sessionId,
    state: 'Completed',
    repositoryState: 'AwaitingAcceptance',
    milestonePath,
    startedAt: timestamp,
    completedAt: timestamp,
    duration: '00:00:01',
    acceptedAt: null,
    rejectedAt: null,
    decisionNote: null,
    lastActivityAt: timestamp,
    providerName: 'Fake',
    providerExecutablePath: 'fake-provider',
    providerProcessId: null,
    providerStartedAt: timestamp,
    handoffPath: artifacts.handoff.relativePath,
    commitSha: null,
    committedAt: null,
    commitMessage: null,
    preparationSnapshotId: null,
    pushAttemptedAt: null,
    pushedAt: null,
    pushedCommitSha: null,
    pushRemoteName: null,
    pushBranchName: null,
    failureReason: null,
  }
  state.content[artifacts.handoff.relativePath] = [
    '# Generated Handoff',
    '',
    'Mock execution completed and produced this handoff for review.',
  ].join('\n')
  state.sessions[sessionId] = {
    ...summary,
    id: sessionId,
    repositoryId,
    repositoryPath: workspace.repository.path,
  }
  workspace.executionState = 'AwaitingAcceptance'
  workspace.executionSummary = summary
  workspace.executionHistory = [
    summary,
    ...workspace.executionHistory.filter((session) => session.sessionId !== sessionId),
  ]
  return summary
}

function createExecutionPromptManifest(state: MockState, sessionId: string): ExecutionPromptManifest {
  const session = state.sessions[sessionId]
  if (!session) {
    throw new Error('Execution session was not found.')
  }

  const workspace = state.workspaces[session.repositoryId]
  if (!workspace) {
    throw new Error(`Repository was not found: ${session.repositoryId}`)
  }

  const requestedArtifacts = [
    createManifestArtifact(state, 'Plan', workspace.artifactInventory.plan?.relativePath ?? '.agents/plan.md'),
    createManifestArtifact(state, 'Milestone', session.milestonePath),
    createManifestArtifact(
      state,
      'OperationalContext',
      workspace.artifactInventory.operationalContext?.relativePath ?? '.agents/operational_context.md',
    ),
    createManifestArtifact(
      state,
      'CurrentHandoff',
      workspace.artifactInventory.currentHandoff?.relativePath ?? '.agents/handoffs/handoff.md',
    ),
    createManifestArtifact(
      state,
      'CurrentDecisions',
      workspace.artifactInventory.currentDecisions?.relativePath ?? '.agents/decisions/decisions.md',
    ),
  ]
  const deliveredArtifacts = requestedArtifacts.filter((artifact) => artifact.delivered)
  const requestedContextBytes = requestedArtifacts.reduce((sum, artifact) => sum + (artifact.byteCount ?? 0), 0)
  const requestedContextCharacters = requestedArtifacts.reduce(
    (sum, artifact) => sum + (artifact.characterCount ?? 0),
    0,
  )

  return {
    sessionId,
    generatedAt: session.startedAt ?? new Date().toISOString(),
    promptText: 'Mock launched prompt text.',
    promptArtifactPath: null,
    requestedArtifacts,
    requestedContextBytes,
    requestedContextCharacters,
    deliveredArtifacts,
    deliveredContextBytes: requestedContextBytes,
    deliveredContextCharacters: requestedContextCharacters,
    dirtyRepositoryAtRequestTime: false,
    dirtyRepositoryAtDeliveryTime: false,
    governedDecisionCountRequested: 3,
    governedDecisionCountDelivered: 3,
    operationalContextSourceRequested: workspace.artifactInventory.operationalContext?.relativePath ?? null,
    operationalContextSourceDelivered: workspace.artifactInventory.operationalContext?.relativePath ?? null,
    handoffSourceRequested: workspace.artifactInventory.currentHandoff?.relativePath ?? null,
    handoffSourceDelivered: workspace.artifactInventory.currentHandoff?.relativePath ?? null,
    milestoneSourceRequested: session.milestonePath,
    milestoneSourceDelivered: session.milestonePath,
    providerDeliveryStatus: 'Delivered',
    providerAdjustments: [],
    divergenceReason: null,
    diagnostics: ['NoProviderDivergenceSignal'],
  }
}

function createMockExecutionEvents(session: ExecutionSession): ExecutionEvent[] {
  const events: ExecutionEvent[] = []
  const startedAt = session.providerStartedAt ?? session.startedAt
  if (startedAt) {
    events.push({
      sequence: events.length + 1,
      timestamp: startedAt,
      type: 'ProviderStarted',
      message: 'Provider process started.',
    })
  }

  if (session.failureReason?.includes('reattached')) {
    events.push({
      sequence: events.length + 1,
      timestamp: session.completedAt ?? session.lastActivityAt ?? new Date().toISOString(),
      type: 'Recovery',
      message: session.failureReason,
    })
  }

  if (session.completedAt) {
    events.push({
      sequence: events.length + 1,
      timestamp: session.completedAt,
      type: 'ProviderExited',
      message: 'Provider process exited with code 0.',
    })
  }

  return events
}

function createExecutionTransparency(state: MockState, sessionId: string): ExecutionSessionTransparency {
  const session = state.sessions[sessionId]
  if (!session) {
    throw new Error('Execution session was not found.')
  }

  const events = createMockExecutionEvents(session)
  const recoveryEvent = events.find((event) => event.type === 'Recovery') ?? null
  const providerExitedEvent = [...events].reverse().find((event) => event.type === 'ProviderExited') ?? null

  return {
    sessionId,
    promptMetadata: {
      generatedAt: session.startedAt ?? new Date().toISOString(),
      repositoryPath: session.repositoryPath,
      milestonePath: session.milestonePath ?? '',
      includedArtifactPaths: ['.agents/plan.md', session.milestonePath ?? '.agents/milestones/m5.md'],
    },
    recovery: {
      recoveryRan: Boolean(recoveryEvent),
      recoveryTrigger: recoveryEvent ? 'StartupRecovery' : null,
      reattachAttempted: recoveryEvent?.message.includes('reattached') ? true : null,
      reattachSucceeded: recoveryEvent?.message.includes('reattached') ? true : null,
      orphanedProviderState: session.failureReason?.includes('reattached') === true,
      sessionMarkedFailedByRecovery:
        session.state === 'Failed' && session.failureReason?.includes('reattached') === true,
      recoveryEventTimestamp: recoveryEvent?.timestamp ?? null,
      recoveryMessage: recoveryEvent?.message ?? null,
    },
    monitoring: {
      providerProcessState: providerExitedEvent
        ? 'Exited'
        : session.state === 'Executing' && session.providerProcessId
          ? 'Running'
          : session.providerStartedAt
            ? 'Unknown'
            : 'NotStarted',
      exitCode: providerExitedEvent?.message.includes('code 0') ? 0 : null,
      lastActivityAt: session.lastActivityAt,
      staleActivity: false,
      retainedEventCount: events.length,
      firstRetainedEventSequence: events[0]?.sequence ?? null,
      lastRetainedEventSequence: events.at(-1)?.sequence ?? null,
      eventRetentionTrimmingDetected: false,
      monitoringWarnings: session.failureReason ? [session.failureReason] : [],
    },
  }
}

function createManifestArtifact(state: MockState, role: string, relativePath: string | null) {
  const content = relativePath ? state.content[relativePath] : undefined

  return {
    role,
    relativePath: relativePath ?? '',
    byteCount: content === undefined ? null : content.length,
    characterCount: content === undefined ? null : content.length,
    delivered: content !== undefined,
  }
}

function commitExecution(state: MockState, args: InvokeArgs): ExecutionSessionSummary {
  const sessionId = getStringArg(args, 'sessionId')
  const message = getStringArg(args, 'message')
  const statusSnapshotId = getStringArg(args, 'statusSnapshotId')
  const selectedPaths = args?.selectedPaths
  if (!Array.isArray(selectedPaths) || selectedPaths.length === 0) {
    throw new Error('At least one path must be selected for commit.')
  }

  const session = state.sessions[sessionId]
  if (!session) {
    throw new Error('Execution session was not found.')
  }

  if (session.repositoryState !== 'AwaitingCommit') {
    throw new Error('Commit can only run while awaiting commit.')
  }

  if (statusSnapshotId !== `snapshot-${sessionId}`) {
    throw new Error('Commit request uses a stale status snapshot.')
  }

  const workspace = state.workspaces[session.repositoryId]
  const timestamp = new Date().toISOString()
  const summary: ExecutionSessionSummary = {
    ...session,
    repositoryState: 'AwaitingPush',
    lastActivityAt: timestamp,
    commitSha: 'mock-commit-sha',
    committedAt: timestamp,
    commitMessage: message,
    preparationSnapshotId: statusSnapshotId,
  }

  state.sessions[sessionId] = {
    ...summary,
    id: session.id,
    repositoryId: session.repositoryId,
    repositoryPath: session.repositoryPath,
  }
  workspace.executionState = 'AwaitingPush'
  workspace.executionSummary = summary
  workspace.executionHistory = [
    summary,
    ...workspace.executionHistory.filter((session) => session.sessionId !== sessionId),
  ]
  return summary
}

function pushExecution(state: MockState, args: InvokeArgs): PushAttemptResult {
  const sessionId = getStringArg(args, 'sessionId')
  const session = state.sessions[sessionId]
  if (!session) {
    throw new Error('Execution session was not found.')
  }

  if (session.repositoryState !== 'AwaitingPush') {
    throw new Error('Push can only run while awaiting push.')
  }

  const workspace = state.workspaces[session.repositoryId]
  const timestamp = new Date().toISOString()
  const summary: ExecutionSessionSummary = {
    ...session,
    repositoryState: 'Ready',
    lastActivityAt: timestamp,
    pushAttemptedAt: timestamp,
    pushedAt: timestamp,
    pushedCommitSha: session.commitSha,
    pushRemoteName: null,
    pushBranchName: 'main',
  }

  state.sessions[sessionId] = {
    ...summary,
    id: session.id,
    repositoryId: session.repositoryId,
    repositoryPath: session.repositoryPath,
  }
  workspace.executionState = 'Ready'
  workspace.executionSummary = summary
  workspace.executionHistory = [
    summary,
    ...workspace.executionHistory.filter((session) => session.sessionId !== sessionId),
  ]
  return {
    succeeded: true,
    retryable: false,
    error: null,
    attemptedAt: timestamp,
    session: summary,
    diagnostics: [],
  }
}

function generateOperationalContextProposal(
  state: MockState,
  repositoryId: string,
): OperationalContextProposal {
  const workspace = state.workspaces[repositoryId]
  if (!workspace) {
    throw new Error(`Repository was not found: ${repositoryId}`)
  }

  const generatedAt = new Date().toISOString()
  const proposalId = `mock-proposal-${(state.operationalContextProposals[repositoryId]?.length ?? 0) + 1}`
  const generatedContent = [
    '# Operational Context',
    '',
    '## Current Mental Model',
    '',
    `- Repository \`${workspace.repository.name}\` uses repository-owned continuity artifacts.`,
    '',
    '## Architecture',
    '',
    '- Backend services own proposal generation and persistence.',
    '',
    '## Authority Boundaries',
    '',
    '## Constraints',
    '',
    '- Generated proposals do not mutate current operational context.',
    '',
    '## Stable Decisions',
    '',
    '## Decision Rationale',
    '',
    '## Open Questions',
    '',
    '## Active Risks',
    '',
    '## Recent Understanding Changes',
    '',
    '- Mock proposal generation ran from the workspace surface.',
  ].join('\n')
  const proposal: OperationalContextProposal = {
    proposalId,
    repositoryId,
    generatedAt,
    status: 'Pending',
    generatedContentRelativePath: `.agents/operational_context/proposals/${proposalId}/proposed.md`,
    generatedContentHash: `mock-hash-${proposalId}`,
    editedContentRelativePath: null,
    semanticChanges: [
      {
        type: 'ConstraintAdded',
        section: 'Constraints',
        description: 'Item added to Constraints: Generated proposals do not mutate current operational context.',
        itemId: 'mock-constraint',
      },
    ],
    compressionSummary: {
      preservedItemCount: 3,
      addedItemCount: 2,
      modifiedItemCount: 0,
      removedItemCount: 0,
      compressedItemCount: 1,
      permanentUnderstandingItemCount: 4,
      activeUnderstandingItemCount: 0,
      historicalUnderstandingItemCount: 1,
      historicalNoiseItemCount: 1,
      resolvedQuestionCount: 0,
      retiredRiskCount: 0,
      warningCount: 0,
      warnings: [],
      revisionSummary: [
        '2 durable understanding item(s) added.',
        '1 historical-noise item(s) compressed.',
      ],
      noiseRemovedIndicators: ['Repeated mock execution status was compressed.'],
      stableUnderstandingRetentionWarnings: [],
    },
    review: {
      proposalId,
      reviewState: 'PendingReview',
      baselineCurrentContextHash: 'mock-current-context-hash',
      reviewedContentHash: null,
      reviewedAt: null,
      reviewNote: null,
      staleReason: null,
    },
    promotion: {
      proposalId,
      promotedAt: null,
      promotedContentHash: null,
      promotedContentSourceRelativePath: null,
      revisionNumber: null,
      archivedRelativePath: null,
      archiveFailureReason: null,
      writeFailureReason: null,
    },
    generatedContent,
    editedContent: null,
  }

  state.operationalContextProposals[repositoryId] = (
    state.operationalContextProposals[repositoryId] ?? []
  ).map((currentProposal) =>
    currentProposal.status === 'Pending'
      ? { ...currentProposal, status: 'Superseded' }
      : currentProposal,
  )
  state.operationalContextProposals[repositoryId].unshift(proposal)
  workspace.operationalContextProposalSummary = {
    pendingProposalExists: true,
    latestProposalId: proposalId,
    generatedAt,
    status: 'Pending',
    sourceInputCount: 3,
    contentByteCount: generatedContent.length,
    contentCharacterCount: generatedContent.length,
    lastPromotedAt: null,
    lastArchivedRelativePath: null,
  }
  workspace.operationalContext.pendingProposalSummary = workspace.operationalContextProposalSummary
  workspace.operationalContext.latestReviewState = 'PendingReview'
  workspace.operationalContext.continuityWarnings = proposal.compressionSummary.warnings
  return proposal
}

function getOperationalContextProposal(
  state: MockState,
  repositoryId: string,
  proposalId: string,
): OperationalContextProposal {
  const proposal = state.operationalContextProposals[repositoryId]?.find(
    (currentProposal) => currentProposal.proposalId === proposalId,
  )
  if (!proposal) {
    throw new Error('Operational-context proposal was not found.')
  }

  return proposal
}

function editOperationalContextProposal(
  state: MockState,
  repositoryId: string,
  proposalId: string,
  content: string,
): OperationalContextProposal {
  const proposal = getOperationalContextProposal(state, repositoryId, proposalId)
  if (proposal.status !== 'Pending' && proposal.status !== 'Edited') {
    throw new Error(`Operational-context proposal is not reviewable from status ${proposal.status}.`)
  }

  proposal.status = 'Edited'
  proposal.editedContentRelativePath = `.agents/operational_context/proposals/${proposalId}/edited.md`
  proposal.editedContent = content
  proposal.review = {
    proposalId,
    reviewState: 'Edited',
    baselineCurrentContextHash: proposal.review.baselineCurrentContextHash,
    reviewedContentHash: `mock-edited-hash-${content.length}`,
    reviewedAt: new Date().toISOString(),
    reviewNote: null,
    staleReason: null,
  }
  proposal.semanticChanges = [
    {
      type: 'SectionChanged',
      section: 'Operational Context',
      description: 'Reviewer-edited proposal content.',
      itemId: null,
    },
  ]
  proposal.compressionSummary = {
    ...proposal.compressionSummary,
    warningCount: content.includes('## Constraints') ? 0 : 1,
    stableUnderstandingRetentionWarnings: content.includes('## Constraints')
      ? []
      : [
          'Constraint disappeared without explicit resolution: Generated proposals do not mutate current operational context.',
        ],
  }
  return proposal
}

function reviewOperationalContextProposal(
  state: MockState,
  repositoryId: string,
  proposalId: string,
  status: 'Accepted' | 'Rejected',
  reviewNote: string | null,
): OperationalContextProposal {
  const proposal = getOperationalContextProposal(state, repositoryId, proposalId)
  if (proposal.status !== 'Pending' && proposal.status !== 'Edited') {
    throw new Error(`Operational-context proposal is not reviewable from status ${proposal.status}.`)
  }

  proposal.status = status
  proposal.review = {
    proposalId,
    reviewState: status,
    baselineCurrentContextHash: proposal.review.baselineCurrentContextHash,
    reviewedContentHash: proposal.editedContent
      ? `mock-edited-hash-${proposal.editedContent.length}`
      : proposal.generatedContentHash,
    reviewedAt: new Date().toISOString(),
    reviewNote,
    staleReason: null,
  }
  const workspace = state.workspaces[repositoryId]
  if (workspace) {
    workspace.operationalContextProposalSummary.status = status
    workspace.operationalContextProposalSummary.pendingProposalExists = false
    workspace.operationalContext.pendingProposalSummary = workspace.operationalContextProposalSummary
    workspace.operationalContext.latestReviewState = status
    workspace.operationalContext.continuityWarnings = proposal.compressionSummary.warnings
  }
  return proposal
}

function promoteOperationalContextProposal(
  state: MockState,
  repositoryId: string,
  proposalId: string,
): OperationalContextProposal {
  const proposal = getOperationalContextProposal(state, repositoryId, proposalId)
  if (proposal.status !== 'Accepted' || proposal.review.reviewState !== 'Accepted') {
    throw new Error(`Operational-context proposal cannot be promoted from status ${proposal.status}.`)
  }

  const workspace = state.workspaces[repositoryId]
  const promotedAt = new Date().toISOString()
  const archivedRelativePath = workspace.artifactInventory.operationalContext
    ? `.agents/operational_context.${String(workspace.artifactInventory.historicalOperationalContexts.length + 1).padStart(4, '0')}.md`
    : null

  if (workspace.artifactInventory.operationalContext && archivedRelativePath) {
    workspace.artifactInventory.historicalOperationalContexts.push({
      ...workspace.artifactInventory.operationalContext,
      relativePath: archivedRelativePath,
      name: archivedRelativePath.split('/').at(-1) ?? 'operational_context.0001.md',
      versionKind: 'Historical',
    })
  }

  workspace.artifactInventory.operationalContext = {
    relativePath: '.agents/operational_context.md',
    name: 'operational_context.md',
    type: 'OperationalContext',
    family: 'OperationalContext',
    versionKind: 'Current',
  }
  workspace.hasOperationalContext = true
  workspace.operationalContextProposalSummary.status = 'Promoted'
  workspace.operationalContextProposalSummary.pendingProposalExists = false
  workspace.operationalContextProposalSummary.lastPromotedAt = promotedAt
  workspace.operationalContextProposalSummary.lastArchivedRelativePath = archivedRelativePath
  workspace.operationalContext = createOperationalContextProjection(
    workspace.artifactInventory,
    workspace.operationalContextProposalSummary,
  )
  workspace.operationalContext.latestReviewState = 'Accepted'

  proposal.status = 'Promoted'
  state.content['.agents/operational_context.md'] = proposal.editedContent ?? proposal.generatedContent ?? ''
  proposal.promotion = {
    proposalId,
    promotedAt,
    promotedContentHash: proposal.review.reviewedContentHash,
    promotedContentSourceRelativePath:
      proposal.editedContentRelativePath ?? proposal.generatedContentRelativePath,
    revisionNumber: archivedRelativePath
      ? workspace.artifactInventory.historicalOperationalContexts.length
      : null,
    archivedRelativePath,
    archiveFailureReason: null,
    writeFailureReason: null,
  }
  return proposal
}

function zeroTrend(): ContinuityTrend {
  return {
    addedCount: 0,
    removedCount: 0,
    resolvedCount: 0,
    lostCount: 0,
  }
}

function getContinuityDiagnostics(state: MockState, repositoryId: string): ContinuityDiagnostics {
  const workspace = state.workspaces[repositoryId]
  if (!workspace) {
    throw new Error(`Repository was not found: ${repositoryId}`)
  }

  const content = state.content[workspace.artifactInventory.operationalContext?.relativePath ?? ''] ?? ''
  const proposals = state.operationalContextProposals[repositoryId] ?? []
  const warnings = proposals.flatMap((proposal) =>
    proposal.compressionSummary.warnings.concat(
      proposal.compressionSummary.stableUnderstandingRetentionWarnings,
    ),
  )
  return {
    repositoryId,
    generatedAt: new Date().toISOString(),
    revisionCount: workspace.operationalContext.revisionCount,
    currentContextByteCount: content.length,
    currentContextCharacterCount: content.length,
    contextByteGrowth: workspace.artifactInventory.historicalOperationalContexts.length > 0 ? 128 : 0,
    averageBytesPerRevision: content.length,
    architectureTrend: zeroTrend(),
    constraintTrend: zeroTrend(),
    decisionTrend: zeroTrend(),
    rationaleTrend: zeroTrend(),
    openQuestionTrend: {
      addedCount: workspace.operationalContext.openQuestions.length,
      removedCount: 0,
      resolvedCount: proposals.reduce(
        (count, proposal) => count + proposal.compressionSummary.resolvedQuestionCount,
        0,
      ),
      lostCount: 0,
    },
    activeRiskTrend: {
      addedCount: workspace.operationalContext.activeRisks.length,
      removedCount: 0,
      resolvedCount: proposals.reduce(
        (count, proposal) => count + proposal.compressionSummary.retiredRiskCount,
        0,
      ),
      lostCount: 0,
    },
    compressionTrend: {
      proposalCount: proposals.length,
      compressedItemCount: proposals.reduce(
        (count, proposal) => count + proposal.compressionSummary.compressedItemCount,
        0,
      ),
      removedItemCount: proposals.reduce(
        (count, proposal) => count + proposal.compressionSummary.removedItemCount,
        0,
      ),
      resolvedQuestionCount: proposals.reduce(
        (count, proposal) => count + proposal.compressionSummary.resolvedQuestionCount,
        0,
      ),
      retiredRiskCount: proposals.reduce(
        (count, proposal) => count + proposal.compressionSummary.retiredRiskCount,
        0,
      ),
      warningCount: warnings.length,
      warnings,
      noiseRemovedIndicators: proposals.flatMap(
        (proposal) => proposal.compressionSummary.noiseRemovedIndicators,
      ),
    },
    repeatedInvestigationIndicators: [],
    repeatedQuestionIndicators: [],
    decisionReworkIndicators: [],
    continuityWarnings: warnings,
  }
}

function generateContinuityReport(state: MockState, repositoryId: string): ContinuityReport {
  const timestamp = new Date().toISOString()
  const reportId = `continuity.${Date.now()}`
  const report: ContinuityReport = {
    reportId,
    repositoryId,
    generatedAt: timestamp,
    relativePath: `.agents/operational_context/reports/${reportId}.json`,
    diagnostics: getContinuityDiagnostics(state, repositoryId),
  }
  state.continuityReports[repositoryId] = [report, ...(state.continuityReports[repositoryId] ?? [])]
  return report
}

function decideHandoff(
  state: MockState,
  sessionId: string,
  decision: 'accept' | 'reject',
): ExecutionSessionSummary {
  const session = state.sessions[sessionId]
  if (!session) {
    throw new Error('Execution session was not found.')
  }

  if (session.repositoryState !== 'AwaitingAcceptance') {
    throw new Error('Execution can only be decided while awaiting acceptance.')
  }

  const workspace = state.workspaces[session.repositoryId]
  const timestamp = new Date().toISOString()
  const repositoryState = decision === 'accept' ? 'AwaitingCommit' : 'Ready'
  const summary: ExecutionSessionSummary = {
    ...session,
    repositoryState,
    acceptedAt: decision === 'accept' ? timestamp : null,
    rejectedAt: decision === 'reject' ? timestamp : null,
    decisionNote: null,
    lastActivityAt: timestamp,
  }

  state.sessions[sessionId] = {
    ...summary,
    id: session.id,
    repositoryId: session.repositoryId,
    repositoryPath: session.repositoryPath,
  }
  workspace.executionState = repositoryState
  workspace.executionSummary = summary
  workspace.executionHistory = [
    summary,
    ...workspace.executionHistory.filter((session) => session.sessionId !== sessionId),
  ]
  return summary
}

export function installDevTauriMock() {
  const searchParams = new URLSearchParams(window.location.search)
  if (searchParams.get('mock') !== 'workspace-certification') {
    return
  }

  const state = createInitialState()
  window.__COMMAND_CENTER_MOCK_STATE__ = state
  window.__TAURI_INTERNALS__ = {
    callbacks: {},
    convertFileSrc: (filePath: string) => filePath,
    transformCallback: () => 0,
    unregisterCallback: () => undefined,
    invoke: async (cmd: string, args?: InvokeArgs) => {
      state.commandCalls[cmd] = (state.commandCalls[cmd] ?? 0) + 1

      switch (cmd) {
        case 'get_backend_url':
          return 'mock'
        case 'list_repositories':
          return clone(state.repositories.map((repository) => dashboardEntry(state.workspaces[repository.id])))
        case 'get_repository_workspace':
        case 'refresh_repository_workspace':
          return clone(state.workspaces[getStringArg(args, 'repositoryId')])
        case 'preview_execution_context':
          return clone(
            createContextPreview(
              state,
              getStringArg(args, 'repositoryId'),
              getStringArg(args, 'milestonePath'),
            ),
          )
        case 'generate_operational_context_proposal':
          return clone(generateOperationalContextProposal(state, getStringArg(args, 'repositoryId')))
        case 'list_operational_context_proposals':
          return clone(state.operationalContextProposals[getStringArg(args, 'repositoryId')] ?? [])
        case 'get_operational_context_proposal': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const proposalId = getStringArg(args, 'proposalId')
          return clone(getOperationalContextProposal(state, repositoryId, proposalId))
        }
        case 'edit_operational_context_proposal':
          return clone(
            editOperationalContextProposal(
              state,
              getStringArg(args, 'repositoryId'),
              getStringArg(args, 'proposalId'),
              getStringArg(args, 'content'),
            ),
          )
        case 'accept_operational_context_proposal': {
          return clone(
            reviewOperationalContextProposal(
              state,
              getStringArg(args, 'repositoryId'),
              getStringArg(args, 'proposalId'),
              'Accepted',
              typeof args?.reviewNote === 'string' ? args.reviewNote : null,
            ),
          )
        }
        case 'reject_operational_context_proposal': {
          return clone(
            reviewOperationalContextProposal(
              state,
              getStringArg(args, 'repositoryId'),
              getStringArg(args, 'proposalId'),
              'Rejected',
              typeof args?.reviewNote === 'string' ? args.reviewNote : null,
            ),
          )
        }
        case 'promote_operational_context_proposal':
          return clone(
            promoteOperationalContextProposal(
              state,
              getStringArg(args, 'repositoryId'),
              getStringArg(args, 'proposalId'),
            ),
          )
        case 'get_continuity_diagnostics':
          return clone(getContinuityDiagnostics(state, getStringArg(args, 'repositoryId')))
        case 'generate_continuity_report':
          return clone(generateContinuityReport(state, getStringArg(args, 'repositoryId')))
        case 'list_continuity_reports':
          return clone(state.continuityReports[getStringArg(args, 'repositoryId')] ?? [])
        case 'get_decision_context':
        case 'build_decision_context':
          return clone(state.decisionContexts[getStringArg(args, 'repositoryId')])
        case 'list_decision_candidates':
          return clone(state.decisionCandidates[getStringArg(args, 'repositoryId')] ?? [])
        case 'list_decision_proposals':
          return clone(state.decisionProposalBrowserItems[getStringArg(args, 'repositoryId')] ?? [])
        case 'list_decision_proposal_browser':
          return clone(
            filterDecisionProposalBrowserItems(
              state.decisionProposalBrowserItems[getStringArg(args, 'repositoryId')] ?? [],
              getStringArrayArg(args, 'states'),
            ),
          )
        case 'get_decision_lifecycle_eligibility':
          return clone(createDecisionLifecycleEligibility(state, getStringArg(args, 'repositoryId')))
        case 'get_decision_proposal_review': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const proposalId = getStringArg(args, 'proposalId')
          return clone(getDecisionReviewWorkspace(state, repositoryId, proposalId))
        }
        case 'get_decision_proposal_lineage': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const proposalId = getStringArg(args, 'proposalId')
          return clone(createDecisionProposalLineage(state, repositoryId, proposalId))
        }
        case 'refine_decision_proposal': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const proposalId = getStringArg(args, 'proposalId')
          return clone(
            refineDecisionProposalMock(
              state,
              repositoryId,
              proposalId,
              args?.request as DecisionRefinementRequest,
            ),
          )
        }
        case 'analyze_decision_refinement': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const proposalId = getStringArg(args, 'proposalId')
          return clone(
            analyzeDecisionRefinementMock(
              state,
              repositoryId,
              proposalId,
              args?.request as DecisionRefinementAnalysisRequest,
            ),
          )
        }
        case 'regenerate_decision_refinement': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const proposalId = getStringArg(args, 'proposalId')
          return clone(
            regenerateDecisionRefinementMock(
              state,
              repositoryId,
              proposalId,
              args?.request as DecisionPackageRegenerationRequest,
            ),
          )
        }
        case 'resolve_decision_proposal': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const proposalId = getStringArg(args, 'proposalId')
          return clone(
            resolveDecisionProposalMock(
              state,
              repositoryId,
              proposalId,
              (args?.request ?? {}) as {
                rationale?: unknown
                resolver?: unknown
                selectedOptionId?: unknown
                outcome?: unknown
              },
            ),
          )
        }
        case 'get_decision_assimilation_recommendation': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const decisionId = getStringArg(args, 'decisionId')
          return clone(getDecisionAssimilationRecommendationMock(state, repositoryId, decisionId))
        }
        case 'propose_decision_operational_context_assimilation': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const decisionId = getStringArg(args, 'decisionId')
          return clone(
            proposeDecisionOperationalContextAssimilationMock(
              state,
              repositoryId,
              decisionId,
              (args?.request ?? {}) as { requestedBy?: unknown; notes?: unknown },
            ),
          )
        }
        case 'get_decision_option_comparison': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const proposalId = getStringArg(args, 'proposalId')
          return clone(createDecisionOptionComparison(state, repositoryId, proposalId))
        }
        case 'get_decision_evidence_inspection': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const proposalId = getStringArg(args, 'proposalId')
          return clone(createDecisionEvidenceInspection(state, repositoryId, proposalId))
        }
        case 'list_decision_source_attributions': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const proposalId = getStringArg(args, 'proposalId')
          return clone(listDecisionSourceAttributionsForProposal(state, repositoryId, proposalId))
        }
        case 'get_decision_governance':
          return clone(createDecisionGovernanceReport(state, getStringArg(args, 'repositoryId'), false))
        case 'generate_decision_governance_report':
          return clone(createDecisionGovernanceReport(state, getStringArg(args, 'repositoryId'), true))
        case 'list_decision_governance_reports':
          return clone(state.decisionGovernanceReports[getStringArg(args, 'repositoryId')] ?? [])
        case 'get_decision_certification':
          return clone(createDecisionCertificationReport(state, getStringArg(args, 'repositoryId'), false))
        case 'run_decision_certification':
          return clone(createDecisionCertificationReport(state, getStringArg(args, 'repositoryId'), true))
        case 'list_decision_certification_reports':
          return clone(state.decisionCertificationReports[getStringArg(args, 'repositoryId')] ?? [])
        case 'get_decision_generation_certification':
          return clone(createDecisionGenerationCertificationReport(state, getStringArg(args, 'repositoryId'), false))
        case 'run_decision_generation_certification':
          return clone(createDecisionGenerationCertificationReport(state, getStringArg(args, 'repositoryId'), true))
        case 'list_decision_generation_certification_reports':
          return clone(state.decisionGenerationCertificationReports[getStringArg(args, 'repositoryId')] ?? [])
        case 'assess_decision_quality': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const proposalId = getStringArg(args, 'proposalId')
          return clone(assessDecisionQualityMock(state, repositoryId, proposalId))
        }
        case 'list_decision_quality_assessments':
          return clone(state.decisionQualityAssessments[getStringArg(args, 'repositoryId')] ?? [])
        case 'get_decision_quality_report':
          return clone(createDecisionQualityReport(state, getStringArg(args, 'repositoryId'), false))
        case 'generate_decision_quality_report':
          return clone(createDecisionQualityReport(state, getStringArg(args, 'repositoryId'), true))
        case 'list_decision_quality_reports':
          return clone(state.decisionQualityReports[getStringArg(args, 'repositoryId')] ?? [])
        case 'get_decision_quality_trend':
          return clone(createDecisionQualityTrend(state, getStringArg(args, 'repositoryId'), false))
        case 'generate_decision_quality_trend':
          return clone(createDecisionQualityTrend(state, getStringArg(args, 'repositoryId'), true))
        case 'list_decision_quality_trends':
          return clone(state.decisionQualityTrends[getStringArg(args, 'repositoryId')] ?? [])
        case 'get_execution_decision_projection':
          return clone(createExecutionDecisionProjection(state, getStringArg(args, 'repositoryId')))
        case 'get_execution_decision_influence':
          return clone(
            createDecisionInfluenceTrace(
              state,
              getStringArg(args, 'repositoryId'),
              getStringArg(args, 'executionId'),
            ),
          )
        case 'get_decision_influence':
          return clone(
            listDecisionInfluenceTraces(
              state,
              getStringArg(args, 'repositoryId'),
              getStringArg(args, 'decisionId'),
            ),
          )
        case 'list_reasoning_events':
          return clone(state.reasoningEvents[getStringArg(args, 'repositoryId')] ?? [])
        case 'get_reasoning_event': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const eventId = getStringArg(args, 'eventId')
          const event = (state.reasoningEvents[repositoryId] ?? []).find((item) => item.id === eventId)
          if (!event) {
            throw new Error(`Reasoning event was not found: ${eventId}`)
          }

          return clone(event)
        }
        case 'create_reasoning_event': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const command = args?.command as Partial<ReasoningEvent>
          const events = state.reasoningEvents[repositoryId] ?? []
          const event: ReasoningEvent = {
            id: `EVT-${String(events.length + 1).padStart(4, '0')}`,
            repositoryId,
            createdAt: new Date().toISOString(),
            family: command.family ?? 'Evidence',
            type: command.type ?? 'EvidenceAdded',
            title: command.title ?? 'Reasoning event',
            narrative: command.narrative ?? { summary: 'Reasoning event.', details: '' },
            references: command.references ?? [],
            provenance: command.provenance ?? {
              sourceKind: 'ManualCapture',
              capturedBy: 'mock',
              relativePath: null,
              section: null,
              excerpt: null,
              fingerprint: null,
            },
            threadIds: command.threadIds ?? [],
            tags: command.tags ?? [],
          }
          state.reasoningEvents[repositoryId] = [...events, event]
          refreshReasoningSummary(state, repositoryId)
          return clone(event)
        }
        case 'list_reasoning_manual_capture_templates':
          return clone(createManualReasoningCaptureTemplates())
        case 'capture_manual_reasoning': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const command = args?.command as ManualReasoningCaptureCommand
          const template = createManualReasoningCaptureTemplates().find(
            (item) => item.kind === command.kind,
          )
          if (!template) {
            throw new Error(`Unsupported manual reasoning capture kind: ${command.kind}`)
          }

          const events = state.reasoningEvents[repositoryId] ?? []
          const event: ReasoningEvent = {
            id: `EVT-${String(events.length + 1).padStart(4, '0')}`,
            repositoryId,
            createdAt: new Date().toISOString(),
            family: template.family,
            type: template.type,
            title: command.title,
            narrative: command.narrative,
            references: command.references ?? [],
            provenance: command.provenance,
            threadIds: command.threadIds ?? [],
            tags: command.tags ?? [],
          }
          state.reasoningEvents[repositoryId] = [...events, event]
          for (const threadId of command.threadIds ?? []) {
            state.reasoningThreads[repositoryId] = (state.reasoningThreads[repositoryId] ?? []).map(
              (thread) =>
                thread.id === threadId
                  ? { ...thread, eventIds: [...thread.eventIds, event.id], updatedAt: event.createdAt }
                  : thread,
            )
          }
          refreshReasoningSummary(state, repositoryId)
          return clone(event)
        }
        case 'list_reasoning_threads':
          return clone(state.reasoningThreads[getStringArg(args, 'repositoryId')] ?? [])
        case 'get_reasoning_thread': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const threadId = getStringArg(args, 'threadId')
          const thread = (state.reasoningThreads[repositoryId] ?? []).find((item) => item.id === threadId)
          if (!thread) {
            throw new Error(`Reasoning thread was not found: ${threadId}`)
          }

          return clone(thread)
        }
        case 'create_reasoning_thread': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const command = args?.command as Partial<ReasoningThread>
          const threads = state.reasoningThreads[repositoryId] ?? []
          const timestamp = new Date().toISOString()
          const thread: ReasoningThread = {
            id: `THR-${String(threads.length + 1).padStart(4, '0')}`,
            repositoryId,
            title: command.title ?? 'Reasoning thread',
            theme: command.theme ?? 'General',
            createdAt: timestamp,
            updatedAt: timestamp,
            summary: command.summary ?? 'Reasoning thread.',
            eventIds: command.eventIds ?? [],
            tags: command.tags ?? [],
          }
          state.reasoningThreads[repositoryId] = [...threads, thread]
          refreshReasoningSummary(state, repositoryId)
          return clone(thread)
        }
        case 'append_reasoning_thread_event': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const threadId = getStringArg(args, 'threadId')
          const eventId = getStringArg(args, 'eventId')
          const threads = state.reasoningThreads[repositoryId] ?? []
          const thread = threads.find((item) => item.id === threadId)
          if (!thread) {
            throw new Error(`Reasoning thread was not found: ${threadId}`)
          }

          const updatedThread = {
            ...thread,
            updatedAt: new Date().toISOString(),
            eventIds: thread.eventIds.includes(eventId) ? thread.eventIds : [...thread.eventIds, eventId],
          }
          state.reasoningThreads[repositoryId] = threads.map((item) =>
            item.id === threadId ? updatedThread : item,
          )
          refreshReasoningSummary(state, repositoryId)
          return clone(updatedThread)
        }
        case 'list_reasoning_relationships':
          return clone(state.reasoningRelationships[getStringArg(args, 'repositoryId')] ?? [])
        case 'create_reasoning_relationship': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const command = args?.command as Partial<ReasoningRelationship>
          const relationships = state.reasoningRelationships[repositoryId] ?? []
          const relationship: ReasoningRelationship = {
            id: `REL-${String(relationships.length + 1).padStart(4, '0')}`,
            repositoryId,
            createdAt: new Date().toISOString(),
            type: command.type ?? 'Supports',
            source: command.source ?? {
              kind: 'ReasoningEvent',
              id: 'EVT-0001',
              relativePath: null,
              section: null,
              excerpt: null,
            },
            target: command.target ?? {
              kind: 'ReasoningEvent',
              id: 'EVT-0001',
              relativePath: null,
              section: null,
              excerpt: null,
            },
            narrative: command.narrative ?? { summary: 'Reasoning relationship.', details: '' },
            provenance: command.provenance ?? {
              sourceKind: 'ManualCapture',
              capturedBy: 'mock',
              relativePath: null,
              section: null,
              excerpt: null,
              fingerprint: null,
            },
          }
          state.reasoningRelationships[repositoryId] = [...relationships, relationship]
          refreshReasoningSummary(state, repositoryId)
          return clone(relationship)
        }
        case 'get_reasoning_graph':
          return clone(createReasoningGraph(state, getStringArg(args, 'repositoryId')))
        case 'trace_reasoning_backward': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const graph = createReasoningGraph(state, repositoryId)
          return clone(
            createReasoningTrace(
              graph,
              'Backward',
              getStringArg(args, 'kind') as ReasoningTrace['target']['kind'],
              getStringArg(args, 'id'),
            ),
          )
        }
        case 'trace_reasoning_forward': {
          const repositoryId = getStringArg(args, 'repositoryId')
          const graph = createReasoningGraph(state, repositoryId)
          return clone(
            createReasoningTrace(
              graph,
              'Forward',
              getStringArg(args, 'kind') as ReasoningTrace['target']['kind'],
              getStringArg(args, 'id'),
            ),
          )
        }
        case 'query_reasoning': {
          const repositoryId = getStringArg(args, 'repositoryId')
          return clone(createReasoningQueryResult(state, repositoryId, args?.query as ReasoningQuery))
        }
        case 'reconstruct_reasoning': {
          const repositoryId = getStringArg(args, 'repositoryId')
          return clone(createReasoningReconstruction(state, repositoryId, args?.query as ReasoningQuery))
        }
        case 'get_reasoning_materialization_review':
        case 'run_reasoning_materialization_review':
          return clone(createReasoningMaterializationReview(state, getStringArg(args, 'repositoryId')))
        case 'get_reasoning_certification':
          return clone(createReasoningCertificationReport(state, getStringArg(args, 'repositoryId'), false))
        case 'run_reasoning_certification':
          return clone(createReasoningCertificationReport(state, getStringArg(args, 'repositoryId'), true))
        case 'list_reasoning_certification_reports':
          return clone(state.reasoningCertificationReports[getStringArg(args, 'repositoryId')] ?? [])
        case 'start_execution':
          return clone(
            startExecution(
              state,
              getStringArg(args, 'repositoryId'),
              getStringArg(args, 'milestonePath'),
            ),
          )
        case 'get_active_execution': {
          const workspace = state.workspaces[getStringArg(args, 'repositoryId')]
          if (!workspace?.executionSummary) {
            throw new Error('No active execution session.')
          }

          return clone(workspace.executionSummary)
        }
        case 'get_git_status':
          return clone(createGitStatus(state, getStringArg(args, 'repositoryId')))
        case 'prepare_commit':
          return clone(createCommitPreparation(state, getStringArg(args, 'sessionId')))
        case 'get_execution_git_eligibility':
          return clone(createExecutionGitEligibility(state, args))
        case 'commit_execution':
          return clone(commitExecution(state, args))
        case 'push_execution':
          return clone(pushExecution(state, args))
        case 'get_execution_session': {
          const session = state.sessions[getStringArg(args, 'sessionId')]
          if (!session) {
            throw new Error('Execution session was not found.')
          }

          return clone(session)
        }
        case 'get_execution_prompt_manifest':
          return clone(createExecutionPromptManifest(state, getStringArg(args, 'sessionId')))
        case 'get_execution_transparency':
          return clone(createExecutionTransparency(state, getStringArg(args, 'sessionId')))
        case 'accept_execution_handoff':
          return clone(decideHandoff(state, getStringArg(args, 'sessionId'), 'accept'))
        case 'reject_execution_handoff':
          return clone(decideHandoff(state, getStringArg(args, 'sessionId'), 'reject'))
        case 'load_artifact_content':
          return state.content[getStringArg(args, 'relativePath')] ?? ''
        case 'save_artifact_content':
          state.content[getStringArg(args, 'relativePath')] = getStringArg(args, 'content')
          return undefined
        case 'rotate_current_handoff': {
          const repositoryId = getStringArg(args, 'repositoryId')
          rotateCurrentArtifact(state, repositoryId, 'currentHandoff', 'historicalHandoffs', 'handoff')
          return clone(state.workspaces[repositoryId])
        }
        case 'rotate_current_decisions': {
          const repositoryId = getStringArg(args, 'repositoryId')
          rotateCurrentArtifact(
            state,
            repositoryId,
            'currentDecisions',
            'historicalDecisions',
            'decisions',
          )
          return clone(state.workspaces[repositoryId])
        }
        case 'remove_repository': {
          const repositoryId = getStringArg(args, 'repositoryId')
          state.repositories = state.repositories.filter((repository) => repository.id !== repositoryId)
          return undefined
        }
        default:
          throw new Error(`Unhandled mock command: ${cmd}`)
      }
    },
  }
}
