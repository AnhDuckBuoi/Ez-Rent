using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace EZ_Rent.Areas.Landlord.ViewModels;

public sealed class PagedList<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int Total { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    public int FirstIndex => Total == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int LastIndex => Math.Min(Total, Page * PageSize);

    /// <summary>Kẹp số trang yêu cầu vào khoảng hợp lệ trước khi Skip/Take.</summary>
    public static int ClampPage(int requested, int total, int pageSize) =>
        Math.Clamp(requested, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
}

/// <summary>Phân trang đánh số 1, 2, 3 … (không dùng cuộn vô hạn). Giữ nguyên bộ lọc trên query string.</summary>
public sealed class PagerModel
{
    public int Page { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public int Total { get; init; }
    public int FirstIndex { get; init; }
    public int LastIndex { get; init; }
    public string Label { get; init; } = "Phân trang";
    public string? Region { get; init; }
    public Func<int, string> PageUrl { get; init; } = _ => "?";

    public IEnumerable<int> VisiblePages
    {
        get
        {
            var pages = new SortedSet<int> { 1, TotalPages };
            for (var i = Page - 2; i <= Page + 2; i++)
                if (i >= 1 && i <= TotalPages) pages.Add(i);
            return pages;
        }
    }

    public static PagerModel For<T>(HttpRequest request, PagedList<T> list, string? region, string label = "Phân trang", string key = "page")
    {
        var path = (request.PathBase + request.Path).ToUriComponent();
        var query = request.Query.Where(q => !string.Equals(q.Key, key, StringComparison.OrdinalIgnoreCase)).ToList();
        return new PagerModel
        {
            Page = list.Page, TotalPages = list.TotalPages, Total = list.Total, FirstIndex = list.FirstIndex,
            LastIndex = list.LastIndex, Label = label, Region = region,
            PageUrl = n =>
            {
                var items = new List<KeyValuePair<string, StringValues>>(query);
                if (n > 1) items.Add(new(key, n.ToString(CultureInfo.InvariantCulture)));
                return path + QueryString.Create(items).ToUriComponent();
            }
        };
    }
}

public sealed record TabLink(string Text, string Url, int? Count, bool Current);

public sealed class LandlordNavCounts
{
    public string Name { get; init; } = "Chủ trọ";
    public string? AvatarUrl { get; init; }
    public int PendingViewings { get; init; }
    public int UnreadMessages { get; init; }
    public int OpenRepairs { get; init; }
    public int SupportReplies { get; init; }
}

public sealed record ProblemModel(string Title, string Message, string BackUrl, string BackText = "Quay lại");

public static class LandlordFlash
{
    public const string Key = "LandlordFlash";
    public const string KindKey = "LandlordFlashKind";
}

public sealed record TabsModel(IReadOnlyList<TabLink> Tabs, string? Region, string Label);
