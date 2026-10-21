using System.ComponentModel.DataAnnotations;

namespace EZ_Rent.Models;

/// <summary>A retained document version. Uploading a file does not establish legal validity.</summary>
public class ContractDocument
{
    public int ContractDocumentId { get; set; }
    public int RentalContractId { get; set; }
    public RentalContract RentalContract { get; set; } = null!;
    public int Version { get; set; }
    // Retain the recipient so reassigning a legacy contract cannot disclose an old private file.
    [MaxLength(450)] public string TenantId { get; set; } = "";
    [MaxLength(80)] public string StorageKey { get; set; } = "";
    [MaxLength(180)] public string FileName { get; set; } = "";
    [MaxLength(80)] public string ContentType { get; set; } = "";
    public long ByteLength { get; set; }
    [MaxLength(64)] public string Sha256 { get; set; } = "";
    [MaxLength(450)] public string UploadedById { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // Acknowledgement belongs to this exact version, not to the contract's signing status.
    public DateTime? TenantAcknowledgedAt { get; set; }
    [MaxLength(450)] public string? TenantAcknowledgedById { get; set; }
}
