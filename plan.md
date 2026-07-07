# CLI Permission Settings Plan

## Goal

Externalize the hardcoded permission policy from `LoopRelay.Permissions` into a CLI consumer-owned `settings.json`, and make CLI publish produce a default `settings.json` containing today's effective permission values.

The runtime behavior should remain unchanged when the generated default file is used.

## Current State

Permission policy is currently embedded in code:

- `src/LoopRelay.Permissions/Services/PermissionConstants.cs`
  - `FingerprintVersion = "v1"`
  - `CommandsWithSubcommands`
  - `SafeTools`
  - `SafeBashCommands`
  - chain-splitting regex
- `src/LoopRelay.Permissions/Services/PermissionEvaluatorEngine.cs`
  - hard-deny commands and patterns
  - review-required commands and subcommands
  - allow-listed command/subcommand combinations
  - closed-world deny fallback
- `src/LoopRelay.Permissions/Services/InvariantGuard.cs`
  - non-bypassable guardrails duplicating the dangerous hard-deny cases

Both `LoopRelay.Cli` and `LoopRelay.Plan.Cli` call `services.AddAgents()`, and `AddAgents()` calls `AddCodexPermissions()`, so permission settings need to be shared infrastructure loaded by each CLI host before the service provider is built.

## Target Configuration

Add a source-controlled default settings file, for example:

- `src/LoopRelay.Cli/settings.default.json`
- or a shared root template such as `config/settings.default.json` if both CLIs should publish the same file

Publish should materialize this as:

- `C:\tools\command-center\settings.json`
- `C:\tools\command-center-plan\settings.json`

The runtime should load `settings.json` from `AppContext.BaseDirectory` by default, because that is the deployed CLI consumer directory. An optional environment override such as `LOOPRELAY_SETTINGS_PATH` can be added for tests and local development, but the normal path should not depend on the caller's current working directory or the target repository directory.

## Default `settings.json` Shape

Use a typed options object instead of scattering raw JSON reads through evaluator code.

Recommended shape:

```json
{
  "permissions": {
    "fingerprintVersion": "v1",
    "commandsWithSubcommands": [
      "git",
      "npm",
      "pnpm",
      "yarn",
      "docker",
      "kubectl",
      "dotnet",
      "cargo",
      "go",
      "pip",
      "conda",
      "apt-get",
      "apt",
      "brew",
      "systemctl",
      "terraform",
      "az",
      "gcloud",
      "gh"
    ],
    "safeTools": [
      "read",
      "glob",
      "grep",
      "ls"
    ],
    "safeBashCommands": [
      "echo",
      "cat",
      "head",
      "tail",
      "wc",
      "sort",
      "uniq",
      "diff",
      "less",
      "more",
      "file",
      "stat",
      "which",
      "whoami",
      "date",
      "uname",
      "hostname",
      "basename",
      "dirname",
      "realpath",
      "true",
      "false",
      "test",
      "env",
      "printenv",
      "id",
      "groups",
      "tee",
      "tr",
      "cut",
      "paste",
      "fold",
      "fmt",
      "nl",
      "seq",
      "yes",
      "printf",
      "find",
      "xargs",
      "type",
      "command",
      "rg"
    ],
    "hardDeny": {
      "privilegeEscalationCommands": [
        "sudo",
        "su",
        "doas"
      ],
      "recursiveForceDelete": {
        "command": "rm",
        "flagSets": [
          [
            "-rf"
          ],
          [
            "-fr"
          ],
          [
            "-r",
            "-f"
          ],
          [
            "-r",
            "--force"
          ],
          [
            "--recursive",
            "-f"
          ],
          [
            "--recursive",
            "--force"
          ]
        ]
      },
      "systemControlCommands": [
        "shutdown",
        "reboot",
        "halt",
        "poweroff"
      ],
      "networkFetchCommands": [
        "curl",
        "wget"
      ],
      "gitForcePushFlags": [
        "--force",
        "-f"
      ],
      "indirectShellExecution": {
        "commands": [
          "bash",
          "sh",
          "zsh"
        ],
        "flag": "-c"
      }
    },
    "reviewRequired": {
      "gitCommit": true,
      "gitCommitAmendFlags": [
        "--amend"
      ],
      "gitPush": true,
      "installCommands": [
        "npm",
        "pnpm",
        "yarn",
        "pip",
        "dotnet",
        "cargo",
        "apt-get",
        "apt",
        "brew",
        "conda"
      ],
      "installSubcommand": "install",
      "infrastructureCommands": [
        "docker",
        "kubectl",
        "terraform"
      ]
    },
    "allow": {
      "alwaysAllowedCommands": [
        "pwd"
      ],
      "gitReadOnlySubcommands": [
        "status",
        "diff"
      ],
      "gitLogAllowedUnlessFlags": [
        "-p",
        "--patch"
      ],
      "dotnetAllowedSubcommands": [
        "build",
        "test",
        "restore"
      ],
      "packageManagerAllowedSubcommands": {
        "npm": [
          "test",
          "run"
        ],
        "pnpm": [
          "test",
          "run"
        ],
        "yarn": [
          "test",
          "run"
        ]
      },
      "testCommands": {
        "pytest": [],
        "go": [
          "test"
        ]
      }
    }
  }
}
```

The chain-splitting regex can stay code-owned because it is parser syntax, not consumer policy. Closed-world deny should also stay code-owned as the final safety behavior.

## Non-Bypassable Invariants

Keep `InvariantGuard` as a separate safety layer, but drive its command lists from the same resolved policy object after validation.

Do not let an operator turn off these invariant categories through `settings.json`:

- privilege escalation commands
- recursive forced delete
- system control commands
- network fetch commands
- Git force push
- indirect shell execution

The settings file may add stricter deny/review/allow entries, but it must not weaken these minimum guardrails. Implement this by merging loaded policy with a code-owned `MinimumPermissionPolicy`, then validating that all required invariant entries are present.

## Implementation Steps

1. Add typed permission settings models in `LoopRelay.Permissions`.
   - Suggested types: `PermissionPolicyOptions`, `PermissionHardDenyOptions`, `PermissionReviewRequiredOptions`, `PermissionAllowOptions`.
   - Normalize all configured commands, subcommands, and flags with `StringComparer.OrdinalIgnoreCase`.
   - Validate missing sections, duplicate entries, empty strings, and malformed recursive-delete flag sets.

2. Replace `PermissionConstants` list usage with injected policy.
   - Keep `ChainSplitter` in code.
   - Move `CommandsWithSubcommands` into `CommandParser` through an injected `PermissionPolicyOptions`.
   - Move `SafeTools` and `SafeBashCommands` into `PermissionEvaluatorEngine`.

3. Convert `PermissionEvaluatorEngine` from static policy checks to instance checks.
   - Keep `EvaluateSingle` behavior equivalent.
   - Preserve exact decisions for the generated default settings.
   - Keep `IsRecursiveForceDelete` behavior, but source the flag sets from the policy object.

4. Update `InvariantGuard` to use the validated policy.
   - Enforce the minimum guardrail merge before DI registration completes or before the first evaluation.
   - Preserve invariant-denial reasons closely enough that existing tests still assert meaningful fragments.

5. Add a settings loader owned by CLI startup.
   - Suggested type: `CliSettingsLoader` in a shared CLI-accessible location, or duplicate small host loaders if avoiding a new shared project.
   - Load `settings.json` from `AppContext.BaseDirectory`.
   - Support `LOOPRELAY_SETTINGS_PATH` only if useful for tests.
   - Fail fast with a clear error if a present settings file is invalid.
   - For source/dev runs, either copy the default settings file to output via project item metadata or allow the loader to read `settings.default.json` from the build output and report that a consumer `settings.json` is missing.

6. Change DI registration.
   - Add an overload such as `AddCodexPermissions(PermissionPolicyOptions policy)`.
   - Add an overload such as `AddAgents(PermissionPolicyOptions policy)`.
   - Update `LoopCliComposition.Create(...)` and `PlanCliComposition.Create(...)` to load settings and pass the policy into `AddAgents(...)`.
   - Keep a test-only/default overload if existing tests need simple construction, but make production CLI startup use explicit settings.

7. Publish default settings.
   - Update `publish-cli.bat` to copy the default settings template to `%OUTPUT_DIR%\settings.json` after successful `dotnet publish`.
   - Update `publish-plan-cli.bat` the same way.
   - Prefer preserving an existing `%OUTPUT_DIR%\settings.json` so operator edits are not overwritten during republish.
   - If an overwrite mode is needed later, add it explicitly rather than making publish destructive by default.

8. Update tests.
   - `LoopRelay.Permissions.Tests`: verify the default JSON policy reproduces current allow/deny behavior.
   - Add validation tests for missing required invariant entries and invalid JSON shape.
   - Update parser tests to construct `CommandParser` with default policy.
   - Update gateway/session tests to construct the permission handler with default loaded policy.
   - Add CLI tests for loading `settings.json` from a controlled path or temporary output directory.
   - Add publish-script coverage if this repo already has script-level tests; otherwise document manual verification.

## Acceptance Criteria

- `settings.json` generated by publish contains the current hardcoded permission values.
- Main CLI and Plan CLI both consume the deployed `settings.json`.
- The default generated file produces the same decisions currently covered by `PermissionCoreTests` and `PermissionGatewayTests`.
- Invalid settings fail startup or permission registration with a clear diagnostic.
- Missing or edited allow-list entries cannot disable the non-bypassable invariant guardrails.
- Republish does not silently overwrite an existing consumer-edited `settings.json`.

## Verification

Run:

```powershell
dotnet test tests\LoopRelay.Permissions.Tests\LoopRelay.Permissions.Tests.csproj
dotnet test tests\LoopRelay.Agents.Tests\LoopRelay.Agents.Tests.csproj
dotnet test tests\LoopRelay.Cli.Tests\LoopRelay.Cli.Tests.csproj
dotnet test tests\LoopRelay.Plan.Cli.Tests\LoopRelay.Plan.Cli.Tests.csproj
```

Manual publish checks:

```powershell
.\publish-cli.bat
Test-Path C:\tools\command-center\settings.json
Test-Path C:\tools\command-center-plan\settings.json
```

Then edit one published `settings.json`, rerun publish, and confirm the edit is preserved.
