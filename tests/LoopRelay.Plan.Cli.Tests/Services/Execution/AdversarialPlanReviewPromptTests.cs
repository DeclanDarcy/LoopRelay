using LoopRelay.Core.Prompts;
using Xunit;

namespace LoopRelay.Plan.Cli.Tests.Services.Execution;

public class AdversarialPlanReviewPromptTests
{
    [Fact]
    public void Render_ReplacesInputsAndCarriesTemplateOwnedReviewSemantics()
    {
        string prompt = AdversarialPlanReview.Render("PROJECTION", "PLAN");

        Assert.Contains("PROJECTION", prompt, StringComparison.Ordinal);
        Assert.Contains("PLAN", prompt, StringComparison.Ordinal);
        AdversarialPlanReviewPromptTestAssertions.AssertContainsImplementationFirstReviewSemantics(prompt);
        AdversarialPlanReviewPromptTestAssertions.AssertNoUnresolvedPlaceholders(prompt);
    }

    [Fact]
    public void Render_PreservesReviewOutputCompatibility()
    {
        string prompt = AdversarialPlanReview.Render("PROJECTION", "PLAN");

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
            "{projectContextProjection}",
            "{planToReview}"
        ];

        foreach (string placeholder in placeholders)
        {
            Assert.DoesNotContain(placeholder, prompt, StringComparison.Ordinal);
        }
    }
}
