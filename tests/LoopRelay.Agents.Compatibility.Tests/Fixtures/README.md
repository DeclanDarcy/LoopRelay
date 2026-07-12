# Codex continuity certification fixtures

These fixtures are scrubbed, repository-owned results of disposable certification runs. The harness creates a new repository, schema directory, and `CODEX_HOME` for every run. It records booleans and digests only; provider thread IDs, prompts, responses, credentials, and rollout bodies are never checked in.

Certified exact identities:

| Codex version | Canonical experimental app-server v2 schema digest | Evidence digest |
| --- | --- | --- |
| `0.142.5` | `f0ead6fd0bb0f21a9a3194c65e068f5c8ba333d24464d2575933065066b76a5e` | `d28f9111ac48038de0e7427de8a3363bc792541bf96644dd0913b815aa35bf8e` |
| `0.144.0` | `d3639dd0e04172c9dbb9ca7af048e32cfe197d482d9344a137d2bca95f946c6f` | `e2cbb8b6928af5637b656174118ff1e812de4d4bfc8b1033aa1f1f55c7a54637` |
| `0.144.1` | `d3639dd0e04172c9dbb9ca7af048e32cfe197d482d9344a137d2bca95f946c6f` | `25b5ed3bdbb55dee15f88b287beee1297750a9d4d729c5e87031dd878b29e4b3` |

Observed in the disposable live matrix:

- `thread/start` returned a durable ID;
- `thread/inject_items` materialized harmless synthetic history without reading a user session store;
- exact `thread/read` preserved the requested ID;
- `thread/resume` with client `experimentalApi` and `excludeTurns=true` preserved the original ID;
- `thread/fork` returned a distinct child ID and submitted no turn;
- initialize did not explicitly report an `experimentalApi` server capability.

Promotion is deliberately narrower than method recognition. Resume, `excludeTurns`, and read are `Supported`. Conversation write remains `Unknown` because a marker turn could not be certified in an unauthenticated disposable home. Maximum recoverable context remains unknown because the server supplied no certified limit. Fork remains `Unknown` because unique child reconciliation after a lost response was not established. These gates keep reconstructed and native-fork recovery inactive in production while preserving their tested implementation behind the mechanism catalog.

Release command:

```powershell
$env:LOOPRELAY_CODEX_CERT_BINARY = 'C:\path\to\codex.cmd'
$env:CODEX_EXECUTABLE = $env:LOOPRELAY_CODEX_CERT_BINARY
$env:LOOPRELAY_CODEX_CERT_IDENTITY_PROBE = '1'
dotnet test tests\LoopRelay.Agents.Compatibility.Tests\LoopRelay.Agents.Compatibility.Tests.csproj
```
