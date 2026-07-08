# RepositoryWork Interaction Observation

| Field | Value |
|---|---|
| Subject | repository-work:9019bb423464ac27 |
| Intent | intent-4c38374a189f4efdb1045aa182bff9ec |
| Protocol | repositorywork.semantic-execution.v1 |
| Admission | Admitted |
| Source | plan.md |
| Source SHA-256 | 943c2f58afc13ea5c524680b447d56e5b664529deb2215f6168b021cf1661492 |
| Observed At | 2026-07-08T02:05:03.8326118+00:00 |

## Authority Checks

| Check | Passed | Scope | Reason |
|---|---:|---|---|
| authority:repository.read | True | repository.read | Requested authority includes the required scope. |
| authority:repositorywork.semantic-execution | True | repositorywork.semantic-execution | Requested authority includes the required scope. |
| authority:repositorywork.artifact-promotion | True | repositorywork.artifact-promotion | Requested authority includes the required scope. |
| authority:repositorywork.decision-acceptance | True | repositorywork.decision-acceptance | Requested authority includes the required scope. |
| authority:repositorywork.state-entry | True | repositorywork.state-entry | Requested authority includes the required scope. |
| authority:repositorywork.recovery-review | True | repositorywork.recovery-review | Requested authority includes the required scope. |
| authority:repositorywork.certification | True | repositorywork.certification | Requested authority includes the required scope. |
| authority:repositorywork.distillation | True | repositorywork.distillation | Requested authority includes the required scope. |
| authority:repositorywork.capability-evaluation | True | repositorywork.capability-evaluation | Requested authority includes the required scope. |
| authority:repositorywork.report | True | repositorywork.report | Requested authority includes the required scope. |

## Invariant Checks

| Check | Passed | Scope | Reason |
|---|---:|---|---|
| no-subjectless-interaction | True | repository-work:9019bb423464ac27 | Subject identity is present. |
| no-intentless-interaction | True | intent-4c38374a189f4efdb1045aa182bff9ec | Subject-bound intent is present. |
| no-authority-without-scope | True | repository.read, repositorywork.semantic-execution, repositorywork.artifact-promotion, repositorywork.decision-acceptance, repositorywork.state-entry, repositorywork.recovery-review, repositorywork.certification, repositorywork.distillation, repositorywork.capability-evaluation, repositorywork.report | Every requested authority value names a scope. |
| report-fields-do-not-create-authority | True | repositorywork.report | Report authority is recorded separately and is not counted as mutation or acceptance authority. |
