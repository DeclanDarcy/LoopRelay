import { Badge, EmptyState } from '../../components/design'
import { ActionEligibilityView, DiagnosticList } from '../../components/explainability'
import type {
  DecisionLifecycleEntityEligibility,
  DecisionProposal,
  DecisionReviewWorkspace,
} from '../../types'
import {
  decisionDiagnosticsToExplanation,
  decisionGenerationDiagnosticsToRejectedOptionDiagnostics,
  decisionLifecycleEligibilityToActions,
} from '../../lib/explainability'
import { DecisionEvidenceBlock, DecisionSourceList } from './DecisionEvidenceFragments'
import { DecisionOptionEvaluationTable } from './DecisionOptionEvaluationTable'
import { DecisionRecommendationExplanation } from './DecisionRecommendationExplanation'
import { DecisionRejectedOptionList } from './DecisionRejectedOptionList'

type DecisionProposalViewerProps = {
  workspace: DecisionReviewWorkspace | null
  eligibility?: DecisionLifecycleEntityEligibility | null
  isLoading: boolean
}

export function DecisionProposalViewer({ workspace, eligibility = null, isLoading }: DecisionProposalViewerProps) {
  if (!workspace) {
    return (
      <section className="decision-lifecycle-panel decision-proposal-viewer" aria-label="Proposal viewer">
        <h5>Proposal Viewer</h5>
        <EmptyState className="empty-state">
          {isLoading ? 'Loading proposal review workspace...' : 'Select a proposal to inspect.'}
        </EmptyState>
      </section>
    )
  }

  const { proposal, review, notes, revisions, diagnostics } = workspace
  const lastTransition = getLastTransition(proposal.history)

  return (
    <section className="decision-lifecycle-panel decision-proposal-viewer" aria-label="Proposal viewer">
      <div className="decision-panel-heading">
        <h5>Proposal Viewer</h5>
        <span>{proposal.id}</span>
      </div>

      <article className="decision-viewer-summary" aria-label="Proposal summary">
        <div>
          <span>Proposal</span>
          <strong>{proposal.title}</strong>
        </div>
        <div className="decision-badge-row">
          <Badge tone="info">{proposal.state}</Badge>
          <Badge>{review.state}</Badge>
          <Badge>{proposal.candidateId}</Badge>
          <Badge tone="done">Current authority</Badge>
        </div>
        <p>{proposal.context}</p>
        <small>Historical revisions explain proposal evolution; this current proposal remains authoritative.</small>
      </article>

      <section className="decision-lifecycle-eligibility" aria-label="Proposal review state">
        <div>
          <span>Current review state</span>
          <strong>{review.state}</strong>
        </div>
        <div>
          <span>Review updated</span>
          <strong>{formatDate(review.updatedAt)}</strong>
        </div>
        <div>
          <span>Proposal lifecycle state</span>
          <strong>{eligibility?.currentState ?? proposal.state}</strong>
        </div>
        <div>
          <span>Last transition</span>
          <strong>{lastTransition ? formatTransition(lastTransition) : 'No lifecycle transition recorded'}</strong>
        </div>
        {lastTransition?.reason ? (
          <div>
            <span>Last transition reason</span>
            <strong>{lastTransition.reason}</strong>
          </div>
        ) : null}
        {review.reason ? (
          <div>
            <span>Review reason</span>
            <strong>{review.reason}</strong>
          </div>
        ) : null}
        {eligibility ? (
          <>
            <div>
              <span>Allowed transitions</span>
              <strong>{eligibility.allowedNextStates.join(', ') || 'None'}</strong>
            </div>
            <div>
              <span>Allowed actions</span>
              <strong>{eligibility.allowedActions.map((action) => action.displayName).join(', ') || 'None'}</strong>
            </div>
            <div aria-label="Proposal unavailable transition reasons">
              <ActionEligibilityView
                actions={decisionLifecycleEligibilityToActions(eligibility)}
                title="Proposal Action Eligibility"
              />
            </div>
            <DiagnosticList
              diagnostics={decisionDiagnosticsToExplanation(eligibility.diagnostics, 'Proposal review')}
              title="Proposal Review Diagnostics"
            />
          </>
        ) : (
          <div>
            <span>Allowed transitions</span>
            <strong>Lifecycle eligibility has not loaded.</strong>
          </div>
        )}
      </section>

      <div className="decision-diagnostics-grid" aria-label="Review diagnostics">
        <span>{diagnostics.optionCount} options</span>
        <span>{diagnostics.tradeoffCount} tradeoffs</span>
        <span>{diagnostics.assumptionCount} assumptions</span>
        <span>{diagnostics.noteCount} notes</span>
        <span>{diagnostics.hasRecommendation ? 'Recommendation present' : 'No recommendation'}</span>
        <span>{diagnostics.hasEvidence ? 'Proposal evidence present' : 'No proposal evidence'}</span>
      </div>

      {proposal.generationDiagnostics ? (
        <div className="decision-diagnostics-grid" aria-label="Proposal generation diagnostics">
          <span>{proposal.generationDiagnostics.generatedOptionCount} generated options</span>
          <span>{proposal.generationDiagnostics.acceptedOptionCount} accepted options</span>
          <span>{proposal.generationDiagnostics.rejectedOptionCount} rejected options</span>
          <span>{proposal.generationDiagnostics.deduplicatedOptionCount} deduplicated options</span>
          <span>{proposal.generationDiagnostics.fallbackOptionCount} fallback options</span>
          <span>{proposal.generationDiagnostics.optionValidationResults.length} validation results</span>
        </div>
      ) : null}

      <DiagnosticList
        diagnostics={decisionDiagnosticsToExplanation(
          proposal.generationDiagnostics?.diagnostics ?? [],
          'Generation diagnostic',
        )}
        title="Generation Diagnostics"
      />

      <DiagnosticList
        diagnostics={decisionDiagnosticsToExplanation(diagnostics.warnings, 'Review warning')}
        title="Review Diagnostics Warnings"
      />

      <ProposalEvidenceBlock title="Proposal Evidence" evidence={proposal.evidence} />

      <div className="decision-option-grid" aria-label="Decision options">
        {proposal.options.map((option) => (
          <article className="decision-inspection-card" key={option.id}>
            <div>
              <span>Option {option.id}</span>
              <strong>{option.title}</strong>
            </div>
            <p>{option.description}</p>
            <OptionTransparency proposal={proposal} optionId={option.id} />
            <ProposalEvidenceBlock title="Option Evidence" evidence={option.evidence} />
            <TradeoffsForOption proposal={proposal} optionId={option.id} />
          </article>
        ))}
      </div>

      <DecisionRecommendationExplanation recommendation={proposal.recommendation} />
      <DecisionOptionEvaluationTable proposal={proposal} />
      <DecisionRejectedOptionList diagnostics={proposal.generationDiagnostics} />

      {proposal.assumptions.length > 0 ? (
        <div className="decision-inspection-list" aria-label="Decision assumptions">
          <h6>Assumptions</h6>
          {proposal.assumptions.map((assumption) => (
            <article className="decision-inspection-card" key={assumption.id}>
              <div>
                <span>{assumption.id}</span>
                <strong>{assumption.statement}</strong>
              </div>
              <ProposalEvidenceBlock title="Assumption Evidence" evidence={assumption.evidence} />
            </article>
          ))}
        </div>
      ) : null}

      <div className="decision-review-grid">
        <section aria-label="Review notes">
          <h6>Review Notes</h6>
          {notes.length > 0 ? (
            <div className="decision-inspection-list">
              {notes.map((note) => (
                <article className="decision-inspection-card" key={note.id}>
                  <div>
                    <span>{formatDate(note.createdAt)}</span>
                    <strong>{note.reviewer}</strong>
                  </div>
                  <p>{note.body}</p>
                  <SourceList sources={note.sources} />
                </article>
              ))}
            </div>
          ) : (
            <EmptyState className="empty-state">No review notes recorded.</EmptyState>
          )}
        </section>

        <section aria-label="Proposal revisions">
          <h6>Revisions</h6>
          {revisions.length > 0 ? (
            <div className="decision-inspection-list">
              {revisions.map((revision) => (
                <article className="decision-inspection-card" key={revision.id}>
                  <div>
                    <span>{formatDate(revision.createdAt)}</span>
                    <strong>{revision.id}</strong>
                  </div>
                  <p>{revision.reason}</p>
                  <small>{revision.changedFields.join(', ') || 'No changed fields recorded'}</small>
                  <SourceList sources={revision.sources} />
                </article>
              ))}
            </div>
          ) : (
            <EmptyState className="empty-state">No proposal revisions recorded.</EmptyState>
          )}
        </section>
      </div>
    </section>
  )
}

function TradeoffsForOption({ proposal, optionId }: { proposal: DecisionProposal; optionId: string }) {
  const tradeoffs = proposal.tradeoffs.filter((tradeoff) => tradeoff.optionId === optionId)

  if (tradeoffs.length === 0) {
    return null
  }

  return (
    <div className="decision-inspection-list" aria-label={`Tradeoffs for ${optionId}`}>
      {tradeoffs.map((tradeoff) => (
        <article className="decision-tradeoff" key={`${tradeoff.optionId}-${tradeoff.benefit}-${tradeoff.cost}`}>
          <p><strong>Benefit:</strong> {tradeoff.benefit}</p>
          <p><strong>Cost:</strong> {tradeoff.cost}</p>
          <ProposalEvidenceBlock title="Tradeoff Evidence" evidence={tradeoff.evidence} />
        </article>
      ))}
    </div>
  )
}

function OptionTransparency({ proposal, optionId }: { proposal: DecisionProposal; optionId: string }) {
  const option = proposal.options.find((candidate) => candidate.id === optionId)
  const analyzedOption = proposal.analyzedOptions?.find((candidate) => candidate.optionId === optionId)
  const comparison = proposal.tradeoffComparisons?.find((candidate) => candidate.optionId === optionId)
  const validation = proposal.generationDiagnostics?.optionValidationResults.find(
    (candidate) => candidate.optionId === optionId,
  )

  return (
    <div className="decision-option-transparency" aria-label={`Option transparency for ${optionId}`}>
      <div className="decision-diagnostics-grid">
        {option?.type ? <span>Type {option.type}</span> : null}
        {validation ? <span>{validation.isValid ? 'Valid option' : 'Invalid option'}</span> : null}
        {comparison?.disqualifyingConstraints.length ? (
          <span>{comparison.disqualifyingConstraints.length} disqualifying constraints</span>
        ) : null}
      </div>
      {option?.dependencies?.length ? <FactChips title={`Dependencies for ${optionId}`} values={option.dependencies} /> : null}
      {option?.assumptions?.length ? <FactChips title={`Assumptions for ${optionId}`} values={option.assumptions} /> : null}
      {option?.diagnostics?.length ? <FactChips title={`Diagnostics for ${optionId}`} values={option.diagnostics} /> : null}
      {validation && !validation.isValid ? (
        <DiagnosticList
          diagnostics={decisionGenerationDiagnosticsToRejectedOptionDiagnostics({
            generatedOptionCount: 0,
            acceptedOptionCount: 0,
            rejectedOptionCount: 0,
            deduplicatedOptionCount: 0,
            fallbackOptionCount: 0,
            optionValidationResults: [validation],
            diagnostics: [],
            rejectedOptions: [],
            deduplicatedOptions: [],
          })}
          title={`Required human action for ${optionId}`}
        />
      ) : null}
      {analyzedOption ? (
        <div className="decision-inspection-list" aria-label={`Analyzed option details for ${optionId}`}>
          <AnalyzedFacts title="Benefits" facts={analyzedOption.benefits.map((item) => `${item.impact}: ${item.statement}`)} />
          <AnalyzedFacts title="Costs" facts={analyzedOption.costs.map((item) => `${item.impact}: ${item.statement}`)} />
          <AnalyzedFacts title="Risks" facts={analyzedOption.risks.map((item) => `${item.severity}: ${item.statement}`)} />
          <AnalyzedFacts title="Dependencies" facts={analyzedOption.dependencies.map((item) => item.statement)} />
          <AnalyzedFacts title="Consequences" facts={analyzedOption.consequences.map((item) => `${item.impact}: ${item.statement}`)} />
          <FactChips title={`Analysis diagnostics for ${optionId}`} values={analyzedOption.diagnostics} />
          <ProposalEvidenceBlock title="Analysis Evidence" evidence={analyzedOption.evidence} />
        </div>
      ) : null}
      {comparison ? (
        <div className="decision-inspection-list" aria-label={`Tradeoff comparison for ${optionId}`}>
          <FactChips title={`Relative strengths for ${optionId}`} values={comparison.relativeStrengths} />
          <FactChips title={`Relative weaknesses for ${optionId}`} values={comparison.relativeWeaknesses} />
          <FactChips title={`Unique advantages for ${optionId}`} values={comparison.uniqueAdvantages} />
          <FactChips title={`Unique risks for ${optionId}`} values={comparison.uniqueRisks} />
          <FactChips title={`Disqualifying constraints for ${optionId}`} values={comparison.disqualifyingConstraints} />
          <ProposalEvidenceBlock title="Comparison Evidence" evidence={comparison.evidence} />
        </div>
      ) : null}
    </div>
  )
}

function FactChips({ title, values }: { title: string; values: string[] }) {
  if (values.length === 0) {
    return null
  }

  return (
    <div className="decision-warning-list" aria-label={title}>
      {values.map((value) => (
        <span key={value}>{value}</span>
      ))}
    </div>
  )
}

function AnalyzedFacts({ title, facts }: { title: string; facts: string[] }) {
  return <FactChips title={title} values={facts} />
}

const ProposalEvidenceBlock = DecisionEvidenceBlock
const SourceList = DecisionSourceList

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  return date.toLocaleString()
}

function getLastTransition(history: DecisionProposal['history'][number][]) {
  return [...history]
    .reverse()
    .find((entry) => entry.fromState !== entry.toState && (entry.fromState || entry.toState))
}

function formatTransition(entry: DecisionProposal['history'][number]) {
  const fromState = entry.fromState ?? 'None'
  const toState = entry.toState ?? 'None'

  return `${entry.action}: ${fromState} -> ${toState}`
}
