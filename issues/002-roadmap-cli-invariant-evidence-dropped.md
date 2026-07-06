# Roadmap CLI invariant evidence dropped

## Severity

Medium

## Finding

`InvariantValidator` writes a detailed evidence artifact and returns its path, but `RoadmapStateMachine` discards that path at both validation call sites. The outer failure handler then writes a generic blocker that does not link the original invariant evidence.

Affected code:

- `src/CommandCenter.Roadmap.CLI/InvariantValidator.cs`
- `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs`

The validator returns `InvariantValidationResult.EvidencePath`, but callers throw only `invariant.Error`. The diagnostic artifact remains on disk under orchestration evidence, yet `.agents/state.md` does not point at it through `LastTransition.Output` or `TransitionIntent.EvidencePaths`.

## Impact

Invariant failures are harder to diagnose and audit than intended. The authoritative state points to a generic blocker rather than the detailed invariant evidence that explains the failed check.

This weakens recovery for:

- Stale execution preparation.
- Missing execution prerequisites.
- Multiple active epics.
- Projection manifest/provenance violations.
- Active epic/spec mismatch.

It also creates duplicate failure artifacts for a single failure while linking only the less-specific one.

## Proposal

Make invariant failures first-class transition failures instead of rethrowing them as generic exceptions.

The robust design is:

- Add a helper such as `PersistInvariantFailureAsync(InvariantValidationResult result, RoadmapState current, string transition)`.
- Save state with:
  - `CurrentState = result.FailureState`
  - `LastTransition.Output = result.EvidencePath`
  - `TransitionIntent.EvidencePaths = [result.EvidencePath]`
  - blocker text from `result.Error`
- Append a journal event such as `InvariantFailed`.
- Avoid creating a second generic blocker when the failure has already been persisted.
- Consider changing `RoadmapStepException` to carry an optional evidence path for similar future cases.

This keeps the invariant validator as the owner of diagnostic evidence while letting the state machine own workflow persistence.

## Acceptance Criteria

- Invariant validation failures leave `.agents/state.md` pointing to the validator evidence path.
- No duplicate generic blocker is created for already-classified invariant failures.
- The transition journal records the invariant failure event and evidence path.
- Tests cover stale execution preparation and projection provenance invariant failures end to end through `RoadmapStateMachine`.
