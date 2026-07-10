# LoopRelay

LoopRelay is currently a CLI-oriented project for running and observing automation loops.

Today, the CLI experience is mostly run-and-spectate: start a run, watch the output, and inspect the resulting artifacts after the loop advances. That behavior is useful, but it is not the long-term ceiling for the project.

The project intends to eventually incorporate richer interactive features directly into the CLI so the operator can participate more actively while a loop is running. Future CLI work should move beyond passive observation toward workflows that support live inspection, guided decisions, and interactive control where that improves the loop.

## Runtime Persistence

LoopRelay stores local runtime state under `.LoopRelay/`. The directory is local-only runtime state and is protected by a create-only `.LoopRelay/.gitignore` containing `*`.

Structured runtime state is canonical in `.LoopRelay/persistence/looprelay.sqlite3`. Decision-session resume state is stored in SQLite, with one-time import of an existing `.LoopRelay/decision-session.json` followed by deletion of that legacy file. Session telemetry events are also stored canonically in SQLite.

Telemetry JSONL files under `.LoopRelay/telemetry/sessions.YYYY-MM-DD.NNNN.jsonl` remain as a compatibility export for existing diagnostics tooling; they are not the canonical telemetry store.

Runtime switches remain unchanged:

- `LoopRelay_DECISION_RESUME=0` or `false` disables resume and automatic replacement without deleting the active pointer, lineage, or recovery journal. If an active decision session exists, Execute stops with `ContinuityDisabled`; it never silently starts clean.
- `LoopRelay_DECISION_RECOVERY_POLICY=resume-only|reconstructed|certified` controls which already-implemented recovery mechanisms may run. The default is `resume-only`. `reconstructed` enables verified public/repository reconstruction; `certified` additionally permits a native fork only for an exact supported compatibility profile.
- Codex continuity is fail-closed by exact version and app-server schema digest. Unlisted profiles do not emit resume/read/fork requests.
- The checked-in `0.142.5` profile currently certifies exact-ID resume/read only. Reconstruction and native fork remain profile-gated until authenticated context-write/limit evidence and lost-response fork reconciliation are certified.
- `LoopRelay_SESSION_LOG=0` or `false` disables telemetry recording.

Roadmap storage verification validates the SQLite runtime rows when present and reports corrupt telemetry/resume rows or legacy resume files that conflict with canonical SQLite state.
