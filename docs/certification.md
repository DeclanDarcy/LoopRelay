# Certification fixture program

The `LoopRelay.Certification` executable runs disposable-repository fixtures against the assembled LoopRelay product. It exists to harden a completed epic, collect release evidence, and expose integration failures that component tests cannot establish.

## When to run it

The certification fixture program is reserved for post-epic completion hardening. Run it only after the epic implementation is complete, the production tree is stable, and routine build and test verification has passed.

Do **not** add the certification executable to a generic “run all tests” command, per-save verification, or ordinary pull-request test loop. The program creates repositories and Git remotes, starts real LoopRelay and Codex processes, consumes provider capacity, and may retain raw provider evidence. Those properties make it an explicit operator campaign rather than a component-test target.

Run a new campaign when at least one of these conditions applies:

- an epic has reached implementation completion and is entering hardening;
- a release candidate needs current assembled-product evidence;
- the supported Codex binary, app-server schema, model, or effort changes;
- the workflow, transition, prompt, effect, persistence, recovery, or completion denominator changes;
- previous evidence is stale, invalidated, or being replaced after an investigated failure.

Routine verification remains:

```powershell
dotnet build LoopRelay.slnx
dotnet test LoopRelay.slnx --no-build --no-restore
```

A green component suite is a prerequisite for certification. It is not a substitute for certification, and certification is not part of that routine suite.

Provider approval and posture checks must not reappear as skipped component-test placeholders. Those vestigial placeholders were removed; their behavior is owned by the explicit live `provider-profile` fixture, and routine verification is expected to report zero skips.

## What the program certifies

The program has three kinds of command:

1. Deterministic fixtures exercise public CLI, persistence, Git, oracle, and local platform behavior in disposable repositories.
2. Live fixtures exercise the exact installed Codex profile, recovery boundaries, workflow segments, completion, and complete roadmap-to-execution chains.
3. Projections inspect coverage or evaluate already-produced evidence. They do not run the missing fixtures for the operator.

The command names describe the behavior under certification. Historical certification-milestone numbers are not part of the command or evidence contract.

| Command | Kind | Required inputs | Purpose | Evidence file |
| --- | --- | --- | --- | --- |
| `coverage-ledger` | Projection | workspace | Projects production-derived coverage and uncovered obligations to stdout. | none |
| `platform-probe` | Deterministic diagnostic | workspace, case root | Probes separator, line-ending, UTF-8, Git, filesystem, and path behavior on the local platform. | `platform-<os>.latest.json` |
| `status-canary` | Deterministic | CLI | Repeats a composed status fixture and compares normalized behavior. | `status-canary.latest.json` |
| `public-cli-contracts` | Deterministic | CLI | Exercises public outcomes, bounded commands, mutation expectations, and storage commands. | `public-cli-contracts.latest.json` |
| `provider-profile` | Live | Codex, auth | Certifies the exact provider profile, read-only `xhigh`, approval-before-mutation, exact requested paths, declined writes, scoped accepted writes, app-server behavior, and privacy boundary. | `provider-profile.latest.json` |
| `transition-recovery` | Live | CLI, Codex, auth | Exercises pre-submission, accepted-request, provider-complete, effect, and persisted-completion recovery boundaries. | `transition-recovery.latest.json` |
| `plan-workflow` | Live | CLI, Codex, auth | Runs Plan from both roadmap producers and checks continuity, mutation scope, rollback, and Execute-entry products. | `plan-workflow.latest.json` |
| `execute-workflow` | Live | CLI, Codex, auth | Runs execution through handoff while checking implementation continuity, decisions, acceptance, and the publication boundary. | `execute-workflow.latest.json` |
| `git-publication` | Deterministic | CLI | Certifies parent and nested `.agents` repository publication and fail-closed behavior. | `git-publication.latest.json` |
| `persistence-lifecycle` | Deterministic | CLI | Certifies schema inventory, initialization, verification, import, unsupported-schema, and corrupt-store behavior. | `persistence-lifecycle.latest.json` |
| `traditional-roadmap` | Live | CLI, Codex, auth | Runs the TraditionalRoadmap segment to its bounded Plan-entry products. | `traditional-roadmap.latest.json` |
| `eval-roadmap` | Live | CLI, Codex, auth | Runs the EvalRoadmap segment to the same bounded Plan-entry contract. | `eval-roadmap.latest.json` |
| `completion-closure` | Live | CLI, Codex, auth | Certifies completion certification, archive closure, roadmap updates, continuity retirement, and idempotent rerun. | `completion-closure.latest.json` |
| `failure-oracle-matrix` | Deterministic audit | workspace | Audits maintained failure classes, transition recovery coverage, oracle controls, exclusions, and governance. | `failure-oracle-matrix.latest.json` |
| `traditional-full-chain` | Live full chain | CLI, Codex, auth | Runs TraditionalRoadmap → Plan → Execute, publication, completion, and rerun checks. | `traditional-full-chain.latest.json` |
| `eval-full-chain` | Live full chain | CLI, Codex, auth | Runs EvalRoadmap → Plan → Execute, publication, completion, and rerun checks. | `eval-full-chain.latest.json` |
| `release-gate` | Projection | workspace, completed evidence | Evaluates evidence freshness, durability, profile, production-surface binding, budgets, and release dimensions. | `release-gate.latest.json` |

## Prerequisites

Use a clean worktree at the exact commit being certified. Do not certify an unrelated dirty checkout and later attribute the evidence to a different revision.

The operator needs:

- the .NET SDK required by `global.json` and a successful Debug build;
- PowerShell and Git available to child processes;
- the Debug CLI assembly and adjacent `settings.default.json`;
- for live commands, the current native Codex executable and a readable Codex auth file;
- capacity for multiple live model turns, including both full chains;
- a dedicated case authority root that is not shared with another running campaign.

Use the current installed Codex executable. Do not reuse a binary copied into an old certification case. Confirm its version before the campaign:

```powershell
& $codex --version
```

The harness accepts the certified models `gpt-5.3-codex-spark` and `gpt-5.4-mini` at medium effort. It defaults to `gpt-5.3-codex-spark`; pass `--model` explicitly in an evidence-producing campaign so the operator command records the intended profile.

## Establish the campaign paths

From the repository root:

```powershell
$workspace = (Resolve-Path '.').Path
$project = 'src/LoopRelay.Certification'
$cli = (Resolve-Path 'src/LoopRelay.Cli/bin/Debug/net10.0/LoopRelay.Cli.dll').Path
$codex = (Resolve-Path '<current-native-codex-executable>').Path
$auth = (Resolve-Path (Join-Path $env:USERPROFILE '.codex/auth.json')).Path
$caseRoot = 'C:\LoopRelay-certification-evidence\<commit-or-campaign-id>'
$model = 'gpt-5.3-codex-spark'
```

Choose a case root outside `.tmp` when its JSON evidence must receive durable release-gate credit. The release gate classifies evidence beneath a `.tmp` path as local temporary evidence. Raw retained cases should remain in restricted storage even when the derived JSON evidence is copied into a durable bundle.

Build and run the routine suite before starting:

```powershell
dotnet build LoopRelay.slnx
dotnet test LoopRelay.slnx --no-build --no-restore
```

## Run the deterministic fixtures

Run these commands separately and stop on the first nonzero exit:

```powershell
dotnet run --no-build --project $project -- status-canary `
  --workspace $workspace --cli $cli --case-root $caseRoot

dotnet run --no-build --project $project -- public-cli-contracts `
  --workspace $workspace --cli $cli --case-root $caseRoot

dotnet run --no-build --project $project -- git-publication `
  --workspace $workspace --cli $cli --case-root $caseRoot

dotnet run --no-build --project $project -- persistence-lifecycle `
  --workspace $workspace --cli $cli --case-root $caseRoot

dotnet run --no-build --project $project -- failure-oracle-matrix `
  --workspace $workspace --case-root $caseRoot

dotnet run --no-build --project $project -- platform-probe `
  --workspace $workspace --case-root $caseRoot
```

`platform-probe` is diagnostic while the release contract is local-Windows-only. It does not establish a cross-platform claim by itself.

Optionally inspect the current coverage projection:

```powershell
dotnet run --no-build --project $project -- coverage-ledger --workspace $workspace
```

## Run the targeted live fixtures

First certify the exact provider profile—including approval and posture behavior formerly represented by skipped component placeholders—then recovery, the two roadmap producers, Plan, Execute, and completion:

```powershell
dotnet run --no-build --project $project -- provider-profile `
  --workspace $workspace --codex $codex --auth $auth `
  --case-root $caseRoot --model $model

dotnet run --no-build --project $project -- transition-recovery `
  --workspace $workspace --cli $cli --codex $codex --auth $auth `
  --case-root $caseRoot --model $model

dotnet run --no-build --project $project -- traditional-roadmap `
  --workspace $workspace --cli $cli --codex $codex --auth $auth `
  --case-root $caseRoot --model $model

dotnet run --no-build --project $project -- eval-roadmap `
  --workspace $workspace --cli $cli --codex $codex --auth $auth `
  --case-root $caseRoot --model $model

dotnet run --no-build --project $project -- plan-workflow `
  --workspace $workspace --cli $cli --codex $codex --auth $auth `
  --case-root $caseRoot --model $model

dotnet run --no-build --project $project -- execute-workflow `
  --workspace $workspace --cli $cli --codex $codex --auth $auth `
  --case-root $caseRoot --model $model

dotnet run --no-build --project $project -- completion-closure `
  --workspace $workspace --cli $cli --codex $codex --auth $auth `
  --case-root $caseRoot --model $model
```

Each live runner creates an isolated `CODEX_HOME`, copies the supplied auth file into it for execution, disables analytics where supported, and removes the copied auth file during cleanup. That cleanup does not make every retained rollout safe to publish.

## Run both full chains

The full chains exercise the assembled product across workflow boundaries. Run both profiles and request failed-case retention so a non-obvious failure remains diagnosable:

```powershell
dotnet run --no-build --project $project -- traditional-full-chain `
  --workspace $workspace --cli $cli --codex $codex --auth $auth `
  --case-root $caseRoot --model $model --retain-case

dotnet run --no-build --project $project -- eval-full-chain `
  --workspace $workspace --cli $cli --codex $codex --auth $auth `
  --case-root $caseRoot --model $model --retain-case
```

For the full-chain commands, `--retain-case` retains a non-passing case; passing cases are still removed after their derived evidence is written. `status-canary` and `public-cli-contracts` also honor `--retain-case`. Other targeted runners have command-specific cleanup behavior, so do not assume the flag preserves them.

The current budget contract requires each full chain to complete within two hours, retain no more than 500 MiB of provider evidence, and emit the recognized provisional release-budget decision.

## Investigate every failure before continuing

A nonzero exit is evidence, not a diagnosis. Stop the campaign and preserve the output before replacing it with a rerun.

At minimum:

1. Read `evidence/<command>.latest.json` and identify the first failed check or transition.
2. Record expected and actual workflow/transition identities, exit code, explanation, elapsed time, and stdout/stderr digests.
3. Inspect the disposable repository and persistence ledger to determine which authoritative products and effects actually exist.
4. For a retained live case, correlate the failure with the exact rollout under `codex-home/sessions/YYYY/MM/DD/rollout-*.jsonl`.
5. Inspect the relevant Codex tool call and its tool output, then compare the paths or effects it reported with the paths or effects required by the transition contract.
6. Classify the root cause separately from the harness routing category. `ProductRegression`, for example, is not a sufficient explanation of whether the defect was in orchestration, validation, a provider action, or the fixture.
7. Produce a sanitized incident summary containing digests and necessary facts. Keep the raw rollout private.

The harness currently scans rollouts automatically only for a narrow quota-exhaustion signature. It does not perform the complete correlation and causal inspection above. That inspection is therefore a required operator responsibility for non-obvious failures.

Do not publish raw `codex-home` directories. Rollouts can contain prompts, responses, tool arguments, absolute paths, session identifiers, and other private execution context. Never publish the auth file or an environment dump.

If a rerun passes, retain both the original failure evidence and the passing repeat. Do not overwrite the historical failure and report only the later pass. Record whether the failure reproduced and whether the two attempts failed at the same boundary.

## Evaluate the release gate

Run the release gate only after all required evidence files exist and failures have been resolved or explicitly adjudicated:

```powershell
dotnet run --no-build --project $project -- release-gate `
  --workspace $workspace --case-root $caseRoot
```

`release-gate` is an evidence evaluator, not a fixture runner. It will not fill in missing commands. It requires:

- every required dimension to have passing or accepted evidence;
- evidence no older than seven days;
- a production-surface digest matching the current source and issue denominator;
- the exact certified model/effort evidence for provider-dependent dimensions;
- cross-machine-durable evidence paths rather than `.tmp` paths;
- current checked-in Codex compatibility fixtures;
- distinct classification routes and passing full-chain budgets.

On its first complete pass, the gate establishes `production-baseline.v1.json`. A later production-surface change invalidates that baseline and requires recertification rather than silently reusing old evidence.

The gate supports a user-approved `*.adjudication.json` sidecar for exceptional `satisfactory-no-rerun` dispositions. Such an adjudication must identify the exact evidence file and retained case, preserve raw evidence, be approved by `user`, carry a non-future timestamp, and include the raw failed/not-run transitions and budget facts. Adjudication is not a shortcut around investigation and should remain exceptional.

## Evidence handoff

The campaign root contains derived `*.latest.json` results and, depending on command and outcome, disposable cases. Before handing evidence to another machine or publishing it:

- bind the campaign to the exact Git commit and command/model parameters in a manifest;
- preserve the original results and any accepted adjudication sidecars;
- generate SHA-256 checksums after final sanitization;
- keep raw provider rollouts and credentials out of the public bundle;
- include the derivation and sanitization code used to construct public summaries;
- disclose failed attempts and manual adjudications alongside passes;
- run the privacy scanner or an equivalent independent scan over the public output.

The release claim should cite the named command evidence and revision. It should not cite historical certification-milestone numbers, an unqualified “all tests passed,” or a private local path that another reviewer cannot inspect.
