/// <reference types="node" />

import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

const uiRoot = process.cwd()
const repositoryRoot = join(uiRoot, '..', '..')
const architectureTestRoot = join(uiRoot, 'src', 'test', 'architecture')
const mechanismsDocumentPath = join(repositoryRoot, 'docs', 'architectural-mechanisms.md')

describe('frontend architecture regression framework', () => {
  it('keeps the frontend architecture regression area discoverable', () => {
    expect(existsSync(architectureTestRoot)).toBe(true)
    expect(readdirSync(architectureTestRoot).some((entry) => /\.test\.tsx?$/.test(entry))).toBe(true)
  })

  it('records frontend regression ownership before enforcing broad UI architecture rules', () => {
    const mechanisms = readFileSync(mechanismsDocumentPath, 'utf8')

    expect(mechanisms).toContain('Frontend architecture tests')
    expect(mechanisms).toContain('TypeScript clients and React consume authoritative facts without semantic inference.')
    expect(mechanisms).toContain('Every mutable state has one owner.')
    expect(mechanisms).toContain('Feature controllers own resources, actions, refresh, loading, errors, and view-model construction.')
    expect(mechanisms).toContain('Workspaces compose controllers and local interaction flow only.')
    expect(mechanisms).toContain('Application root composes repository selection, global shell state, primary navigation, and workspaces only.')
  })
})
