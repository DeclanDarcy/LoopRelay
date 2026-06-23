# Decisions

## Newly Authorized

- Treat the successful `Generic Reconstruction -> Project-Level UI Consumption` slice as evidence that generic reconstruction is usable at project scale.
- Reframe the rest of M7 as answerability and usability certification rather than infrastructure expansion.
- Do not immediately implement decision evolution reconstruction, direction reconstruction, or other specialized reconstruction behavior.
- Do not add specialized reconstruction services, category-specific narrative engines, specialized read models, specialized caches, or first-class decision, direction, hypothesis, alternative, or contradiction entities without a demonstrated failure.
- Make the next M7 slice `M7 Answerability Certification`.
- Certify whether the current `Graph -> Trace -> Reconstruction -> Grouped Presentation` pipeline can answer the target reasoning-trajectory questions:
  - why one decision replaced another,
  - what alternative was rejected,
  - what assumption failed,
  - what contradiction changed direction,
  - how the current strategy emerged.
- If those answerability checks pass, record that specialized reconstruction is not justified for M7.
- If an answerability check fails, identify the exact failure and first improve filtering, ordering, grouping, or evidence highlighting before considering additional reconstruction behavior.
- Continue treating derived reasoning infrastructure as sufficient unless a reconstruction, answerability, persistence, recovery, or usability failure demonstrates otherwise.
