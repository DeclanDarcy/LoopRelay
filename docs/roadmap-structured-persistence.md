# Roadmap Structured Persistence

## Inventory

The Roadmap CLI previously had authoritative Markdown stores that were loaded to reconstruct runtime state:

- `.agents/state.md`: roadmap workflow state, last transition, transition intent, blockers, retired epics, projection counts, and next transitions.
- `.agents/projections/manifest.md`: projection provenance, causal inputs, hashes, validation status, stale status, and validation diagnostics.
- `.agents/artifacts/lifecycle.md`: artifact lifecycle state, timestamps, and notes.
- `.agents/decision-ledger.md`: decision IDs and append-only decision metadata used by state summaries.
- `.agents/splits/split-family-*.md`: split-family metadata used to validate split-child promotion.

The runtime also writes other persisted artifacts:

- `.agents/journal/transitions.jsonl`, `.agents/execution-preparation-manifest.json`, and `.agents/selection-provenance-manifest.json` are already structured persistence.
- Bundle manifests, prompt-contract snapshots, projections, prompts, and evidence files remain human-facing logs, generated artifacts, or prompt outputs. They are not canonical workflow-state stores.

## Persistence Model

The canonical stores are versioned JSON documents:

- `.agents/state.json`
- `.agents/projections/manifest.json`
- `.agents/artifacts/lifecycle.json`
- `.agents/decision-ledger.json`
- `.agents/splits/split-family-*.json`

Each store owns explicit DTOs, schema-version validation, deterministic serialization, and conversion to and from the domain objects used by the roadmap runtime. The Roadmap CLI no longer writes Markdown shadows for these stores; engineers inspect the canonical JSON directly.

## Migration

Load order is:

1. Load canonical JSON when present.
2. If JSON is absent, parse legacy Markdown as migration input.
3. Validate the reconstructed DTO.
4. Persist canonical JSON.
5. Continue using the canonical JSON document for runtime behavior.

Malformed legacy Markdown fails the load instead of being silently migrated. Once JSON exists, Markdown is not parsed for normal runtime behavior.

## Inspection

Runtime correctness and operator inspection both depend on the structured JSON documents, not table delimiters, escaping, or presentation syntax. Legacy Markdown files may remain on disk after migration, but they are not rewritten as shadows.
