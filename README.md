# LoopRelay

LoopRelay is currently a CLI-oriented project for running and observing automation loops.

Today, the CLI experience is mostly run-and-spectate: start a run, watch the output, and inspect the resulting artifacts after the loop advances. That behavior is useful, but it is not the long-term ceiling for the project.

The project intends to eventually incorporate richer interactive features directly into the CLI so the operator can participate more actively while a loop is running. Future CLI work should move beyond passive observation toward workflows that support live inspection, guided decisions, and interactive control where that improves the loop.

## Runtime Persistence

LoopRelay stores local runtime state under `.LoopRelay/`. The directory is local-only runtime state and is protected by a create-only `.LoopRelay/.gitignore` containing `*`.

Structured runtime state is canonical in `.LoopRelay/persistence/looprelay.sqlite3`. Decision-session resume state and session telemetry events are stored canonically in SQLite. A legacy `.LoopRelay/decision-session.json` is diagnostic input only and is never read as runtime authority.

Telemetry JSONL files under `.LoopRelay/telemetry/sessions.YYYY-MM-DD.NNNN.jsonl` remain as a compatibility export for existing diagnostics tooling; they are not the canonical telemetry store.

Runtime switches remain unchanged:

- `LoopRelay_DECISION_RESUME=0` or `false` disables resume and automatic replacement without deleting the active pointer, lineage, or recovery journal. If an active decision session exists, Execute stops with `ContinuityDisabled`; it never silently starts clean.
- `LoopRelay_DECISION_RECOVERY_POLICY=resume-only|reconstructed|certified` controls which already-implemented recovery mechanisms may run. The default is `resume-only`. `reconstructed` enables verified public/repository reconstruction; `certified` additionally permits a native fork only for an exact supported compatibility profile.
- Codex continuity is fail-closed by exact version and app-server schema digest. Unlisted profiles do not emit resume/read/fork requests.
- The checked-in `0.142.5` profile currently certifies exact-ID resume/read only. Reconstruction and native fork remain profile-gated until authenticated context-write/limit evidence and lost-response fork reconciliation are certified.
- `LoopRelay_SESSION_LOG=0` or `false` disables telemetry recording.

Roadmap storage verification validates the SQLite runtime rows when present and reports corrupt telemetry/resume rows or legacy resume files that conflict with canonical SQLite state.

## Unified CLI contract

The supported public executable is `LoopRelay.Cli`. It accepts `--repo <path>`, optional `--eval` or `--traditional` chain selection, and these commands:

- no command runs the selected chain; `eval`, `traditional`, `plan`, and `execute` run one bounded workflow;
- `status` observes selection, gates, blockers, storage authority, and continuity without mutation;
- `storage init` creates the canonical SQLite schema;
- `storage migrate` applies supported canonical schema migrations;
- `storage export [path]` exports through the storage authority and reports the resulting evidence;
- `storage sync` reconciles rebuildable projections and effect work with canonical facts; it is not a bidirectional legacy import;
- `storage verify` is byte-for-byte non-mutating and reports missing, stale, conflicting, corrupt, unsupported, unresolved, and partial-transaction conditions.
- `import detect|preview|execute|verify [identity]` runs the explicit compatibility-import portfolio; no legacy adapter participates in ordinary runs.
- `recovery inspect|plan|execute <identity>`, `interactions list|show|respond|cancel`, `completion status|reconcile`, and `capabilities` expose their canonical owners through the same application boundary.

Exit codes are `0` for successful/completed/waiting observations, `1` for failure, `2` for command-line errors, `3` for a stall, `4` for blocked/ambiguous/no-eligible work, and `130` for cancellation.

## Production certification

`LoopRelay.Certification` is a separate authority around the production CLI. It owns disposable cases, independent snapshots and oracles, privacy checks, retained evidence, cleanup, and production-derived coverage accounting. Component tests remain evidence level 1 and are never promoted to live certification.

Run the deterministic trust-root and public-control-surface certifications with:

```powershell
dotnet run --project src/LoopRelay.Certification -- canary --workspace . --cli src/LoopRelay.Cli/bin/Debug/net10.0/LoopRelay.Cli.dll
dotnet run --project src/LoopRelay.Certification -- milestone2 --workspace . --cli src/LoopRelay.Cli/bin/Debug/net10.0/LoopRelay.Cli.dll
```

Evidence is retained under `.tmp/certification/evidence`; disposable case directories are removed unless `--retain-case` is supplied.
