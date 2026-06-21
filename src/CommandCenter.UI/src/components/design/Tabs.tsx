import type { ButtonHTMLAttributes, ReactNode } from 'react'

type TabButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  active?: boolean
  children: ReactNode
}

export function Tabs({ className, children }: { className?: string; children: ReactNode }) {
  return <div className={['cc-tabs', className].filter(Boolean).join(' ')}>{children}</div>
}

export function TabButton({ active = false, className, children, ...props }: TabButtonProps) {
  return (
    <button
      className={['cc-tab', active ? 'cc-tab-active' : '', className].filter(Boolean).join(' ')}
      aria-pressed={active}
      {...props}
    >
      {children}
    </button>
  )
}
