import type { HTMLAttributes, ReactNode } from 'react'

type InspectorSectionProps = HTMLAttributes<HTMLElement> & {
  title: string
  actions?: ReactNode
  children: ReactNode
}

export function InspectorSection({ title, actions, children, className, ...props }: InspectorSectionProps) {
  return (
    <section className={['cc-inspector-section', className].filter(Boolean).join(' ')} {...props}>
      <div className="cc-inspector-section-header">
        <h4>{title}</h4>
        {actions}
      </div>
      {children}
    </section>
  )
}
