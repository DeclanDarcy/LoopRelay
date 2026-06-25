# Decisions

## Newly Authorized

- Treat the navigation registry slice as the correct first implementation slice for Milestone 9.
- Keep `NavigationTarget.classification` as the concrete policy mechanism for primary versus contextual navigation destinations.
- Continue Milestone 9 with execution history/live activity consolidation as the next highest-leverage slice.
- For the execution consolidation slice:
  - identify the single primary execution workspace,
  - classify every other execution surface as contextual summary, contextual link, compatibility surface, or retire candidate,
  - move duplicated execution presentation into the primary workspace while leaving lightweight summaries elsewhere,
  - ensure contextual links navigate into the primary execution view instead of reproducing the same information.
- Continue applying the navigation-registry pattern across Milestone 9 cohesion work:
  - one registry,
  - one primary presentation,
  - contextual references instead of duplicated implementations,
  - characterization tests protecting reachability.
