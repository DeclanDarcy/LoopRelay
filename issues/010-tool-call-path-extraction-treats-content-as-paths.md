# Tool-call path extraction treats content as paths

## Severity

Medium

## Finding

The Codex permission adapter infers path arguments from every string in a tool-call payload, including file content and patch text.

Affected code:

- `src/LoopRelay.Permissions/Codex/CodexPermissionAdapter.cs`
- `src/LoopRelay.Permissions/Services/OperationPermissionHandler.cs`

`CodexPermissionAdapter.ExtractPathArguments` recursively walks the tool-call arguments and adds any string whose parent key looks path-bearing or whose value looks path-like. A value is considered path-like when it contains `/`, contains `\`, starts with `.`, or ends with `.md`.

`OperationPermissionHandler` then requires every extracted string to match the operation's allowed read or write profile. For write and patch tools, normal content can contain markdown links, relative references such as `./milestones/m1.md`, URLs, or mentions of `.agents/plan.md`. Those content strings become synthetic path arguments and cause an otherwise valid scoped write to be declined.

## Impact

Valid scoped operations can fail nondeterministically based on document content. The most affected flows are milestone extraction and operational document optimization, because those documents naturally contain relative links and markdown file references.

This also makes the permission decision depend on payload text that is not an access request, which creates brittle behavior as Codex tool schemas evolve.

## Proposal

Parse path arguments by tool schema instead of broad string heuristics.

The robust shape is:

- For known read/list/search tools, inspect only fields that are actual path inputs.
- For known write/edit/patch tools, inspect the target path field but exclude `content`, `text`, `patch`, `diff`, and similar payload fields.
- Deny unknown tool schemas rather than guessing from arbitrary strings.
- Add regression tests where a permitted write contains markdown links, URLs, and `.agents/*.md` references in content.

## Acceptance Criteria

- A write to an allowed file is accepted even when the content contains relative markdown links or `.agents/*.md` references.
- Content, patch, diff, and text payload fields are not treated as requested filesystem paths.
- Unknown tool schemas remain denied unless explicitly modeled.
- Tests cover both valid content with path-like strings and disallowed target paths.
