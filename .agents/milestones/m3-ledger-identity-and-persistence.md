# Milestone 3 - Ledger Identity And Persistence

## Objective

make semantic disposition identity durable before semantic confirmation depends on it.

## Work
- [ ] Expand ledger records with:
  - [ ] schema version
  - [ ] entry ID
  - [ ] execution slice ID or discovery context
  - [ ] path
  - [ ] previous path for renames when available
  - [ ] baseline status
  - [ ] post status
  - [ ] content hash reviewed, or deleted marker plus baseline hash for deleted files
  - [ ] baseline content hash when available
  - [ ] `PreExisted` flag
  - [ ] deterministic classification route and evidence
  - [ ] semantic disposition, nullable until confirmation
  - [ ] semantic rationale and evidence
  - [ ] classifier version
  - [ ] confirmation prompt source hash
  - [ ] first seen and last seen timestamps
  - [ ] HITL provenance kind: `None`, `HitlRequested`, `HitlKept`
  - [ ] HITL provenance evidence path or excerpt when available
  - [ ] resolution state: `Unresolved`, `HitlKept`, `HitlDeleted`, `HitlFalsePositive`, `HitlDeferred`
  - [ ] human decision metadata when present
- [ ] Keep confirmed, false-positive, and semantically uncertain entries distinguishable in the same JSON document. Expose query methods that return them separately.
- [ ] Add duplicate suppression rules:
  - [ ] skip semantic confirmation only when path, reviewed content hash or deleted-reviewed identity, classifier version, and confirmation prompt source hash match a valid existing semantic disposition
  - [ ] re-confirm when content changes, path identity changes, classifier version changes, or prompt source hash changes
  - [ ] never skip solely because a path appeared in the ledger before
- [ ] Add request-capture hooks that can attach explicit HITL request evidence from a structured plan/decision marker or later completion decision. Do not infer HITL request evidence from plan prose, agent-authored decisions, or deliverable names.
- [ ] Add tests:
  - [ ] writes schema version
  - [ ] records pending, confirmed, false-positive, and semantically uncertain entries separately
  - [ ] same path/hash/version suppresses duplicate confirmation
  - [ ] changed hash requires confirmation
  - [ ] path-only match does not suppress confirmation
  - [ ] HITL request kind and evidence are durable
  - [ ] invalid schema blocks with a clear error

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
- [ ] Ledger state is repository-local and durable before semantic confirmation integration.
- [ ] False positives are never merged into confirmed review state.
- [ ] Known unchanged files avoid repeated semantic confirmation only through hash/version identity.
- [ ] Later synthesis and HITL review can consume ledger state directly.
