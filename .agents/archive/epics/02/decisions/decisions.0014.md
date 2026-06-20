# Decisions

## Newly Authorized Decisions

- M4A.2 is accepted as clean and correctly bounded.
- The core handoff invariant is now protected: current handoff is the latest execution result, historical handoff is preserved prior state.
- Providers must remain uninvolved in historical handoff numbering and archival behavior.
- M4A.3 is authorized as the next implementation slice.
- M4A.3 scope is visibility only.
- The M4A.3 lifecycle target is: completed and validated handoff, then `AwaitingAcceptance` visible, then generated handoff reviewable.
- M4A.3 must implement dashboard and workspace projection refresh after handoff processing.
- M4A.3 must display `AwaitingAcceptance`.
- M4A.3 must display generated handoff metadata.
- M4A.3 must expose generated handoff content for review, either through a handoff review endpoint or by clean reuse of existing artifact loading.
- M4A.3 must include a full handoff content viewer.
- M4A.3 must preserve restart restoration of `AwaitingAcceptance`.
- M4A.3 certification must verify completion with validated handoff shows `AwaitingAcceptance`.
- M4A.3 certification must verify handoff content is visible in the workspace.
- M4A.3 certification must verify session `HandoffPath` persists after reload.
- M4A.3 certification must verify historical archive is visible in artifact inventory after refresh.
- M4A.3 certification must verify failed handoff validation or archive failure displays the failure reason.
- M4A.3 certification must verify no accept/reject controls appear yet.

## Explicitly Deferred

- Do not begin M4A.3 implementation before this commit-and-push stop point.
- Do not add accept controls in M4A.3.
- Do not add reject controls in M4A.3.
- Do not add commit controls in M4A.3.
- Do not add push controls in M4A.3.
- Acceptance workflow remains M5.
- Git lifecycle workflow remains M6.
