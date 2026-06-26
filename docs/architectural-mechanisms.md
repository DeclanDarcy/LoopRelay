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
| Rust tests | `cargo test` in `src/CommandCenter.Shell` | Test harness executes and includes the first shell behavior regression for successful opaque JSON relay. |

## Current Quarantines

| Mechanism gap | Risk | Retirement condition |
| --- | --- | --- |
| Missing CI baseline | Local-only verification is not remotely enforced. | Add CI that runs the accepted baseline or an approved supported subset. |
| Serialized .NET execution | Parallel build/test runs can contend for shared outputs. | Prove isolated output paths or another build isolation mechanism. |
| Rust shell behavioral coverage partial | Shell transport can still drift outside the protected successful opaque JSON GET helper path. | Add command-family classification, error-envelope relay, POST relay, and domain-mirror retirement coverage before shell behavior certification. |
| IDE verification path unknown | IDE feedback can disagree with command-line verification. | Inventory IDE validation or formally scope it outside verifier authority. |
| Tauri packaged release path unknown | Release packaging can fail outside local compile checks. | Add or quarantine a release verifier before release-path claims. |

## Passive Transport Invariants

Introduced: Milestone 0.1 follow-on preparation for Milestones 0.3, 1.2, and 1.3.

Status: seeded with one executable Rust regression for successful opaque JSON relay.

Primary evidence:

- `src/CommandCenter.Shell/src/main.rs` test `backend_get_value_relays_opaque_json_without_interpretation`

Invariant matrix:

| Transport invariant | Current protection | Remaining gap |
| --- | --- | --- |
| Transport preserves payload semantics | Successful JSON responses that flow through `serde_json::Value` are compared as opaque values in the Rust shell test. | Domain-shaped command mirrors still exist and are not yet inventoried or retired. |
| Transport preserves unknown fields | The Rust shell test includes unknown nested objects, arrays, nulls, empty strings, empty arrays, and enum-like strings. | Protection currently covers the generic GET value helper only. |
| Transport preserves null and empty values | The Rust shell test asserts explicit null, empty object, empty array, and empty string preservation. | Additional command families still need migration or classification. |
| Transport preserves backend errors | Not yet protected. | Add error-envelope relay regression before claiming passive transport certification. |

## Mechanism Lifecycle Rule

A verifier can be treated as architectural protection only when its command or source, protected surface, known gaps, owner, and retirement criteria for any quarantine are recorded.

Weakening, removing, or bypassing an accepted verifier requires a decision record and replacement or quarantine evidence.
