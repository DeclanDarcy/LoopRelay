import { useEffect, useState } from 'react'
import type { PlanCompletionTokens } from '../../types'

type RenderedPlanViewProps = {
  plan: string
  tokens: PlanCompletionTokens | null
}

export function RenderedPlanView({ plan, tokens }: RenderedPlanViewProps) {
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    if (!copied) {
      return
    }

    const timeoutId = window.setTimeout(() => setCopied(false), 1600)
    return () => window.clearTimeout(timeoutId)
  }, [copied])

  const copyPlan = async () => {
    try {
      await navigator.clipboard.writeText(plan)
      setCopied(true)
    } catch {
      setCopied(false)
    }
  }

  return (
    <section className="cc-plan-document" aria-label="Rendered plan">
      <header className="cc-plan-document-head">
        <span className="cc-plan-eyebrow">Plan</span>
        <div className="cc-plan-document-meta">
          {tokens ? (
            <span className="cc-plan-tokens">
              {tokens.promptTokens.toLocaleString()} in · {tokens.outputTokens.toLocaleString()} out
            </span>
          ) : null}
          <button
            type="button"
            className="cc-plan-copy"
            aria-label="Copy plan"
            onClick={() => void copyPlan()}
          >
            <CopyGlyph />
            <span className="cc-plan-copy-status" aria-live="polite">
              {copied ? 'Copied' : ''}
            </span>
          </button>
        </div>
      </header>
      <pre className="cc-plan-rendered">{plan}</pre>
    </section>
  )
}

function CopyGlyph() {
  return (
    <svg
      className="cc-plan-copy-icon"
      width="15"
      height="15"
      viewBox="0 0 15 15"
      fill="none"
      aria-hidden="true"
    >
      <rect x="4.5" y="4.5" width="8" height="9" rx="1.5" stroke="currentColor" strokeWidth="1.2" />
      <path
        d="M10.5 4.5V3a1.5 1.5 0 0 0-1.5-1.5H3A1.5 1.5 0 0 0 1.5 3v6A1.5 1.5 0 0 0 3 10.5h1.5"
        stroke="currentColor"
        strokeWidth="1.2"
        strokeLinecap="round"
      />
    </svg>
  )
}
