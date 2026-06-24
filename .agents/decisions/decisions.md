# Decisions

## Newly Authorized

- Proceed next with duplicate-domain-evidence detection before any preparation
  domain command invocation.
- Treat the completed preparation slice as the preparation equivalent of the
  earlier continuation-evaluation milestone:
  - preparation framework established.
  - preparation persistence established.
  - preparation idempotency established.
  - preparation refusal model established.
- Preserve the invariant that preparation refuses at every open authority gate.
- Add duplicate detection for all three preparation categories before invoking
  any existing domain command.
- Decision preparation duplicate checks must cover:
  - equivalent candidate exists.
  - equivalent proposal exists.
  - equivalent package exists.
- Operational-context preparation duplicate checks must cover:
  - equivalent proposal exists.
  - equivalent assimilation exists.
  - equivalent linkage exists.
- Commit preparation duplicate checks must cover:
  - equivalent commit preparation exists.
  - equivalent prepared commit exists.
- Strengthen preparation evaluation outcomes so certification can distinguish:
  - allowed.
  - refused.
  - skipped.
  - duplicate.
- Preserve the distinction:
  - preparation allowed does not mean preparation executed.
- Stop again for review before allowing the first actual domain command
  invocation from workflow.

## Explicitly Deferred

- Do not invoke Decisions commands yet.
- Do not invoke Continuity commands yet.
- Do not invoke Execution commands yet.
- Do not create decision candidates, proposals, or packages yet.
- Do not create operational-context proposals or linkages yet.
- Do not create commit preparations yet.
- Do not implement decision generation logic, context proposal logic, or commit
  preparation logic inside `CommandCenter.Workflow`.
