type ExecutionContextMissingOptionalListProps = {
  paths: string[]
}

export function ExecutionContextMissingOptionalList({ paths }: ExecutionContextMissingOptionalListProps) {
  if (paths.length === 0) {
    return <p>None</p>
  }

  return (
    <ul>
      {paths.map((path) => (
        <li key={path}>{path}</li>
      ))}
    </ul>
  )
}
