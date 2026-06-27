# Milestone 10: Publish the Reference Architecture

Objective: separate the Command Center implementation baseline from a consumable reference architecture package for future systems.

This milestone does not introduce new implementation architecture. It packages, validates, and publishes the accepted baseline from Milestone 9.

Implementation tasks:

- [ ] Split Command Center-specific implementation details from general reference architecture guidance.
- [ ] Publish a reference architecture package under `docs/reference/` or an equivalent durable documentation structure.
- [ ] Include canonical diagrams for the layer chain, authority flow, contract flow, transport flow, resource/controller/workspace flow, runtime failure flow, and mechanism lifecycle.
- [ ] Include capability lifecycle guidance: introduction, protection, certification, baseline, publication, evolution, and retirement.
- [ ] Include decision governance guidance and templates suitable for future systems.
- [ ] Include architectural evidence guidance and examples suitable for future systems.
- [ ] Include implementation adoption guidance: minimum viable Oracle, minimum passive transport, minimum authority inventory, minimum state ownership matrix, and minimum regression framework.
- [ ] Include migration guidance for existing systems and greenfield guidance for new systems.
- [ ] Include anti-patterns and recovery guidance: duplicate authority, manual contract truth, transport-owned semantics, root-owned feature state, presentation inference, unscoped runtime failure, and cleanup without replacement.
- [ ] Define versioning for the reference architecture package.
- [ ] Add publication validation: links, examples, glossary consistency, capability matrix consistency, and traceability back to the certified Command Center baseline.
- [ ] Define ownership for future updates to the reference package.

Required outputs:

- [ ] Published reference architecture package.
- [ ] Command Center-specific implementation appendix.
- [ ] General adoption guide.
- [ ] Decision governance template set.
- [ ] Architectural evidence template set.
- [ ] Capability lifecycle guide.
- [ ] Architecture anti-pattern and recovery guide.
- [ ] Reference architecture versioning policy.
- [ ] Publication validation report.
- [ ] Reference ownership model.
- [ ] Reference architecture publication certification.

Exit criteria:

- [ ] Future systems can consume the reference package without reading Command Center implementation history.
- [ ] Command Center-specific and general guidance are separated.
- [ ] The published package traces back to certified implementation evidence.
- [ ] Reference ownership and evolution rules are explicit.
