using System.Globalization;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeRateLimitParser
{
    private const string PolicyHeaderName = "X-Rate-Limit-Policy";
    private const string RulesHeaderName = "X-Rate-Limit-Rules";
    private const string RetryAfterHeaderName = "Retry-After";
    private const string RateLimitPrefix = "X-Rate-Limit-";
    private const string StateSuffix = "-State";

    public PathOfExileTradeRateLimitParseResult Parse(
        IEnumerable<KeyValuePair<string, IEnumerable<string>>>? headers)
    {
        var diagnostics = new List<PathOfExileTradeQueryDiagnostic>();
        var headerMap = BuildHeaderMap(headers);
        var policy = FirstValue(headerMap, PolicyHeaderName);
        var ruleNames = DiscoverRuleNames(headerMap);
        var rules = new List<PathOfExileTradeRateLimitRule>();

        foreach (var ruleName in ruleNames)
        {
            ParseRule(ruleName, headerMap, rules, diagnostics);
        }

        var retryAfterSeconds = ParseRetryAfter(headerMap, diagnostics);
        return PathOfExileTradeRateLimitParseResult.Success(
            new PathOfExileTradeRateLimitSnapshot
            {
                Policy = policy,
                Rules = rules,
                RetryAfterSeconds = retryAfterSeconds,
            },
            diagnostics);
    }

    private static Dictionary<string, List<string>> BuildHeaderMap(
        IEnumerable<KeyValuePair<string, IEnumerable<string>>>? headers)
    {
        var headerMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (headers is null)
        {
            return headerMap;
        }

        foreach (var header in headers)
        {
            if (string.IsNullOrWhiteSpace(header.Key))
            {
                continue;
            }

            if (!headerMap.TryGetValue(header.Key, out var values))
            {
                values = [];
                headerMap[header.Key] = values;
            }

            if (header.Value is not null)
            {
                values.AddRange(header.Value.Where(value => value is not null)!);
            }
        }

        return headerMap;
    }

    private static IReadOnlyList<string> DiscoverRuleNames(
        IReadOnlyDictionary<string, List<string>> headerMap)
    {
        var ruleNames = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ruleName in SplitCommaSeparatedValues(Values(headerMap, RulesHeaderName)))
        {
            AddRuleName(ruleNames, seen, ruleName);
        }

        foreach (var headerName in headerMap.Keys)
        {
            if (!TryGetRuleNameFromRuleHeader(headerName, out var ruleName))
            {
                continue;
            }

            AddRuleName(ruleNames, seen, ruleName);
        }

        return ruleNames;
    }

    private static void ParseRule(
        string ruleName,
        IReadOnlyDictionary<string, List<string>> headerMap,
        List<PathOfExileTradeRateLimitRule> rules,
        List<PathOfExileTradeQueryDiagnostic> diagnostics)
    {
        var ruleEntries = SplitCommaSeparatedValues(Values(headerMap, RuleHeaderName(ruleName))).ToArray();
        var stateEntries = SplitCommaSeparatedValues(Values(headerMap, StateHeaderName(ruleName))).ToArray();

        for (var index = 0; index < ruleEntries.Length; index++)
        {
            if (!TryParseTriple(ruleEntries[index], out var maximum, out var interval, out var timeout))
            {
                diagnostics.Add(Diagnostic(
                    PathOfExileTradeRateLimitDiagnosticCodes.MalformedRuleEntry,
                    $"Rate-limit rule '{ruleName}' entry at index {index} is malformed."));
                continue;
            }

            int? currentRequestCount = null;
            int? currentInterval = null;
            int? currentTimeout = null;
            if (index < stateEntries.Length)
            {
                if (TryParseTriple(stateEntries[index], out var current, out var stateInterval, out var stateTimeout))
                {
                    currentRequestCount = current;
                    currentInterval = stateInterval;
                    currentTimeout = stateTimeout;
                }
                else
                {
                    diagnostics.Add(Diagnostic(
                        PathOfExileTradeRateLimitDiagnosticCodes.MalformedStateEntry,
                        $"Rate-limit state '{ruleName}' entry at index {index} is malformed."));
                }
            }

            rules.Add(new PathOfExileTradeRateLimitRule
            {
                RuleName = ruleName,
                MaximumRequestCount = maximum,
                IntervalSeconds = interval,
                TimeoutSeconds = timeout,
                CurrentRequestCount = currentRequestCount,
                CurrentIntervalSeconds = currentInterval,
                CurrentTimeoutSeconds = currentTimeout,
            });
        }

        for (var index = ruleEntries.Length; index < stateEntries.Length; index++)
        {
            diagnostics.Add(Diagnostic(
                PathOfExileTradeRateLimitDiagnosticCodes.ExtraStateEntry,
                $"Rate-limit state '{ruleName}' entry at index {index} has no matching rule entry."));
        }
    }

    private static int? ParseRetryAfter(
        IReadOnlyDictionary<string, List<string>> headerMap,
        List<PathOfExileTradeQueryDiagnostic> diagnostics)
    {
        var value = FirstValue(headerMap, RetryAfterHeaderName);
        if (value is null)
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) &&
            seconds >= 0)
        {
            return seconds;
        }

        diagnostics.Add(Diagnostic(
            PathOfExileTradeRateLimitDiagnosticCodes.InvalidRetryAfter,
            "Retry-After must be a non-negative integer number of seconds."));
        return null;
    }

    private static bool TryParseTriple(
        string value,
        out int first,
        out int second,
        out int third)
    {
        first = 0;
        second = 0;
        third = 0;

        var parts = value.Split(':', StringSplitOptions.TrimEntries);
        return parts.Length == 3 &&
            TryParseNonNegativeInteger(parts[0], out first) &&
            TryParseNonNegativeInteger(parts[1], out second) &&
            TryParseNonNegativeInteger(parts[2], out third);
    }

    private static bool TryParseNonNegativeInteger(string value, out int result)
    {
        return int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out result) &&
            result >= 0;
    }

    private static string? FirstValue(
        IReadOnlyDictionary<string, List<string>> headerMap,
        string headerName)
    {
        return Values(headerMap, headerName)
            .Select(value => value.Trim())
            .FirstOrDefault(value => value.Length > 0);
    }

    private static IReadOnlyList<string> Values(
        IReadOnlyDictionary<string, List<string>> headerMap,
        string headerName)
    {
        return headerMap.TryGetValue(headerName, out var values)
            ? values
            : [];
    }

    private static IEnumerable<string> SplitCommaSeparatedValues(
        IEnumerable<string> values)
    {
        return values
            .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Where(value => value.Length > 0);
    }

    private static bool TryGetRuleNameFromRuleHeader(string headerName, out string ruleName)
    {
        ruleName = string.Empty;
        if (!headerName.StartsWith(RateLimitPrefix, StringComparison.OrdinalIgnoreCase) ||
            headerName.EndsWith(StateSuffix, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(headerName, PolicyHeaderName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(headerName, RulesHeaderName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        ruleName = headerName[RateLimitPrefix.Length..];
        return !string.IsNullOrWhiteSpace(ruleName);
    }

    private static void AddRuleName(
        List<string> ruleNames,
        HashSet<string> seen,
        string ruleName)
    {
        if (seen.Add(ruleName))
        {
            ruleNames.Add(ruleName);
        }
    }

    private static string RuleHeaderName(string ruleName)
    {
        return $"{RateLimitPrefix}{ruleName}";
    }

    private static string StateHeaderName(string ruleName)
    {
        return $"{RuleHeaderName(ruleName)}{StateSuffix}";
    }

    private static PathOfExileTradeQueryDiagnostic Diagnostic(
        string code,
        string message)
    {
        return new PathOfExileTradeQueryDiagnostic(code, message);
    }
}
