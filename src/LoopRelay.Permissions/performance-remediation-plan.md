# LoopRelay.Permissions Performance Remediation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make rule evaluation honor the configured policy (currently it silently evaluates the built-in default), and remove per-call reconstruction work from the approval-blocking path.

**Architecture:** Three small, independent fixes in `Services/Evaluation`. Task 1 is a **deliberate behavior change** disguised as a one-liner — it must ship with tests and a release note.

**Tech Stack:** .NET 10, xUnit (`tests/LoopRelay.Permissions.Tests`).

**Source findings:** PERF-07, PERF-27a/c/d in [production-code-performance-audit.md](../../production-code-performance-audit.md). Line references are against commit `0de6b5a8`.

## Global Constraints

- Deny short-circuit aggregation, closed-world deny, and hard-deny minimum are untouchable.
- `InvariantGuard` ordering after engine evaluation stays exactly as is.
- The reparse-point security check in `OperationPermissionHandler` must keep detecting reparse points on every path segment — only its *implementation* gets cheaper.
- Approval handling runs synchronously on the codex read pump (codex blocks awaiting the reply): correctness first, latency second, but never trade the first for the second.

## Cross-Project Dependencies

- None inbound. Outbound: the Task 1 behavior change affects any operator with a custom `permissions` section in `settings.json` (loaded via `CliSettingsLoader.cs:110-111` in LoopRelay.Cli) — coordinate the release note.

---

### Task 1: Evaluate against the configured policy, not the default — PERF-07

**Files:**
- Modify: `src/LoopRelay.Permissions/Services/Evaluation/PermissionEvaluatorEngine.cs:30` and `:46-47`
- Modify: `src/LoopRelay.Permissions/Services/Evaluation/PermissionPolicyFactory.cs:13-26` (hoist the duplicated `MergeHardDeny` — PERF-27d)
- Test: `tests/LoopRelay.Permissions.Tests` (new `PermissionEvaluatorEnginePolicyTests.cs`)

**Interfaces:**
- Consumes: nothing new. `Evaluate(CanonicalCommand[])` signature unchanged.
- Produces: same API; `internal static EvaluateSingle` kept **only if** a test references it (check with `grep -r "EvaluateSingle" tests/`), otherwise delete it.

**Current behavior (verified):** `Evaluate` (line 30) calls the static `EvaluateSingle`, which allocates `new PermissionEvaluatorEngine()` — parameterless ctor → `PermissionPolicyOptions.Default` — and evaluates on *that* instance. The DI singleton's `_policy` (constructed from settings, `ServiceCollectionExtensions.cs:32-48`) is never consulted by rule evaluation. Additionally `MergeWithMinimum` computes `MergeHardDeny` twice per call (`PermissionPolicyFactory.cs:16,23`), and every stray engine construction rebuilds ~10 FrozenSets.

**Exact change (verified against the file):**

```csharp
// PermissionEvaluatorEngine.cs line 30 — before:
EvalResult single = EvaluateSingle(command);
// after:
EvalResult single = EvaluateSingleCore(command);
```

In `PermissionPolicyFactory.MergeWithMinimum`, compute `var hardDeny = MergeHardDeny(...)` once into a local and use it at both former call sites (read lines 13-26 first; keep output identical).

- [ ] **Step 1: Write the failing test** — custom policy is honored:

```csharp
[Fact]
public void Evaluate_HonorsConfiguredSafeBashCommands()
{
    // A command NOT in the default SafeBashCommands set, allowed by custom policy.
    var custom = PermissionPolicyOptions.Default with
    {
        SafeBashCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "mytool" },
    }; // adapt construction to the real options type after reading PermissionPolicyOptions.
    var engine = new PermissionEvaluatorEngine(custom);
    EvalResult result = engine.Evaluate([Command("mytool")]); // helper: parse/canonicalize "mytool"
    Assert.Equal(RuleDecision.Allow, result.Decision);
}

[Fact]
public void Evaluate_DefaultPolicyBehaviorUnchanged()
{
    var engine = new PermissionEvaluatorEngine();
    Assert.Equal(RuleDecision.Deny, engine.Evaluate([Command("unknowncmd")]).Decision); // closed-world deny
    Assert.Equal(RuleDecision.Deny, engine.Evaluate([Command("sudo")]).Decision);       // hard deny floor
}
```

- [ ] **Step 2: Run, verify the first test FAILS** (`dotnet test tests/LoopRelay.Permissions.Tests --filter PermissionEvaluatorEnginePolicy`) — current code denies `mytool` because it evaluates the default policy.
- [ ] **Step 3: Apply the one-line fix + the `MergeHardDeny` hoist.**
- [ ] **Step 4: Run the full Permissions suite.** Expected: all pass. If any existing test pinned the old wrong behavior, treat that as a test bug — but read it carefully first; it may encode an intended invariant.
- [ ] **Step 5: Add a hard-deny floor test with a custom policy** — a custom policy must NOT be able to allow what `MergeWithMinimum`'s floor denies (e.g., `sudo`): construct a permissive custom policy, assert `sudo` still denies. This is the guarantee that makes the behavior change safe.
- [ ] **Step 6: Commit** — `fix(permissions): evaluate configured policy instead of default in rule engine` — and add a release-note entry: *operator-configured permission rules now take effect in rule evaluation; previously only the hard-deny floor and invariant guard were honored.*

### Task 2: Validate the evaluation flow once, not per request — PERF-27a

**Files:**
- Modify: `src/LoopRelay.Permissions/Services/Evaluation/PermissionHandler.cs:19,23,60-91`
- Test: `tests/LoopRelay.Permissions.Tests`

**Change contract:** `GuardEvaluationFlow(PermissionEvaluationFlow.Default)` fully re-validates a `static readonly` structure on every `Evaluate`. Move the validation of the default flow to a static constructor (or `static readonly bool _defaultFlowValidated = Guard(...)` initializer) so it runs once per process. If the flow is ever injected in the future, the guard call for non-default flows stays where it is — read the file first to see whether flow is currently injectable; if it is only ever `Default`, guard once and done.

- [ ] **Step 1: Read `PermissionHandler.cs` fully; write a test** asserting a deliberately malformed flow still throws (keeps the guard alive) — construct the malformed flow directly if the type allows, else assert via the existing guard method made `internal`.
- [ ] **Step 2: Implement; run suite; commit** — `perf(permissions): validate default evaluation flow once`

### Task 3: One filesystem stat per path segment in the reparse-point check — PERF-27c

**Files:**
- Modify: `src/LoopRelay.Permissions/Services/Evaluation/OperationPermissionHandler.cs:210-238` (`ContainsReparsePoint`)
- Test: `tests/LoopRelay.Permissions.Tests`

**Change contract:** current implementation performs `File.Exists` + `Directory.Exists` + `File.GetAttributes` (3 stats) per segment. Replace with a single `File.GetAttributes(segment)` inside a `try/catch (FileNotFoundException or DirectoryNotFoundException or IOException)` → segment absent → continue. Deny-on-exception semantics for genuinely unreadable paths must be preserved exactly as today — read the current catch behavior first and mirror it.

- [ ] **Step 1: Write tests first**: reparse point detected (create a junction/symlink in a temp dir — mark the test Windows-only if the suite runs cross-platform), non-existent segment tolerated, plain directory chain passes.
- [ ] **Step 2: Implement single-stat version; run suite.**
- [ ] **Step 3: Commit** — `perf(permissions): single-stat reparse-point walk`

---

## Verification Gate (whole plan)

- Full `tests/LoopRelay.Permissions.Tests` sweep green.
- Manual smoke: run a loop iteration with a custom `permissions` block in `settings.json` and confirm a custom-allowed command is approved and a floor-denied command is still denied.
- Release note for Task 1 published with the change.
