# Epic 4 — Decision Engine

## Objective

Establish a dedicated decision-making workflow that is distinct from execution.

Execution is responsible for:

```text
Performing work
```

Decision-making is responsible for:

```text
Understanding project state
Identifying ambiguities
Evaluating tradeoffs
Producing decisions
Guiding future execution
```

The Decision Engine owns decision generation, review, resolution, and continuity across long-running projects.

---

# Architectural Principles

## Responsibility Separation

Execution:

```text
Execution Context Resolution
↓
Fresh Session
↓
Implementation
↓
Handoff
```

Decision-Making:

```text
Decision Context Resolution
↓
Session Router
↓
Decision Session
↓
Decisions
```

Execution sessions are disposable.

Decision sessions may be reused.

---

## Artifact Authority

Decision Engine consumes:

```text
plan.md
milestones/*.md
handoff.md
operational_context.md
```

Decision Engine produces:

```text
decisions.md
```

---

## Human Authority

The Decision Engine may:

```text
Recommend
Explain
Evaluate
```

It may not:

```text
Approve
Override
```

Human approval remains authoritative.

---

# M0 — Decision Domain Foundation

## Objective

Establish core decision concepts, contracts, and persistence.

---

## Scope

### Decision Models

Define:

```csharp
Decision
DecisionOption
DecisionRecommendation
DecisionResolution
```

---

### Decision States

Support:

```text
Pending
Accepted
Rejected
Superseded
Resolved
```

---

### Decision Repository

Persist:

```text
current decisions
historical decisions
decision metadata
```

---

### Decision Artifact Support

Recognize:

```text
.agents/decisions/decisions.md
```

---

### Artifact Rotation

Support:

```text
decisions.md
decisions.0001.md
decisions.0002.md
...
```

---

## Exit Criteria

* Decision domain models implemented
* Persistence implemented
* Rotation implemented
* Tests passing

---

# M1 — Decision Context Resolution

## Objective

Construct decision-making context from project artifacts.

---

## Scope

### Context Sources

Consume:

```text
plan.md
active milestone
handoff.md
operational_context.md
```

---

### Context Assembly

Build:

```text
DecisionContext
```

containing:

```text
Current state
Current milestone
Recent execution outcomes
Open decisions
Operational understanding
```

---

### Context Validation

Verify:

```text
Required artifacts present
Required artifacts readable
```

---

### Context Inspection

Display assembled context before launch.

---

## Exit Criteria

* Context generation works
* Context inspection works
* Validation works

---

# M2 — Decision Session Integration

## Objective

Enable creation and execution of decision-making sessions.

---

## Scope

### Session Launch

Launch decision session.

---

### Session Monitoring

Track:

```text
Running
Completed
Failed
Cancelled
```

---

### Output Capture

Capture:

```text
Decision recommendations
Decision packages
Reasoning output
```

---

### Failure Handling

Handle:

```text
Session failure
Provider failure
Timeouts
```

---

## Exit Criteria

* Decision sessions launch
* Output captured
* Failures handled

---

# M3 — Decision Artifact Generation

## Objective

Generate structured decision artifacts.

---

## Scope

### Decision Extraction

Generate:

```text
decisions.md
```

---

### Required Sections

Each decision contains:

```text
Title
Context
Options
Recommendation
Impact
Blocking Status
```

---

### Multi-Decision Support

Support:

```text
Multiple decisions per session
```

---

### Decision Validation

Verify:

```text
Required sections present
```

---

## Exit Criteria

* Decisions generated
* Decisions validated
* Artifact persisted

---

# M4 — Decision Workspace

## Objective

Provide a dedicated environment for reviewing decisions.

---

## Scope

### Decision Viewer

Display:

```text
Full decisions.md
```

No truncation.

---

### Decision Navigation

Support:

```text
Single decision
Multiple decisions
```

---

### Recommendation Visibility

Highlight:

```text
Recommendation
Impact
```

while preserving full artifact visibility.

---

### Artifact History

Access:

```text
Previous decision artifacts
```

---

## Exit Criteria

* Decisions reviewable
* History accessible
* Workspace functional

---

# M5 — Interactive Decision Refinement

## Objective

Allow the user to engage with decision-making before resolution.

---

## Scope

### Decision Conversation

Support:

```text
Clarifications
Constraint changes
Priority changes
Tradeoff exploration
```

---

### Session Continuation

Continue discussion inside decision session.

---

### Regeneration

Allow:

```text
Update recommendation
Update options
```

based on discussion.

---

## Exit Criteria

* User can refine decisions
* Recommendations can evolve
* History preserved

---

# M6 — Decision Resolution

## Objective

Resolve decisions and persist outcomes.

---

## Scope

### Resolution Actions

Support:

```text
Accept
Reject
```

---

### Resolution Recording

Persist:

```text
Outcome
Timestamp
```

---

### Artifact Updates

Update:

```text
decisions.md
```

to reflect resolution.

---

### Execution Integration

Resolved decisions become available to:

```text
Execution Context Resolution
```

---

## Exit Criteria

* Decisions resolvable
* Outcomes persisted
* Execution receives outcomes

---

# M7 — Decision Session Registry

## Objective

Track decision sessions independently from execution.

---

## Scope

### Session Metadata

Track:

```text
Session ID
Created
Last Active
Status
```

---

### Token Metadata

Track:

```text
Input
Output
Total
```

---

### Repository Association

Associate sessions with repositories.

---

### Persistence

Restore registry after restart.

---

## Exit Criteria

* Registry operational
* Metadata persisted
* Recovery works

---

# M8 — Session Router

## Objective

Determine whether an existing decision session should be reused.

---

## Scope

### Router Inputs

Evaluate:

```text
Session age
Token consumption
Session availability
```

---

### Router Outcomes

Support:

```text
Reuse Existing Session
Create New Session
```

---

### Routing Diagnostics

Record:

```text
Why a decision was made
```

---

## Exit Criteria

* Routing functional
* Reuse supported
* Diagnostics available

---

# M9 — Operational Context Integration

## Objective

Support continuity transfer when a new decision session is required.

---

## Scope

### Operational Context Consumption

Consume:

```text
operational_context.md
```

during decision context resolution.

---

### Context Consolidation Trigger

Trigger only when:

```text
Session Router
↓
Create New Session
```

---

### Context Review Workflow

Display:

```text
Current operational_context.md
Proposed operational_context.md
Diff
```

---

### User Acceptance

Support:

```text
Accept
Edit
Reject
```

---

## Exit Criteria

* Continuity transfer works
* Review workflow works
* New sessions start with accepted context

---

# M10 — Decision Engine Certification

## Objective

Certify the complete decision workflow.

---

## Scope

### End-to-End Validation

Validate:

```text
Handoff
↓
Decision Context Resolution
↓
Decision Session
↓
Decision Generation
↓
Decision Review
↓
Decision Resolution
↓
Execution Context Consumption
```

---

### Recovery Testing

Validate:

```text
Restart recovery
Session recovery
Artifact recovery
```

---

### Multi-Repository Testing

Validate:

```text
Multiple repositories
Independent decision histories
Independent session routing
```

---

## Exit Criteria

Decision Engine is certified when:

* Decisions can be generated
* Decisions can be refined
* Decisions can be resolved
* Session reuse works
* New-session continuity works
* Execution receives resolved outcomes
* Workflow survives application restart

---

# Final Architectural Outcome

```text
Execution Engine
        ↓
handoff.md
        ↓
Decision Context Resolution
        ↓
Session Router
        ↓
Decision Session
        ↓
decisions.md
        ↓
User Resolution
        ↓
Execution Context Resolution
        ↓
Execution Engine
```

The Decision Engine becomes the project's long-horizon reasoning layer, while execution remains a sequence of disposable implementation sessions. This separation allows execution quality to remain high while preserving continuity and project understanding within the decision-making workflow.
