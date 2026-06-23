# Reasoning Repository Contracts

Reasoning artifacts are repository-scoped files under `.agents/reasoning`. The repository filesystem remains the source of truth. Runtime memory, graph indexes, and query results are rebuildable.

## Repository Layout

Initial durable substrate:

```text
.agents/
  reasoning/
    events/
      EVT-0001/
        event.json
        event.md
    threads/
      THR-0001/
        thread.json
        thread.md
    relationships/
      REL-0001/
        relationship.json
        relationship.md
    reports/
      reconstruction.YYYYMMDDHHMMSSFFFFFFF.json
      reconstruction.YYYYMMDDHHMMSSFFFFFFF.md
      certification.YYYYMMDDHHMMSSFFFFFFF.json
      certification.YYYYMMDDHHMMSSFFFFFFF.md
```

Do not initially create:

```text
.agents/reasoning/hypotheses/
.agents/reasoning/alternatives/
.agents/reasoning/contradictions/
.agents/reasoning/directions/
.agents/reasoning/graph/
.agents/reasoning/queries/
```

## Structured Source And Markdown Projection

`event.json`, `thread.json`, and `relationship.json` are structured records. Their matching Markdown files are deterministic human-readable projections.

Markdown projections are not authority. Services must derive behavior from structured records and existing domain artifacts.

Reports are optional persisted outputs created only when a user explicitly asks to persist a reconstruction or certification result.

## Schema And Recovery

Reasoning JSON records must use a schema-version envelope and deterministic serialization with string enums. Unsupported schema versions must be rejected.

Graph state must be rebuildable from events, threads, relationships, and existing domain references. If a graph cache is later added, it must be documented as derived data, not repository authority.

Corrupt required reasoning records should fail integrity checks. Corrupt optional reports may be skipped in listing operations when diagnostics identify the skipped report.

## IDs

IDs are repository-scoped, human-inspectable, sequence allocated by scanning existing artifact directories, and stable across restart.

Initial prefixes:

```text
EVT-0001  Reasoning event
THR-0001  Reasoning thread
REL-0001  Reasoning relationship
```

Report IDs:

```text
reconstruction.YYYYMMDDHHMMSSFFFFFFF
certification.YYYYMMDDHHMMSSFFFFFFF
```

## Path Safety

Reasoning persistence must use repository-relative paths and `ArtifactPath.ResolveRepositoryPath` for path safety. Absolute paths, path traversal, and repository escapes must be rejected.

Services must validate repository ownership on every loaded or saved reasoning artifact.

## Separation From Existing Artifacts

`.agents/reasoning` is separate from `.agents/decisions` and `.agents/operational_context.md`.

Decision artifacts remain the decision record. Operational context remains current settled understanding. Reasoning artifacts preserve event-led explanatory history and may reference those artifacts without owning or replacing them.

## Artifact Discovery

Generic artifact discovery should include human-readable reasoning projections and reports only:

```text
.agents/reasoning/events/*/event.md
.agents/reasoning/threads/*/thread.md
.agents/reasoning/relationships/*/relationship.md
.agents/reasoning/reports/reconstruction.*.md
.agents/reasoning/reports/certification.*.md
```

Structured JSON should remain outside the generic artifact editor until a typed editor exists.
