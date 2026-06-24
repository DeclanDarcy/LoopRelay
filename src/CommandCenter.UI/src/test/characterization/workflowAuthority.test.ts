/// <reference types="node" />

import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { describe, expect, it } from 'vitest'

const sourceRoot = join(process.cwd(), 'src')

function sourceFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((entry) => {
    const path = join(directory, entry)
    if (statSync(path).isDirectory()) {
      return sourceFiles(path)
    }

    return /\.(ts|tsx)$/.test(path) ? [path] : []
  })
}

describe('workflow authority regression', () => {
  it('keeps UI workflow rendering off RepositoryExecutionState-derived lifecycle steps', () => {
    const obsoleteHelperPath = join(sourceRoot, 'lib', 'executionWorkflow.ts')
    const offenders = sourceFiles(sourceRoot)
      .filter((path) => !path.endsWith(join('test', 'characterization', 'workflowAuthority.test.ts')))
      .filter((path) => {
        const content = readFileSync(path, 'utf8')
        return content.includes('getExecutionWorkflowSteps') || content.includes('workflowSteps')
      })
      .map((path) => relative(sourceRoot, path))

    expect(existsSync(obsoleteHelperPath)).toBe(false)
    expect(offenders).toEqual([])
  })
})
