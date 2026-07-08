using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public sealed record ExplicitHitlNonImplementationRequestCaptureResult(
    int CapturedCount,
    IReadOnlyList<NonImplementationHitlRequestEntry> CapturedRequests);
