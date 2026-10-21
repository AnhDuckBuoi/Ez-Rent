using System.ComponentModel.DataAnnotations;

namespace EZ_Rent.Areas.Tenant.ViewModels;

public sealed class TenantMessageSendInput : IValidatableObject
{
    [Range(1, int.MaxValue)] public int? ConversationId { get; set; }
    [Range(1, int.MaxValue)] public int? RentalPostId { get; set; }
    [StringLength(64, ErrorMessage = "Thông tin người nhận không hợp lệ. Hãy mở lại căn phòng.")] public string? ContactStamp { get; set; }
    [Required(ErrorMessage = "Nhập nội dung tin nhắn.")]
    [StringLength(2000, ErrorMessage = "Tin nhắn tối đa 2.000 ký tự.")]
    public string Content { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ConversationId.HasValue == RentalPostId.HasValue)
            yield return new("Chọn đúng một cuộc trò chuyện hoặc căn phòng.");
    }
}

public sealed class TenantMessageReadInput : IValidatableObject
{
    [Range(1, int.MaxValue)] public int ConversationId { get; set; }
    [Range(1, int.MaxValue)] public int MessagePage { get; set; } = 1;
    [MinLength(1), MaxLength(40)] public int[] MessageIds { get; set; } = [];
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MessageIds == null || MessageIds.Any(id => id < 1)) yield return new("Mã tin nhắn không hợp lệ.");
    }
}
