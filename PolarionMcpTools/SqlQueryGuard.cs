namespace PolarionMcpTools;

/// <summary>
/// Read-only statement guard for the opt-in Polarion SQL query tool.
///
/// Polarion resolves a <c>SQL:(...)</c> Lucene filter by executing the enclosed SQL against
/// its database through the endpoint's configured Polarion credential. The SQL text originates
/// from tool input, so it must be constrained to a narrow shape before it is wrapped and sent:
///
///   * Read-only: only a single SELECT-shaped read is accepted. DML/DDL, stored-procedure
///     execution, transaction control, and statement stacking are rejected.
///   * No breakout: SQL comments and semicolons (outside string literals) are rejected, so a
///     value or clause cannot terminate the statement and append another.
///   * Contained grouping: parentheses inside a string literal are rejected, and parentheses
///     are required to balance in both the literal-stripped code and the raw text. A ')' inside
///     a SQL literal can otherwise close Polarion's <c>SQL:(</c> term early and escape the
///     project scope the server applies.
///   * Bounded surface: the statement must query the WORKITEM table and project its primary
///     key (C_PK), matching the contract Polarion requires of a SQL: filter.
///
/// The scanner is literal-aware: keywords, semicolons, and comment tokens INSIDE a single-quoted
/// string literal are data, not code, and are ignored.
/// </summary>
internal static class SqlQueryGuard
{
    /// <summary>Upper bound on accepted SQL length (defense against pathological input).</summary>
    private const int MaxSqlLength = 8000;

    // Keywords that would make the statement do more than read. Matched whole-word,
    // case-insensitively, against the code portion (string literals stripped).
    private static readonly string[] ForbiddenKeywords =
    {
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "CREATE", "TRUNCATE",
        "MERGE", "GRANT", "REVOKE", "EXEC", "EXECUTE", "CALL", "INTO",
        "REPLACE", "RENAME", "ATTACH", "DETACH", "PRAGMA", "VACUUM",
        "COMMIT", "ROLLBACK", "SAVEPOINT", "LOCK", "SET", "USE", "SHUTDOWN",
        "DECLARE", "WAITFOR", "XP_", "SP_",
    };

    private static readonly Regex ForbiddenKeywordRegex = new(
        @"(?<![A-Za-z0-9_])(" + string.Join("|", ForbiddenKeywords.Select(Regex.Escape)) + @")(?![A-Za-z0-9_])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // A leading SELECT (whole word) after optional whitespace / opening parens.
    private static readonly Regex LeadingSelectRegex = new(
        @"^\(*\s*SELECT(?![A-Za-z0-9_])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex WorkItemTableRegex = new(
        @"(?<![A-Za-z0-9_])WORKITEM(?![A-Za-z0-9_])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PrimaryKeyProjectionRegex = new(
        @"(?<![A-Za-z0-9_])C_PK(?![A-Za-z0-9_])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Outcome of validating a candidate SQL statement.</summary>
    internal readonly record struct ValidationResult(bool IsValid, string? Error)
    {
        internal static ValidationResult Ok() => new(true, null);
        internal static ValidationResult Fail(string error) => new(false, error);
    }

    /// <summary>
    /// Validates that <paramref name="sql"/> is a single, read-only, SELECT-shaped Polarion
    /// work-item query safe to wrap in a <c>SQL:(...)</c> Lucene filter.
    /// </summary>
    internal static ValidationResult Validate(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return ValidationResult.Fail("SQL query cannot be empty.");
        }

        var trimmed = sql.Trim();

        if (trimmed.Length > MaxSqlLength)
        {
            return ValidationResult.Fail($"SQL query exceeds the maximum allowed length of {MaxSqlLength} characters.");
        }

        // Strip single-quoted string literals so keyword / comment / ';' scanning only sees
        // executable code. A parenthesis inside a literal, or an unterminated literal, is a
        // rejection: either could close Polarion's SQL:( term early and escape project scope.
        if (!TryStripStringLiterals(trimmed, out var code, out var literalError))
        {
            return ValidationResult.Fail(literalError ?? "SQL query has an unterminated string literal.");
        }

        // Must be a SELECT.
        if (!LeadingSelectRegex.IsMatch(trimmed))
        {
            return ValidationResult.Fail("Only read-only SELECT statements are permitted.");
        }

        // No statement stacking / breakout via ';' (outside string literals).
        if (code.Contains(';'))
        {
            return ValidationResult.Fail("Statement terminators (';') are not permitted; only a single SELECT is allowed.");
        }

        // No SQL comments (outside string literals): --, #, /* ... */.
        if (code.Contains("--") || code.Contains("/*") || code.Contains("*/") || code.Contains('#'))
        {
            return ValidationResult.Fail("SQL comments are not permitted.");
        }

        // No writes / DDL / exec / transaction control (outside string literals).
        var keyword = ForbiddenKeywordRegex.Match(code);
        if (keyword.Success)
        {
            return ValidationResult.Fail(
                $"Disallowed keyword '{keyword.Value.ToUpperInvariant()}' found; only read-only SELECT queries are permitted.");
        }

        // Balanced parentheses, on both the code portion and the raw text (defense in depth:
        // literals are already free of parens by the strip step above).
        if (!AreParenthesesBalanced(code) || !AreParenthesesBalanced(trimmed))
        {
            return ValidationResult.Fail("Unbalanced parentheses in SQL query.");
        }

        // Bound the surface to Polarion work-item queries with the required PK projection.
        if (!WorkItemTableRegex.IsMatch(code))
        {
            return ValidationResult.Fail("SQL query must read from the Polarion WORKITEM table.");
        }

        if (!PrimaryKeyProjectionRegex.IsMatch(code))
        {
            return ValidationResult.Fail(
                "SQL query must project the work-item primary key column (C_PK); Polarion SQL: filters resolve on C_PK.");
        }

        return ValidationResult.Ok();
    }

    /// <summary>
    /// Replaces every single-quoted string literal with an empty literal (<c>''</c>) so that
    /// downstream scanning only sees executable code. Honors SQL literal-internal quote doubling
    /// (<c>''</c>). Returns false, with <paramref name="error"/> set, if a literal contains a
    /// parenthesis (which could close Polarion's SQL:( term early) or is left unterminated.
    /// </summary>
    private static bool TryStripStringLiterals(string sql, out string code, out string? error)
    {
        var sb = new StringBuilder(sql.Length);
        var inString = false;
        error = null;

        for (var i = 0; i < sql.Length; i++)
        {
            var c = sql[i];

            if (!inString)
            {
                if (c == '\'')
                {
                    inString = true;
                    sb.Append("''"); // collapse the whole literal to an empty placeholder literal
                }
                else
                {
                    sb.Append(c);
                }
            }
            else // inside a string literal
            {
                if (c == '\'')
                {
                    if (i + 1 < sql.Length && sql[i + 1] == '\'')
                    {
                        i++; // escaped quote inside the literal; consume both, stay in string
                        continue;
                    }

                    inString = false; // closing quote (placeholder already emitted)
                }
                else if (c == '(' || c == ')')
                {
                    code = sb.ToString();
                    error = "Parentheses are not permitted inside SQL string literals; " +
                            "use '%'/'_' LIKE wildcards instead.";
                    return false;
                }
                // else: literal content, dropped
            }
        }

        code = sb.ToString();
        return !inString;
    }

    private static bool AreParenthesesBalanced(string code)
    {
        var depth = 0;
        foreach (var c in code)
        {
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth < 0)
                {
                    return false;
                }
            }
        }

        return depth == 0;
    }
}
