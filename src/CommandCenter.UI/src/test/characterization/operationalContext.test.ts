import { describe, expect, it } from 'vitest'
import { getOperationalContextSectionItems } from '../../lib'

describe('operational context display parsing characterization', () => {
  it('preserves section item ordering, trimming, flattened nested bullets, and case-insensitive heading matching', () => {
    const content = `# Operational Context

## Stable Decisions

- First stable decision
  - Nested text is flattened
-   Second stable decision with extra spacing

Paragraph text is ignored.

## Open Questions

- Open decision: keep the review gate
- Open question: unrelated question

## Decision Rationale

- Rationale one
- Rationale two`

    expect(getOperationalContextSectionItems(content, 'stable decisions')).toEqual([
      'First stable decision',
      'Nested text is flattened',
      'Second stable decision with extra spacing',
    ])
    expect(getOperationalContextSectionItems(content, 'Open Questions')).toEqual([
      'Open decision: keep the review gate',
      'Open question: unrelated question',
    ])
    expect(getOperationalContextSectionItems(content, 'Decision Rationale')).toEqual([
      'Rationale one',
      'Rationale two',
    ])
  })

  it('omits non-list content, empty list items, and content after the next section', () => {
    const content = `## Stable Decisions

-
-    
- Kept decision
Plain text

## Other Section

- Excluded decision`

    expect(getOperationalContextSectionItems(content, 'Stable Decisions')).toEqual(['Kept decision'])
  })

  it('returns an empty list when a section is absent or only has deeper headings', () => {
    const content = `## Architecture

- Architecture item

### Stable Decisions

- Not included because the parser only uses h2 sections`

    expect(getOperationalContextSectionItems(content, 'Stable Decisions')).toEqual([])
    expect(getOperationalContextSectionItems('', 'Stable Decisions')).toEqual([])
  })
})
