namespace PolarionMcpTools;

/// <summary>
/// Shared input validators for MCP tools and REST endpoints. Keep every guard for
/// user-supplied values that reach the Polarion SDK here so call sites cannot drift.
/// </summary>
public sealed partial class McpTools
{
    /// <summary>
    /// True when <paramref name="value"/> is safe to pass as a Polarion space name or document
    /// ID. The Polarion SDK string-interpolates these directly into SQL, so SQL injection
    /// characters are blocked. Spaces and dashes are allowed (real space names use them, e.g.
    /// "My Space - Section").
    /// </summary>
    internal static bool IsSafeForPolarionPathParam(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return !value.Contains('\'') &&
               !value.Contains(';') &&
               !value.Contains("--") &&
               !value.Contains("/*") &&
               !value.Contains("*/");
    }

    /// <summary>
    /// Maximum accepted length of a document title filter.
    /// </summary>
    internal const int MaxTitleFilterLength = 256;

    /// <summary>
    /// True when <paramref name="value"/> is safe to pass as a document title filter. The
    /// Polarion SDK places the filter inside a SQL pattern, so in addition to the path-param
    /// rules this also blocks the pattern wildcard <c>%</c> and the escape character <c>\</c>.
    /// Plain-text titles (spaces, hyphens, parentheses, dots, underscores) are allowed.
    /// </summary>
    internal static bool IsSafeForPolarionTitleFilter(string? value)
    {
        if (!IsSafeForPolarionPathParam(value))
        {
            return false;
        }

        return value!.Length <= MaxTitleFilterLength &&
               !value.Contains('%') &&
               !value.Contains('\\');
    }

    /// <summary>
    /// Maximum accepted length of a work item ID.
    /// </summary>
    internal const int MaxWorkItemIdLength = 64;

    private static readonly Regex WorkItemIdRegex =
        new(@"^[A-Za-z0-9][A-Za-z0-9_\-]*\z", RegexOptions.Compiled);

    /// <summary>
    /// True when <paramref name="value"/> is a well-formed Polarion work item ID (e.g. "ABC-12345"):
    /// starts with an ASCII letter or digit, then letters, digits, underscores, or hyphens, up to
    /// <see cref="MaxWorkItemIdLength"/> characters.
    /// </summary>
    internal static bool IsValidWorkItemId(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxWorkItemIdLength)
        {
            return false;
        }

        return WorkItemIdRegex.IsMatch(value);
    }

    /// <summary>
    /// Maximum number of digits accepted in a revision ID.
    /// </summary>
    internal const int MaxRevisionDigits = 18;

    /// <summary>
    /// True when <paramref name="value"/> is "-1" (latest revision) or a positive integer revision
    /// ID made of ASCII digits. "0", signs, whitespace, and non-ASCII digits are rejected.
    /// </summary>
    internal static bool IsValidRevision(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value == "-1")
        {
            return true;
        }

        if (value.Length > MaxRevisionDigits || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        return long.Parse(value, System.Globalization.CultureInfo.InvariantCulture) > 0;
    }
}
