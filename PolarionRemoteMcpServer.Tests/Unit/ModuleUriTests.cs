using Polarion;
using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// The server builds the HEAD module URI itself (the SDK helper is internal). These tests pin its
/// format to the SDK's, and check that it agrees with the SDK's public revision builder.
/// </summary>
public sealed class ModuleUriTests
{
    [Fact]
    public void BuildHeadModuleUri_MatchesSdkFormat()
    {
        var uri = ModuleUri.BuildHeadModuleUri("TestProject", "Space Name", "Doc Id - 1");

        uri.Should().Be("subterra:data-service:objects:/default/TestProject${Module}{moduleFolder}Space Name#Doc Id - 1");
    }

    [Fact]
    public void BuildHeadModuleUri_PlusRevisionSuffix_MatchesSdkRevisionBuilder()
    {
        var head = ModuleUri.BuildHeadModuleUri("TestProject", "MySpace", "MyDoc");
        var pinned = PolarionUriParser.BuildModuleUriWithRevision("TestProject", "MySpace", "MyDoc", "900001");

        pinned.Should().Be($"{head}%900001");
    }
}
