using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Identity;

namespace EZ_Rent.Models;

public class ApplicationUser : IdentityUser
{
    [Required(ErrorMessage = "Họ và tên không được để trống")]
    [MaxLength(50, ErrorMessage = "{0} không được vượt quá 50 ký tự")]
    [DisplayName("Họ và tên")]
    public string FullName { get; set; } = "";

    [DisplayName("Ảnh đại diện")]
    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    [DisplayName("Trạng thái tài khoản")]
    public UserStatus Status { get; set; } = UserStatus.Active;

    [DisplayName("Thời điểm tạo")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // ===== Navigation =====
    public ICollection<RentalPost> RentalPosts { get; set; } = new List<RentalPost>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<RentalRequest> RentalRequests { get; set; } = new List<RentalRequest>();
    public ICollection<ViewingAppointment> ViewingAppointments { get; set; } = new List<ViewingAppointment>();
    public ICollection<RentalContract> ContractsAsTenant { get; set; } = new List<RentalContract>();
    public ICollection<RentalContract> ContractsAsLandlord { get; set; } = new List<RentalContract>();
    public ICollection<RepairRequest> RepairRequests { get; set; } = new List<RepairRequest>();
    public ICollection<Conversation> ConversationsInitiated { get; set; } = new List<Conversation>();
    public ICollection<Conversation> ConversationsReceived { get; set; } = new List<Conversation>();
    public ICollection<Message> MessagesSent { get; set; } = new List<Message>();
    public ICollection<MeterReading> MeterReadings { get; set; } = new List<MeterReading>();
}