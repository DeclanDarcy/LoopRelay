import { Button } from '../../components/design'

type SpecListProps = {
  specs: string[]
  disabled: boolean
  onChangeSpec: (index: number, value: string) => void
  onAddSpec: () => void
  onRemoveSpec: (index: number) => void
}

export function SpecList({
  specs,
  disabled,
  onChangeSpec,
  onAddSpec,
  onRemoveSpec,
}: SpecListProps) {
  return (
    <div className="cc-plan-field" aria-label="Specifications">
      <div className="cc-plan-field-row">
        <div>
          <span className="cc-plan-field-label" id="cc-plan-specs-label">
            Specifications
          </span>
          <p className="cc-plan-field-hint">
            Optional supporting detail the plan should account for.
          </p>
        </div>
        <Button
          type="button"
          variant="secondary"
          className="cc-plan-add-spec"
          onClick={onAddSpec}
          disabled={disabled}
        >
          Add Spec
        </Button>
      </div>

      {specs.length === 0 ? (
        <p className="cc-plan-specs-empty">No specifications yet.</p>
      ) : (
        <ul className="cc-plan-specs" aria-labelledby="cc-plan-specs-label">
          {specs.map((spec, index) => (
            <li className="cc-plan-spec" key={index}>
              <textarea
                className="cc-plan-textarea cc-plan-spec-input"
                aria-label={`Specification ${index + 1}`}
                placeholder="Constrain or clarify an area of the plan…"
                rows={3}
                value={spec}
                disabled={disabled}
                onChange={(event) => onChangeSpec(index, event.target.value)}
              />
              <button
                type="button"
                className="cc-plan-spec-remove"
                aria-label={`Remove specification ${index + 1}`}
                onClick={() => onRemoveSpec(index)}
                disabled={disabled}
              >
                Remove
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
