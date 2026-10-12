namespace EZ_Rent.Models.Enums;

/// <summary>
/// Trạng thái vận hành của phòng (tách khỏi trạng thái kiểm duyệt).
/// </summary>
public enum RoomStatus
{
    Available = 1,
    Rented = 2,
    Hidden = 3
}