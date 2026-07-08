using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Completion;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Roadmap.Cli;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.ProjectContextLoader;
using RoadmapArtifacts = LoopRelay.Roadmap.Cli.RoadmapArtifacts;

namespace LoopRelay.Roadmap.Cli.Tests;

internal static class RoadmapSamples
{
    public static string ValidEpic(
        string name = "Test Epic",
        string epicId = "EPIC-TEST",
        string status = "Authored",
        string sourceDisposition = "Create Epic")
    {
        return $"""
        # Epic: {name}

        ## Epic Metadata

        | Field | Value |
        |---|---|
        | Epic ID | {epicId} |
        | Status | {status} |
        | Source Disposition | {sourceDisposition} |
        | Projection Link | Test Projection |

        ## Strategic Purpose

        Deliver a bounded strategic capability for roadmap testing.

        ## Desired Capability

        The roadmap runtime can safely promote this epic as an authoritative artifact.

        ## Scope

        - Preserve artifact promotion boundaries.
        - Support milestone expansion.

        ## Non-Goals

        - Do not implement unrelated roadmap functionality.

        ## Acceptance Criteria

        - The epic has required metadata.
        - The epic has enough structure for milestone generation.

        ## Milestone Roadmap

        | Milestone ID | Milestone Name | Purpose | Outcome | Depends On | Completion Signal |
        |---|---|---|---|---|---|
        | M1 | Promotion Boundary | Verify promotion | Active epic is valid | None | Validation passes |
        """;
    }
}
