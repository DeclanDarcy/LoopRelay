import type {
  Artifact,
  ArtifactInventory,
  ArtifactType,
  ArtifactVersionKind,
  CommitPreparation,
  ContinuityDiagnostics,
  ContinuityReport,
  ContinuityTrend,
  DecisionCandidate,
  DecisionContextSnapshot,
  DecisionEvidenceInspection,
  DecisionProposal,
  DecisionOptionComparison,
  DecisionProposalBrowserItem,
  DecisionProposalLineage,
  DecisionRefinementRequest,
  DecisionReviewWorkspace,
  DecisionSourceAttribution,
  ExecutionContextPreview,
  ExecutionSessionState,
  ExecutionSession,
  ExecutionSessionSummary,
  OperationalContextItem,
  OperationalContextProjection,
  OperationalContextProposal,
  OperationalContextProposalSummary,
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
  }
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

function pushExecution(state: MockState, args: InvokeArgs): ExecutionSessionSummary {
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
  return summary
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
