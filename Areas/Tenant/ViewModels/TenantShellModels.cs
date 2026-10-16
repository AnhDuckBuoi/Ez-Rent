namespace EZ_Rent.Areas.Tenant.ViewModels;

public sealed record TenantHeaderModel(string Controller, string Action, int UnreadCount);

public sealed record TenantPaginationModel(int Page, int TotalPages, Func<int, string?> PageUrl, string Label, bool Ajax = false)
{
    // A bounded set keeps both the markup and the navigation usable for large result sets.
    public IEnumerable<int> VisiblePages => new[] { 1, TotalPages }
        .Concat(Enumerable.Range(Math.Max(1, Page - 2), (int)(Math.Min((long)TotalPages, (long)Page + 2) - Math.Max(1, Page - 2) + 1)))
        .Distinct().Order();
}
