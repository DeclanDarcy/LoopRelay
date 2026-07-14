# Roadmap CLI does not load configurable permission policy

## Severity

Medium

## Finding

`LoopRelay.Roadmap.CLI` still uses the built-in permission policy while the main CLI and Plan CLI load `settings.json`.

Affected code:

- `src/LoopRelay.Roadmap.Cli/RoadmapCliComposition.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapExecutionOptions.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapExecutionBridge.cs`
- `publish-roadmap-cli.bat`

`RoadmapCliComposition.Create` calls `services.AddAgents()` without loading `CliSettingsLoader.LoadPermissionPolicy()`. Roadmap execution defaults to `RequiresApproval: true` and uses persistent sessions for approval-managed execution, so this is a real runtime approval surface.

The publish script also does not seed `settings.json` for the roadmap CLI output directory, unlike the main CLI and Plan CLI publish scripts.

## Impact

Policy behavior differs by CLI surface. Operators can edit `settings.json` expecting a uniform permission policy, but Roadmap CLI approval-managed execution remains on hard-coded defaults. This can cause both over-permissive and over-restrictive behavior depending on the intended policy change.

## Proposal

Route Roadmap CLI through the same permission settings path as the other CLIs.

The robust shape is:

- Load `CliSettingsLoader.LoadPermissionPolicy()` in `RoadmapCliComposition.Create`.
- Call `services.AddAgents(permissionPolicy)`.
- Add `settings.default.json` content publishing to `LoopRelay.Roadmap.Cli.csproj`.
- Update `publish-roadmap-cli.bat` to seed or preserve `settings.json`.
- Add a Roadmap CLI loader test matching the CLI and Plan CLI coverage.

## Acceptance Criteria

- Roadmap CLI approval-managed persistent sessions use the configured permission policy.
- Roadmap CLI published output includes `settings.default.json` and seeds `settings.json` when absent.
- Tests prove Roadmap CLI can load a controlled `settings.json` from its base directory or environment override.
