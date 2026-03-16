using System.Text.RegularExpressions;

namespace RealtimeAuction.Api.Helpers;

public static class MongoRegexHelper
{
    public static string? EscapeLiteralPattern(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        return Regex.Escape(input.Trim());
    }
}
