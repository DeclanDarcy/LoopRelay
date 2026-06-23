import { useMemo } from 'react'
import { EmptyState } from '../../components/design'
import type {
  DecisionGenerationCertificationFinding,
  DecisionGenerationCertificationReport,
  DecisionQualityAssessment,
  DecisionSourceReference,
  HumanAuthoringBurdenReport,
} from '../../types'

type DecisionGenerationCertificationPanelProps = {
  currentReport: DecisionGenerationCertificationReport | null
  reports: DecisionGenerationCertificationReport[]
  isLoading: boolean
  isRunning: boolean
  error: string | null
  onRunCertification: () => void
}

export function DecisionGenerationCertificationPanel({
  currentReport,
  reports,
  isLoading,
  isRunning,
  error,
  onRunCertification,
}: DecisionGenerationCertificationPanelProps) {
  const failedFindings = useMemo(
    () => currentReport?.result.findings.filter((finding) => !finding.passed) ?? [],
    [currentReport?.result.findings],
  )
  const passedFindings = useMemo(
    () => currentReport?.result.findings.filter((finding) => finding.passed) ?? [],
    [currentReport?.result.findings],
  )

  return (
    <section
      className="decision-lifecycle-panel decision-generation-certification-panel"
      aria-label="Decision generation certification"
    >
      <div className="decision-panel-heading">
        <div>
          <h5>Generation Certification</h5>
          <span>Advisory workflow replacement evidence</span>
        </div>
        <button
          type="button"
          className="secondary-action"
          onClick={onRunCertification}
          disabled={!currentReport || isLoading || isRunning}
        >
          {isRunning ? 'Running...' : 'Run Generation Certification'}
        </button>
      </div>

      {error ? <p className="notice error">{error}</p> : null}

      {currentReport ? (
        <>
          <div className="decision-lineage-summary" aria-label="Generation certification status">
            <span>Certified: {currentReport.result.certified ? 'Yes' : 'No'}</span>
            <span>{currentReport.generatedPackageCount} packages</span>
            <span>{currentReport.generatedResolvedDecisionCount} generated resolutions</span>
            <span>{currentReport.executionInfluenceTraceCount} influence traces</span>
          </div>

          <div className="decision-generation-certification-grid" aria-label="Generation certification categories">
            <StatusChip label="Generation" passed={currentReport.result.generationCertified} />
            <StatusChip label="Governance" passed={currentReport.result.governanceCertified} />
            <StatusChip label="Throughput" passed={currentReport.result.throughputCertified} />
            <StatusChip label="Quality" passed={currentReport.result.qualityCertified} />
            <StatusChip label="Consumption" passed={currentReport.result.consumptionCertified} />
            <StatusChip label="Workflow Replacement" passed={currentReport.result.workflowReplacementCertified} />
          </div>

          <div className="decision-governance-meta" aria-label="Generation certification report metadata">
            <span>Current inspection: {currentReport.id}</span>
            <span>Generated: {formatDate(currentReport.generatedAt)}</span>
            <span>Fingerprint: {currentReport.inputFingerprint}</span>
          </div>

          {currentReport.result.failures.length > 0 ? (
            <div className="decision-warning-list" aria-label="Generation certification failures">
              {currentReport.result.failures.map((failure) => (
                <span key={failure}>{failure}</span>
              ))}
            </div>
          ) : (
            <div className="decision-success-list">
              <span>No generation certification failures were reported.</span>
            </div>
          )}

          {currentReport.diagnostics.length > 0 ? (
            <div className="decision-warning-list" aria-label="Generation certification diagnostics">
              {currentReport.diagnostics.map((diagnostic) => (
                <span key={diagnostic}>{diagnostic}</span>
              ))}
            </div>
          ) : null}

          <ExecutiveReadinessSummary report={currentReport} />
          <HumanAuthoringBurdenSummary report={currentReport.humanAuthoringBurden} />

          <div className="decision-inspection-list" aria-label="Generation certification findings">
            <h6>Findings</h6>
            {failedFindings.length > 0 ? (
              <div className="decision-certification-group">
                <h6>Failed</h6>
                {failedFindings.map((finding) => (
                  <GenerationCertificationFindingCard finding={finding} key={finding.id} />
                ))}
              </div>
            ) : null}
            {passedFindings.length > 0 ? (
              <div className="decision-certification-group">
                <h6>Passed</h6>
                {passedFindings.map((finding) => (
                  <GenerationCertificationFindingCard finding={finding} key={finding.id} />
                ))}
              </div>
            ) : (
              <EmptyState className="empty-state">No generation certification findings are available.</EmptyState>
            )}
          </div>

          <QualityEvidenceSummary assessments={currentReport.qualityAssessments} />

          <div className="decision-inspection-list" aria-label="Generated generation certification report history">
            <h6>Generated Reports</h6>
            {reports.length > 0 ? (
              <div className="decision-row-list">
                {reports.map((report) => (
                  <div className="decision-row" key={report.id}>
                    <strong>{report.id}</strong>
                    <span>{formatDate(report.generatedAt)}</span>
                    <p>
                      {report.result.certified ? 'Certified' : 'Not certified'} |{' '}
                      {report.result.findings.filter((finding) => finding.passed).length} passed,{' '}
                      {report.result.findings.filter((finding) => !finding.passed).length} failed
                    </p>
                  </div>
                ))}
              </div>
            ) : (
              <EmptyState className="empty-state">No generated generation certification reports yet.</EmptyState>
            )}
          </div>
        </>
      ) : (
        <EmptyState className="empty-state">
          {isLoading ? 'Loading generation certification...' : 'No generation certification report is available.'}
        </EmptyState>
      )}
    </section>
  )
}

function ExecutiveReadinessSummary({ report }: { report: DecisionGenerationCertificationReport }) {
  return (
    <div className="decision-inspection-list" aria-label="Executive replacement readiness report">
      <h6>Replacement Readiness</h6>
      <div className="decision-row-list">
        <div className="decision-row">
          <strong>{report.executiveReport.replacementReady ? 'Ready' : 'Not Ready'}</strong>
          <span>{report.executiveReport.answer}</span>
          <p>{report.executiveReport.summary}</p>
        </div>
      </div>
      <div className="decision-generation-certification-grid">
        <span>{report.repositoryReport.automaticallyDiscoveredCandidateCount} automatic candidates</span>
        <span>{report.workflowReport.humanResolvedGeneratedDecisionCount} human resolutions</span>
        <span>{formatRate(report.humanAuthoringBurdenSummary.reviewOnlyRate)} review only</span>
        <span>{formatRate(report.humanAuthoringBurdenSummary.generationBypassedRate)} bypassed</span>
        <span>{formatRate(report.workflowReport.executionInfluenceCoverageRate)} influence coverage</span>
        <span>{formatRate(report.workflowReport.recommendationDivergenceRate)} recommendation divergence</span>
      </div>
      {report.executiveReport.evidence.length > 0 ? (
        <div className="decision-warning-list" aria-label="Executive replacement readiness evidence">
          {report.executiveReport.evidence.map((item) => (
            <span key={item}>{item}</span>
          ))}
        </div>
      ) : null}
      {report.executiveReport.blockingGaps.length > 0 ? (
        <div className="decision-warning-list" aria-label="Executive replacement readiness blocking gaps">
          {report.executiveReport.blockingGaps.map((gap) => (
            <span key={gap}>{gap}</span>
          ))}
        </div>
      ) : null}
    </div>
  )
}

function StatusChip({ label, passed }: { label: string; passed: boolean }) {
  return (
    <span data-status={passed ? 'passed' : 'failed'}>
      {label}: {passed ? 'Passed' : 'Failed'}
    </span>
  )
}

function HumanAuthoringBurdenSummary({ report }: { report: HumanAuthoringBurdenReport }) {
  return (
    <div className="decision-inspection-list" aria-label="Human authoring burden certification evidence">
      <h6>Human Authoring Burden</h6>
      <div className="decision-generation-certification-grid">
        <span>{report.decisionCount} decisions</span>
        <span>{report.reviewOnlyCount} review only</span>
        <span>{report.minorEditCount} minor edits</span>
        <span>{report.majorRefinementCount} major refinements</span>
        <span>{report.fullRewriteCount} full rewrites</span>
        <span>{report.generationBypassedCount} generation bypassed</span>
      </div>
      {report.signals.length > 0 ? (
        <div className="decision-row-list">
          {report.signals.map((signal) => (
            <div className="decision-row" key={signal.id}>
              <strong>{signal.burden}</strong>
              <span>{signal.decisionId}</span>
              <p>{signal.summary}</p>
            </div>
          ))}
        </div>
      ) : (
        <EmptyState className="empty-state">No human authoring burden signals are available.</EmptyState>
      )}
    </div>
  )
}

function QualityEvidenceSummary({ assessments }: { assessments: DecisionQualityAssessment[] }) {
  return (
    <div className="decision-inspection-list" aria-label="Generation certification quality evidence">
      <h6>Quality Evidence</h6>
      {assessments.length > 0 ? (
        <div className="decision-row-list">
          {assessments.map((assessment) => (
            <div className="decision-row" key={assessment.id}>
              <strong>{assessment.rating}</strong>
              <span>{assessment.decisionId}</span>
              <p>
                Score {assessment.score} | {assessment.signals.length} quality signals |{' '}
                {assessment.humanAuthoringBurdenSignals.length} burden signals
              </p>
            </div>
          ))}
        </div>
      ) : (
        <EmptyState className="empty-state">No quality assessments are attached to this report.</EmptyState>
      )}
    </div>
  )
}

function GenerationCertificationFindingCard({
  finding,
}: {
  finding: DecisionGenerationCertificationFinding
}) {
  const related = [
    ...finding.relatedDecisionIds.map((id) => `Decision ${id}`),
    ...finding.relatedCandidateIds.map((id) => `Candidate ${id}`),
    ...finding.relatedProposalIds.map((id) => `Proposal ${id}`),
  ]

  return (
    <article className="decision-certification-evidence">
      <div>
        <span>{finding.category}</span>
        <strong>{finding.summary}</strong>
      </div>
      <p>{finding.detail}</p>
      <div className="decision-badge-row">
        <span>{finding.passed ? 'Passed' : 'Failed'}</span>
        {related.map((item) => (
          <span key={item}>{item}</span>
        ))}
      </div>
      <SourceList sources={finding.sources} id={finding.id} />
    </article>
  )
}

function SourceList({ sources, id }: { sources: DecisionSourceReference[]; id: string }) {
  if (sources.length === 0) {
    return null
  }

  return (
    <ul className="decision-source-list">
      {sources.map((source, index) => (
        <li key={`${id}-${source.sourceKind}-${source.relativePath ?? 'none'}-${index}`}>
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
  return new Date(value).toLocaleString()
}

function formatRate(value: number) {
  return `${Math.round(value * 100)}%`
}
