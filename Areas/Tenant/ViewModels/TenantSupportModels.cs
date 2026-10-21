using System.ComponentModel.DataAnnotations;

namespace EZ_Rent.Areas.Tenant.ViewModels;

public sealed class TenantSupportInput
{
    [Required(ErrorMessage = "Chọn vấn đề cần hỗ trợ.")]
    [RegularExpression("^(room-info|viewing|account|other)$", ErrorMessage = "Chọn vấn đề trong danh sách.")]
    public string Category { get; set; } = "";

    [Required(ErrorMessage = "Nhập tiêu đề yêu cầu.")]
    [StringLength(100, ErrorMessage = "Tiêu đề tối đa 100 ký tự.")]
    public string Subject { get; set; } = "";

    [Required(ErrorMessage = "Mô tả nội dung cần hỗ trợ.")]
    [StringLength(1000, ErrorMessage = "Mô tả tối đa 1.000 ký tự.")]
    public string Description { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Phòng liên quan không hợp lệ.")]
    public int? RentalPostId { get; set; }
}

public sealed class TenantSupportModel
{
    public TenantSupportInput Input { get; init; } = new();
    public bool CanSubmit { get; set; }
    public string? Error { get; set; }
    public IReadOnlyList<TenantSupportRoom> Rooms { get; set; } = [];
}

public sealed record TenantSupportRoom(int Id, string Title);
