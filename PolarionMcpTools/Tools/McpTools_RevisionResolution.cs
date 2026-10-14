namespace PolarionMcpTools;

public sealed partial class McpTools
{
    private const int MaxBaselineReferenceLength = 256;
    private const int MaxBaselineCandidatesInError = 5;

    /// <summary>
    /// True when the revision is a baseline name or ID rather than a number. Number-like values
    /// (digits, sign, dot) belong to the numeric revision validators.
    /// </summary>
    private static bool IsBaselineReference(string? revision)
    {
        var value = revision?.Trim() ?? string.Empty;
        return !value.All(c => char.IsDigit(c) || c is '+' or '-' or '.');
    }

    /// <summary>
    /// True for a positive ASCII integer without a leading zero, the only number-like form that can be a baseline ID.
    /// </summary>
    private static bool IsPositiveAsciiInteger(string value)
    {
        return value.Length > 0 && value[0] != '0' && value.All(char.IsAsciiDigit);
    }

    /// <summary>
    /// Maps a baseline name or ID to the revision it points at, for one document. Only baselines taken on
    /// this document are considered, because the SDK query is project-wide and the same name can exist on
    /// several documents. Numeric revisions pass through unchanged unless a baseline with that ID exists for
    /// this document.
    /// </summary>
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    private async Task<Result<string>> ResolveRevisionAsync(IPolarionClient client, string revision, string? projectId, string space, string documentId)
    {
        var value = revision.Trim();
        var isBaselineName = IsBaselineReference(value);

        if (!isBaselineName && !IsPositiveAsciiInteger(value))
        {
            // "-1", "0", "-2", "1.5" and non-ASCII digits go to the numeric validators unchanged.
            return Result.Ok(revision);
        }

        if (isBaselineName && (value.Length > MaxBaselineReferenceLength || value.Any(char.IsControl)))
        {
            return Result.Fail<string>($"ERROR: (112) Baseline reference '{value}' is not valid. Use a baseline name or ID of up to {MaxBaselineReferenceLength} characters without control characters.");
        }

        if (string.IsNullOrEmpty(projectId))
        {
            return Result.Fail<string>("ERROR: (114) Polarion project ID is not available for this configuration.");
        }

        // A baseline taken on this document has baseObjectURI = module URI + "%" + baseline revision.
        var documentUriPrefix = $"{ModuleUri.BuildHeadModuleUri(projectId, space, documentId)}%";

        var query = isBaselineName
            ? $"name:\"{EscapeLuceneQuoted(value)}\""
            : $"id:{value}";

        var lookup = await client.QueryBaselinesAsync(query);
        if (lookup.IsFailed)
        {
            return Result.Fail<string>($"ERROR: (111) Baseline lookup failed. Error: {lookup.Errors.First().Message}");
        }

        // The text after the prefix must be the baseline revision alone, so a document ID containing "%" cannot match.
        var candidates = lookup.Value
            .Where(b => b.baseObjectURI?.StartsWith(documentUriPrefix, StringComparison.Ordinal) == true
                && b.baseObjectURI.Length > documentUriPrefix.Length
                && b.baseObjectURI[documentUriPrefix.Length..].All(char.IsAsciiDigit))
            .ToArray();

        var matches = candidates
            .Where(b => isBaselineName
                ? string.Equals(b.name, value, StringComparison.Ordinal)
                : string.Equals(b.id, value, StringComparison.Ordinal))
            .ToArray();

        if (matches.Length == 0)
        {
            // A numeric value with no baseline ID for this document is a plain revision number.
            if (!isBaselineName)
            {
                return Result.Ok(revision);
            }

            if (candidates.Length == 0)
            {
                return Result.Fail<string>($"ERROR: (112) No baseline named '{value}' exists for this document.");
            }

            var names = string.Join("; ", candidates.Take(MaxBaselineCandidatesInError).Select(b => $"{b.id} {b.name}"));
            return Result.Fail<string>($"ERROR: (112) No baseline named '{value}' exists for this document. Baselines for this document with a similar name: {names}. Use the exact name or the baseline ID.");
        }

        var distinctRevisions = matches.Select(b => b.baseRevision).Distinct(StringComparer.Ordinal).ToArray();
        if (distinctRevisions.Length > 1)
        {
            var listing = string.Join("; ", matches.Select(b => $"{b.id} ({b.name}, rev {b.baseRevision})"));
            return Result.Fail<string>($"ERROR: (113) Baseline '{value}' is ambiguous. Matches: {listing}. Use the baseline ID or a revision number.");
        }

        var baseRevision = distinctRevisions[0];
        if (!IsValidRevision(baseRevision))
        {
            return Result.Fail<string>($"ERROR: (112) Baseline '{value}' does not reference a valid revision.");
        }

        return Result.Ok(baseRevision);
    }

    private static string EscapeLuceneQuoted(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
