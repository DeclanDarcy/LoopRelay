# `.agents` Artifact Ownership

This matrix records current ownership for migration safety. It is descriptive, not
runtime authority: relay work may keep, project, or retire these Markdown artifacts
based on executable evidence.

| Path | Current writer | Current role | Authority status |
| --- | --- | --- | --- |
| `.agents/plan.md` | Plan CLI | Plan rendering consumed by execution | Compatibility projection |
| `.agents/details.md` | Plan CLI | Planning detail rendering | Compatibility projection |
| `.agents/operational_context.md` | Loop CLI / Plan CLI | Operational context rendering | Compatibility projection |
| `.agents/operational_delta.md` | Loop CLI | Observed delta evidence for context evolution | Evidence input |
| `.agents/decisions/decisions.md` | Loop CLI | Human decision session output | Human-authored direction |
| `.agents/handoffs/handoff.md` | Execution agent | Agent handoff between turns | Compatibility evidence |
| `.agents/milestones/*.md` | Plan CLI / execution agent | Milestone checklist rendering | Compatibility projection |
| `.agents/archive/**` | Manual archive tooling | Historical artifact archive | Historical evidence |
| `.agents/roadmap/**` | Roadmap CLI | Roadmap projection and transition artifacts | Legacy projection |
| `.agents/evidence/**` | Roadmap CLI | Numbered execution and transition evidence | Evidence |
