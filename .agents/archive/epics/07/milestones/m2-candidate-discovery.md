# Milestone 2: Decision Candidate Discovery Upgrade

## Goal

discover typed, evidence-backed decision candidates and suppress noise.

## Work

- [ ] Add `DecisionCandidateType`:
  - [ ] `ArchitecturalFork`
  - [ ] `StrategicDirection`
  - [ ] `TacticalChoice`
  - [ ] `OperationalBlocker`
  - [ ] `ConstraintConflict`
  - [ ] `Contradiction`
  - [ ] `Supersession`
  - [ ] `WorkflowContinuation`
- [ ] Add analyzer interface:

```csharp
public interface IDecisionCandidateAnalyzer
{
    Task<IReadOnlyList<DecisionCandidateSignal>> AnalyzeAsync(
        DecisionGenerationContext context,
        CancellationToken cancellationToken);
}
```

- [ ] Implement built-in analyzers:
  - [ ] handoff analyzer
  - [ ] milestone analyzer
  - [ ] operational-context analyzer
  - [ ] decision-history analyzer
  - [ ] repository-state analyzer
- [ ] Refactor current line-scanning signal extraction into analyzers returning raw signals.
- [ ] Add a discovery pipeline:

```text
Typed Context
        |
Source Analyzers
        |
Raw Candidate Signals
        |
Deduplication
        |
Priority Scoring
        |
Resolved-Decision Suppression
        |
Candidate Persistence
```

- [ ] Deduplicate by type, normalized title, source fingerprint, affected artifacts, and related decisions.
- [ ] Suppress candidates when an accepted resolved decision already governs the issue.
- [ ] Do not suppress when new evidence contradicts or invalidates an accepted resolved decision.
- [ ] Persist ignored/deferred-equivalent states using existing candidate lifecycle where possible; do not create a background expiration process.
- [ ] Add `AffectedArtifacts` and `RelatedDecisionIds` fields if existing source references are not sufficient for UI and suppression.

## Tests

- [ ] Handoff blocker creates a blocking `ArchitecturalFork` or `OperationalBlocker`.
- [ ] Existing accepted resolved decision suppresses duplicate candidates.
- [ ] Contradictory operational context and handoff evidence creates a `Contradiction`.
- [ ] Ambiguous milestone direction creates a `TacticalChoice`.
- [ ] Repeated repository failure pattern creates an `OperationalBlocker` when evidence exists.
- [ ] Duplicate signals merge into one candidate.
- [ ] Every candidate has at least one evidence item and at least one source reference.

## Exit Criteria

- [ ] The system can answer what decisions appear necessary, why they are necessary, how urgent they are, and whether they were already resolved.
- [ ] Candidate discovery is analyzer-driven and typed.
- [ ] Noise suppression is test-covered.
