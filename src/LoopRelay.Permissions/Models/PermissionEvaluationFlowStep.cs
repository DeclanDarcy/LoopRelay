namespace LoopRelay.Permissions.Models;

public enum PermissionEvaluationFlowStep
{
    RequestReceived = 0,
    ParseCommand = 1,
    RejectUnknownSyntax = 2,
    RejectEmptyCommand = 3,
    CanonicalizeCommand = 4,
    ComputeFingerprint = 5,
    ReadDecisionCache = 6,
    EvaluateRules = 7,
    EnforceInvariants = 8,
    WriteDecisionCache = 9,
    ReturnDecision = 10,
}
