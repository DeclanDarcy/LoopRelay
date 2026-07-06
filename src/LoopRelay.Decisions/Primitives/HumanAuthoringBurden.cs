namespace LoopRelay.Decisions.Primitives;

public enum HumanAuthoringBurden
{
    Unknown,
    ReviewOnly,
    MinorEdit,
    MajorRefinement,
    FullRewrite,
    GenerationBypassed
}
