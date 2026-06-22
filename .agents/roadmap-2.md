# Epic 5 — Session Continuity

## Objective

Provide reliable continuity across decision-making session boundaries while preserving project understanding, minimizing drift, and enabling long-running projects to survive hundreds of session transitions.

This epic does **not** exist to preserve execution continuity.

Execution sessions are intentionally disposable.

This epic exists solely to preserve:

```text
Project understanding
Architectural understanding
Decision-making continuity
```

across decision-session replacement events.

---

# Architectural Principles

## Execution Continuity Is Out Of Scope

Execution always follows:

```text
ExecutionContextResolution
        ↓
New Execution Session
        ↓
Execution
        ↓
Handoff
```

No reuse.

No continuity management.

No session transfer.

---

## Decision Continuity Is In Scope

Decision-making follows:

```text
DecisionContextResolution
        ↓
SessionRouter
        ↓
Reuse Existing Session
         OR
Create New Session
```

When:

```text
Create New Session
```

occurs, continuity transfer becomes necessary.

---

## Operational Context Authority

```text
operational_context.md
```

becomes the authoritative continuity artifact.

Purpose:

> Preserve the minimum information required for a newly-created decision session to regain full project understanding.

It is not:

```text
Execution state
Execution history
Project archive
```

---

# M0 — Continuity Domain Foundation

## Objective

Define continuity concepts and boundaries.

---

## Scope

### Continuity Models

Define:

```csharp
OperationalContext
ContinuitySnapshot
ContextRevision
ContextTransferResult
```

---

### Continuity Boundaries

Document:

```text
Execution continuity
Decision continuity
Operational context
Session transfer
```

---

### Persistence Contracts

Support:

```text
operational_context.md
revision history
```

---

## Exit Criteria

* Continuity domain established
* Contracts implemented
* Tests passing

---

# M1 — Operational Context Infrastructure

## Objective

Introduce operational context as a first-class artifact.

---

## Scope

### Artifact Support

Recognize:

```text
.agents/operational_context.md
```

---

### Loading

Load current operational context.

---

### Saving

Persist updates.

---

### History Tracking

Maintain revision history.

---

### Version Visibility

Allow viewing prior revisions.

---

## Exit Criteria

* Artifact supported
* Persistence works
* Revision history available

---

# M2 — Session Registry Integration

## Objective

Provide continuity-relevant session metadata.

---

## Scope

### Registry Integration

Consume:

```text
Decision session metadata
```

from Session Registry.

---

### Continuity Metadata

Track:

```text
Session age
Token usage
Last activity
```

---

### Repository Association

Associate continuity state with repository.

---

## Exit Criteria

* Metadata available
* Repository association works

---

# M3 — Session Replacement Detection

## Objective

Detect when continuity transfer is required.

---

## Scope

### Router Integration

Receive:

```text
Reuse Session
Create New Session
```

outcomes.

---

### Continuity Trigger

Only trigger continuity when:

```text
Create New Session
```

occurs.

---

### Diagnostics

Record:

```text
Why transfer occurred
```

---

## Exit Criteria

* Detection works
* Diagnostics available

---

# M4 — Context Consolidation Engine

## Objective

Generate updated operational context.

---

## Scope

### Consolidation Inputs

Consume:

```text
Current operational_context.md

Current handoff.md

Current decisions.md

Decision discussion

Project artifacts
```

---

### Consolidation Output

Produce:

```text
Proposed operational_context.md
```

---

### Preservation Goals

Ensure:

```text
Relevant information retained

Obsolete information removed

Future continuity supported
```

---

## Exit Criteria

* Consolidation works
* Proposed context generated

---

# M5 — Context Review Workspace

## Objective

Provide human oversight over continuity transfer.

---

## Scope

### Current Context Viewer

Display:

```text
Current operational_context.md
```

---

### Proposed Context Viewer

Display:

```text
Proposed operational_context.md
```

---

### Diff View

Display:

```text
Added
Removed
Modified
```

content.

---

### Editing

Allow manual revision.

---

## Exit Criteria

* Review workflow operational
* Editing supported
* Diff supported

---

# M6 — Context Acceptance Workflow

## Objective

Establish authoritative continuity transfer.

---

## Scope

### Acceptance

Support:

```text
Accept
```

---

### Rejection

Support:

```text
Reject
```

---

### Manual Revision

Support:

```text
Edit
Accept
```

workflow.

---

### Persistence

Accepted context becomes authoritative.

---

## Exit Criteria

* Acceptance workflow complete
* Authority transfer works

---

# M7 — New Session Bootstrap

## Objective

Use accepted operational context when creating new decision sessions.

---

## Scope

### Bootstrap Context

Inject:

```text
operational_context.md
```

into decision context resolution.

---

### Startup Validation

Verify:

```text
Operational context loaded
```

before session launch.

---

### Launch Diagnostics

Record:

```text
Context size
Bootstrap success
```

---

## Exit Criteria

* New sessions consume context
* Bootstrap succeeds

---

# M8 — Continuity Assessment

## Objective

Evaluate whether continuity transfer was successful.

---

## Scope

### Session Startup Review

Capture observations:

```text
Repeated questions

Lost decisions

Missing context

Incorrect assumptions
```

---

### Continuity Outcomes

Record:

```text
Successful

Partially Successful

Failed
```

---

### Observation Storage

Persist continuity observations.

---

## Exit Criteria

* Continuity assessments recorded
* Observations preserved

---

# M9 — Continuity Analytics

## Objective

Generate evidence useful for Brainstorm research.

---

## Scope

### Metrics

Track:

```text
Session transfers

Context revisions

Acceptance rate

Context size growth

Continuity outcomes
```

---

### Trend Analysis

Track:

```text
Context expansion

Context contraction

Transfer frequency
```

---

### Reporting

Generate continuity reports.

---

## Exit Criteria

* Metrics available
* Reports available

---

# M10 — Session Continuity Certification

## Objective

Certify continuity behavior across long-running projects.

---

## Scope

### Long-Horizon Testing

Validate:

```text
Many session transitions
```

across:

```text
Axiom
Vector
FrontendCompiler
```

or equivalent repositories.

---

### Transfer Testing

Validate:

```text
Operational context generation

Review

Acceptance

Bootstrap
```

---

### Failure Recovery

Validate:

```text
Application restart

Interrupted transfer

Context rejection
```

---

### Research Validation

Verify:

```text
Continuity observations preserved
```

for future Brainstorm analysis.

---

## Exit Criteria

Session Continuity is certified when:

* New decision sessions can regain project understanding
* Continuity transfer survives restarts
* Operational context remains maintainable
* User-controlled review is enforced
* Continuity observations are captured
* Long-running projects remain operable across repeated session replacement events

---

# Final Architectural Outcome

```text
Decision Session
        ↓
Session Router
        ↓
Create New Session
        ↓
Context Consolidation
        ↓
Proposed operational_context.md
        ↓
User Review
        ↓
Accepted operational_context.md
        ↓
Decision Context Resolution
        ↓
New Decision Session
```

The final outcome is not session persistence.

The final outcome is reliable transfer of project understanding across decision-session boundaries through a user-governed continuity artifact that evolves with the project while remaining concise, relevant, and useful for future reasoning.
