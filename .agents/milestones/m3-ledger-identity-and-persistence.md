# Milestone 3 - Ledger Identity And Persistence

## Objective

make semantic disposition identity durable before semantic confirmation depends on it.

## Work
- [x] Expand ledger records with:
  - [x] schema version
  - [x] entry ID
  - [x] execution slice ID or discovery context
  - [x] path
  - [x] previous path for renames when available
  - [x] baseline status
  - [x] post status
  - [x] content hash reviewed, or deleted marker plus baseline hash for deleted files
  - [x] baseline content hash when available
  - [x] `PreExisted` flag
  - [x] deterministic classification route and evidence
  - [x] semantic disposition, nullable until confirmation
  - [x] semantic rationale and evidence
  - [x] classifier version
  - [x] confirmation prompt source hash
  - [x] first seen and last seen timestamps
  - [x] HITL provenance kind: `None`, `HitlRequested`, `HitlKept`
  - [x] HITL provenance evidence path or excerpt when available
  - [x] resolution state: `Unresolved`, `HitlKept`, `HitlDeleted`, `HitlFalsePositive`, `HitlDeferred`
  - [x] human decision metadata when present
- [x] Keep confirmed, false-positive, and semantically uncertain entries distinguishable in the same JSON document. Expose query methods that return them separately.
- [x] Add duplicate suppression rules:
  - [x] skip semantic confirmation only when path, reviewed content hash or deleted-reviewed identity, classifier version, and confirmation prompt source hash match a valid existing semantic disposition
  - [x] re-confirm when content changes, path identity changes, classifier version changes, or prompt source hash changes
  - [x] never skip solely because a path appeared in the ledger before
- [x] Add request-capture hooks that can attach explicit HITL request evidence from a structured plan/decision marker or later completion decision. Do not infer HITL request evidence from plan prose, agent-authored decisions, or deliverable names.
- [x] Add tests:
  - [x] writes schema version
  - [x] records pending, confirmed, false-positive, and semantically uncertain entries separately
  - [x] same path/hash/version suppresses duplicate confirmation
  - [x] changed hash requires confirmation
  - [x] path-only match does not suppress confirmation
  - [x] HITL request kind and evidence are durable
  - [x] invalid schema blocks with a clear error

## Detail Notes

The ledger is repository-local review state, not a knowledge database, commit gate, or repository acceptance record.

Minimum ledger entry fields:

- schema version
- entry ID
- execution slice ID or discovery context
- path
- previous path for renames when available
- baseline status
- post status
- reviewed content SHA-256, or deleted marker plus baseline hash for deleted files
- baseline content SHA-256 when available
- `PreExisted`
- deterministic classification route and evidence
- semantic disposition, nullable until confirmed
- semantic rationale and evidence
- classifier version
- confirmation prompt source hash
- first seen timestamp
- last seen timestamp
- HITL provenance kind
- HITL provenance evidence path or excerpt
- resolution state
- human decision metadata when present

Keep confirmed non-implementation entries, false positives, and semantic uncertainties distinguishable in the same JSON document. Expose query methods that return them separately.

Duplicate suppression must be exact:

- Skip semantic confirmation only when path, reviewed content hash or deleted-reviewed identity, classifier version, and confirmation prompt source hash match a valid existing semantic disposition.
- Re-confirm when content changes, path identity changes, classifier version changes, or prompt source hash changes.
- Never skip solely because a path appeared in the ledger before.

Invalid schema should fail with a clear error. Do not silently discard or rewrite unknown ledger state.

## Acceptance
- [x] Ledger state is repository-local and durable before semantic confirmation integration.
- [x] False positives are never merged into confirmed review state.
- [x] Known unchanged files avoid repeated semantic confirmation only through hash/version identity.
- [x] Later synthesis and HITL review can consume ledger state directly.
