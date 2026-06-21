import type { ReactNode } from 'react'

type AppShellProps = {
  sidebar: ReactNode
  header: ReactNode
  tabs: ReactNode
  palette: ReactNode
  children: ReactNode
}

export function AppShell({ sidebar, header, tabs, palette, children }: AppShellProps) {
  return (
    <div className="app-shell">
      {sidebar}
      <div className="app-main">
        {header}
        {tabs}
        <main className="app-content">{children}</main>
      </div>
      {palette}
    </div>
  )
}
