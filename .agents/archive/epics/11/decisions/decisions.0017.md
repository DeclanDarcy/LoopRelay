# Decisions: 2026-06-26 Slice 0017 Oracle Certification Posture

These decisions capture only newly authorized direction from the response accepting the Slice 0017 Oracle change workflow and authorizing the next checkpoint.

## Authorized Decisions

1. Stop expanding Milestone 0.2 Oracle mechanisms for now.
   - The fixture comparison, consumer verification, artifact freshness, and change workflow mechanisms are sufficient for the current repository dashboard Oracle ecosystem.
   - New verification capabilities should not be added unless certification exposes a material gap.

2. Treat the Oracle change workflow as the lifecycle closure around the existing mechanisms.
   - The workflow governs how accepted contract changes happen.
   - It must not be treated as another verification mechanism.

3. Keep the Oracle workflow procedural at this stage.
   - Automation is deferred so architectural decisions remain explicit.
   - Generation remains Milestone 1.2 work and must later implement the Oracle rather than redefine it.

4. Begin a Milestone 0.2 consolidation and certification pass next.
   - The next slice should certify the repository dashboard Oracle ecosystem before expanding contract coverage.
   - Certification should be structured around Milestone 0.2 exit criteria and evidence, not only passing tests.

5. Make the gap review adversarial.
   - The certification pass should ask which Milestone 0.2 exit criteria cannot yet be demonstrated by executable evidence.
   - Documentation-only claims should be identified as gaps or explicit limits.

6. Let certification determine the next expansion path.
   - If repository dashboard pilot gaps remain, close them first.
   - If no material pilot gap remains, use the pilot as the template for the second contract family.

## Next Authorized Sequence

1. Commit and push Slice 0017 as an architectural checkpoint.
2. Stop executing after the checkpoint.
3. In the next work slice, run the Milestone 0.2 consolidation/certification pass for the repository dashboard Oracle ecosystem.
