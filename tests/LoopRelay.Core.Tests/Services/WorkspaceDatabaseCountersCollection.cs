namespace LoopRelay.Core.Tests.Services;

/// <summary>
/// Test classes that assert on <c>LoopRelayWorkspaceDatabase</c>'s process-wide test-only
/// counters (<c>FullVerificationRuns</c>, <c>RepairTransactionsOpened</c>,
/// <c>ShapeRequirementProbes</c>) or that reset its per-process schema-verification memo.
///
/// <para>
/// Those counters are plain static fields, so any other test running concurrently that opens a
/// workspace database moves them underneath an assertion. xUnit runs test classes in parallel by
/// default, so membership in this collection - with
/// <see cref="Xunit.CollectionDefinitionAttribute.DisableParallelization"/> - is what makes
/// "the counter did not move" a claim about the code under test rather than about scheduling luck.
/// </para>
/// </summary>
[Xunit.CollectionDefinition("WorkspaceDatabaseCounters", DisableParallelization = true)]
public sealed class WorkspaceDatabaseCountersCollection;
