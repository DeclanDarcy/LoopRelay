# Milestone 6 Reasoning Field-to-Surface Audit

Generated during the reconstruction/query/trace transparency slice.

## Reconstruction Query Fields

| Backend field | UI surface | Coverage |
| --- | --- | --- |
| `ReasoningQueryResult.Query.Category` | `ReasoningQueryPanel` / `Executed reasoning query` | Characterization assertion |
| `ReasoningQueryResult.Query.Question` | `ReasoningQueryPanel` / `Executed reasoning query` | Characterization assertion |
| `ReasoningQueryResult.Query.Target` | `ReasoningQueryPanel` / `Executed reasoning query` | Characterization assertion |
| `ReasoningQueryResult.Query.Direction` | `ReasoningQueryPanel` / `Executed reasoning query` | Characterization assertion |
| `ReasoningQueryResult.Query.HistoricalAt` | `ReasoningQueryPanel` / `Executed reasoning query` | Characterization assertion |

## Reconstruction Confidence Fields

| Backend field | UI surface | Coverage |
| --- | --- | --- |
| `ReasoningReconstruction.Confidence` | `ReasoningReconstructionPanel` context summary | Existing characterization coverage |
| `ConfidenceRationale.Level` | `ReasoningQueryPanel`, `ReasoningReconstructionPanel` | Existing characterization coverage |
| `ConfidenceRationale.Rationale` | `ReasoningQueryPanel`, `ReasoningReconstructionPanel` | Existing characterization coverage |
| `ConfidenceRationale.EventEvidencePresent` | `ReasoningReconstructionPanel` / `Reconstruction confidence rationale` | Existing characterization coverage |
| `ConfidenceRationale.RelationshipEvidencePresent` | `ReasoningReconstructionPanel` / `Reconstruction confidence rationale` | Existing characterization coverage |
| `ConfidenceRationale.TraceDiagnosticsPresent` | `ReasoningReconstructionPanel` / `Reconstruction confidence rationale` | Existing characterization coverage |
| `ConfidenceRationale.MissingEvidence` | `ReasoningQueryPanel`, `ReasoningReconstructionPanel` | Existing characterization coverage |
| `ConfidenceRationale.WhyNotHigher` | `ReasoningQueryPanel`, `ReasoningReconstructionPanel` | Existing characterization coverage |

## Reconstruction Scope Fields

| Backend field | UI surface | Coverage |
| --- | --- | --- |
| `Scope.Direction` | `ReasoningQueryPanel`, `ReasoningReconstructionPanel` | Existing characterization coverage |
| `Scope.Target` | `ReasoningQueryPanel`, `ReasoningReconstructionPanel` | Existing characterization coverage |
| `Scope.Source` | `ReasoningQueryPanel`, `ReasoningReconstructionPanel` | Existing characterization coverage |
| `Scope.HistoricalCutoff` | `ReasoningQueryPanel`, `ReasoningReconstructionPanel` | Existing characterization coverage |
| `Scope.ReachableEvidence` | `ReasoningQueryPanel`, `ReasoningReconstructionPanel` | Characterization assertion |
| `Scope.UnreachableEvidence` | `ReasoningQueryPanel`, `ReasoningReconstructionPanel` | Existing characterization coverage |

## Trace Fields

| Backend field | UI surface | Coverage |
| --- | --- | --- |
| `ReasoningTrace.Direction` | `ReasoningGraphPanel` / trace list | Existing characterization coverage |
| `ReasoningTrace.Target` | `ReasoningGraphPanel` / trace list | Existing characterization coverage |
| `ReasoningTrace.Nodes` | `ReasoningGraphPanel` / trace node table | Characterization assertion |
| `ReasoningTrace.Relationships` | `ReasoningGraphPanel` / trace relationship list | Existing characterization coverage |
| `ReasoningTrace.Diagnostics` | `ReasoningGraphPanel` fallback diagnostics | Existing characterization coverage through grouped/fallback diagnostics |
| `ReasoningTrace.DiagnosticGroups` | `ReasoningGraphPanel` / grouped trace diagnostics | Existing characterization coverage |

## Reconstruction Evidence and Diagnostics

| Backend field | UI surface | Coverage |
| --- | --- | --- |
| `ReasoningReconstruction.Narrative.Summary` | `ReasoningQueryPanel`, `ReasoningReconstructionPanel` | Existing characterization coverage |
| `ReasoningReconstruction.Narrative.Details` | `ReasoningReconstructionPanel` grouped detail parser | Existing characterization coverage |
| `ReasoningReconstruction.Evidence` | `ReasoningReconstructionPanel` / `Reconstruction evidence` | Existing characterization coverage |
| `ReasoningReconstruction.Diagnostics` | `ReasoningReconstructionPanel` fallback diagnostics | Existing characterization coverage through grouped/fallback diagnostics |
| `ReasoningReconstruction.DiagnosticGroups` | `ReasoningQueryPanel`, `ReasoningReconstructionPanel` | Existing characterization coverage |

## Capture and Materialization Cross-Checks

| Backend field | UI surface | Coverage |
| --- | --- | --- |
| `ReasoningCaptureProvenance.DiagnosticGroups` | `ReasoningEventFeed` / capture diagnostics | Characterization assertion |
| `ReasoningMaterializationReviewReport.DiagnosticGroups` | `ReasoningMaterializationReviewPanel` | Existing characterization coverage |
