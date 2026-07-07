# Indirect shell configured flag is silently ignored

## Severity

Medium

## Finding

`permissions.hardDeny.indirectShellExecution.flag` is accepted from settings but is replaced during policy merge.

Affected code:

- `src/LoopRelay.Permissions/Configuration/CliSettingsLoader.cs`
- `src/LoopRelay.Permissions/Models/PermissionPolicyOptions.cs`
- `src/LoopRelay.Permissions/Services/PermissionEvaluatorEngine.cs`
- `src/LoopRelay.Permissions/Services/InvariantGuard.cs`

The loader maps `permissions.hardDeny.indirectShellExecution.flag` into `IndirectShellExecutionOptions`. `PermissionPolicyFactory.MergeHardDeny` then always replaces the configured flag with the minimum flag `-c`.

This makes settings appear more expressive than runtime behavior. For example, a policy that adds `powershell` to the indirect-shell commands and sets the flag to `-Command` appears valid, but runtime denial still checks only `powershell -c`.

## Impact

Administrators can believe they configured additional indirect shell hard-deny coverage when the effective policy silently ignores that flag. This is a security-footgun because configuration review says a rule exists while the evaluator does not enforce it.

## Proposal

Avoid accepting configuration that cannot be honored.

The robust shape is one of:

- Reject any configured `permissions.hardDeny.indirectShellExecution.flag` other than `-c` with a clear validation error.
- Model indirect shell execution as command-specific flags, for example `commands: { "bash": ["-c"], "powershell": ["-Command"] }`, and enforce all configured entries.
- Include the effective policy behavior in tests, not only loader mapping.

## Acceptance Criteria

- Settings cannot silently configure an ignored indirect-shell flag.
- If non-`-c` flags are supported, evaluator and invariant guard enforce them.
- Tests cover both invalid ignored-flag configuration and any supported command-specific flag behavior.
