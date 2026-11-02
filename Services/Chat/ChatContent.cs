namespace EZ_Rent.Services.Chat;

/// <summary>
/// Quy ước nội dung tin nhắn đặc biệt (không đổi schema bảng Messages):
/// - "ezimg:{key}"  : tin ảnh, file lưu riêng tư bằng RentalPrivateFileStore, chỉ thành viên cuộc trò chuyện mở được (/Chat/Image/{id});
/// - "ezdel:"       : tin đã bị người gửi thu hồi (giữ dòng để lịch sử hai bên vẫn khớp thứ tự).
/// Mọi nơi hiển thị nội dung tin nhắn dùng Preview/IsImage/IsRecalled để không lộ chuỗi kỹ thuật ra giao diện.
/// </summary>
public static class ChatContent
{
    public const string ImagePrefix = "ezimg:";
    public const string Recalled = "ezdel:";

    public static bool IsImage(string? content) => content != null && content.StartsWith(ImagePrefix, StringComparison.Ordinal);
    public static bool IsRecalled(string? content) => content == Recalled;
    public static string? ImageKey(string? content) => IsImage(content) ? content![ImagePrefix.Length..] : null;

    /// <summary>Chữ dùng cho dòng xem trước và các trang chỉ hiển thị chữ.</summary>
    public static string Preview(string? content) =>
        IsRecalled(content) ? "Tin nhắn đã bị thu hồi" : IsImage(content) ? "[Hình ảnh]" : content ?? "";
}
