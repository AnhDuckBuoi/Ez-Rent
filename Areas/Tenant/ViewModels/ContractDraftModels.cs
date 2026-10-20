using System.ComponentModel.DataAnnotations;
using EZ_Rent.Models;

namespace EZ_Rent.Areas.Tenant.ViewModels;

// Only personal data and concurrency tokens are bindable. There are no financial/owner fields.
public sealed class ContractTenantDraftInput
{
    [Range(0, int.MaxValue, ErrorMessage = "Phiên bản không hợp lệ. Hãy mở lại bản nháp.")] public int ExpectedRevision { get; set; }
    [Required(ErrorMessage = "Hãy mở lại bản nháp trước khi lưu."), StringLength(64, MinimumLength = 64, ErrorMessage = "Thông tin đối chiếu không hợp lệ. Hãy mở lại bản nháp.")] public string ContractStamp { get; set; } = "";
    [Required(ErrorMessage = "Hãy nhập họ và tên."), StringLength(80, ErrorMessage = "Họ tên tối đa 80 ký tự.")]
    [Display(Name = "Họ và tên")] public string FullName { get; set; } = "";
    [Phone(ErrorMessage = "Số điện thoại chưa đúng định dạng."), StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
    [Display(Name = "Số điện thoại")] public string? PhoneNumber { get; set; }
    [DataType(DataType.Date), Display(Name = "Ngày sinh")] public DateOnly? DateOfBirth { get; set; }
    [StringLength(500, ErrorMessage = "Địa chỉ tối đa 500 ký tự."), Display(Name = "Địa chỉ thường trú")]
    public string? PermanentAddress { get; set; }
    [EnumDataType(typeof(IdentityDocumentKind), ErrorMessage = "Hãy chọn loại giấy tờ trong danh sách.")]
    [Display(Name = "Loại giấy tờ")] public IdentityDocumentKind? DocumentKind { get; set; }
    [StringLength(30, ErrorMessage = "Số giấy tờ tối đa 30 ký tự."), Display(Name = "Số giấy tờ")]
    public string? DocumentNumber { get; set; }
    [DataType(DataType.Date), Display(Name = "Ngày cấp")] public DateOnly? DocumentIssuedOn { get; set; }
    [StringLength(200, ErrorMessage = "Nơi cấp tối đa 200 ký tự."), Display(Name = "Nơi cấp")]
    public string? DocumentIssuedAt { get; set; }
}

public sealed record ContractDraftPage(RentalContract Contract, ContractTenantDraft Snapshot,
    ContractTenantDraftInput Input, IReadOnlyList<ContractTenantDraft> History, int LatestRevision,
    int HistoryPage, int HistoryPages, IReadOnlyList<string> MissingFields, bool ContractChanged)
{
    public bool IsHistorical => Snapshot.Revision > 0 && Snapshot.Revision != LatestRevision;
    public bool HasTerms => !string.IsNullOrWhiteSpace(Snapshot.ContractTerms);
    public bool Conflict { get; init; }
}

public enum DraftSaveStatus { Saved, Missing, Conflict, Invalid }
public sealed record DraftSaveResult(DraftSaveStatus Status, int Revision = 0,
    IReadOnlyDictionary<string, string>? Errors = null);
