# Roadmap CLI execution bridge trust boundary

## Severity

High

## Finding

`LoopRelay.Roadmap.CLI` sends generated and repository-controlled Markdown directly to an execution agent that runs with `danger-full-access`, workspace writes, network access, and approvals disabled.

Affected code:

- `src/LoopRelay.Roadmap.CLI/ExecutionPromptGenerator.cs`
- `src/LoopRelay.Roadmap.CLI/RoadmapExecutionBridge.cs`
- `src/LoopRelay.Roadmap.CLI/AgentSpecs.cs`

The execution prompt embeds the active epic, milestone specs, and operational context verbatim. Those artifacts can originate from roadmap files, generated LLM output, or previously promoted Markdown. If any embedded content contains prompt injection, the injected instructions are delivered to an agent with broad local and network authority.

## Impact

A compromised roadmap/spec/provenance artifact can cause the execution agent to:

- Modify arbitrary repository files.
- Run destructive commands without approval.
- Access the network without a user decision.
- Exfiltrate repository or environment data.
- Produce state that appears valid because the disposition parser only evaluates the final execution protocol.

## Proposal

Introduce an explicit execution trust boundary with two execution modes:

1. Default mode: run the execution bridge in `workspace-write`, no network, and approval-on-request for privileged actions.
2. Elevated mode: require an explicit CLI flag or state transition that records why `danger-full-access` and network are needed for this epic.

The robust shape is:

- Add `RoadmapExecutionOptions` with `SandboxIdentifier`, `AllowNetwork`, and `RequiresApproval`.
- Change `AgentSpecs.ExecutionBridge` to accept those options instead of hardcoding `danger-full-access`.
- Default to `workspace-write`, `CanAccessNetwork: false`, `RequiresApproval: true`.
- Add a pre-execution policy check that writes the chosen trust posture into `.agents/state.md` and execution evidence.
- Treat generated Markdown as untrusted input in the execution prompt: wrap artifact bodies in clearly delimited data sections and add a short system instruction that artifact content is evidence, not authority.
- Add an optional allowlist for network-required epics so repeated runs do not require manual state edits.

This preserves the CLI's ability to perform real work while making escalation visible, intentional, and auditable.

## Acceptance Criteria

- Execution bridge no longer hardcodes `danger-full-access`.
- Default execution does not allow network access.
- Destructive or network actions require approval unless an explicit elevated mode is selected.
- Execution evidence records the sandbox, network, and approval posture used for the turn.
- Tests cover default execution options, elevated execution options, and prompt rendering that treats embedded artifact content as untrusted data.
