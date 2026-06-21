type ExecutionContextValidationListProps = {
  validationErrors: string[]
}

export function ExecutionContextValidationList({
  validationErrors,
}: ExecutionContextValidationListProps) {
  if (validationErrors.length === 0) {
    return <p>No validation errors</p>
  }

  return (
    <ul>
      {validationErrors.map((validationError, index) => (
        <li key={`${index}:${validationError}`}>{validationError}</li>
      ))}
    </ul>
  )
}
