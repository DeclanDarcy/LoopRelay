# Decisions

## Newly Authorized Decisions

- M0 is certified and closed.
- The full React to Tauri IPC to shell command to .NET backend to `/api/ping` to UI `Pong` runtime chain is accepted as verified.
- The shell startup to backend sidecar spawn to health wait to normal operation to window close to backend termination lifecycle is accepted as verified.
- Runtime verification through the compiled shell executable is acceptable for certification, despite `cargo tauri dev` being unavailable.
- The current sidecar ownership boundary is architecturally clean: shell owns process lifecycle, backend startup/shutdown, IPC bridge, and native capabilities; backend owns repository/artifact/planning/configuration/projection business logic; UI owns rendering, view state, and user interaction.
- M1 is ready to begin immediately.
- M1 should start with Slice 1 before validation, availability projection, or UI work.
- M1 Slice 1 scope is repository model, application configuration model, configuration persistence, `IRepositoryService` skeleton, repository add/remove, and restart persistence tests.
- Repository path normalization should be locked down early, including tests for case differences, trailing slashes, relative paths, and mixed separators.
- Establishing `.agents/decisions/decisions.md` for Command Center itself is approved for repository governance going into M1.
