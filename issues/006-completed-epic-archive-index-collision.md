# Completed epic archive index can collide after gaps

## Severity

Medium

## Finding

Completed epic archive index selection uses the count of child directories rather than the maximum numeric archive label.

Affected code:

- `src/LoopRelay.Completion/CompletedEpicArchiveService.cs`

`ComputeArchiveIndexAsync` returns `directories.Count + 1`. If archive directories are non-contiguous, for example `.agents/archive/epics/1/` and `.agents/archive/epics/3/`, the next computed index is `3`, which collides with an existing archive. Non-archive child directories under the archive root also skew the count.

## Impact

Manual cleanup, failed partial archive attempts, or non-archive directories can make completion closure fail even though a safe next numeric index exists. This turns archive layout drift into a hard blocker for closing future epics.

## Proposal

Derive the next archive index from existing numeric archive labels.

The robust shape is:

- Enumerate direct child directories under `.agents/archive/epics`.
- Parse directory names that are positive integers.
- Choose `max + 1`.
- Ignore non-numeric directories or report them separately if they should be forbidden.
- Consider existing top-level synthesis files like `.agents/archive/epics/{index}.md` when detecting collisions.

## Acceptance Criteria

- Existing `1/` and `3/` directories produce next index `4`.
- Non-numeric archive-root directories do not cause numeric collisions.
- Tests cover non-contiguous directories and synthesis-file collisions.
