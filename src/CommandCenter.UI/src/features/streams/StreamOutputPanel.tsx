import './StreamPrimitives.css'

// The live streamed-output panel shared by the plan, execution, and decision surfaces: an
// eyebrow, a live phase pill (or a token summary once the turn is done), and the accumulating
// output in a polite live region with a blinking caret. Presentational only — every source
// derives its own phase label and token data and feeds them in.
type StreamTokens = {
  promptTokens: number
  outputTokens: number
}

type StreamOutputPanelProps = {
  ariaLabel: string
  eyebrow?: string
  streamedText: string
  live: boolean
  // The active-phase label, shown in the pill while live. Falsy renders no pill.
  phaseLabel?: string | null
  // The completed-turn token summary, shown in the head slot once the run is no longer live.
  tokens?: StreamTokens | null
}

export function StreamOutputPanel({
  ariaLabel,
  eyebrow = 'Output',
  streamedText,
  live,
  phaseLabel = null,
  tokens = null,
}: StreamOutputPanelProps) {
  return (
    <section
      className={`cc-stream-panel${live ? ' cc-stream-panel-live' : ''}`}
      aria-label={ariaLabel}
    >
      <header className="cc-stream-panel-head">
        <span className="cc-stream-eyebrow">{eyebrow}</span>
        {live && phaseLabel ? (
          <span className="cc-stream-phase-pill" aria-live="polite">
            <span className="cc-stream-phase-dot" aria-hidden="true" />
            {phaseLabel}…
          </span>
        ) : !live && tokens ? (
          <span className="cc-stream-tokens">
            {tokens.promptTokens.toLocaleString()} in · {tokens.outputTokens.toLocaleString()} out
          </span>
        ) : null}
      </header>
      <pre className="cc-stream-output" aria-live="polite" aria-atomic="false">
        {streamedText}
        {live ? <span className="cc-stream-caret" aria-hidden="true" /> : null}
      </pre>
    </section>
  )
}
