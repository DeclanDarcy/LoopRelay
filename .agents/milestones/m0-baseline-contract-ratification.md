# M0 — baseline contract ratification


### Implementation

- [ ] Add or update enduring ADRs for D1-D4 and D2's obstacle vocabulary.
- [ ] Add architecture tests asserting:
   - [ ] provider sends require a persisted rendered-prompt fact and authorized dispatch identity;
   - [ ] transport receives identity-only prompt input and cannot append provider-visible content;
   - [ ] runtime consumes a resolved policy/runtime profile rather than raw configuration or recommendations;
   - [ ] canonical outcome enums contain no new generic blocker/latch values; and
   - [ ] fresh and upgraded canonical databases agree on identity/family/version/shape.
- [ ] Replace stale tests that encode post-render policy append, optional prompt evidence, or a public `unblock` latch.
- [ ] Capture build, full component suite, CLI component suite, and static exact-profile compatibility results.

### Exit gate

- [ ] D1-D4 are accepted and executable.
- [ ] Active canonical tests agree on prompt, policy, schema, dispatch, and outcome vocabulary.
- [ ] Build and component suites pass without unexpected warnings.
- [ ] No M8+ authority is claimed by this gate.

### Ratification and specification-integrity details

- [ ] Treat D1–D4 as blocking proposals until the owner accepts them and the acceptance is encoded
  in enduring ADRs and executable architecture tests. M8 cannot begin before that evidence exists.
- [ ] Correct the generated deep-dive roadmap links so they resolve to the selected durable roadmap
  path. The supplied roadmap currently lives at `.agents/specs/epic.md`, not `.agents/epic.md`.
- [ ] Either restore/generate the indexed M0–M7 preservation specifications or remove those links
  and state that their accepted commits plus the roadmap are the preservation authority. This
  input-integrity correction does not reopen M0–M7.
