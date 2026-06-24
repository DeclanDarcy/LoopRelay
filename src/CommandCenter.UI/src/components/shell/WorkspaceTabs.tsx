import { TabButton, Tabs } from '../design'
import type { PrimaryWorkspaceTab } from '../../state/shellState'

type WorkspaceTabsProps = {
  activeTab: PrimaryWorkspaceTab
  onSelectTab: (tab: PrimaryWorkspaceTab) => void
}

const workspaceTabs: { id: PrimaryWorkspaceTab; label: string }[] = [
  { id: 'workspace', label: 'Workspace' },
  { id: 'execution', label: 'Execution' },
  { id: 'operational-context', label: 'Operational Context' },
  { id: 'governance', label: 'Governance' },
  { id: 'decisions', label: 'Decisions' },
  { id: 'reasoning', label: 'Reasoning' },
  { id: 'continuity', label: 'Continuity' },
]

export function WorkspaceTabs({ activeTab, onSelectTab }: WorkspaceTabsProps) {
  return (
    <Tabs className="workspace-tabs">
      {workspaceTabs.map((tab) => (
        <TabButton
          type="button"
          key={tab.id}
          active={tab.id === activeTab}
          onClick={() => onSelectTab(tab.id)}
        >
          {tab.label}
        </TabButton>
      ))}
    </Tabs>
  )
}
