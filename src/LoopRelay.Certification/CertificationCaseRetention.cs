namespace LoopRelay.Certification;

internal static class CertificationCaseRetention
{
    public static bool ShouldPreserve(
        bool retainSuccessfulCase,
        CertificationClassification classification) =>
        retainSuccessfulCase || classification != CertificationClassification.Passed;
}
