# Handoff

## New State This Slice

- M0 desktop runtime certification is complete.
- `src/CommandCenter.Shell/src/main.rs` now starts the .NET backend sidecar during Tauri setup, waits for `/api/ping`, exposes `ping_backend`, and stops the backend when the desktop window closes.
- `src/CommandCenter.Shell/tauri.conf.json` now builds `CommandCenter.Backend` before Tauri dev/build frontend commands so the debug backend executable exists for shell launch.
- `docs/architecture.md` now states the M0 shell-owned backend start/stop behavior.
- `.agents/milestones/m0-architecture-ratification.md` now has all acceptance criteria checked.
- Previous handoff was archived as `.agents/handoffs/handoff.0001.md`.

## Verification

- `dotnet test CommandCenter.slnx` passes: 6 tests.
- `npm --prefix src/CommandCenter.UI run build` passes.
- `cargo build` passes in `src/CommandCenter.Shell`.
- Runtime certification used Vite plus the compiled debug shell executable because `cargo tauri dev` is not installed in this environment.
- Desktop app launched, React rendered, clicking `Ping Backend` displayed `Pong`.
- Shell startup spawned `CommandCenter.Backend`; closing the desktop window terminated both shell and backend processes.

## Immediate Gaps

- No commit or push was performed in this slice.
- `cargo tauri dev` remains unavailable unless the Tauri CLI is installed or invoked through another project script.
- M1 can now start; M0 is no longer the blocker.
