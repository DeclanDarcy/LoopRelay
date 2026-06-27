type RoadmapFieldProps = {
  value: string
  disabled: boolean
  onChange: (value: string) => void
}

export function RoadmapField({ value, disabled, onChange }: RoadmapFieldProps) {
  return (
    <div className="cc-plan-field">
      <label className="cc-plan-field-label" htmlFor="cc-plan-roadmap">
        Roadmap
      </label>
      <p className="cc-plan-field-hint">
        Describe where the codebase is headed. Required to write a plan.
      </p>
      <textarea
        id="cc-plan-roadmap"
        className="cc-plan-textarea cc-plan-roadmap"
        aria-label="Roadmap"
        placeholder="The product should ship a repository dashboard, then a planning workflow…"
        rows={6}
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
      />
    </div>
  )
}
