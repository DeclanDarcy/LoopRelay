import type { HTMLAttributes, ReactNode } from 'react'

type SectionHeaderProps = HTMLAttributes<HTMLDivElement> & {
  eyebrow?: string
  title: string
  actions?: ReactNode
}

export function SectionHeader({ eyebrow, title, actions, className, ...props }: SectionHeaderProps) {
  return (
    <div className={['cc-section-header', className].filter(Boolean).join(' ')} {...props}>
      <div>
        {eyebrow ? <p>{eyebrow}</p> : null}
        <h3>{title}</h3>
      </div>
      {actions}
    </div>
  )
}
