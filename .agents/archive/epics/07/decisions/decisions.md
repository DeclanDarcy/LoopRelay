# Decisions

## Newly Authorized

- Treat Milestone 10 as complete.
- Treat the Tier 0 real-repository validation gate as the next required proof step.
- Do not create an `M11` or additional roadmap milestone before Tier 0 validation.
- Execute Tier 0 against an actual repository using the completed M1-M10 workflow.
- Treat Tier 0 as the test of whether the workflow-replacement hypothesis holds on real repository artifacts.
- Measure Tier 0 workflow replacement by:
  - `ReviewOnly`
  - `MinorEdit`
  - `MajorRefinement`
  - `FullRewrite`
  - `GenerationBypassed`
- Measure Tier 0 recommendation behavior by:
  - accepted recommendation
  - alternative selected
  - recommendation withheld
  - recommendation rejected
- Measure Tier 0 execution consumption by:
  - projected decision guidance
  - execution influence recorded
  - influence trace retrieved
- Measure Tier 0 certification by:
  - generation certification result
  - workflow replacement certification result
  - explanatory failure reasons when certification does not pass
- Preserve the decision that executive replacement readiness remains evidence-driven and explainable, not score-driven.

## Not Authorized

- Do not continue feature execution in this slice after staging, committing, and pushing.
- Do not add new roadmap construction before Tier 0 validation.
- Do not reduce replacement readiness to an opaque numeric score.
