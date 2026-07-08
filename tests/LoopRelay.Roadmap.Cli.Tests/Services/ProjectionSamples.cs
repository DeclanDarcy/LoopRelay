using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Completion;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Roadmap.Cli;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.ProjectContextLoader;
using RoadmapArtifacts = LoopRelay.Roadmap.Cli.RoadmapArtifacts;

namespace LoopRelay.Roadmap.Cli.Tests;

internal static class ProjectionSamples
{
    public static string Valid(string runtimePromptName)
    {
        string title = runtimePromptName switch
        {
            "CreateRoadmapCompletionContext" => "# Roadmap Completion Projection",
            "UpdateRoadmapCompletionContext" => "# Roadmap Completion Update Projection",
            "SelectNextEpic" => "# Select Next Epic Projection",
            "EpicPreparationAudit" => "# Epic Preparation Audit Projection",
            "RealignEpic" => "# Epic Realignment Projection",
            "ReimagineEpic" => "# Epic Reimagination Projection",
            "CreateNewEpic" => "# Create New Epic Projection",
            "SplitEpic" => "# Split Epic Projection",
            "GenerateMilestoneDeepDivesForEpic" => "# Milestone Deep Dive Projection",
            "EvaluateEpicCompletionAndDrift" => "# Epic Completion Evaluation Projection",
            _ => throw new ArgumentOutOfRangeException(nameof(runtimePromptName)),
        };

        return $"""
        {title}

        ## Purpose

        Test projection.

        ## Authority Boundary

        Test boundary.

        ## Projection Metadata

        | Field | Value |
        |---|---|
        | Intended Consumer | {runtimePromptName} |

        ## Canonical Vocabulary

        | Term | Definition |
        |---|---|
        | Test | Test |

        ## Downstream Use Instructions

        Use the projection.

        ## Projection Integrity Checklist

        - The projection is structurally valid.
        """;
    }
}
