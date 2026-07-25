# LoopRelay.Agents Performance Remediation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the telemetry path a cheap rollout-file lookup (today it forensically reads the user's entire CODEX_HOME to return one filename), and remove per-call re-parsing on the negotiation and approval paths.

**Architecture:** Three independent tasks. Task 1 adds a fast path **beside** the forensic reader — the resume-failure and recovery diagnosis consumers keep their full-rigor semantics untouched.

**Tech Stack:** .NET 10, xUnit (`tests/LoopRelay.Agents.Tests`, `tests/LoopRelay.Agents.Compatibility.Tests`).

**Source findings:** PERF-08 (repository side), PERF-27b, PERF-27c (frame-parse share) in [production-code-performance-audit.md](../../production-code-performance-audit.md). Line references are against commit `0de6b5a8`.

## Global Constraints

- `ReadExactAsync`'s exact-match + Ambiguous/Partial/Corrupt classification semantics are untouchable for the diagnosis consumers (`AgentRuntime.cs:477` resume-failure refinement; recovery sources in LoopRelay.Cli). Do not modify `ReadExactAsync` itself except to extract shared helpers.
- Telemetry lookups stay fail-open (null/absent on any failure).
- Approval handling: deny-on-exception and the security checks stay; only redundant parsing goes.
- The codex rollout store belongs to the user's real codex installation — never write to it, and keep all reads tolerant of concurrent codex activity (files appearing/rotating mid-scan).

## Cross-Project Dependencies

- Task 1's fast path is consumed by `SessionTelemetryRecorder` in LoopRelay.Cli (its plan Task CLI-2), which also adds caller-side path caching (`cachedLogPath` is currently passed as null and the result discarded — `GatedAgentRuntime.cs:49-53`). Land Task 1 first; the Cli task consumes the new API.

---

### Task 1: Location-only rollout lookup — PERF-08

**Files:**
- Modify: `src/LoopRelay.Agents/Services/Codex/CodexRolloutRepository.cs` (add a method beside `ReadExactAsync:39-56`; today `:88` reads whole files before the first-line id check can bail, `:96,112` split + parse every line, `:270` hashes full content, and the ambiguity check scans everything with no early exit)
- Test: `tests/LoopRelay.Agents.Tests`

**Interfaces:**
- Produces: `public Task<string?> LocateAsync(string codexHome, string providerThreadId, CancellationToken)` returning the rollout file path or null. Contract:
  1. **Filename filter first:** codex rollout filenames embed the thread id — enumerate the same three directories (`sessions/`, `archived_sessions/`, `archived/`) and match on filename before opening anything. Verify the filename convention against real fixtures before relying on it (`ls` a real `~/.codex/sessions` day directory or check the repo's test fixtures); if the id is genuinely in the filename, most lookups open zero files.
  2. **First-line fallback:** for candidate files not resolvable by name, read only the first line (`StreamReader.ReadLineAsync`) and match `session_meta.id`; never read the full file.
  3. Return the newest match by directory date/file time when multiple match (mirror `ReadExactAsync`'s newest-match choice — read its selection logic first and reuse it).
  4. No hashing, no record materialization, no omissions/digest computation.
- Consumes: same directory-enumeration helpers as `ReadExactAsync` — extract them into private shared methods rather than duplicating.

- [ ] **Step 1: Read `CodexRolloutRepository.cs` end-to-end**; confirm the filename convention from fixtures; record it in `## Decisions` below.
- [ ] **Step 2: Write failing tests** against a temp fixture store: `Locate_FindsByFilename_WithoutOpeningOtherFiles` (populate decoy files with unreadable/locked content — if `LocateAsync` opens them the test fails), `Locate_FallsBackToFirstLineProbe` (id not in filename), `Locate_ReturnsNullWhenAbsent`, `Locate_PicksNewestOnDuplicates`.
- [ ] **Step 3: Run tests, verify fail; implement; verify pass.** (`dotnet test tests/LoopRelay.Agents.Tests --filter Locate`)
- [ ] **Step 4: Confirm diagnosis-path suites untouched and green** (`ReadExactAsync` fixtures).
- [ ] **Step 5: Commit** — `perf(agents): add location-only rollout lookup for telemetry`

### Task 2: Parse the embedded compatibility manifest once per process — PERF-27b

**Files:**
- Modify: `src/LoopRelay.Agents/Services/Codex/Compatibility/CodexCompatibilityManifest.cs:64-95` (`LoadEmbedded`: resource stream + JsonDocument + validation + duplicate grouping per call) and/or `src/LoopRelay.Agents/Services/Sessions/AgentRuntime.cs:56-58` (production passes no `_continuityProfileResolver`, so every `NegotiateAsync` re-loads)
- Test: `tests/LoopRelay.Agents.Compatibility.Tests`

**Change contract:** the embedded manifest is immutable per binary. Either give `LoadEmbedded` a `static readonly Lazy<CodexCompatibilityManifest>` backing (keep a `LoadEmbeddedUncached` internal for tests that assert parse/validation failures), or construct the continuity resolver once in `AgentRuntime`'s constructor. Prefer the Lazy — it fixes every caller (`DecisionSession.cs:384,453,528,792`; `CompositionPromptExecutionOwner.cs:818,1459` in LoopRelay.Cli) without touching them.

- [ ] **Step 1: Read both files; check whether any test relies on repeated fresh parses** (`grep -rn "LoadEmbedded" src/ tests/`).
- [ ] **Step 2: Write test:** two `LoadEmbedded` calls return the same instance; validation-failure tests still work via the uncached internal.
- [ ] **Step 3: Implement; run compatibility suite; commit** — `perf(agents): cache embedded compatibility manifest`

### Task 3: Parse each approval frame once — PERF-27c (Agents share)

**Files:**
- Modify: `src/LoopRelay.Agents/Services/Codex/CodexAppServerSession.cs:647` (`EnrichFileChangeApproval` re-parses `rawLine` with `JsonNode.Parse`) and the hand-off into `CodexPermissionAdapter.cs:23` (`JsonSerializer.Deserialize` of the same frame — locate the file: `Glob src/LoopRelay.Agents/**/CodexPermissionAdapter.cs`)
- Test: `tests/LoopRelay.Agents.Tests`

**Change contract:** the read pump already parsed the frame via `CodexAppServerMessage.Parse` (`:430-436`). Thread the parsed representation (message object or its `JsonElement`/`JsonNode`) through `Dispatch` → approval enrichment → permission adapter so the same bytes are parsed once, not up to three times, on the pump thread while codex blocks awaiting the reply. Preserve deny-on-exception: any conversion failure must produce the same denial/refusal the current parse failure produces — read the current failure handling at each of the three parse sites first and match it.

- [ ] **Step 1: Map the three parse sites and their failure behaviors** (table in `## Decisions`).
- [ ] **Step 2: Write tests:** a valid file-change approval round-trips identically (same enriched fields, same adapter output); a malformed frame produces the same outcome as today at the same stage.
- [ ] **Step 3: Implement the pass-through; run the Agents suite; commit** — `perf(agents): single parse per approval frame`

---

## Explicitly No Action (audit §14)

Reviewed clean — leave as-is: per-line JSON parse in the read pump (one parse per stdout line, incremental turn detection — necessary work); `AgentProcess` bounded stderr tail and exit waits; `CodexCompatibilityIdentityProbe` (already `Lazy`, once per process); one-shot pump; `AgentSessionRegistry`; argument builders. Robustness note parked in audit §15 Q9 (stdout-before-stderr read ordering in the identity probe) is out of scope here.

## Decisions

*(append: Task 1 filename convention evidence; Task 3 parse-site/failure-behavior table)*
