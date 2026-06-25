import { useMemo } from 'react'
import { EmptyState } from '../../components/design'
import { DecisionBurdenExplanation } from './DecisionBurdenExplanation'
import { DecisionQualityExplanation } from './DecisionQualityExplanation'
import type {
  DecisionQualityAssessment,
  DecisionQualityReport,
  DecisionQualitySignal,
  DecisionQualityTrend,
} from '../../types'

type DecisionQualityPanelProps = {
  assessments: DecisionQualityAssessment[]
  currentReport: DecisionQualityReport | null
  reports: DecisionQualityReport[]
  currentTrend: DecisionQualityTrend | null
  trends: DecisionQualityTrend[]
  selectedProposalId: string | null
  isLoading: boolean
  isAssessing: boolean
  isGeneratingReport: boolean
  isGeneratingTrend: boolean
  error: string | null
  onAssessProposal: () => void
  onGenerateReport: () => void
  onGenerateTrend: () => void
}

const priorityCategories = [
  'HumanAuthoringBurden',
  'RecommendationStability',
  'TradeoffQuality',
  'ContextQuality',
  'ConstraintQuality',
]

export function DecisionQualityPanel({
  assessments,
  currentReport,
  reports,
  currentTrend,
  trends,
  selectedProposalId,
  isLoading,
  isAssessing,
  isGeneratingReport,
  isGeneratingTrend,
  error,
  onAssessProposal,
  onGenerateReport,
  onGenerateTrend,
}: DecisionQualityPanelProps) {
  const prioritizedSignals = useMemo(
    () => collectPrioritizedSignals(currentReport?.assessments ?? assessments),
    [assessments, currentReport?.assessments],
  )

  return (
    <section className="decision-lifecycle-panel decision-quality-panel" aria-label="Decision quality">
      <div className="decision-panel-heading">
        <div>
          <h5>Quality</h5>
          <span>Assessment history and trend surface</span>
        </div>
        <div className="decision-governance-actions" aria-label="Quality actions">
          <button
            type="button"
            className="secondary-action"
            onClick={onAssessProposal}
            disabled={!selectedProposalId || isLoading || isAssessing}
          >
            {isAssessing ? 'Assessing...' : 'Assess Proposal'}
          </button>
          <button
            type="button"
            className="secondary-action"
            onClick={onGenerateReport}
            disabled={!currentReport || isLoading || isGeneratingReport}
          >
            {isGeneratingReport ? 'Saving...' : 'Save Report'}
          </button>
          <button
            type="button"
            className="secondary-action"
            onClick={onGenerateTrend}
            disabled={!currentTrend || isLoading || isGeneratingTrend}
          >
            {isGeneratingTrend ? 'Saving...' : 'Save Trend'}
          </button>
        </div>
      </div>

      {error ? <p className="notice error">{error}</p> : null}

      {currentReport ? (
        <>
          <div className="decision-lineage-summary" aria-label="Decision quality burden summary">
            <span>Review only: {currentReport.reviewOnlyCount}</span>
            <span>Minor edit: {currentReport.minorEditCount}</span>
            <span>Major refinement: {currentReport.majorRefinementCount}</span>
            <span>Full rewrite: {currentReport.fullRewriteCount}</span>
          </div>

          <div className="decision-lineage-summary" aria-label="Decision quality outcome summary">
            <span>{currentReport.acceptedCount} accepted</span>
            <span>{currentReport.rejectedCount} rejected</span>
            <span>{currentReport.recommendationDivergenceCount} recommendation divergences</span>
            <span>{currentReport.alternativeUtilizationCount} alternatives used</span>
          </div>

          <div className="decision-governance-meta" aria-label="Quality report metadata">
            <span>Current inspection: {currentReport.id}</span>
            <span>Generated: {formatDate(currentReport.generatedAt)}</span>
            <span>Rating: {currentReport.rating}</span>
          </div>

          {currentTrend ? (
            <div className="decision-lineage-summary" aria-label="Decision quality trend summary">
              <span>Trend: {currentTrend.direction}</span>
              <span>Current: {currentTrend.currentRating}</span>
              <span>Previous: {currentTrend.previousRating}</span>
              <span>{currentTrend.assessmentCount} assessments</span>
            </div>
          ) : null}

          {currentReport.diagnostics.length > 0 || currentTrend?.diagnostics.length ? (
            <div className="decision-warning-list" aria-label="Quality diagnostics">
              {currentReport.diagnostics.map((diagnostic) => (
                <span key={`report-${diagnostic}`}>{diagnostic}</span>
              ))}
              {currentTrend?.diagnostics.map((diagnostic) => (
                <span key={`trend-${diagnostic}`}>{diagnostic}</span>
              ))}
            </div>
          ) : null}

          <div className="decision-inspection-list" aria-label="Priority quality signals">
            <h6>Priority Signals</h6>
            {prioritizedSignals.length > 0 ? (
              prioritizedSignals.map((signal) => (
                <QualitySignalCard signal={signal} key={`${signal.decisionId}-${signal.id}`} />
              ))
            ) : (
              <EmptyState className="empty-state">No priority quality signals are available.</EmptyState>
            )}
          </div>

          <div className="decision-inspection-list" aria-label="Quality explanations">
            <h6>Quality Explanations</h6>
            {currentReport.assessments.length > 0 ? (
              currentReport.assessments.map((assessment) => (
                <DecisionQualityExplanation assessment={assessment} key={`quality-${assessment.id}`} />
              ))
            ) : (
              <EmptyState className="empty-state">No quality explanations are available.</EmptyState>
            )}
          </div>

          <div className="decision-inspection-list" aria-label="Human authoring burden explanations">
            <h6>Human Authoring Burden</h6>
            {currentReport.humanAuthoringBurdenExplanations?.length ? (
              currentReport.humanAuthoringBurdenExplanations.map((explanation) => (
                <DecisionBurdenExplanation explanation={explanation} key={`burden-${explanation.decisionId}`} />
              ))
            ) : (
              <EmptyState className="empty-state">No burden explanations are available.</EmptyState>
            )}
          </div>

          <div className="decision-inspection-list" aria-label="Quality assessment history">
            <h6>Assessments</h6>
            {assessments.length > 0 ? (
              <div className="decision-row-list">
                {assessments.map((assessment) => (
                  <div className="decision-row" key={assessment.id}>
                    <strong>{assessment.id}</strong>
                    <span>
                      Decision {assessment.decisionId} | {formatDate(assessment.assessedAt)}
                    </span>
                    <p>
                      {assessment.rating} | {assessment.signals.length} signal(s),{' '}
                      {assessment.humanAuthoringBurdenSignals.length} burden signal(s)
                    </p>
                  </div>
                ))}
              </div>
            ) : (
              <EmptyState className="empty-state">No saved quality assessments yet.</EmptyState>
            )}
          </div>

          <div className="decision-inspection-list" aria-label="Saved quality report history">
            <h6>Saved Reports</h6>
            {reports.length > 0 ? (
              <div className="decision-row-list">
                {reports.map((report) => (
                  <div className="decision-row" key={report.id}>
                    <strong>{report.id}</strong>
                    <span>{formatDate(report.generatedAt)}</span>
                    <p>
                      {report.rating} | {report.acceptedCount} accepted, {report.fullRewriteCount} full rewrite
                    </p>
                  </div>
                ))}
              </div>
            ) : (
              <EmptyState className="empty-state">No saved quality reports yet.</EmptyState>
            )}
          </div>

          <div className="decision-inspection-list" aria-label="Saved quality trend history">
            <h6>Saved Trends</h6>
            {trends.length > 0 ? (
              <div className="decision-row-list">
                {trends.map((trend) => (
                  <div className="decision-row" key={trend.id}>
                    <strong>{trend.id}</strong>
                    <span>{formatDate(trend.generatedAt)}</span>
                    <p>
                      {trend.direction} | {trend.previousRating} to {trend.currentRating}
                    </p>
                  </div>
                ))}
              </div>
            ) : (
              <EmptyState className="empty-state">No saved quality trends yet.</EmptyState>
            )}
          </div>
        </>
      ) : (
        <EmptyState className="empty-state">
          {isLoading ? 'Loading quality...' : 'No quality report is available.'}
        </EmptyState>
      )}
    </section>
  )
}

function QualitySignalCard({ signal }: { signal: DecisionQualitySignal }) {
  return (
    <article className="decision-quality-signal">
      <div>
        <span>
          {signal.category} / {signal.direction} / {signal.severity}
        </span>
        <strong>{signal.summary}</strong>
      </div>
      <p>{signal.detail}</p>
      {signal.sources.length > 0 ? (
        <ul className="decision-source-list">
          {signal.sources.map((source, index) => (
            <li key={`${signal.id}-${source.sourceKind}-${source.relativePath ?? 'none'}-${index}`}>
              <strong>{source.sourceKind}</strong>
              {source.relativePath ? <span>{source.relativePath}</span> : null}
              {source.section ? <span>{source.section}</span> : null}
              {source.excerpt ? <p>{source.excerpt}</p> : null}
            </li>
          ))}
        </ul>
      ) : null}
    </article>
  )
}

function collectPrioritizedSignals(assessments: DecisionQualityAssessment[]) {
  const signals = assessments.flatMap((assessment) => assessment.signals)
  const byCategory = new Map(signals.map((signal) => [signal.category, signal]))

  return priorityCategories.flatMap((category) => {
    const signal = byCategory.get(category)
    return signal ? [signal] : []
  })
}

function formatDate(value: string) {
  return new Date(value).toLocaleString()
}
