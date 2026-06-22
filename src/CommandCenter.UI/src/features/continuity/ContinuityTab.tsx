import { EmptyState, Panel, SectionHeader } from '../../components/design'
import type { ContinuityDiagnostics, ContinuityReport } from '../../types'
import { ContinuityDiagnosticsPanel } from './ContinuityDiagnosticsPanel'

type ContinuityTabProps = {
  diagnostics: ContinuityDiagnostics | null
  reports: ContinuityReport[]
  hasSelectedRepository: boolean
  isDiagnosticsLoading: boolean
  isReportGenerating: boolean
  onRefreshDiagnostics: () => void
  onGenerateReport: () => void
  onOpenOperationalContextSection: (sectionId: string) => void
  onOpenReport: (relativePath: string) => void
}

export function ContinuityTab({
  diagnostics,
  reports,
  hasSelectedRepository,
  isDiagnosticsLoading,
  isReportGenerating,
  onRefreshDiagnostics,
  onGenerateReport,
  onOpenOperationalContextSection,
  onOpenReport,
}: ContinuityTabProps) {
  return (
    <Panel
      className="execution-context-panel tab-panel tab-continuity"
      id="continuity-diagnostics"
      aria-label="Continuity diagnostics"
    >
      <SectionHeader
        className="context-toolbar"
        eyebrow="Continuity"
        title="Diagnostics"
        headingLevel={4}
        actions={
          <div className="context-controls">
            <button
              type="button"
              className="secondary-action"
              onClick={onRefreshDiagnostics}
              disabled={!hasSelectedRepository || isDiagnosticsLoading}
            >
              {isDiagnosticsLoading ? 'Loading...' : 'Refresh Diagnostics'}
            </button>
            <button
              type="button"
              onClick={onGenerateReport}
              disabled={!hasSelectedRepository || isReportGenerating}
            >
              {isReportGenerating ? 'Generating...' : 'Generate Report'}
            </button>
          </div>
        }
      />

      {diagnostics ? (
        <ContinuityDiagnosticsPanel
          diagnostics={diagnostics}
          reports={reports}
          onOpenOperationalContextSection={onOpenOperationalContextSection}
          onOpenReport={onOpenReport}
        />
      ) : (
        <EmptyState className="empty-state">
          {isDiagnosticsLoading
            ? 'Loading continuity diagnostics...'
            : 'No continuity diagnostics loaded.'}
        </EmptyState>
      )}
    </Panel>
  )
}
