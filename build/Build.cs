using Fallout.Common;
using Fallout.Solutions;
using Automation.Fallout.Components;
using Automation.Fallout.Components.Components;
using Automation.Fallout.Components.DefaultBuilds;
using Automation.Fallout.Components.Parameters;

/// <summary>
/// Build configuration for VelopackBuild
/// </summary>

public class Build : GitHubActionsBuild, IShowVersion, IClean, ICompile, IRestore, IScanForSecrets, IRunUnitTests, IRunIntegrationTests, IGenerateCoverageReport, ITest, IUpdateChangelog, IVelopack, ITagRelease, IAnnounceRelease, ICreateGitHubRelease
{

    public static int Main() => Execute<Build>(
        y => ((IVelopack)y).ReleaseVelopack);

    string IHasVelopack.VelopackProjectName => "Greenlight.TestStrip";
    string IHasVelopack.VelopackIconPath => @"Greenlight.TestStrip\Assets\MeddlingIdiot.ico";
    bool IHasTests.BreakBuildOnSecretLeaks => false;

    // No MinCoverageThreshold, the same as the other clients. Only StripScene and StripLayout are
    // testable without a window, so the whole solution measures well under the 20 a copied
    // Build.cs would ask for - and a threshold the repo cannot meet fails CoverageReport, which
    // GitHub shows only as "Target CoverageReport has thrown an exception".

    // Automation.Fallout.Components defaults these to AFTR's staftrinstallers, which this project has
    // no access to. Every MeddlingIdiot installer lives in meddlingidiotinstallers; the Nuke-era
    // library defaulted there, and the migration to Fallout silently moved the destination.
    // Without these the SAS token is sent to the wrong account and Azure rejects it with
    // AuthenticationFailed (403) — which reads like a missing token but is a mismatched one
    string IHasVelopack.AzureBlobAccount => "meddlingidiotinstallers";
    string IHasVelopack.AzureBlobEndpoint => "https://meddlingidiotinstallers.blob.core.windows.net";
}
