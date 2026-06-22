import { Badge, EmptyState } from '../../components/design'
import type {
  DecisionEvidence,
  DecisionProposal,
  DecisionReviewWorkspace,
  DecisionSourceReference,
} from '../../types'

type DecisionProposalViewerProps = {
  workspace: DecisionReviewWorkspace | null
  isLoading: boolean
}

export function DecisionProposalViewer({ workspace, isLoading }: DecisionProposalViewerProps) {
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
        </div>
        <p>{proposal.context}</p>
      </article>

      <div className="decision-diagnostics-grid" aria-label="Review diagnostics">
        <span>{diagnostics.optionCount} options</span>
        <span>{diagnostics.tradeoffCount} tradeoffs</span>
        <span>{diagnostics.assumptionCount} assumptions</span>
        <span>{diagnostics.noteCount} notes</span>
        <span>{diagnostics.hasRecommendation ? 'Recommendation present' : 'No recommendation'}</span>
        <span>{diagnostics.hasEvidence ? 'Proposal evidence present' : 'No proposal evidence'}</span>
      </div>

      {diagnostics.warnings.length > 0 ? (
        <div className="decision-warning-list" aria-label="Review diagnostics warnings">
          {diagnostics.warnings.map((warning) => (
            <span key={warning}>{warning}</span>
          ))}
        </div>
      ) : null}

      <ProposalEvidenceBlock title="Proposal Evidence" evidence={proposal.evidence} />

      <div className="decision-option-grid" aria-label="Decision options">
        {proposal.options.map((option) => (
          <article className="decision-inspection-card" key={option.id}>
            <div>
              <span>Option {option.id}</span>
              <strong>{option.title}</strong>
            </div>
            <p>{option.description}</p>
            <ProposalEvidenceBlock title="Option Evidence" evidence={option.evidence} />
            <TradeoffsForOption proposal={proposal} optionId={option.id} />
          </article>
        ))}
      </div>

      {proposal.recommendation ? (
        <article className="decision-inspection-card" aria-label="Decision recommendation">
          <div>
            <span>Recommendation</span>
            <strong>{proposal.recommendation.optionId}</strong>
          </div>
          <p>{proposal.recommendation.rationale}</p>
          <ProposalEvidenceBlock title="Recommendation Evidence" evidence={proposal.recommendation.evidence} />
        </article>
      ) : null}

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

function ProposalEvidenceBlock({ title, evidence }: { title: string; evidence: DecisionEvidence[] }) {
  if (evidence.length === 0) {
    return null
  }

  return (
    <div className="decision-evidence-block" aria-label={title}>
      <span>{title}</span>
      {evidence.map((evidenceItem) => (
        <article key={`${title}-${evidenceItem.summary}`}>
          <p>{evidenceItem.summary}</p>
          <SourceList sources={evidenceItem.sources} />
        </article>
      ))}
    </div>
  )
}

function SourceList({ sources }: { sources: DecisionSourceReference[] }) {
  if (sources.length === 0) {
    return null
  }

  return (
    <ul className="decision-source-list" aria-label="Source attribution">
      {sources.map((source, index) => (
        <li key={`${source.sourceKind}-${source.relativePath ?? 'none'}-${index}`}>
          <strong>{source.sourceKind}</strong>
          {source.relativePath ? <span>{source.relativePath}</span> : null}
          {source.section ? <span>{source.section}</span> : null}
          {source.excerpt ? <p>{source.excerpt}</p> : null}
        </li>
      ))}
    </ul>
  )
}

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  return date.toLocaleString()
}
