namespace EZ_Rent.Models.Enums;

/// <summary>
/// Trạng thái kiểm duyệt tin đăng (Admin).
/// </summary>
public enum ApprovalStatus
{
    Draft = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    NeedRevision = 4
}