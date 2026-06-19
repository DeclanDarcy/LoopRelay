# Command Center Epic 1 Implementation Plan

## Objective

Build the foundational Command Center application for repository, artifact, plan, milestone, handoff, and decision-history management.

At completion, Command Center must be able to answer:

- What repositories are registered?
- Which repositories are available on disk?
- What artifacts exist in each repository?
- Which handoff and decision artifacts are current?
- Which handoff and decision artifacts are historical?
- Does the repository have a plan?
- Does the repository have milestones?
- Is the repository ready for future execution workflows?

Epic 1 does not launch execution sessions, generate decisions, manage session continuity, generate operational context, or perform Git automation.

## Architectural Principles

### Filesystem Authority

Repository-owned state lives in the repository filesystem. Command Center observes, edits, and organizes that state, but does not replace it with an internal database.

Canonical artifact layout:

```text
<repository>/
  .agents/
    plan.md
    operational_context.md
    handoffs/
      handoff.md
      handoff.0001.md
      handoff.0002.md
    decisions/
      decisions.md
      decisions.0001.md
      decisions.0002.md
    milestones/
      *.md
```

Missing files and missing artifact directories are valid repository states. Missing artifacts are shown explicitly in the UI rather than treated as fatal errors.

### Memory Cache Is Performance Only

In-memory state exists for UI responsiveness and reduced disk access. It must be reconstructable from the filesystem and local application configuration.

If memory state is lost, Command Center must recover by:

```text
Load application configuration
Load registered repositories
Scan repository filesystems
Rebuild artifact and planning projections
Refresh UI state
```

### Backend Owns Business Logic

React owns presentation logic and view state only. Repository validation, artifact discovery, artifact rotation, planning readiness, persistence, and filesystem access belong in the .NET backend.

### Tauri Shell Remains Thin

Rust/Tauri owns windowing, packaging, file dialog access, permissions, process lifecycle, and IPC bridging. It must not contain repository or artifact business rules.

### Services Communicate Through Contracts

Backend behavior is exposed through interfaces to keep business logic testable and replaceable:

- `IRepositoryService`
- `IArtifactService`
- `IArtifactStore`
- `IArtifactRotationService`
- `IPlanningService`
- `IRepositoryProjectionService`
- `IApplicationConfigurationStore`

Deferred service boundary:

- `IRepositorySnapshotService`

`IRepositorySnapshotService` is intentionally not implemented during Epic 1. It is reserved for future repository execution context concerns such as current branch, modified file count, and Git working tree state.

## Target Solution Structure

```text
/src
  /CommandCenter.UI
  /CommandCenter.Shell
  /CommandCenter.Backend

/tests
  /CommandCenter.Backend.Tests

/docs
  architecture.md
```

### CommandCenter.UI

Technology:

```text
React
TypeScript
```

Responsibilities:

- Repository dashboard views
- Repository workspace views
- Artifact explorer
- Markdown artifact viewer and editor
- Refresh and navigation controls
- Displaying backend-derived status projections

The UI must not inspect `.git`, scan `.agents`, classify artifacts, rotate files, or derive readiness directly.

### CommandCenter.Shell

Technology:

```text
Rust
Tauri
```

Responsibilities:

- Launch the desktop application
- Open native directory picker dialogs
- Host Tauri commands callable from React
- Start and stop the .NET backend sidecar
- Bridge React requests to backend API calls
- Apply filesystem permissions required by the desktop shell

### CommandCenter.Backend

Technology:

```text
.NET
ASP.NET Core minimal API sidecar
```

Responsibilities:

- Repository registration and validation
- Application configuration persistence
- Artifact store abstraction
- Artifact discovery, loading, saving, and indexing
- Handoff and decision classification
- Handoff and decision rotation
- Plan and milestone discovery
- Execution readiness projection
- Repository dashboard and workspace projection composition
- Manual refresh projection rebuilds

The backend process should expose a local API consumed only by the Tauri shell. The Tauri shell owns external IPC; React calls Tauri commands, and Tauri calls the backend.

Epic 1 uses an explicit manual refresh model. Do not add `FileSystemWatcher`, realtime filesystem monitoring, background repository polling, or automatic artifact rescans.

Minimum M0 health endpoint:

```text
GET /api/ping -> "Pong"
```

## Core Domain Models

### RepositoryAvailability

```csharp
public enum RepositoryAvailability
{
    Available,
    Missing,
    AccessDenied
}
```

Rules:

- `Available` means the configured repository path exists, is accessible, and still contains `.git`.
- `Missing` means the configured repository path no longer exists.
- `AccessDenied` means the configured repository path exists but cannot be inspected.
- Availability is projected by the backend and displayed by the UI.

### Repository

```csharp
public sealed class Repository
{
    public Guid Id { get; init; }

    public string Name { get; init; } = "";

    public string Path { get; init; } = "";
}
```

Rules:

- `Id` is a Command Center identifier and is never derived from path.
- `Name` defaults to the repository directory name.
- `Path` is the absolute repository root.
- `Path` is backend-owned. It may leave backend APIs only as intentional display data in repository dashboard and workspace projections.
- The UI must treat `Path` as inert display text, not as an authority for filesystem operations.
- Duplicate detection uses normalized absolute paths.
- Canonical Git remote identity is deferred to a later execution-oriented phase and must not be inferred or persisted during Epic 1.

### ArtifactType

```csharp
public enum ArtifactType
{
    Plan,
    OperationalContext,
    Milestone,
    Handoff,
    Decision
}
```

### ArtifactFamily

```csharp
public enum ArtifactFamily
{
    Plan,
    OperationalContext,
    Milestone,
    Handoff,
    Decision
}
```

`ArtifactFamily` groups artifacts that share lifecycle behavior. It initially mirrors `ArtifactType`, but lifecycle logic should use family concepts so handoff and decision rotation rules do not become scattered filename checks.

### ArtifactVersionKind

```csharp
public enum ArtifactVersionKind
{
    Current,
    Historical
}
```

For static artifacts such as `plan.md`, `operational_context.md`, and milestone files, use `Current`.

### Artifact

```csharp
public sealed class Artifact
{
    public string RelativePath { get; init; } = "";

    public string Name { get; init; } = "";

    public ArtifactType Type { get; init; }

    public ArtifactFamily Family { get; init; }

    public ArtifactVersionKind VersionKind { get; init; }
}
```

Rules:

- `RelativePath` is always repository-relative and uses normalized separators.
- Absolute artifact paths must never be returned to the UI.
- File operations must resolve `RelativePath` through backend-owned repository roots.
- The backend must reject any relative path that escapes the repository root.

### Milestone

```csharp
public sealed class Milestone
{
    public string Name { get; init; } = "";

    public string RelativePath { get; init; } = "";
}
```

Milestone content is not parsed during Epic 1. The existence of markdown files in `.agents/milestones` is sufficient.

### ExecutionReadiness

```csharp
public enum ExecutionReadiness
{
    MissingPlan,
    MissingMilestones,
    Ready
}
```

Readiness rules:

```text
plan.md missing
  -> MissingPlan

plan.md present and no milestone markdown files
  -> MissingMilestones

plan.md present and at least one milestone markdown file exists
  -> Ready
```

## Projection Models

### ArtifactInventory

```csharp
public sealed class ArtifactInventory
{
    public Artifact? Plan { get; init; }

    public Artifact? OperationalContext { get; init; }

    public IReadOnlyList<Artifact> Milestones { get; init; } = [];

    public Artifact? CurrentHandoff { get; init; }

    public IReadOnlyList<Artifact> HistoricalHandoffs { get; init; } = [];

    public Artifact? CurrentDecisions { get; init; }

    public IReadOnlyList<Artifact> HistoricalDecisions { get; init; } = [];
}
```

`ArtifactInventory` is the authoritative artifact projection for Epic 1. Repository dashboard, workspace, and explorer projections must derive artifact status from this inventory instead of rediscovering artifacts independently.

### RepositoryDashboardProjection

```csharp
public sealed class RepositoryDashboardProjection
{
    public Repository Repository { get; init; } = new();

    public RepositoryAvailability Availability { get; init; }

    public ExecutionReadiness Readiness { get; init; }

    public int MilestoneCount { get; init; }

    public bool HasCurrentHandoff { get; init; }

    public bool HasCurrentDecisions { get; init; }
}
```

### RepositoryWorkspaceProjection

```csharp
public sealed class RepositoryWorkspaceProjection
{
    public Repository Repository { get; init; } = new();

    public RepositoryAvailability Availability { get; init; }

    public ExecutionReadiness Readiness { get; init; }

    public ArtifactInventory ArtifactInventory { get; init; } = new();

    public int MilestoneCount { get; init; }

    public bool HasPlan { get; init; }

    public bool HasOperationalContext { get; init; }

    public bool HasCurrentHandoff { get; init; }

    public bool HasCurrentDecisions { get; init; }
}
```

Repository workspace and dashboard endpoints should return backend-composed projections instead of requiring the UI to combine smaller DTOs.

## Backend Service Contracts

### IRepositoryService

```csharp
public interface IRepositoryService
{
    Task<IReadOnlyList<Repository>> GetAllAsync();

    Task<Repository> RegisterAsync(string repositoryPath);

    Task RemoveAsync(Guid repositoryId);
}
```

Responsibilities:

- Load registered repositories from application configuration.
- Validate registration requests.
- Create `.agents/` when registering a valid repository that lacks it.
- Persist repository registrations.
- Remove repository registrations without deleting repository files.
- Project repository availability as `Available`, `Missing`, or `AccessDenied`.

### IArtifactStore

```csharp
public interface IArtifactStore
{
    Task<bool> ExistsAsync(string path);

    Task<string?> ReadAsync(string path);

    Task WriteAsync(string path, string content);

    Task DeleteAsync(string path);

    Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern);
}
```

Implementations:

- `MemoryArtifactStore`
- `FileSystemArtifactStore`

The store is a persistence boundary. It does not classify artifacts, validate repositories, determine readiness, or perform rotation decisions.

### IArtifactService

```csharp
public interface IArtifactService
{
    Task<IReadOnlyList<Artifact>> DiscoverAsync(Repository repository);

    Task<Artifact?> GetCurrentHandoffAsync(Repository repository);

    Task<Artifact?> GetCurrentDecisionsAsync(Repository repository);

    Task<bool> ExistsAsync(Repository repository, string relativePath);

    Task<string> LoadAsync(Repository repository, string relativePath);

    Task SaveAsync(Repository repository, string relativePath, string content);
}
```

Responsibilities:

- Discover known artifact categories under `.agents`.
- Return explicit artifact metadata to the UI.
- Resolve current handoff and current decisions through backend APIs.
- Load and save markdown artifact content.
- Keep filesystem authority intact.
- Recompute artifact inventory on refresh.
- Use `ArtifactFamily` for lifecycle grouping.

### IArtifactRotationService

```csharp
public interface IArtifactRotationService
{
    Task<Artifact> RotateCurrentHandoffAsync(Repository repository);

    Task<Artifact> RotateCurrentDecisionsAsync(Repository repository);

    Task<Artifact> RotateAsync(Repository repository, ArtifactFamily family);
}
```

Responsibilities:

- Rotate only current handoff and current decision artifact families.
- Archive the current file to the next historical filename.
- Leave the current file in place and unchanged.
- Preserve historical numbering and historical file contents.
- Reject unsupported artifact families.
- Fail rather than overwrite an existing historical target.

Epic 1 rotation is archive-only. It does not replace current artifacts after archiving them.

### IRepositoryProjectionService

```csharp
public interface IRepositoryProjectionService
{
    Task<IReadOnlyList<RepositoryDashboardProjection>> GetDashboardAsync();

    Task<RepositoryWorkspaceProjection> GetWorkspaceAsync(Guid repositoryId);

    Task<RepositoryWorkspaceProjection> RefreshWorkspaceAsync(Guid repositoryId);
}
```

Responsibilities:

- Own the refresh pipeline.
- Compose dashboard projections.
- Compose repository workspace projections.
- Derive artifact state from `ArtifactInventory`.
- Combine repository availability, artifact inventory, planning state, and readiness.
- Keep projection-building logic out of React, Tauri, and unrelated backend services.

### IPlanningService

```csharp
public interface IPlanningService
{
    Task<bool> HasPlanAsync(Repository repository);

    Task<IReadOnlyList<Milestone>> GetMilestonesAsync(Repository repository);

    Task<ExecutionReadiness> DetermineReadinessAsync(Repository repository);
}
```

Responsibilities:

- Detect `.agents/plan.md`.
- Enumerate `.agents/milestones/*.md`.
- Determine readiness from plan and milestone existence.
- Treat missing milestone directories as zero milestones.

### IApplicationConfigurationStore

```csharp
public interface IApplicationConfigurationStore
{
    Task<ApplicationConfiguration> LoadAsync();

    Task SaveAsync(ApplicationConfiguration configuration);
}
```

Configuration is local Command Center metadata only. It is not repository artifact state.

Initial configuration shape:

```json
{
  "repositories": []
}
```

## Backend API Surface

Expose backend operations through local endpoints. Tauri commands should call these endpoints and return typed responses to React.

Minimum API:

```text
GET    /api/ping
GET    /api/repositories
POST   /api/repositories
DELETE /api/repositories/{repositoryId}
GET    /api/repositories/{repositoryId}/workspace
GET    /api/repositories/{repositoryId}/artifacts
GET    /api/repositories/{repositoryId}/artifacts/current-handoff
GET    /api/repositories/{repositoryId}/artifacts/current-decisions
GET    /api/repositories/{repositoryId}/artifacts/content?relativePath=<relativePath>
PUT    /api/repositories/{repositoryId}/artifacts/content
POST   /api/repositories/{repositoryId}/artifacts/rotate-current-handoff
POST   /api/repositories/{repositoryId}/artifacts/rotate-current-decisions
GET    /api/repositories/{repositoryId}/planning
POST   /api/repositories/{repositoryId}/refresh
```

`GET /api/repositories` should return `RepositoryDashboardProjection` entries, not raw repository configuration records.

`workspace` should return a composed projection suitable for the repository workspace:

```text
Repository
Availability
Artifact Inventory
Plan Status
Operational Context Status
Milestone Count
Current Handoff Status
Current Decisions Status
Readiness
```

The concrete response model is `RepositoryWorkspaceProjection`.

## M0: Architecture Ratification

(See ./milestones/m0-architecture-ratification.md)

## M1: Repository Management

(See ./milestones/m1-repository-management.md)

## M2: Artifact Infrastructure

(See ./milestones/m2-artifact-infrastructure.md)

## M3: Artifact Lifecycle Management

(See ./milestones/m3-artifact-lifecycle-management.md)

## M4: Planning and Readiness Infrastructure

(See ./milestones/m4-planning-and-readiness-infrastructure.md)

## M5: Repository Workspace Experience

(See ./milestones/m5-repository-workspace-experience.md)

## Epic Certification Gate

Epic 1 is complete only after all certification domains pass.

### Repository Lifecycle Certification

Verify:

- Register repository.
- Persist repository.
- Restore repository after application restart.
- Open repository workspace.
- Remove repository registration.
- Confirm repository files remain untouched after removal.

Use representative repositories such as:

```text
Axiom
Vector
FrontendCompiler
```

### Artifact Infrastructure Certification

Verify discovery, load, edit, save, reload, and restart recovery for:

```text
plan.md
operational_context.md
handoff.md
decisions.md
milestones/*.md
```

Pass criteria:

- Artifacts are discovered correctly.
- Edits persist on disk.
- Restart reconstructs artifact inventory.

### Artifact Lifecycle Certification

Verify:

- Current handoff resolution.
- Historical handoff resolution.
- Current decisions resolution.
- Historical decisions resolution.
- Historical files remain immutable during refresh and rotation.

### Rotation Certification

Verify handoff rotation:

```text
handoff.md
handoff.0001.md
handoff.0002.md
handoff.0003.md
```

Verify decision rotation:

```text
decisions.md
decisions.0001.md
decisions.0002.md
decisions.0003.md
```

Pass criteria:

- Current artifact remains current.
- Historical numbering is stable.
- No historical file is overwritten.
- No artifact content is lost.

### Planning Certification

Verify all readiness states:

```text
MissingPlan
MissingMilestones
Ready
```

Pass criteria:

- Missing plan repository reports `MissingPlan`.
- Repository with plan and no milestones reports `MissingMilestones`.
- Repository with plan and at least one milestone reports `Ready`.

### Workspace Certification

Verify:

- Repository dashboard displays status projections.
- Repository dashboard displays current handoff and current decisions status.
- Repository workspace displays summary data.
- Artifact explorer displays all categories.
- Artifact viewer displays selected markdown content.
- Refresh updates summary, explorer, and viewer state.
- Missing artifacts are visible and not treated as application errors.

### Restart Recovery Certification

Verify:

```text
Close application
Restart application
Load configuration
Restore repository inventory
Rebuild workspace state
Rediscover artifacts
Recompute readiness
```

Pass criteria:

- Registered repositories reappear.
- Artifact inventories are reconstructed from disk.
- Planning and readiness projections are correct.
- No state requires SQLite or hidden repository metadata.

## Required Test Strategy

Backend tests are mandatory for each milestone and should use temporary directories for filesystem behavior.

Recommended test groups:

- `MemoryArtifactStoreTests`
- `FileSystemArtifactStoreTests`
- `ApplicationConfigurationStoreTests`
- `RepositoryServiceTests`
- `ArtifactDiscoveryTests`
- `ArtifactLoadSaveTests`
- `ArtifactRotationTests`
- `PlanningServiceTests`
- `RepositoryProjectionServiceTests`
- `WorkspaceProjectionTests`

UI tests should cover critical rendering and state transitions where practical:

- Dashboard empty state.
- Registered repository list.
- Repository selection.
- Current handoff and current decisions status display.
- Artifact category rendering.
- Artifact content loading.
- Missing artifact display.
- Readiness display.

Manual desktop certification must verify the complete React to Tauri to .NET path.

## Out of Scope For Epic 1

Do not implement:

- Execution session launch.
- Milestone execution.
- Milestone completion tracking.
- Handoff acceptance workflows.
- Decision generation.
- Decision session management.
- Operational context generation or consolidation.
- Git automation.
- Pull request creation.
- Branch management.
- Structured parsing of plan or milestone content.
- SQLite or repository-state database persistence.

## Final Epic Exit State

Command Center is ready for the next phase when it can reliably:

- Manage repository registrations.
- Restore repository registrations after restart.
- Validate repository availability.
- Create `.agents/` during registration.
- Discover artifacts in `.agents`.
- Load, edit, and save markdown artifacts.
- Classify current and historical handoff artifacts.
- Classify current and historical decision artifacts.
- Rotate handoff and decision artifacts safely.
- Detect plan presence.
- Enumerate milestone files.
- Determine readiness.
- Present repository state through a unified workspace.

The application remains an authoritative repository and artifact management workspace, with execution orchestration intentionally deferred.
