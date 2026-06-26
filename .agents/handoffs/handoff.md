# Handoff: 2026-06-26 Slice 0004

Current milestone state: Milestone 0.1 is accepted; this slice completed the authorized shell passivity preparation before Milestone 0.2.

New state from this slice:

- Added Rust shell regression `backend_get_value_relays_opaque_json_without_interpretation` in `src/CommandCenter.Shell/src/main.rs`.
- Added testable helper `backend_get_value_from`; production `backend_get_value` still uses `BACKEND_URL`.
- Updated `docs/architectural-mechanisms.md` with a passive transport invariant matrix.
- Updated `docs/architectural-capabilities.md` so Rust shell behavioral coverage is partial instead of absent.
- Added evidence package `.agents/milestones/m0.1-shell-passivity-prep-slice-0004.md`.
- Rotated previous active handoff to `.agents/handoffs/handoff.0003.md`.

Verified:

- `cargo fmt` in `src/CommandCenter.Shell`.
- `cargo test` in `src/CommandCenter.Shell`: 1 passed.
- `cargo build` in `src/CommandCenter.Shell`.

Current limits:

- Shell passivity is protected only for successful opaque JSON relay through the generic GET value helper.
- Backend error-envelope preservation, generic POST relay, command-family classification, and domain-shaped Rust mirror retirement remain open.

Next suggested slice:

- Add a backend error-envelope relay regression in the Rust shell before starting the Contract Oracle implementation, then begin Milestone 0.2 with contract surface inventory and fixture pilot.
