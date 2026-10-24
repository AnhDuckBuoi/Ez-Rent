namespace EZ_Rent.Areas.Landlord.ViewModels.Shared;

/// <summary>Số đếm trên thanh chức năng chủ trọ (lịch chờ, tin chưa đọc, sự cố mới, phản hồi hỗ trợ).</summary>
public sealed class LandlordMenuCounts
{
    public const string Key = "LandlordMenuCounts";
    public string Name { get; init; } = "Chủ trọ";
    public int PendingViewings { get; init; }
    public int UnreadMessages { get; init; }
    public int OpenRepairs { get; init; }
    public int SupportReplies { get; init; }
}

public sealed record LandlordProblem(string Title, string Message, string BackUrl, string BackText = "Quay lại");

public sealed record OptionItem(string Value, string Text, string? Hint = null);

/// <summary>Một tab lọc trạng thái (nav-tabs Bootstrap) kèm số lượng.</summary>
public sealed record StatusTab(string Text, string Url, int? Count, bool Active);

public static class EmptyPage
{
    public static EZ_Rent.Helpers.PaginatedList<T> Of<T>() => new(new List<T>(), 0, 1, 10);
}

public sealed record StatusTabsModel(IReadOnlyList<StatusTab> Tabs, string Region, string Label);
