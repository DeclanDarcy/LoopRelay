# Milestone 9 Navigation Registry Slice

## Implemented

- Promoted `src/CommandCenter.UI/src/lib/navigation.ts` into the shared registry for:
  - workspace tab ids and labels,
  - global sidebar navigation entries,
  - static section ids and owning tabs,
  - destination classification as `primary`, `contextual`, `deprecated`, or `hidden`.
- Added `classification` to `NavigationTarget` so palette and discovery targets carry the same registry semantics as static destinations.
- Updated `WorkspaceTabs` to render from `workspaceTabDefinitions`.
- Updated `Sidebar` to render only implemented global navigation entries from `globalNavigationItems`, removing the disabled `Overview`, `Executions`, and `Insights` buttons.

## Characterization

- `navigation.test.ts` now verifies:
  - one primary workspace destination exists for each major capability,
  - section anchors are contextual and unique,
  - implemented global navigation entries are the only registered global entries.
- `sidebarNavigation.test.tsx` verifies the rendered sidebar has no disabled global navigation entries.
- `commandPalette.test.tsx` fixtures now include target classification.

## Verification

- `npm test -- navigation.test.ts sidebarNavigation.test.tsx commandPalette.test.tsx`
- `npm run build`

## Deferred

- Execution history/live activity duplication is still present.
- Broader UI reachability tests should expand from registry-level guarantees to rendered app flows.
