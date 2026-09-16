using FluentAssertions;
using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Tests for the read-only SQL statement guard.
///
/// Two responsibilities are exercised:
///  * The SELECT-shape guard accepts representative join-heavy read patterns (parent-state
///    joins, cross-document filters, substring LIKE matches).
///  * Breakout attempts (statement stacking, comments, DML/DDL, exec, transaction control,
///    and parentheses inside string literals that could close Polarion's SQL:( term early)
///    are rejected.
///
/// Fixtures use synthetic project/type/field names; the guard is schema-shape sensitive, not
/// value sensitive.
/// </summary>
public sealed class SqlQueryGuardTests
{
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

    // --- Representative read patterns: all must validate ---------------------

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

    // --- Breakout attempts: all must be rejected -----------------------------

    [Theory]
    // statement stacking
    [InlineData("SELECT item.C_PK FROM WORKITEM item; DROP TABLE WORKITEM")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE item.C_ID = '1'; DELETE FROM WORKITEM")]
    // comment-based breakout
    [InlineData("SELECT item.C_PK FROM WORKITEM item -- WHERE item.C_ID = '1'")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item /* comment */")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item # comment")]
    // not a SELECT / DML / DDL
    [InlineData("UPDATE WORKITEM SET C_STATUS = 'x' WHERE C_PK = 1")]
    [InlineData("DELETE FROM WORKITEM WHERE C_PK = 1")]
    [InlineData("DROP TABLE WORKITEM")]
    [InlineData("INSERT INTO WORKITEM (C_PK) VALUES (1)")]
    [InlineData("SELECT item.C_PK INTO backup FROM WORKITEM item")]
    // exec / transaction control
    [InlineData("SELECT item.C_PK FROM WORKITEM item; EXEC sp_who")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE 1=1 COMMIT")]
    // unterminated / unbalanced
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE item.C_ID = 'unterminated")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE (item.C_ID = '1'")]
    // parentheses inside a string literal (could close Polarion's SQL:( term early)
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE item.C_ID LIKE '%)%' OR 1=1")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE item.C_ID LIKE '%) OR project.id:other OR (%'")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item WHERE item.C_ID = '('")]
    // missing required WORKITEM / C_PK contract
    [InlineData("SELECT 1")]
    [InlineData("SELECT proj.C_ID FROM PROJECT proj")]
    public void Validate_BreakoutAndNonReadOnly_AreRejected(string sql)
    {
        var result = SqlQueryGuard.Validate(sql);

        result.IsValid.Should().BeFalse($"'{sql}' must be rejected by the read-only gate");
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Validate_ParenthesisInLiteral_ExplainsWildcardAlternative()
    {
        var result = SqlQueryGuard.Validate(
            "SELECT item.C_PK FROM WORKITEM item WHERE item.C_ID LIKE '%)%' OR 1=1");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Parentheses are not permitted inside SQL string literals");
    }

    [Fact]
    public void Validate_KeywordsInsideStringLiterals_AreNotTreatedAsCode()
    {
        // 'DELETE' and ';' appear only inside a string literal; they are data, not code, so this
        // is a legitimate substring search and must be accepted (no parentheses in the literal).
        var sql = "SELECT item.C_PK FROM WORKITEM item " +
                  "INNER JOIN CF_WORKITEM cf ON cf.FK_WORKITEM = item.C_PK " +
                  "WHERE cf.C_STRING_VALUE LIKE '%DELETE FROM; --%'";

        SqlQueryGuard.Validate(sql).IsValid.Should().BeTrue();
    }
}
