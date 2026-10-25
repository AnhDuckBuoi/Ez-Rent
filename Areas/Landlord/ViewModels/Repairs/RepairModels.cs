using EZ_Rent.Areas.Landlord.ViewModels.Shared;
using EZ_Rent.Helpers;
using System.ComponentModel.DataAnnotations;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Areas.Landlord.ViewModels.Repairs;

public sealed class RepairListInput
{
    [EnumDataType(typeof(RepairRequestStatus))] public RepairRequestStatus? Status { get; set; }
    [EnumDataType(typeof(RepairPriority))] public RepairPriority? Priority { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
}

public sealed class RepairRow
{
    public int Id { get; init; }
    public int RoomId { get; init; }
    public string RoomTitle { get; init; } = "";
    public string TenantName { get; init; } = "";
    public string ProblemType { get; init; } = "";
    public string Description { get; init; } = "";
    public RepairPriority Priority { get; init; }
    public RepairRequestStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public bool HasImage { get; init; }
}

public sealed class RepairListModel
{
    public RepairListInput Input { get; init; } = new();
    public PaginatedList<RepairRow> Items { get; init; } = EmptyPage.Of<RepairRow>();
    public PaginationInfo Pager { get; init; } = new();
    public Dictionary<RepairRequestStatus, int> Counts { get; init; } = new();
    public bool InvalidQuery { get; init; }
}

public sealed class RepairDetailModel
{
    public RepairRow Item { get; init; } = new();
    public string TenantId { get; init; } = "";
    public string? TenantPhone { get; init; }
    public string? ImageUrl { get; init; }
    public string? ResolutionNote { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class RepairActionInput
{
    [StringLength(2000, ErrorMessage = "Nội dung xử lý tối đa 2000 ký tự.")]
    public string? Note { get; set; }
}
