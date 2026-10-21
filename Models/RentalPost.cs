using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Models;

public class RentalPost
{
    public int RentalPostId { get; set; }

    // ===== Thông tin cơ bản =====
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = "";

    [Required]
    public string Description { get; set; } = "";

    [Required]
    [Range(0, double.MaxValue)]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public double Area { get; set; }

    [Required]
    [MaxLength(255)]
    public string Address { get; set; } = "";

    [Required]
    [MaxLength(100)]
    public string City { get; set; } = "";

    [MaxLength(100)]
    public string District { get; set; } = "";

    [MaxLength(100)]
    public string Ward { get; set; } = "";

    [MaxLength(500)]
    public string? MapLocation { get; set; }

    public string? AmenitiesJson { get; set; }

    // ===== Loại & trạng thái =====
    [Required]
    public RoomType RoomType { get; set; }

    public RoomStatus RoomStatus { get; set; } = RoomStatus.Available;

    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;

    [MaxLength(1000)]
    public string? ApprovalNote { get; set; }

    public string? ApprovedById { get; set; }
    public ApplicationUser? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // ===== Đơn giá mặc định của phòng =====
    [Column(TypeName = "decimal(18,2)")]
    public decimal ElectricityUnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal WaterUnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ServiceFee { get; set; }

    // ===== Thời gian =====
    public DateTime AvailableFrom { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PostedAt { get; set; }

    // ===== Owner =====
    [Required]
    public string OwnerId { get; set; } = "";
    public ApplicationUser Owner { get; set; } = null!;
    // // == Delete ===
    // public bool IsDeleted { get; set; } = false;
    // public DateTime? DeletedAt { get; set; }
    // public string? DeletedReason { get; set; }
    // public string? DeletedById { get; set; }

    // ===== Navigation =====
    public ICollection<RoomImage> RoomImages { get; set; } = new List<RoomImage>();
    public ICollection<Service> Services { get; set; } = new List<Service>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<RentalRequest> RentalRequests { get; set; } = new List<RentalRequest>();
    public ICollection<ViewingAppointment> ViewingAppointments { get; set; } = new List<ViewingAppointment>();
    public ICollection<RentalContract> RentalContracts { get; set; } = new List<RentalContract>();
    public ICollection<MeterReading> MeterReadings { get; set; } = new List<MeterReading>();
    public ICollection<RepairRequest> RepairRequests { get; set; } = new List<RepairRequest>();
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}