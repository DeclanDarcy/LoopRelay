# M1: Repository Management

## Goal

Allow users to register, validate, persist, display, open, and remove repository registrations.

## Repository Registration Flow

```text
Add Repository
Select Directory
Validate Repository
Initialize .agents Directory
Persist Repository
Refresh Dashboard
```

## Validation Rules

Registration succeeds only when:

- The selected path exists.
- The selected path is a directory.
- The selected directory contains `.git`.
- The normalized absolute path is not already registered.

Registration must fail without partial state when:

- The directory does not exist.
- The directory does not contain `.git`.
- The repository is already registered.
- The backend cannot access the directory.

## Workspace Initialization

When registering a valid repository, create:

```text
.agents/
```

if it does not already exist.

Do not create these during M1:

```text
plan.md
operational_context.md
handoffs/
decisions/
milestones/
```

## UI Tasks

- [ ] Add repository dashboard.
- [ ] Add `Add Repository` action.
- [ ] Use native directory picker through Tauri.
- [ ] Show validation errors clearly.
- [ ] Display registered repositories with name and path.
- [ ] Add repository open/select behavior.
- [ ] Add repository details view showing name and path.
- [ ] Add remove action with confirmation.
- [ ] Show repository availability status when a registered repository is available, missing, or access denied.

## Backend Tasks

- [ ] Implement `Repository` model.
- [ ] Implement `IRepositoryService`.
- [ ] Persist repositories in application configuration.
- [ ] Normalize paths for duplicate detection.
- [ ] Create `.agents/` on successful registration when absent.
- [ ] Remove registrations without deleting repository files or `.agents`.
- [ ] Return `RepositoryAvailability` in repository projections.

## Tests

- [ ] Valid repository registers successfully.
- [ ] Invalid non-directory path fails.
- [ ] Directory without `.git` fails.
- [ ] Duplicate registration fails.
- [ ] `.agents/` is created when missing.
- [ ] Existing `.agents/` is not modified.
- [ ] Registered repositories survive configuration reload.
- [ ] Removing a repository updates configuration and does not modify repository contents.
- [ ] Missing registered repository reports `Missing`.
- [ ] Inaccessible registered repository reports `AccessDenied` when the environment allows that condition to be simulated.

## Acceptance Criteria

- [ ] Repositories can be added through the UI.
- [ ] Invalid repositories are rejected with clear errors.
- [ ] Repository registrations survive application restart.
- [ ] Registered repositories appear on the dashboard.
- [ ] Repository details view opens.
- [ ] Removing a repository registration leaves repository files untouched.
