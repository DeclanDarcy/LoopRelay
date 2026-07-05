namespace CommandCenter.Roadmap.Cli;

internal sealed class RoadmapStepException : Exception
{
    public RoadmapStepException(
        string message,
        RoadmapFailurePersistence persistence = RoadmapFailurePersistence.RequiresPersistence,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Persistence = persistence;
    }

    public RoadmapFailurePersistence Persistence { get; }

    public static RoadmapStepException AlreadyPersisted(Exception exception) =>
        exception is RoadmapStepException { Persistence: RoadmapFailurePersistence.AlreadyPersisted } persisted
            ? persisted
            : new RoadmapStepException(exception.Message, RoadmapFailurePersistence.AlreadyPersisted, exception);
}

internal enum RoadmapFailurePersistence
{
    RequiresPersistence,
    AlreadyPersisted,
}
