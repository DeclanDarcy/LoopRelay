# Architectural Mechanisms

Architectural mechanisms are executable or governed protections that make architecture invariants observable, verifiable, enforceable, recoverable, or easier to maintain.

## Verification Baseline

Introduced: Milestone 0.1.

Status: certified for local command-line verification with bounded quarantines.

Primary evidence:

- `.agents/milestones/m0.1-structural-verification-slice-0001.md`
- `.agents/milestones/m0.1-structural-verification-slice-0002.md`
- `.agents/milestones/m0.1-structural-verification-certification.md`

Accepted verifier entry points:

| Surface | Command | Notes |
| --- | --- | --- |
| .NET build | `dotnet build CommandCenter.slnx` | Run serially with backend tests. |
| Backend tests | `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` | Run serially with solution build. |
| TypeScript build | `npm run build` in `src/CommandCenter.UI` | Runs `tsc -b` before Vite. |
| Frontend lint | `npm run lint` in `src/CommandCenter.UI` | Static frontend verification. |
| Frontend tests | `npm run test` in `src/CommandCenter.UI` | Vitest characterization suite. |
| Browser E2E | `npm run test:e2e` in `src/CommandCenter.UI` | Playwright workspace coverage. |
| Rust build | `cargo build` in `src/CommandCenter.Shell` | Shell compiler health. |
| Rust tests | `cargo test` in `src/CommandCenter.Shell` | Test harness executes and includes shell behavior regressions for successful opaque JSON relay and boundary-violation error-envelope relay. |

## Current Quarantines

| Mechanism gap | Risk | Retirement condition |
| --- | --- | --- |
| Missing CI baseline | Local-only verification is not remotely enforced. | Add CI that runs the accepted baseline or an approved supported subset. |
| Serialized .NET execution | Parallel build/test runs can contend for shared outputs. | Prove isolated output paths or another build isolation mechanism. |
| Rust shell behavioral coverage partial | Shell transport can still drift outside the protected generic GET helper paths. | Add command-family classification, POST relay, non-boundary error semantics, and domain-mirror retirement coverage before shell behavior certification. |
| IDE verification path unknown | IDE feedback can disagree with command-line verification. | Inventory IDE validation or formally scope it outside verifier authority. |
| Tauri packaged release path unknown | Release packaging can fail outside local compile checks. | Add or quarantine a release verifier before release-path claims. |

## Passive Transport Invariants

Introduced: Milestone 0.1 follow-on preparation for Milestones 0.3, 1.2, and 1.3.

Status: seeded with executable Rust regressions for successful opaque JSON relay and boundary-violation error-envelope relay.

Primary evidence:

- `src/CommandCenter.Shell/src/main.rs` test `backend_get_value_relays_opaque_json_without_interpretation`
- `src/CommandCenter.Shell/src/main.rs` test `backend_get_value_preserves_boundary_violation_error_envelope`

Invariant matrix:

| Transport invariant | Current protection | Remaining gap |
| --- | --- | --- |
| Transport preserves payload semantics | Successful JSON responses that flow through `serde_json::Value` are compared as opaque values in the Rust shell test. Boundary-violation error envelopes are preserved as backend-owned JSON errors. | Domain-shaped command mirrors still exist and are not yet inventoried or retired. |
| Transport preserves unknown fields | The Rust shell test includes unknown nested objects, arrays, nulls, empty strings, empty arrays, and enum-like strings. | Protection currently covers the generic GET value helper only. |
| Transport preserves null and empty values | The Rust shell test asserts explicit null, empty object, empty array, and empty string preservation. | Additional command families still need migration or classification. |
| Transport preserves backend errors | Boundary-violation error envelopes returned through the generic GET value helper are serialized back to JSON and compared for preservation. | Non-boundary error semantics and additional command families still need migration or classification before passive transport certification. |

## Contract Oracle

Introduced: Milestone 0.2.

Status: defined and inventoried at the contract-family level; not yet executable protection.

Primary evidence:

- `docs/contracts.md`
- `docs/contract-endpoint-catalog.md`
- `.agents/milestones/m0.2-contract-inventory-slice-0006.md`
- `.agents/milestones/m0.2-contract-endpoint-catalog-slice-0007.md`

Mechanism intent:

| Oracle mechanism | Current protection | Remaining gap |
| --- | --- | --- |
| Canonical contract authority | `docs/contracts.md` defines contract truth as backend-owned projection and command-result shape after backend JSON serialization. | Endpoint-level and field-level ownership still need inventory. |
| Endpoint and consumer visibility | `docs/contract-endpoint-catalog.md` catalogs 177 backend endpoint mappings by family, defines required endpoint-level inventory fields, identifies consumer classes, and records priority fixture candidates. | Field-level ownership, exact backend JSON options, and a full dependency graph still need inventory. |
| Fixture gating | `docs/contracts.md` forbids golden fixtures before identity, owner, producer, consumers, parallel representations, compatibility, and serialization rules are known. `docs/contract-endpoint-catalog.md` adds narrow serialization rules that fixtures must observe. | No fixtures exist yet. |
| Parallel truth visibility | Initial matrix identifies backend, Rust, TypeScript, API wrapper, mock, test, and docs surfaces that can drift. Endpoint catalog records compatibility consumer classes. | No automated stale-artifact or drift comparison exists yet. |
| Oracle drift detection | Not active. | Add golden fixtures and recursive backend serialization comparison tests. |

## Mechanism Lifecycle Rule

A verifier can be treated as architectural protection only when its command or source, protected surface, known gaps, owner, and retirement criteria for any quarantine are recorded.

Weakening, removing, or bypassing an accepted verifier requires a decision record and replacement or quarantine evidence.
