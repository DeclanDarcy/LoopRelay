import type { ButtonHTMLAttributes, ReactNode } from 'react'

type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'ghost'

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant
  children: ReactNode
}

export function Button({ variant = 'secondary', className, children, ...props }: ButtonProps) {
  return (
    <button className={['cc-button', `cc-button-${variant}`, className].filter(Boolean).join(' ')} {...props}>
      {children}
    </button>
  )
}
