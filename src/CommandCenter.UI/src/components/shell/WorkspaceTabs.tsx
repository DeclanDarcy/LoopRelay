import { TabButton, Tabs } from '../design'
import { workspaceTabDefinitions } from '../../lib'
import type { PrimaryWorkspaceTab } from '../../state/shellState'

type WorkspaceTabsProps = {
  activeTab: PrimaryWorkspaceTab
  onSelectTab: (tab: PrimaryWorkspaceTab) => void
}

export function WorkspaceTabs({ activeTab, onSelectTab }: WorkspaceTabsProps) {
  return (
    <Tabs className="workspace-tabs">
      {workspaceTabDefinitions.map((tab) => (
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
