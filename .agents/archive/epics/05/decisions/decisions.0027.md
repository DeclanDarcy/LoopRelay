# Decisions

## Newly Authorized

- M6 Decision Resolution is started, and the first M6 foundation is accepted:
  - proposal authority flows into explicit human resolution
  - human resolution creates an immutable resolution snapshot
  - the snapshot becomes part of decision authority
- The resolution snapshot model is accepted as the correct first M6 move because it answers: "What exactly was resolved?"
- A decision record should not require reconstructing mutable proposal state, revision history, comparison history, or current proposal files to explain what was resolved.
- Future decision consumers should treat the resolution snapshot as sufficient to explain resolution.
- Resolution should become its own lifecycle/service boundary:
  - `IDecisionGenerationService` owns proposal creation
  - `IDecisionRefinementService` owns proposal evolution
  - `IDecisionResolutionService` owns decision authority creation
- The next M6 slice should introduce `IDecisionResolutionService` and `DecisionResolutionService`, then move `ResolveProposalAsync` behind that boundary without changing endpoint behavior.
- Accept, reject, and defer must receive focused backend lifecycle tests before UI work.
- Accept, reject, and defer must be tested for:
  - proposal state
  - decision state
  - decision artifact creation
  - resolution snapshot creation
- Accept, reject, and defer are not assumed to be symmetrical outcomes.
- Accept is expected to create straightforward decision authority.
- Reject likely creates decision authority too, but not the same authority as acceptance; it should prevent rejected proposals from reappearing indefinitely.
- Defer needs careful review because it may be closer to review state than decision authority.
- Resolution UI should wait until accept/reject/defer semantics are stable.
