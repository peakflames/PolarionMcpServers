using FluentAssertions;
using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Unit tests for the raw-Lucene passthrough behavior.
///
/// Field-scoped filters, boolean grouping, and parenthesized composition are now passed
/// through to the transport verbatim, while the legacy simple-term OR behavior is preserved
/// for existing callers.
/// </summary>
public sealed class LucenePassthroughTests
{
    // --- Passthrough: field-scoped filters -----------------------------------

    [Theory]
    [InlineData("category.KEY:MyCategory")]
    [InlineData("type:requirement")]
    [InlineData("customFieldA.KEY:yes")]
    [InlineData("HAS_VALUE:rationale")]
    public void BuildTextSearchQuery_FieldScopedFilter_IsPassedThroughVerbatim(string query)
    {
        McpTools.BuildTextSearchQuery(query).Should().Be(query,
            "field-scoped Lucene filters must not be re-tokenized into an OR of terms");
    }

    // --- Passthrough: boolean grouping ---------------------------------------

    [Theory]
    [InlineData("HVBIT AND timeout")]
    [InlineData("HVBIT OR timeout")]
    [InlineData("NOT HAS_VALUE:rationale")]
    [InlineData("type:(artifactA artifactB requirement) AND NOT HAS_VALUE:rationale")]
    public void BuildTextSearchQuery_BooleanOperators_ArePassedThroughVerbatim(string query)
    {
        McpTools.BuildTextSearchQuery(query).Should().Be(query,
            "explicit Lucene boolean operators must be preserved as written");
    }

    // --- Passthrough: parenthesized composition ------------------------------

    [Theory]
    [InlineData("(timeout)")]
    [InlineData("category.KEY:MyCategory AND (timeout)")]
    [InlineData("type:requirement AND customFieldA.KEY:yes AND document.id:(MySpace/my_alerts_doc MySpace/my_comms_doc)")]
    public void BuildTextSearchQuery_ParenthesizedComposition_IsPassedThroughVerbatim(string query)
    {
        McpTools.BuildTextSearchQuery(query).Should().Be(query,
            "parenthesized Lucene composition must be preserved as written");
    }

    // --- Regression: legacy simple-term behavior is unchanged ----------------

    [Fact]
    public void BuildTextSearchQuery_TwoSimpleTerms_StillBecomeOrGroup()
    {
        McpTools.BuildTextSearchQuery("HVBIT timeout").Should().Be("(HVBIT OR timeout)",
            "simple multi-term queries must keep the default OR behavior");
    }

    [Fact]
    public void BuildTextSearchQuery_SingleTerm_IsUnchanged()
    {
        McpTools.BuildTextSearchQuery("HVBIT").Should().Be("HVBIT");
    }

    [Fact]
    public void BuildTextSearchQuery_ExactPhrase_IsUnchanged()
    {
        McpTools.BuildTextSearchQuery("\"HVBIT timeout\"").Should().Be("\"HVBIT timeout\"");
    }

    [Theory]
    [InlineData("HVBIT", false)]
    [InlineData("HVBIT timeout", false)]
    [InlineData("voltage regulator failure", false)]
    [InlineData("error and timeout", false)]              // lowercase 'and' is a term
    [InlineData("category.KEY:MyCategory", true)]
    [InlineData("HVBIT AND timeout", true)]
    [InlineData("(timeout)", true)]
    [InlineData("NOT HAS_VALUE:rationale", true)]
    public void LooksLikeRawLucene_ClassifiesQueriesCorrectly(string query, bool expected)
    {
        McpTools.LooksLikeRawLucene(query).Should().Be(expected);
    }

    // --- Raw-Lucene acceptance: a field-scoped query reaches the transport intact ---

    [Fact]
    public void BuildLuceneQuery_RawLuceneAcceptanceScenario_EmitsLuceneVerbatim()
    {
        const string rawLucene = "category.KEY:MyCategory AND (timeout)";

        var built = McpTools.BuildLuceneQuery(rawLucene, itemTypes: null, statusFilter: null);

        built.Should().Be("(category.KEY:MyCategory AND (timeout))");
        built.Should().NotContain("category.KEY:MyCategory OR",
            "the raw Lucene must not be re-tokenized into an OR of terms");
    }

    [Fact]
    public void BuildLuceneQuery_RawLucenePlusTypeAndStatusFilters_PreservesComposition()
    {
        var built = McpTools.BuildLuceneQuery(
            "category.KEY:MyCategory AND (timeout)",
            itemTypes: "requirement",
            statusFilter: "open");

        built.Should().Be(
            "(category.KEY:MyCategory AND (timeout)) AND type:requirement AND status:open");
    }
}
