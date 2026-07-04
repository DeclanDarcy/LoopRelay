type EpicFieldProps = {
  value: string
  disabled: boolean
  onChange: (value: string) => void
}

export function EpicField({ value, disabled, onChange }: EpicFieldProps) {
  return (
    <div className="cc-plan-field">
      <label className="cc-plan-field-label" htmlFor="cc-plan-epic">
        Epic
      </label>
      <p className="cc-plan-field-hint">
        Describe where the codebase is headed. Required to write a plan.
      </p>
      <textarea
        id="cc-plan-epic"
        className="cc-plan-textarea cc-plan-epic"
        aria-label="Epic"
        placeholder="The product should ship a repository dashboard, then a planning workflow…"
        rows={6}
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
      />
    </div>
  )
}
