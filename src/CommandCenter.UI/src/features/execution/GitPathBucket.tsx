type GitPathBucketProps = {
  label: string
  paths: string[]
}

export function GitPathBucket({ label, paths }: GitPathBucketProps) {
  return (
    <div>
      <h5>{label}</h5>
      {paths.length === 0 ? (
        <p>None</p>
      ) : (
        <ul>
          {paths.map((path) => (
            <li key={path}>{path}</li>
          ))}
        </ul>
      )}
    </div>
  )
}
