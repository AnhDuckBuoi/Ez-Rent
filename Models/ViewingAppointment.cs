using System.ComponentModel.DataAnnotations;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Models;

public class ViewingAppointment
{
    public int ViewingAppointmentId { get; set; }

    public int RentalPostId { get; set; }
    public RentalPost RentalPost { get; set; } = null!;

    public string TenantId { get; set; } = "";
    public ApplicationUser Tenant { get; set; } = null!;

    public DateTime ViewingDate { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    [MaxLength(500)]
    public string? LandlordNote { get; set; }

    public ViewingAppointmentStatus Status { get; set; } = ViewingAppointmentStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}