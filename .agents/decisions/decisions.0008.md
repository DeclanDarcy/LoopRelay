# Decisions

## Newly Authorized Decisions

- M3 implementation scope is complete.
- M3 automated tests are complete.
- M3 manual certification remains pending.
- M3 remaining work is validation, not architecture.
- Before M4, run an explicit certification pass covering repository lifecycle, artifact infrastructure, lifecycle rotation, refresh behavior, and restart recovery.
- Repository lifecycle certification should verify repository registration, removal, application restart, and repository restoration.
- Artifact infrastructure certification should verify artifact open, edit, save, refresh, restart, and persisted changes.
- Lifecycle certification should verify handoff and decision rotation create sequential historical files while leaving current artifacts and existing historical files unchanged.
- Refresh certification should verify externally added artifacts appear after refresh and externally deleted artifacts disappear after refresh.
- Restart recovery certification should verify workspace rebuild, artifact rediscovery, and repository restoration after closing and reopening the app.
- M4 is ready to start only after certification passes.
- M4 should remain isolated to planning/readiness concerns and should not revisit repository, artifact, lifecycle, or refresh infrastructure unless certification exposes a defect.
- M4 implementation order should be backend planning model, plan discovery, milestone discovery, readiness determination, projection integration, then UI rendering.
- M4 must not introduce execution behavior.
- Current epic status is M0 certified; M1 ready for certification; M2 ready for certification; M3 implementation and automated tests complete with manual certification pending; M4 ready to start after certification.
