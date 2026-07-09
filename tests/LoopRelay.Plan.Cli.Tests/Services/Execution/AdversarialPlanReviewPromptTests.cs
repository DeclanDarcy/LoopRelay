using LoopRelay.Core.Prompts;
using Xunit;

namespace LoopRelay.Plan.Cli.Tests.Services.Execution;

public class AdversarialPlanReviewPromptTests
{
    [Fact]
    public void Render_ReplacesPolicySlotsAndInputs()
    {
        string prompt = AdversarialPlanReview.Render(
            "P1",
            "P2",
            "P3",
            "P4",
            "P5",
            "P6",
            "P7",
            "PROJECTION",
            "PLAN");

        string[] renderedValues =
        [
            "P1",
            "P2",
            "P3",
            "P4",
            "P5",
            "P6",
            "P7",
            "PROJECTION",
            "PLAN"
        ];

        foreach (string renderedValue in renderedValues)
        {
            Assert.Contains(renderedValue, prompt, StringComparison.Ordinal);
        }

        AdversarialPlanReviewPromptTestAssertions.AssertNoUnresolvedPlaceholders(prompt);
    }

    [Fact]
    public void Render_PreservesReviewOutputCompatibility()
    {
        string prompt = AdversarialPlanReviewPromptTestAssertions.RenderWithEmptyPolicySlots();

        string[] requiredHeadings =
        [
            "## Finding N",
            "## Missing Decisions",
            "## False Closure Tests",
            "## Authority Drift",
            "## Projection Blind Spots",
            "## Silent Drift Risks",
            "## Suggested Patch",
            "## Final Adversarial Question",
            "## Verdict"
        ];

        foreach (string heading in requiredHeadings)
        {
            Assert.Contains(heading, prompt, StringComparison.Ordinal);
        }

        string[] verdictBullets =
        [
            "- FAIL:",
            "- CONDITIONAL PASS:",
            "- PASS:"
        ];

        foreach (string verdictBullet in verdictBullets)
        {
            Assert.Contains(verdictBullet, prompt, StringComparison.Ordinal);
        }
    }
}

internal static class AdversarialPlanReviewPromptTestAssertions
{
    public static string RenderWithEmptyPolicySlots(
        string projectContextProjection = "PROJECTION",
        string planToReview = "PLAN") =>
        AdversarialPlanReview.Render(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            projectContextProjection,
            planToReview);

    public static void AssertContainsImplementationFirstReviewSemantics(string prompt)
    {
        string[] semanticAnchors =
        [
            "implementation-first",
            "implemented capability",
            "documentation theater",
            "false implementation confidence",
            "machine-consumed operational artifacts",
            "executable progress"
        ];

        foreach (string anchor in semanticAnchors)
        {
            Assert.Contains(anchor, prompt, StringComparison.Ordinal);
        }
    }

    public static void AssertNoUnresolvedPlaceholders(string prompt)
    {
        string[] placeholders =
        [
            "{p1}",
            "{p2}",
            "{p3}",
            "{p4}",
            "{p5}",
            "{p6}",
            "{p7}",
            "{projectContextProjection}",
            "{planToReview}"
        ];

        foreach (string placeholder in placeholders)
        {
            Assert.DoesNotContain(placeholder, prompt, StringComparison.Ordinal);
        }
    }
}
