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
        "DECLARE", "WAITFOR",
    };

    private static readonly Regex ForbiddenKeywordRegex = new(
        @"(?<![A-Za-z0-9_])(" + string.Join("|", ForbiddenKeywords.Select(Regex.Escape)) + @")(?![A-Za-z0-9_])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Stored-procedure call prefix patterns (xp_cmdshell, sp_executesql, …).
    // EXEC/EXECUTE/CALL already block invocation; this adds defense against bare
    // proc-name references. Requires at least one identifier char after the prefix so
    // the trailing negative lookahead on the main keyword regex (which treated 'XP_' as a
    // complete token and therefore never matched 'xp_cmdshell') does not apply here.
    private static readonly Regex StoredProcPrefixRegex = new(
        @"(?<![A-Za-z0-9_])(XP|SP)_[A-Za-z0-9_]",
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

        // No stored-procedure prefix calls (defense-in-depth; EXEC is already blocked above).
        if (StoredProcPrefixRegex.IsMatch(code))
        {
            return ValidationResult.Fail(
                "Stored-procedure call (XP_/SP_ prefix) found; only read-only SELECT queries are permitted.");
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

        // C_PK must appear in the SELECT list (before the first top-level FROM), not merely
        // anywhere in the statement (e.g. a WHERE clause). Polarion resolves the SQL: filter
        // on C_PK values returned by the SELECT, so projecting a different column produces
        // nonsense results or a Polarion error rather than the expected work-item set.
        if (!CpkAppearsInSelectList(code))
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

    /// <summary>
    /// Returns true when the literal token <c>C_PK</c> appears in the outermost SELECT list
    /// (i.e. between the leading SELECT keyword and the first top-level FROM keyword).
    /// Parenthesis depth is tracked so inner subquery SELECTs and their FROMs are skipped.
    /// Falls back to a presence-anywhere check if no top-level FROM is found (e.g. unusual
    /// bare SELECT without FROM), which is consistent with the previous behaviour.
    /// </summary>
    private static bool CpkAppearsInSelectList(string code)
    {
        // Locate the leading SELECT (already enforced by LeadingSelectRegex).
        var selectMatch = LeadingSelectRegex.Match(code);
        if (!selectMatch.Success)
        {
            return false;
        }

        var pos = selectMatch.Index + selectMatch.Length;
        var depth = 0;

        while (pos < code.Length)
        {
            var c = code[pos];

            if (c == '(')
            {
                depth++;
                pos++;
                continue;
            }

            if (c == ')')
            {
                depth--;
                pos++;
                continue;
            }

            if (depth == 0)
            {
                // Check for C_PK at a word boundary — it must precede the first top-level FROM.
                if (MatchesWordAt(code, pos, "C_PK"))
                {
                    return true;
                }

                // First top-level FROM reached: C_PK was not in the SELECT list.
                if (MatchesWordAt(code, pos, "FROM"))
                {
                    return false;
                }
            }

            pos++;
        }

        // No top-level FROM found (e.g. correlated subquery shaped query): allow if C_PK
        // appears anywhere in the remaining code — this is the pre-existing check behaviour.
        return PrimaryKeyProjectionRegex.IsMatch(code.Substring(selectMatch.Index + selectMatch.Length));
    }

    /// <summary>
    /// True when <paramref name="code"/> contains <paramref name="word"/> at position
    /// <paramref name="pos"/> with identifier-character word boundaries on both sides
    /// (case-insensitive). Does NOT advance <paramref name="pos"/>.
    /// </summary>
    private static bool MatchesWordAt(string code, int pos, string word)
    {
        if (pos + word.Length > code.Length)
        {
            return false;
        }

        if (!code.Substring(pos, word.Length).Equals(word, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var beforeOk = pos == 0 || !IsIdentChar(code[pos - 1]);
        var afterOk = pos + word.Length >= code.Length || !IsIdentChar(code[pos + word.Length]);
        return beforeOk && afterOk;
    }

    private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
