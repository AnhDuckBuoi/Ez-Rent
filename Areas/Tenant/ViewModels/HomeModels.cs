using System.Globalization;
using EZ_Rent.Models;

namespace EZ_Rent.Areas.Tenant.ViewModels;

public sealed class TenantHomeModel
{
    public string DisplayName { get; init; } = "bạn";
    public DateTime Today { get; init; }
    public int PendingAppointments { get; init; }
    public int SavedCount { get; init; }
    public int UnreadCount { get; init; }
    public AppointmentModel? NextAppointment { get; init; }
    public IReadOnlyList<RentalCardModel> SavedRooms { get; init; } = [];
    public IReadOnlyList<TenantNoticeModel> Notices { get; init; } = [];
}

public sealed class TenantNoticeModel
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string Content { get; init; } = "";
    public bool IsRead { get; init; }
    public DateTime CreatedAt { get; init; }
    public string CreatedLabel => CreatedAt.ToString("dd/MM/yyyy · HH:mm", CultureInfo.GetCultureInfo("vi-VN"));
    public bool CanOpenTarget { get; set; }
    public static TenantNoticeModel From(Notification n) => new()
    {
        Id = n.NotificationId, Title = n.Title, Content = n.Content, IsRead = n.IsRead,
        // CreatedAt is written in UTC; relational storage does not preserve DateTime.Kind.
        CreatedAt = DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc).AddHours(7)
    };
}

public sealed class TenantNoticeListModel
{
    public IReadOnlyList<TenantNoticeModel> Items { get; init; } = [];
    public int Page { get; init; } = 1;
    public int Total { get; init; }
    public int UnreadCount { get; init; }
    public int TotalPages => Math.Max(1, (Total - 1) / 10 + 1);
    public bool InvalidPage { get; init; }
}
