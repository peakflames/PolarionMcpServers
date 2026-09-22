using FluentAssertions;
using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Containment tests for the plain search path.
///
/// The server scopes every search to the route project by AND-ing <c>project.id</c> onto
/// the caller's Lucene. These tests prove three escape vectors are rejected before a query
/// is built or sent:
///   1. a SQL:(...) filter smuggled through the search text (owned by the opt-in SQL tool);
///   2. an unbalanced parenthesis/quote that could re-associate the project.id suffix;
///   3. a non-identifier type/status value that could re-open Lucene grouping.
/// </summary>
public sealed class LuceneDetectorHardeningTests
{
    // --- ContainsSqlFilter ---------------------------------------------------

    [Theory]
    [InlineData("SQL:(SELECT item.C_PK FROM WORKITEM item)")]
    [InlineData("sql:(SELECT item.C_PK FROM WORKITEM item)")]
    [InlineData("SQL :(SELECT 1)")]
    [InlineData("customFieldA:subsection_of=PROJ* AND SQL:(SELECT item.C_PK FROM WORKITEM item)")]
    // Adjacent-operator forms — previously missed by the (^|\s|\() anchor.
    // All of these must be detected; passing them to the plain search would bypass
    // the SqlQueryGuard and execute raw SQL through Polarion's credential.
    [InlineData("-SQL:(SELECT item.C_PK FROM WORKITEM item)")]   // Lucene exclusion prefix
    [InlineData("+SQL:(SELECT item.C_PK FROM WORKITEM item)")]   // Lucene required prefix
    [InlineData("!SQL:(SELECT item.C_PK FROM WORKITEM item)")]   // Lucene NOT prefix
    [InlineData("x:SQL:(SELECT item.C_PK FROM WORKITEM item)")] // colon-adjacent (field:SQL:)
    public void ContainsSqlFilter_DetectsEmbeddedSql(string query)
    {
        McpTools.ContainsSqlFilter(query).Should().BeTrue(
            "SQL:(...) filters must be recognized so they can be blocked from the plain search path");
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("category.KEY:MyCategory AND (timeout)")]
    [InlineData("MySQL:database")]        // 'SQL' not at a token boundary — 'y' is an identifier char
    [InlineData("NoSQL migration")]
    public void ContainsSqlFilter_DoesNotFlagOrdinaryQueries(string query)
    {
        McpTools.ContainsSqlFilter(query).Should().BeFalse();
    }

    // --- HasBalancedLuceneGrouping -------------------------------------------

    [Theory]
    [InlineData("(a OR b) AND c")]
    [InlineData("\"a (b\"")]              // '(' inside a phrase is literal text
    [InlineData("category.KEY:MyCategory AND (timeout)")]
    [InlineData("plain text")]
    public void HasBalancedLuceneGrouping_AcceptsBalanced(string query)
    {
        McpTools.HasBalancedLuceneGrouping(query).Should().BeTrue();
    }

    [Theory]
    [InlineData("x)) AND project.id:other OR ((y")]
    [InlineData("a) OR project.id:other OR (b")]
    [InlineData("(unclosed")]
    [InlineData("\"unterminated phrase")]
    // Escaped-quote bypass: the attacker uses \" (Lucene backslash-escape for a literal ")
    // to make the naive toggler think the ) is "inside a phrase" when Lucene sees it as a
    // real close-paren.  Input: \")\" OR project.id:other OR \"(\"
    // Without the fix the checker returned balanced=true; Lucene sees ) at depth=-1.
    [InlineData("\\\")\\\" OR project.id:other OR \\\"(\\\"")]
    public void HasBalancedLuceneGrouping_RejectsUnbalanced(string query)
    {
        McpTools.HasBalancedLuceneGrouping(query).Should().BeFalse();
    }

    // --- IsSafeIdentifier ----------------------------------------------------

    [Theory]
    [InlineData("requirement")]
    [InlineData("testCase")]
    [InlineData("in-progress")]
    [InlineData("system.subsystem_req")]
    public void IsSafeIdentifier_AcceptsIdentifiers(string value)
    {
        McpTools.IsSafeIdentifier(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("req) OR project.id:other OR (x")]
    [InlineData("a b")]
    [InlineData("type:x")]
    [InlineData("\"quoted\"")]
    public void IsSafeIdentifier_RejectsNonIdentifiers(string value)
    {
        McpTools.IsSafeIdentifier(value).Should().BeFalse();
    }

    // --- Tool-level rejection (no transport contact) -------------------------

    [Fact]
    public async Task SearchWorkitems_RejectsSqlSmuggledThroughSearchText()
    {
        // A null service provider proves no Polarion client is ever created for a rejected
        // query: rejection happens before the service scope is opened.
        var tools = new McpTools(serviceProvider: null!);

        var result = await tools.SearchWorkitems("SQL:(SELECT item.C_PK FROM WORKITEM item)");

        result.Should().StartWith("ERROR: (105)");
        result.Should().Contain("search_workitems_sql");
    }

    [Fact]
    public async Task SearchWorkitems_RejectsUnbalancedGrouping()
    {
        var tools = new McpTools(serviceProvider: null!);

        var result = await tools.SearchWorkitems("a) OR project.id:other OR (b");

        result.Should().StartWith("ERROR: (106)");
    }

    [Fact]
    public async Task SearchWorkitems_RejectsNonIdentifierItemType()
    {
        var tools = new McpTools(serviceProvider: null!);

        var result = await tools.SearchWorkitems("timeout", itemTypes: "req) OR project.id:other OR (x");

        result.Should().StartWith("ERROR: (107)");
    }

    // --- IsSafeForPolarionPathParam ------------------------------------------

    [Theory]
    [InlineData("MySpace")]
    [InlineData("My Space - Section")]          // example space name with spaces and dashes
    [InlineData("my_doc_id")]
    [InlineData("System Requirements")]
    public void IsSafeForPolarionPathParam_AcceptsValidNames(string value)
    {
        McpTools.IsSafeForPolarionPathParam(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("' OR '1'='1")]            // SQL injection via single-quote
    [InlineData("--injection")]            // SQL line-comment token
    [InlineData("foo; DROP TABLE WORKITEM")] // statement terminator
    [InlineData("x /* comment */")]        // block-comment open
    [InlineData("x */ y")]                 // block-comment close
    [InlineData(null)]
    [InlineData("")]
    public void IsSafeForPolarionPathParam_RejectsInjectionAttempts(string? value)
    {
        McpTools.IsSafeForPolarionPathParam(value).Should().BeFalse();
    }

    // --- AreCsvTokensSafeIdentifiers -----------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AreCsvTokensSafeIdentifiers_NullOrEmptyOrWhitespace_ReturnsTrue(string? csv)
    {
        McpTools.AreCsvTokensSafeIdentifiers(csv).Should().BeTrue(
            "empty/absent filter contributes nothing and is safe");
    }

    [Fact]
    public void AreCsvTokensSafeIdentifiers_SingleValidToken_ReturnsTrue()
    {
        McpTools.AreCsvTokensSafeIdentifiers("requirement").Should().BeTrue();
    }

    [Fact]
    public void AreCsvTokensSafeIdentifiers_MultipleValidTokens_ReturnsTrue()
    {
        McpTools.AreCsvTokensSafeIdentifiers("requirement,testCase,testStep").Should().BeTrue();
    }

    [Fact]
    public void AreCsvTokensSafeIdentifiers_OneInvalidTokenAmongValid_ReturnsFalse()
    {
        McpTools.AreCsvTokensSafeIdentifiers("requirement, x) OR y, testCase").Should().BeFalse(
            "a single invalid token in the CSV must cause the whole value to be rejected");
    }

    [Theory]
    [InlineData("my_type")]
    [InlineData("system.subsystem_req")]
    [InlineData("in-progress")]
    public void AreCsvTokensSafeIdentifiers_TokensWithAllowedChars_ReturnsTrue(string csv)
    {
        McpTools.AreCsvTokensSafeIdentifiers(csv).Should().BeTrue(
            $"'_', '.', '-' are allowed identifier chars; '{csv}' must be accepted");
    }

    [Theory]
    [InlineData("type with space")]
    [InlineData("\"quoted\"")]
    [InlineData("type:colon")]
    public void AreCsvTokensSafeIdentifiers_TokensWithDisallowedChars_ReturnsFalse(string csv)
    {
        McpTools.AreCsvTokensSafeIdentifiers(csv).Should().BeFalse(
            $"spaces, quotes, and colons must be rejected; input: '{csv}'");
    }

    // --- LooksLikeRawLucene: fill untested branches --------------------------

    [Fact]
    public void LooksLikeRawLucene_UrlWithDoubleSlash_ReturnsFalse()
    {
        // The !query.Contains("//") guard prevents URLs from triggering passthrough.
        McpTools.LooksLikeRawLucene("http://example.com").Should().BeFalse(
            "URLs must not be treated as raw Lucene field-scoped queries");
    }

    [Theory]
    [InlineData("12:30")]
    [InlineData("3:1")]
    public void LooksLikeRawLucene_NumericFirstFieldToken_ReturnsFalse(string query)
    {
        // FieldScopedRegex requires [A-Za-z_] as the first char, so numeric-first tokens
        // like time ratios must not trigger passthrough.
        McpTools.LooksLikeRawLucene(query).Should().BeFalse(
            $"'{query}' has a numeric first char — FieldScopedRegex must not match it");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LooksLikeRawLucene_EmptyOrWhitespace_ReturnsFalse(string query)
    {
        McpTools.LooksLikeRawLucene(query).Should().BeFalse();
    }

    // --- HasBalancedLuceneGrouping: edge branches ----------------------------

    [Fact]
    public void HasBalancedLuceneGrouping_EmptyString_ReturnsTrue()
    {
        McpTools.HasBalancedLuceneGrouping(string.Empty).Should().BeTrue();
    }

    [Fact]
    public void HasBalancedLuceneGrouping_TrailingBackslashNotBeforeQuote_ReturnsTrue()
    {
        // A backslash not followed by '"' is treated as a normal character, not an escape.
        McpTools.HasBalancedLuceneGrouping("a\\b").Should().BeTrue(
            "a backslash not before a quote is a normal char; balanced query must pass");
    }

    [Fact]
    public void HasBalancedLuceneGrouping_TrailingLoneBackslash_ReturnsTrue()
    {
        // A trailing backslash (not before '"') — exercises the non-quote path of the escape branch.
        McpTools.HasBalancedLuceneGrouping("a\\").Should().BeTrue(
            "a trailing lone backslash is treated as a normal char; balanced query must pass");
    }

    // --- IsSafeIdentifier: empty and null edge cases -------------------------

    [Fact]
    public void IsSafeIdentifier_EmptyString_ReturnsFalse()
    {
        McpTools.IsSafeIdentifier(string.Empty).Should().BeFalse(
            "regex requires at least one character");
    }

    [Fact]
    public void IsSafeIdentifier_Null_ReturnsFalse()
    {
        McpTools.IsSafeIdentifier(null).Should().BeFalse();
    }

    // --- IsSafeForPolarionPathParam: verify each blocked sequence individually ---

    [Theory]
    [InlineData("foo'bar")]
    [InlineData("foo;bar")]
    [InlineData("foo--bar")]
    [InlineData("foo/*bar")]
    [InlineData("foo*/bar")]
    public void IsSafeForPolarionPathParam_EachBlockedSequence_ReturnsFalse(string value)
    {
        McpTools.IsSafeForPolarionPathParam(value).Should().BeFalse(
            $"'{value}' contains a blocked injection sequence");
    }

    // --- ContainsSqlFilter: boundary and format cases ------------------------

    [Fact]
    public void ContainsSqlFilter_SqlColonAtEndOfString_ReturnsTrue()
    {
        McpTools.ContainsSqlFilter("timeout AND SQL:").Should().BeTrue();
    }

    [Theory]
    [InlineData("\tSQL:(SELECT 1)")]
    [InlineData("a\tSQL:(SELECT 1)")]
    public void ContainsSqlFilter_TabAdjacentSqlFilter_ReturnsTrue(string query)
    {
        McpTools.ContainsSqlFilter(query).Should().BeTrue(
            "tab-preceded SQL: must be detected by the non-identifier lookbehind");
    }

    [Fact]
    public void ContainsSqlFilter_SqlWithoutColon_ReturnsFalse()
    {
        McpTools.ContainsSqlFilter("bare SQL keyword").Should().BeFalse(
            "SQL without a colon is not a SQL:(...) filter");
    }

    // --- BuildTextSearchQuery: edge cases ------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildTextSearchQuery_EmptyOrWhitespace_DoesNotThrow(string input)
    {
        var result = McpTools.BuildTextSearchQuery(input);

        result.Should().NotBeNull();
    }

    [Fact]
    public void BuildTextSearchQuery_TwoQuoteChars_IsNotTreatedAsPhrase()
    {
        // trimmed.Length == 2 fails the > 2 guard, so it is NOT treated as an exact phrase.
        // It falls through to the single-term path and is returned as-is (not wrapped via OR).
        var result = McpTools.BuildTextSearchQuery("\"\"");

        result.Should().Be("\"\"",
            "two-char empty-quote string must be returned as-is through the single-term path, not the phrase path");
        result.Should().NotContain(" OR ",
            "the single-term path must not produce an OR group");
    }

    // --- BuildLuceneQuery: multi-value filters and status-only shape ---------

    [Fact]
    public void BuildLuceneQuery_MultipleItemTypes_WrapsInOrGroup()
    {
        var built = McpTools.BuildLuceneQuery("timeout", itemTypes: "requirement,testCase", statusFilter: null);

        built.Should().Contain("(type:requirement OR type:testCase)",
            "multiple types must be wrapped in an OR group");
    }

    [Fact]
    public void BuildLuceneQuery_SingleItemType_NoWrappingGroup()
    {
        var built = McpTools.BuildLuceneQuery("timeout", itemTypes: "requirement", statusFilter: null);

        built.Should().Contain("type:requirement");
        built.Should().NotContain("(type:requirement)",
            "a single type must not be wrapped in an OR group");
    }

    [Fact]
    public void BuildLuceneQuery_MultipleStatuses_WrapsInOrGroup()
    {
        var built = McpTools.BuildLuceneQuery("timeout", itemTypes: null, statusFilter: "open,in-progress");

        built.Should().Contain("(status:open OR status:in-progress)",
            "multiple statuses must be wrapped in an OR group");
    }

    [Fact]
    public void BuildLuceneQuery_SingleStatus_NoWrappingGroup()
    {
        var built = McpTools.BuildLuceneQuery("timeout", itemTypes: null, statusFilter: "open");

        built.Should().Contain("status:open");
        built.Should().NotContain("(status:open)",
            "a single status must not be wrapped in an OR group");
    }

    [Fact]
    public void BuildLuceneQuery_TextAndStatusFilter_ContainsBoth()
    {
        var built = McpTools.BuildLuceneQuery(
            "type:requirement",
            itemTypes: null,
            statusFilter: "open");

        built.Should().Contain("status:open");
        built.Should().Contain("type:requirement");
    }
}
