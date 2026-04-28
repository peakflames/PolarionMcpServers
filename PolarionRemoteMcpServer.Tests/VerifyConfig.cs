using System.Runtime.CompilerServices;

namespace PolarionRemoteMcpServer.Tests;

public static class VerifyConfig
{
    [ModuleInitializer]
    public static void Init()
    {
        // Scrub volatile fields (DateTimes/Guids) for stable cross-run comparisons.
        // This is the Verify default — declared explicitly for clarity.
        // If you need exact date values in snapshots, call VerifierSettings.DontScrubDateTimes() here.
        VerifierSettings.DontScrubDateTimes();
    }
}
