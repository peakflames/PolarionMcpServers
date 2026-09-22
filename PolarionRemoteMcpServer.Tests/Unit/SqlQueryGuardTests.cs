using FluentAssertions;
using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Tests for the read-only SQL statement guard.
///
/// Every decision branch in SqlQueryGuard.Validate is exercised, and every rejection
/// asserts on the specific error message (not just IsValid==false), so a case can only
/// pass tests if it tripped the intended branch.
///
/// Fixtures use synthetic project/type/field names; the guard is schema-shape sensitive,
/// not value sensitive.
/// </summary>
public sealed class SqlQueryGuardTests
{
    // -------------------------------------------------------------------------
    // Shared valid-query building blocks
    // -------------------------------------------------------------------------

    private const string ValidBase =
        "SELECT item.C_PK FROM WORKITEM item WHERE item.C_TYPE = 'requirement'";

    private const string SubstringMatch =
        "SELECT item.C_PK FROM WORKITEM item " +
        "INNER JOIN PROJECT proj ON proj.C_URI = item.FK_URI_PROJECT " +
        "INNER JOIN CF_WORKITEM cf ON cf.FK_WORKITEM = item.C_PK " +
        "WHERE proj.C_ID = 'Starlight_Main' AND item.C_TYPE = 'artifactA' " +
        "AND cf.C_NAME = 'customFieldA' AND cf.C_STRING_VALUE LIKE '%STR-1234%'";

    private const string ChildrenWithParents =
        "SELECT child.C_PK FROM WORKITEM child WHERE child.C_TYPE = 'artifactB' " +
        "AND EXISTS ( SELECT link.* FROM STRUCT_WORKITEM_LINKEDWORKITEMS link, WORKITEM parent " +
        "WHERE link.C_ROLE = 'configures' AND link.FK_URI_WORKITEM = parent.C_URI " +
        "AND link.FK_URI_P_WORKITEM = child.C_URI AND parent.C_TYPE = 'artifactA' )";

    private const string ChildrenByParentProps =
        "SELECT item.C_PK FROM WORKITEM item " +
        "INNER JOIN PROJECT AS proj ON item.FK_URI_PROJECT = proj.C_URI " +
        "INNER JOIN CF_WORKITEM AS customfield1 ON customfield1.FK_WORKITEM = item.C_PK " +
        "WHERE proj.C_ID = 'Starlight_Main' AND item.C_TYPE = 'artifactA' " +
        "AND customfield1.C_NAME = 'customFieldC' AND customfield1.C_STRING_VALUE = 'no' " +
        "AND EXISTS (SELECT link.* FROM STRUCT_WORKITEM_LINKEDWORKITEMS link, WORKITEM parent " +
        "INNER JOIN CF_WORKITEM customfield2 on customfield2.FK_WORKITEM = parent.C_PK " +
        "WHERE link.C_ROLE = 'satisfies' AND link.FK_URI_P_WORKITEM = item.C_URI " +
        "AND link.FK_URI_WORKITEM = parent.C_URI AND customfield2.C_NAME = 'customFieldC' " +
        "AND customfield2.C_STRING_VALUE = 'yes')";

    private const string ParentStateJoin =
        "SELECT child.C_PK FROM WORKITEM child, PROJECT proj, MODULE doc2 " +
        "WHERE proj.C_ID = 'Starlight_Main' AND doc2.C_ID = 'my_requirements_doc' " +
        "AND doc2.FK_URI_PROJECT = proj.C_URI AND child.C_TYPE = 'artifactB' " +
        "AND child.FK_URI_MODULE = doc2.C_URI AND EXISTS ( SELECT link.* " +
        "FROM STRUCT_WORKITEM_LINKEDWORKITEMS link INNER JOIN WORKITEM parent " +
        "ON link.FK_URI_WORKITEM = parent.C_URI WHERE link.C_ROLE = 'configures' " +
        "AND link.FK_URI_P_WORKITEM = child.C_URI AND parent.C_TYPE = 'requirement' " +
        "AND ( parent.C_STATUS NOT IN ('proposed', 'inValidation', 'validated') OR " +
        "( parent.C_STATUS IN ('proposed', 'inValidation') AND ( EXISTS ( SELECT 1 FROM CF_WORKITEM cf1 " +
        "WHERE cf1.FK_WORKITEM = parent.C_PK AND cf1.C_NAME = 'customFieldB' AND cf1.C_STRING_VALUE = 'no' ) ) ) ) )";

    // -------------------------------------------------------------------------
    // A1 — Representative read patterns: all must validate
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(nameof(SubstringMatch))]
    [InlineData(nameof(ChildrenWithParents))]
    [InlineData(nameof(ChildrenByParentProps))]
    [InlineData(nameof(ParentStateJoin))]
    public void Validate_ReadPatterns_AreAccepted(string which)
    {
        var sql = which switch
        {
            nameof(SubstringMatch) => SubstringMatch,
            nameof(ChildrenWithParents) => ChildrenWithParents,
            nameof(ChildrenByParentProps) => ChildrenByParentProps,
            nameof(ParentStateJoin) => ParentStateJoin,
            _ => throw new ArgumentOutOfRangeException(nameof(which)),
        };

        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeTrue($"'{which}' is a legitimate read-only query. Error: {result.Error}");
    }

    // -------------------------------------------------------------------------
    // A1 — Per-cause rejection theories with specific error message assertions
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrWhitespace_RejectsWithEmptyMessage(string? sql)
    {
        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("cannot be empty");
    }

    [Theory]
    [InlineData("UPDATE WORKITEM SET C_STATUS = 'x' WHERE C_PK = 1")]
    [InlineData("DELETE FROM WORKITEM WHERE C_PK = 1")]
    [InlineData("DROP TABLE WORKITEM")]
    [InlineData("1 + 1")]
    public void Validate_NotASelect_RejectsWithSelectMessage(string sql)
    {
        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Only read-only SELECT");
    }

    [Theory]
    [InlineData("SELECT item.C_PK FROM WORKITEM item; DROP TABLE WORKITEM")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE item.C_ID = '1'; DELETE FROM WORKITEM")]
    public void Validate_StatementStacking_RejectsWithSemicolonMessage(string sql)
    {
        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Statement terminators");
    }

    [Theory]
    [InlineData("SELECT item.C_PK FROM WORKITEM item -- WHERE item.C_ID = '1'")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item /* comment */")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE 1=1 */")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item # comment")]
    public void Validate_Comments_RejectsWithCommentMessage(string sql)
    {
        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("SQL comments are not permitted");
    }

    [Theory]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE item.C_ID LIKE '%)%' OR 1=1")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE item.C_ID LIKE '%) OR project.id:other OR (%'")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE item.C_ID = '('")]
    public void Validate_ParenthesisInLiteral_RejectsWithLiteralMessage(string sql)
    {
        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Parentheses are not permitted inside SQL string literals");
    }

    [Fact]
    public void Validate_UnterminatedLiteral_RejectsWithUnterminatedMessage()
    {
        var result = SqlQueryGuard.Validate(
            "SELECT item.C_PK FROM WORKITEM item WHERE item.C_ID = 'unterminated");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("unterminated string literal");
    }

    [Theory]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE (item.C_ID = '1'")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE item.C_ID = '1'))")]
    public void Validate_UnbalancedParens_RejectsWithBalanceMessage(string sql)
    {
        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Unbalanced parentheses");
    }

    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("SELECT proj.C_ID FROM PROJECT proj")]
    public void Validate_MissingWorkitemTable_RejectsWithWorkitemMessage(string sql)
    {
        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("must read from the Polarion WORKITEM table");
    }

    [Theory]
    [InlineData("SELECT item.C_TYPE FROM WORKITEM item WHERE item.C_PK > 0")]
    [InlineData("SELECT item.C_STATUS FROM WORKITEM item WHERE item.C_PK IN (1, 2, 3)")]
    public void Validate_MissingCpkInSelectList_RejectsWithCpkMessage(string sql)
    {
        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("must project the work-item primary key");
    }

    [Theory]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE C_PK IN xp_cmdshell('dir')")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE C_PK IN sp_executesql('SELECT 1')")]
    public void Validate_StoredProcPrefix_RejectsWithStoredProcMessage(string sql)
    {
        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Stored-procedure call");
    }

    // -------------------------------------------------------------------------
    // A2 — All 32 forbidden keywords, one InlineData each, with message assertion
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("INSERT INTO WORKITEM (C_PK) VALUES (1)", "INSERT")]
    [InlineData("SELECT item.C_PK INTO backup FROM WORKITEM item", "INTO")]
    [InlineData("UPDATE WORKITEM SET C_STATUS = 'x' WHERE C_PK = 1", "UPDATE")]
    [InlineData("DELETE FROM WORKITEM WHERE C_PK = 1", "DELETE")]
    [InlineData("DROP TABLE WORKITEM", "DROP")]
    [InlineData("ALTER TABLE WORKITEM ADD COLUMN C_EXTRA VARCHAR(100)", "ALTER")]
    [InlineData("CREATE TABLE evil AS SELECT C_PK FROM WORKITEM", "CREATE")]
    [InlineData("TRUNCATE TABLE WORKITEM", "TRUNCATE")]
    [InlineData("MERGE WORKITEM USING src ON WORKITEM.C_PK = src.C_PK WHEN MATCHED THEN UPDATE SET C_STATUS = 'x'", "MERGE")]
    [InlineData("GRANT SELECT ON WORKITEM TO PUBLIC", "GRANT")]
    [InlineData("REVOKE SELECT ON WORKITEM FROM PUBLIC", "REVOKE")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item; EXEC sp_who", "EXEC")]
    [InlineData("EXECUTE xp_cmdshell('dir')", "EXECUTE")]
    [InlineData("CALL someproc()", "CALL")]
    [InlineData("REPLACE INTO WORKITEM (C_PK) VALUES (1)", "REPLACE")]
    [InlineData("RENAME TABLE WORKITEM TO WI_BACKUP", "RENAME")]
    [InlineData("ATTACH DATABASE 'other.db' AS other", "ATTACH")]
    [InlineData("DETACH DATABASE other", "DETACH")]
    [InlineData("PRAGMA journal_mode=WAL", "PRAGMA")]
    [InlineData("VACUUM", "VACUUM")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE 1=1 COMMIT", "COMMIT")]
    [InlineData("ROLLBACK TRANSACTION", "ROLLBACK")]
    [InlineData("SAVEPOINT sp1", "SAVEPOINT")]
    [InlineData("LOCK TABLE WORKITEM IN EXCLUSIVE MODE", "LOCK")]
    [InlineData("SET TRANSACTION ISOLATION LEVEL READ COMMITTED", "SET")]
    [InlineData("USE master", "USE")]
    [InlineData("SHUTDOWN", "SHUTDOWN")]
    [InlineData("DECLARE @v INT = 1", "DECLARE")]
    [InlineData("SELECT C_PK FROM WORKITEM WHERE C_PK > 0 WAITFOR DELAY '0:0:5'", "WAITFOR")]
    [InlineData("SELECT C_PK FROM WORKITEM UNION SELECT secret FROM other_table", "UNION")]
    [InlineData("SELECT C_PK FROM WORKITEM INTERSECT SELECT C_PK FROM PROJECT", "INTERSECT")]
    [InlineData("SELECT C_PK FROM WORKITEM EXCEPT SELECT C_PK FROM PROJECT", "EXCEPT")]
    public void Validate_ForbiddenKeyword_AreRejected(string sql, string keyword)
    {
        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeFalse($"keyword '{keyword}' must be rejected");
        result.Error.Should().NotBeNullOrEmpty();
    }

    // SELECT-shaped queries that do not trip any earlier guard (no ';', no comments, no
    // paren-in-literal, no unterminated literal) — these hit the keyword branch specifically.
    [Theory]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE 1=1 COMMIT", "COMMIT")]
    [InlineData("SELECT C_PK FROM WORKITEM WHERE C_PK > 0 WAITFOR DELAY '0:0:5'", "WAITFOR")]
    [InlineData("SELECT C_PK FROM WORKITEM UNION SELECT secret FROM other_table", "UNION")]
    [InlineData("SELECT C_PK FROM WORKITEM INTERSECT SELECT C_PK FROM PROJECT", "INTERSECT")]
    [InlineData("SELECT C_PK FROM WORKITEM EXCEPT SELECT C_PK FROM PROJECT", "EXCEPT")]
    [InlineData("SELECT item.C_PK INTO backup FROM WORKITEM item", "INTO")]
    public void Validate_SelectShapedForbiddenKeyword_RejectsWithDisallowedKeywordMessage(string sql, string keyword)
    {
        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Disallowed keyword");
        result.Error.Should().Contain(keyword);
    }

    // -------------------------------------------------------------------------
    // A3 — Untested branches
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_QueryExceedsMaxLength_RejectsWithLengthMessage()
    {
        var padding = new string('A', 8001);
        var sql = $"SELECT item.C_PK FROM WORKITEM item WHERE item.C_ID = '{padding}'";

        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("maximum allowed length");
    }

    [Fact]
    public void Validate_QueryExactlyAtMaxLength_IsAccepted()
    {
        var baseQuery = "SELECT item.C_PK FROM WORKITEM item WHERE item.C_TYPE = 'requirement'";
        var paddingNeeded = 8000 - baseQuery.Length;
        var segment = " AND 1=1";
        var fullPadding = string.Concat(Enumerable.Repeat(segment, (paddingNeeded / segment.Length) + 1));
        var sql = (baseQuery + fullPadding).Substring(0, 8000);

        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeTrue($"a query padded to exactly 8000 chars must be accepted. Error: {result.Error}");
    }

    [Fact]
    public void Validate_ExtraClosingParen_RejectsWithBalanceMessage()
    {
        var sql = "SELECT item.C_PK FROM WORKITEM item WHERE (item.C_TYPE = 'requirement'))";

        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Unbalanced parentheses");
    }

    [Fact]
    public void Validate_LeadingParenSelect_IsAccepted()
    {
        // Exercises LeadingSelectRegex ^\(*\s*SELECT pattern.
        var sql = "(SELECT item.C_PK FROM WORKITEM item WHERE item.C_TYPE = 'requirement')";

        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeTrue($"leading-paren SELECT must be accepted. Error: {result.Error}");
    }

    [Fact]
    public void Validate_CpkOnlyInSubquerySelectList_RejectsWithCpkMessage()
    {
        // The outer SELECT projects C_TYPE, not C_PK. C_PK appears only inside the subquery's
        // SELECT list. CpkAppearsInSelectList must skip inner SELECTs via depth tracking and
        // reject this because the top-level SELECT does not project C_PK.
        var sql =
            "SELECT item.C_TYPE FROM WORKITEM item " +
            "WHERE EXISTS (SELECT item.C_PK FROM WORKITEM item WHERE item.C_PK > 0)";

        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("must project the work-item primary key");
    }

    // -------------------------------------------------------------------------
    // A4 — Accept-side robustness (proves the guard doesn't over-reject)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE item.C_UPDATED > '2024-01-01'",
        "C_UPDATED contains UPDATE as a substring — word boundary must not flag it")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE item.C_SETTING = 'created'",
        "C_SETTING contains SET; 'created' contains CREATE — word boundaries must not flag these")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE item.C_TYPE = 'requirement'",
        "column alias 'requirement' contains no flagged keyword")]
    public void Validate_IdentifiersContainingForbiddenKeywordSubstrings_AreAccepted(string sql, string reason)
    {
        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeTrue(reason + $". Error: {result.Error}");
    }

    [Fact]
    public void Validate_FullyLowercase_IsAccepted()
    {
        var result = SqlQueryGuard.Validate(
            "select item.c_pk from workitem item where item.c_type = 'requirement'");

        result.IsValid.Should().BeTrue($"fully lowercase valid query must be accepted. Error: {result.Error}");
    }

    [Fact]
    public void Validate_LeadingAndTrailingWhitespace_IsAccepted()
    {
        var result = SqlQueryGuard.Validate(
            "   SELECT item.C_PK FROM WORKITEM item WHERE item.C_TYPE = 'requirement'   ");

        result.IsValid.Should().BeTrue($"leading/trailing whitespace must be trimmed before validation. Error: {result.Error}");
    }

    [Fact]
    public void Validate_ForbiddenKeywordsInsideStringLiterals_AreNotTreatedAsCode()
    {
        // 'DELETE' and ';' appear only inside a string literal; they are data, not code.
        var sql = "SELECT item.C_PK FROM WORKITEM item " +
                  "INNER JOIN CF_WORKITEM cf ON cf.FK_WORKITEM = item.C_PK " +
                  "WHERE cf.C_STRING_VALUE LIKE '%DELETE FROM; --%'";

        SqlQueryGuard.Validate(sql).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DropKeywordInsideStringLiteral_IsAccepted()
    {
        // 'DROP TABLE' appears only inside a LIKE pattern — the guard must treat it as data.
        var sql = "SELECT item.C_PK FROM WORKITEM item " +
                  "WHERE item.C_TITLE LIKE '%DROP TABLE%'";

        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeTrue($"forbidden keyword inside a string literal must be ignored. Error: {result.Error}");
    }
}
