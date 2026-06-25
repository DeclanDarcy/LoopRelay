# Decisions

## Newly Authorized

- Treat the completed Milestone 9 audit slice as the correct planning-only start for the milestone.
- Begin Milestone 9 implementation with a centralized navigation registry.
- Make the navigation registry the single source of truth for:
  - workspace tabs,
  - section identifiers,
  - navigation labels,
  - icons,
  - routing metadata,
  - primary vs. contextual destinations,
  - deep-link anchors.
- Encode destination classifications in the registry:
  - primary destination,
  - contextual link,
  - deprecated entry point,
  - hidden/internal section.
- Preserve the Milestone 1-8 semantic authority boundaries while consolidating Milestone 9 presentation and navigation.
- Navigation characterization tests should verify user-facing invariants rather than implementation details.
- Navigation characterization tests should cover:
  - every major capability has exactly one primary navigation path,
  - approved contextual links remain functional,
  - disabled navigation entries are implemented or removed,
  - no capability becomes unreachable,
  - duplicate primary entry points are not introduced.
- Use this Milestone 9 sequence:
  - navigation registry and reachability,
  - information density and workspace layout,
  - interaction normalization,
  - endpoint/projection/component retirement after replacements are verified,
  - final cohesion audit.
