using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Polarion;
using Polarion.Generated.Tracker;
using PolarionMcpTools;
using PolarionRemoteMcpServer.Tests.Unit.Infrastructure;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Baseline name or ID handling for the revision parameter. Exercised through get_workitems_in_module
/// with a strict client, so the exact baseline query and the URI sent to the SDK are both checked.
/// </summary>
public sealed class RevisionResolutionTests
{
    private const string ThisDocumentUri = "subterra:data-service:objects:/default/Starlight_Main${Module}{moduleFolder}MySpace#MyDoc";

    private static (McpTools Tool, Mock<IPolarionClient> Client) NewTool()
    {
        var client = new Mock<IPolarionClient>(MockBehavior.Strict);
        var services = new ServiceCollection();
        services.AddSingleton<IPolarionClientFactory>(new StubPolarionClientFactory(client.Object));
        services.AddSingleton<List<PolarionProjectConfig>>(new List<PolarionProjectConfig>());
        return (new McpTools(services.BuildServiceProvider()), client);
    }

    // Baseline taken on the document under test: baseObjectURI = module URI + "%" + baseline revision.
    private static Baseline NewBaseline(string id, string name, string baseRevision) =>
        new() { id = id, name = name, baseRevision = baseRevision, baseObjectURI = $"{ThisDocumentUri}%{baseRevision}" };

    // Baseline taken on a different document in the same project.
    private static Baseline NewOtherDocumentBaseline(string id, string name, string baseRevision) =>
        new() { id = id, name = name, baseRevision = baseRevision, baseObjectURI = $"subterra:data-service:objects:/default/Starlight_Main${{Module}}{{moduleFolder}}OtherSpace#OtherDoc%{baseRevision}" };

    private static void SetupBaselineQuery(Mock<IPolarionClient> client, string expectedQuery, params Baseline[] result)
    {
        client.Setup(c => c.QueryBaselinesAsync(
                It.Is<string>(q => q == expectedQuery),
                It.IsAny<string>(),
                It.IsAny<bool>()))
            .ReturnsAsync(Result.Ok(result));
    }

    private static void SetupModuleRead(Mock<IPolarionClient> client, string expectedUri)
    {
        client.Setup(c => c.GetModuleWorkItemsAsync(
                It.Is<string>(u => u == expectedUri),
                null,
                true,
                null))
            .ReturnsAsync(Result.Ok(Array.Empty<ModuleWorkItem>()));
    }

    [Fact]
    public async Task BaselineId_ResolvesToItsBaseRevision()
    {
        var (tool, client) = NewTool();
        SetupBaselineQuery(client, "id:301", NewBaseline("301", "Release 301", "900001"));
        SetupModuleRead(client, "subterra:data-service:objects:/default/Starlight_Main${Module}{moduleFolder}MySpace#MyDoc%900001");

        var result = await tool.GetWorkItemsInModule("MySpace", "MyDoc", revision: "301");

        // Empty module: the strict URI setup is what proves the revision reached the SDK call.
        result.Should().Be("No work items found in module 'MySpace/MyDoc'.");
    }

    [Fact]
    public async Task BaselineOnDocumentWhoseIdContainsPercent_IsNotMatchedToThisDocument()
    {
        var (tool, client) = NewTool();
        var otherDocumentBaseline = new Baseline { id = "900012", name = "Shared", baseRevision = "900012", baseObjectURI = $"{ThisDocumentUri}%x%900012" };
        SetupBaselineQuery(client, "name:\"Shared\"", otherDocumentBaseline);
        SetupBaselineQuery(client, "id:900012", otherDocumentBaseline);
        SetupModuleRead(client, "subterra:data-service:objects:/default/Starlight_Main${Module}{moduleFolder}MySpace#MyDoc%900012");

        var byName = await tool.GetWorkItemsInModule("MySpace", "MyDoc", revision: "Shared");
        var byId = await tool.GetWorkItemsInModule("MySpace", "MyDoc", revision: "900012");

        byName.Should().Contain("(112)");
        byId.Should().Be("No work items found in module 'MySpace/MyDoc'.");
    }

    [Fact]
    public async Task NumericRevisionWithNoBaselineId_PassesThroughAsRevision()
    {
        var (tool, client) = NewTool();
        SetupBaselineQuery(client, "id:900002");
        SetupModuleRead(client, "subterra:data-service:objects:/default/Starlight_Main${Module}{moduleFolder}MySpace#MyDoc%900002");

        var result = await tool.GetWorkItemsInModule("MySpace", "MyDoc", revision: "900002");

        result.Should().Be("No work items found in module 'MySpace/MyDoc'.");
    }

    [Fact]
    public async Task BaselineId_OfAnotherDocument_PassesThroughAsRevision()
    {
        var (tool, client) = NewTool();
        SetupBaselineQuery(client, "id:400", NewOtherDocumentBaseline("400", "Other", "900010"));
        SetupModuleRead(client, "subterra:data-service:objects:/default/Starlight_Main${Module}{moduleFolder}MySpace#MyDoc%400");

        var result = await tool.GetWorkItemsInModule("MySpace", "MyDoc", revision: "400");

        // The ID belongs to another document, so it is not resolved to 900010; it is read as a plain revision.
        result.Should().Be("No work items found in module 'MySpace/MyDoc'.");
    }

    [Fact]
    public async Task BaselineName_IsEscapedInQuery_AndMatchesExactly()
    {
        var (tool, client) = NewTool();
        const string name = "Release \"A\" \\ 1/2";
        SetupBaselineQuery(client, "name:\"Release \\\"A\\\" \\\\ 1/2\"", NewBaseline("10", name, "900001"));
        SetupModuleRead(client, "subterra:data-service:objects:/default/Starlight_Main${Module}{moduleFolder}MySpace#MyDoc%900001");

        var result = await tool.GetWorkItemsInModule("MySpace", "MyDoc", revision: name);

        result.Should().NotStartWith("ERROR");
    }

    [Fact]
    public async Task BaselineName_DifferentCaseOnly_IsRejectedWith112()
    {
        var (tool, client) = NewTool();
        SetupBaselineQuery(client, "name:\"Release X\"", NewBaseline("11", "release x", "900001"));

        var result = await tool.GetWorkItemsInModule("MySpace", "MyDoc", revision: "Release X");

        result.Should().StartWith("ERROR: (112)");
    }

    [Fact]
    public async Task BaselineName_BracketsMissing_Returns112AndListsTheExactName()
    {
        var (tool, client) = NewTool();
        SetupBaselineQuery(client, "name:\"Release X\"", NewBaseline("40", "[Release X] Plan", "900004"));

        var result = await tool.GetWorkItemsInModule("MySpace", "MyDoc", revision: "Release X");

        result.Should().StartWith("ERROR: (112)");
        result.Should().Contain("40 [Release X] Plan");
    }

    [Fact]
    public async Task BaselineName_NoMatches_ReturnsCode112()
    {
        var (tool, client) = NewTool();
        SetupBaselineQuery(client, "name:\"Missing\"");

        var result = await tool.GetWorkItemsInModule("MySpace", "MyDoc", revision: "Missing");

        result.Should().StartWith("ERROR: (112)");
    }

    [Fact]
    public async Task BaselineName_OnlyAnotherDocumentHasIt_Returns112()
    {
        var (tool, client) = NewTool();
        SetupBaselineQuery(client, "name:\"Shared\"", NewOtherDocumentBaseline("30", "Shared", "900010"));

        var result = await tool.GetWorkItemsInModule("MySpace", "MyDoc", revision: "Shared");

        // Must not silently resolve to the other document's revision.
        result.Should().StartWith("ERROR: (112)");
    }

    [Fact]
    public async Task BaselineName_SameNameOnTwoDocuments_UsesThisDocumentOnly()
    {
        var (tool, client) = NewTool();
        SetupBaselineQuery(client, "name:\"Shared\"",
            NewOtherDocumentBaseline("30", "Shared", "900010"),
            NewBaseline("31", "Shared", "900011"));
        SetupModuleRead(client, "subterra:data-service:objects:/default/Starlight_Main${Module}{moduleFolder}MySpace#MyDoc%900011");

        var result = await tool.GetWorkItemsInModule("MySpace", "MyDoc", revision: "Shared");

        // No 113: the other document's baseline with the same name is out of scope.
        result.Should().NotStartWith("ERROR");
    }

    [Fact]
    public async Task BaselineName_TwoMatchesWithDifferentRevisions_ReturnsCode113()
    {
        var (tool, client) = NewTool();
        SetupBaselineQuery(client, "name:\"Dup\"",
            NewBaseline("20", "Dup", "900001"),
            NewBaseline("21", "Dup", "900003"));

        var result = await tool.GetWorkItemsInModule("MySpace", "MyDoc", revision: "Dup");

        result.Should().StartWith("ERROR: (113)");
    }

    [Fact]
    public async Task BaselineName_TwoMatchesWithSameRevision_IsAccepted()
    {
        var (tool, client) = NewTool();
        SetupBaselineQuery(client, "name:\"Dup\"",
            NewBaseline("20", "Dup", "900001"),
            NewBaseline("21", "Dup", "900001"));
        SetupModuleRead(client, "subterra:data-service:objects:/default/Starlight_Main${Module}{moduleFolder}MySpace#MyDoc%900001");

        var result = await tool.GetWorkItemsInModule("MySpace", "MyDoc", revision: "Dup");

        result.Should().NotStartWith("ERROR");
    }

    [Fact]
    public async Task MissingDocument_AtHead_Returns1044WithoutRevisionCauses()
    {
        var (tool, client) = NewTool();
        client.Setup(c => c.GetModuleWorkItemsAsync(It.Is<string>(u => u == ThisDocumentUri), null, true, null))
            .ReturnsAsync(Result.Fail<ModuleWorkItem[]>("UnresolvableObjectException: no such module"));

        var result = await tool.GetWorkItemsInModule("MySpace", "MyDoc");

        result.Should().StartWith("ERROR: (1044) Failed to fetch work items from module 'MySpace/MyDoc'.");
        result.Should().Contain("UnresolvableObjectException");
        result.Should().NotContain("Common causes");
    }

    [Fact]
    public async Task MissingDocument_AtRevision_Returns1044WithRevisionCauses()
    {
        var (tool, client) = NewTool();
        SetupBaselineQuery(client, "id:900002");
        client.Setup(c => c.GetModuleWorkItemsAsync(It.Is<string>(u => u == $"{ThisDocumentUri}%900002"), null, true, null))
            .ReturnsAsync(Result.Fail<ModuleWorkItem[]>("UnresolvableObjectException: no such module"));

        var result = await tool.GetWorkItemsInModule("MySpace", "MyDoc", revision: "900002");

        result.Should().StartWith("ERROR: (1044) The document 'MySpace/MyDoc' could not be found at revision '900002'.");
        result.Should().Contain("Common causes");
    }

    [Fact]
    public async Task BaselineLookup_Failure_ReturnsCode111()
    {
        var (tool, client) = NewTool();
        client.Setup(c => c.QueryBaselinesAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>()))
            .ReturnsAsync(Result.Fail<Baseline[]>("boom"));

        var result = await tool.GetWorkItemsInModule("MySpace", "MyDoc", revision: "Release X");

        result.Should().StartWith("ERROR: (111)");
    }
}
