# Configurable policy can bypass git force-push hard deny

## Severity

High

## Finding

The minimum `git push --force` deny depends on configurable command parsing.

Affected code:

- `src/LoopRelay.Permissions/Models/PermissionPolicyOptions.cs`
- `src/LoopRelay.Permissions/Services/CommandParser.cs`
- `src/LoopRelay.Permissions/Services/PermissionEvaluatorEngine.cs`
- `src/LoopRelay.Permissions/Services/InvariantGuard.cs`

`PermissionPolicyFactory.MergeWithMinimum` preserves the configured `CommandsWithSubcommands` set without merging required parser invariants. `CommandParser` only assigns `push` as the subcommand when `git` is present in that configurable set. Both the evaluator and invariant guard then require `command.Subcommand == "push"` before denying a force push.

A settings file can remove `git` from `commandsWithSubcommands` and add `git` to an allow list. In that configuration, `git push --force` is parsed as command `git`, flag `--force`, and arg `push`, so the minimum force-push invariant is not triggered.

## Impact

A local policy edit can allow `git push --force` even though the code intends force-push denial to be a non-removable minimum invariant. This weakens the hard-deny boundary and can permit destructive remote Git operations through an approval path that should reject them unconditionally.

## Proposal

Make parser-critical command/subcommand recognition non-removable for hard-deny rules.

The robust shape is:

- Merge a minimum `CommandsWithSubcommands` set containing at least `git` into configured policy.
- Alternatively, make force-push detection inspect both parsed subcommand and args when `git` is not parsed as a subcommand command.
- Add a test that removes `git` from `commandsWithSubcommands`, adds `git` to an allow list, and verifies `git push --force` is still denied.

## Acceptance Criteria

- `git push --force` and `git push -f` are denied under edited policies that omit `git` from `commandsWithSubcommands`.
- Minimum hard-deny semantics do not depend on user-removable parser configuration.
- Tests cover the edited-policy bypass case.
