using System.ComponentModel.DataAnnotations;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Areas.Tenant.ViewModels;

public sealed class MyRoomModel
{
    public IReadOnlyList<RentalContract> Contracts { get; init; } = [];
    public RentalContract? Selected { get; init; }
    public IReadOnlyList<Invoice> Invoices { get; init; } = [];
    public IReadOnlyList<RepairRequest> Repairs { get; init; } = [];
    public decimal UnpaidTotal { get; init; }
    public int UnpaidCount { get; init; }
    public int OpenRepairCount { get; init; }
}

public class TenantPageInput
{
    [Range(1, int.MaxValue, ErrorMessage = "Số trang phải là số nguyên dương.")]
    public int Page { get; set; } = 1;
}

public sealed class ContractListInput : TenantPageInput
{
    [EnumDataType(typeof(ContractStatus), ErrorMessage = "Trạng thái hợp đồng không hợp lệ.")]
    public ContractStatus? Status { get; set; }
}

public sealed class InvoiceListInput : TenantPageInput
{
    [EnumDataType(typeof(PaymentStatus), ErrorMessage = "Trạng thái hóa đơn không hợp lệ.")]
    public PaymentStatus? Status { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Hợp đồng không hợp lệ.")]
    public int? ContractId { get; set; }
}

public sealed class RepairListInput : TenantPageInput
{
    [EnumDataType(typeof(RepairRequestStatus), ErrorMessage = "Trạng thái yêu cầu không hợp lệ.")]
    public RepairRequestStatus? Status { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Phòng không hợp lệ.")]
    public int? RoomId { get; set; }
}

// Lists are read-only. Write forms continue to bind the dedicated RepairRequestInputModel.
public sealed record ContractListModel(ContractListInput Input, IReadOnlyList<RentalContract> Items, int Total, int TotalPages);
public sealed record InvoiceListModel(InvoiceListInput Input, IReadOnlyList<Invoice> Items, int Total, int TotalPages);
public sealed record RepairListModel(RepairListInput Input, IReadOnlyList<RepairRequest> Items, int Total, int TotalPages);
