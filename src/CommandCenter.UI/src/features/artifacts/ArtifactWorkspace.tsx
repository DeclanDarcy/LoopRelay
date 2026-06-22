import { EmptyState, Panel, SectionHeader } from '../../components/design'
import { getArtifactCategories } from '../../lib'
import type { Artifact, ArtifactInventory } from '../../types'
import { ArtifactMarkdownPreview } from './ArtifactMarkdownPreview'
import { ArtifactMetadata } from './ArtifactMetadata'

type ArtifactWorkspaceProps = {
  inventory: ArtifactInventory | null
  selectedArtifact: Artifact | null
  selectedArtifactPath: string | null
  draftContent: string
  canRotateSelectedArtifact: boolean
  hasDraftChanges: boolean
  isArtifactLoading: boolean
  isRotating: boolean
  isSaving: boolean
  onSelectArtifact: (relativePath: string) => void
  onDraftContentChange: (content: string) => void
  onRotateSelectedArtifact: () => void
  onSaveArtifact: () => void
}

export function ArtifactWorkspace({
  inventory,
  selectedArtifact,
  selectedArtifactPath,
  draftContent,
  canRotateSelectedArtifact,
  hasDraftChanges,
  isArtifactLoading,
  isRotating,
  isSaving,
  onSelectArtifact,
  onDraftContentChange,
  onRotateSelectedArtifact,
  onSaveArtifact,
}: ArtifactWorkspaceProps) {
  if (!inventory) {
    return <EmptyState className="empty-state">Loading workspace...</EmptyState>
  }

  return (
    <Panel
      id="artifact-workspace"
      className="artifact-workspace-shell"
      aria-label="Artifact workspace"
    >
      <SectionHeader
        eyebrow="Repository Artifacts"
        title="Explorer and Editor"
        headingLevel={4}
      />
      <div className="artifact-workspace">
        <section className="artifact-explorer" aria-label="Artifact explorer">
          {getArtifactCategories(inventory).map((category) => (
            <div className="artifact-category" key={category.label}>
              <h4>{category.label}</h4>
              {category.artifacts.length === 0 ? (
                <p className="missing-artifact">{category.missingLabel}</p>
              ) : (
                <div className="artifact-list">
                  {category.artifacts.map((artifact) => (
                    <button
                      type="button"
                      key={artifact.relativePath}
                      className={`artifact-item${
                        artifact.relativePath === selectedArtifactPath ? ' selected' : ''
                      }`}
                      onClick={() => onSelectArtifact(artifact.relativePath)}
                    >
                      <span>{artifact.name}</span>
                      <span>{artifact.versionKind}</span>
                    </button>
                  ))}
                </div>
              )}
            </div>
          ))}
        </section>

        <section className="artifact-panel" aria-label="Artifact content">
          {selectedArtifact ? (
            <>
              <div className="artifact-panel-header">
                <ArtifactMetadata artifact={selectedArtifact} />
                <div className="artifact-panel-actions">
                  {canRotateSelectedArtifact ? (
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={onRotateSelectedArtifact}
                      disabled={isRotating || isArtifactLoading || isSaving || hasDraftChanges}
                      title={
                        hasDraftChanges
                          ? 'Save changes before rotating.'
                          : 'Archive the current artifact to the next historical file.'
                      }
                    >
                      {isRotating ? 'Rotating...' : 'Rotate'}
                    </button>
                  ) : null}
                  <button
                    type="button"
                    className="primary-action"
                    onClick={onSaveArtifact}
                    disabled={isSaving || isArtifactLoading || !hasDraftChanges}
                  >
                    {isSaving ? 'Saving...' : 'Save'}
                  </button>
                </div>
              </div>
              <textarea
                className="artifact-editor"
                value={draftContent}
                onChange={(event) => onDraftContentChange(event.target.value)}
                spellCheck={false}
                disabled={isArtifactLoading}
              />
              <ArtifactMarkdownPreview content={draftContent} isLoading={isArtifactLoading} />
            </>
          ) : (
            <EmptyState className="empty-state">No artifact selected.</EmptyState>
          )}
        </section>
      </div>
    </Panel>
  )
}
