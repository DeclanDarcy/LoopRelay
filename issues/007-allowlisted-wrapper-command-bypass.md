# Allowlisted wrapper commands bypass hard denies

## Severity

High

## Finding

The permission evaluator allows several wrapper-style commands solely by first token, so they can dispatch commands that would be denied if parsed directly.

Affected code:

- `src/LoopRelay.Permissions/Services/PermissionConstants.cs`
- `src/LoopRelay.Permissions/Services/CommandParser.cs`
- `src/LoopRelay.Permissions/Services/PermissionEvaluatorEngine.cs`
- `src/LoopRelay.Permissions/Services/InvariantGuard.cs`

`PermissionConstants.SafeBashCommands` includes commands such as `env`, `xargs`, `type`, and `command`. `PermissionEvaluatorEngine.CheckAllowList` then accepts these commands without inspecting whether the arguments name another executable.

Examples that are accepted by the wrapper allow rule but should be denied by the underlying operation:

- `command rm -rf build`
- `env git push`
- `xargs rm -rf build`

## Impact

The hard-deny boundary can be bypassed through an allowlisted command wrapper. This defeats the intent of closed-world permission evaluation and can allow destructive workspace or remote Git operations through approval-managed execution.

## Proposal

Do not treat command dispatchers as intrinsically safe.

The robust shape is:

- Remove wrapper/dispatcher commands from the unconditional safe list.
- If any wrapper remains supported, parse the wrapped executable and recursively evaluate the resulting command shape.
- Deny `xargs` unless the invoked command can be identified and evaluated safely.
- Add tests proving hard-denied commands remain denied when invoked through `command`, `env`, and `xargs`.

## Acceptance Criteria

- `command rm -rf build` is denied.
- `env git push` is denied.
- `xargs rm -rf build` is denied or rejected as unsupported syntax.
- Wrapper commands cannot auto-allow an operation that direct parsing would deny.
