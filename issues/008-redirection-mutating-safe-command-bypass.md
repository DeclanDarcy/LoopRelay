# Redirection and mutating safe commands are approved

## Severity

High

## Finding

The permission parser does not reject shell output redirection, and the safe command list includes commands that can mutate files.

Affected code:

- `src/LoopRelay.Permissions/Services/PermissionConstants.cs`
- `src/LoopRelay.Permissions/Services/CommandParser.cs`
- `src/LoopRelay.Permissions/Services/PermissionEvaluatorEngine.cs`

`CommandParser.DetectUnsupportedSyntax` rejects several unsupported shell constructs, but it does not reject `>`, `>>`, `2>`, `&>`, or related output redirection forms. `PermissionConstants.SafeBashCommands` also includes commands such as `echo`, `cat`, `printf`, `tee`, and `find`; the evaluator accepts them by command name alone.

Examples that are accepted as safe despite mutating the workspace:

- `echo owned > src/file.txt`
- `cat a.txt > b.txt`
- `printf hi | tee generated.txt`
- `find . -delete`

## Impact

Approval-managed execution can write, overwrite, or delete files through commands labeled as read-only. This makes the new permission path materially more permissive than its rule descriptions and can allow unreviewed workspace mutation under the default roadmap execution posture.

## Proposal

Make shell syntax and command-specific mutation flags part of the permission decision.

The robust shape is:

- Reject redirection operators as unsupported unless the policy explicitly models them.
- Remove `tee` from the unconditional safe list, or allow it only in non-writing modes if such modes are modeled.
- Add command-specific deny checks for mutating options such as `find -delete`.
- Treat safe shell commands as safe only when their flags and syntax cannot write to the workspace.

## Acceptance Criteria

- Commands using output redirection are denied or rejected as unsupported syntax.
- `tee generated.txt` and `printf hi | tee generated.txt` are not auto-approved as read-only.
- `find . -delete` is denied.
- Tests cover redirection, `tee`, and mutating `find` flags.
