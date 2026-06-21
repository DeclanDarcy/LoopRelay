import type { HTMLAttributes, ReactNode } from 'react'

type PanelProps = HTMLAttributes<HTMLElement> & {
  children: ReactNode
}

export function Panel({ className, children, ...props }: PanelProps) {
  return (
    <section className={['cc-panel', className].filter(Boolean).join(' ')} {...props}>
      {children}
    </section>
  )
}
