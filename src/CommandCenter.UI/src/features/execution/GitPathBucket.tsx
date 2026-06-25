import type { CommitChangeOrigin } from '../../types'

export type GitPathBucketItem = {
  path: string
  origin?: CommitChangeOrigin
  originBasis?: string
}

type GitPathBucketProps = {
  label: string
  paths?: string[]
  items?: GitPathBucketItem[]
}

export function GitPathBucket({ label, paths = [], items }: GitPathBucketProps) {
  const bucketItems: GitPathBucketItem[] = items ?? paths.map((path) => ({ path }))

  return (
    <div>
      <h5>{label}</h5>
      {bucketItems.length === 0 ? (
        <p>None</p>
      ) : (
        <ul>
          {bucketItems.map((item) => (
            <li key={item.path}>
              <span>{item.path}</span>
              {item.origin ? (
                <small>
                  {item.origin === 'PreExisting' ? 'Pre-existing' : 'Execution generated'}
                </small>
              ) : null}
              {item.originBasis ? <small>{item.originBasis}</small> : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
