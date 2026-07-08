using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public sealed class NonImplementationSemanticConfirmationParseException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
