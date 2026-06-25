import { EmptyState } from '../../components/design'
import type { DecisionProposal, OptionEvaluation } from '../../types'
import { DecisionEvidenceBlock } from './DecisionEvidenceFragments'

export function DecisionOptionEvaluationTable({ proposal }: { proposal: DecisionProposal }) {
  const evaluations = proposal.recommendation?.optionEvaluations ?? []

  if (evaluations.length === 0) {
    return (
      <section className="decision-inspection-list" aria-label="Decision option evaluations">
        <h6>Option Evaluations</h6>
        <EmptyState className="empty-state">No backend option evaluations are attached to this proposal.</EmptyState>
      </section>
    )
  }

  return (
    <section className="decision-inspection-list" aria-label="Decision option evaluations">
      <h6>Option Evaluations</h6>
      <div className="decision-option-evaluation-grid">
        {evaluations.map((evaluation) => (
          <OptionEvaluationCard key={evaluation.optionId} evaluation={evaluation} />
        ))}
      </div>
    </section>
  )
}

function OptionEvaluationCard({ evaluation }: { evaluation: OptionEvaluation }) {
  return (
    <article className="decision-inspection-card">
      <div>
        <span>Option {evaluation.optionId}</span>
        <strong>Rank {evaluation.rank} / Score {evaluation.score}</strong>
      </div>
      <p>{evaluation.summary}</p>
      <small>{evaluation.scoreExplanation}</small>
      <OptionEvaluationList title="Strengths" values={evaluation.strengths} />
      <OptionEvaluationList title="Weaknesses" values={evaluation.weaknesses} />
      <OptionEvaluationList title="Risks" values={evaluation.risks} />
      <OptionEvaluationList title="Constraints" values={evaluation.constraints} />
      {evaluation.evidence.length > 0 ? (
        <div className="decision-inspection-list" aria-label={`Evaluation evidence for ${evaluation.optionId}`}>
          {evaluation.evidence.map((item) => (
            <article className="decision-tradeoff" key={`${evaluation.optionId}-${item.type}-${item.summary}`}>
              <div>
                <span>{item.type}</span>
                <strong>{item.optionId}</strong>
              </div>
              <p>{item.summary}</p>
              <DecisionEvidenceBlock title={`${item.type} Evidence`} evidence={item.evidence} />
            </article>
          ))}
        </div>
      ) : null}
    </article>
  )
}

function OptionEvaluationList({ title, values }: { title: string; values: string[] }) {
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
