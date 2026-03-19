using System.Collections.Generic;

namespace RealtimeAuction.Api.Helpers;

public static class SearchHelper
{
    private static readonly Dictionary<string, List<string>> SynonymGroups = new()
    {
        { "điện thoại", new List<string> { "điện thoại", "iphone", "samsung", "ip", "phone", "smartphone", "oppo", "xiaomi", "nokia", "điện thoại thông minh" } },
        { "ip", new List<string> { "iphone", "ip", "điện thoại" } },
        { "máy tính", new List<string> { "máy tính", "laptop", "pc", "computer", "macbook", "dell", "hp", "asus" } },
        { "laptop", new List<string> { "laptop", "máy tính", "macbook", "dell", "hp", "asus", "acer" } },
        { "xe", new List<string> { "xe", "ô tô", "xe máy", "oto", "car", "motorcycle", "bike" } },
        { "oto", new List<string> { "ô tô", "oto", "car", "xe" } },
        { "phụ kiện", new List<string> { "phụ kiện", "ốp", "cáp", "sạc", "tai nghe", "headphone", "cable", "charger" } }
    };

    public static List<string> GetExpandedKeywords(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return new List<string>();
        }

        var normalizedKeyword = keyword.Trim().ToLower();
        var results = new List<string> { normalizedKeyword };

        foreach (var group in SynonymGroups)
        {
            if (normalizedKeyword.Contains(group.Key) || group.Value.Contains(normalizedKeyword))
            {
                foreach (var synonym in group.Value)
                {
                    if (!results.Contains(synonym))
                    {
                        results.Add(synonym);
                    }
                }
            }
        }

        return results;
    }
}
