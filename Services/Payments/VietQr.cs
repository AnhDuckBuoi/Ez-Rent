using System.Globalization;
using System.Text;
using QRCoder;

using EZ_Rent.Services.Common;

namespace EZ_Rent.Services.Payments;

/// <summary>
/// Tạo nội dung VietQR (chuẩn EMVCo/NAPAS, dịch vụ chuyển khoản tới tài khoản "QRIBFTTA").
/// Chuỗi này được lưu vào Invoice.VietQRContent; mọi ứng dụng ngân hàng quét được ảnh QR sinh từ chuỗi.
/// Hiển thị QR không có nghĩa là đã thanh toán: chủ trọ vẫn phải đối soát và bấm "Xác nhận đã nhận tiền".
/// </summary>
public static class VietQr
{
    private const string NapasGuid = "A000000727";
    private const string TransferToAccount = "QRIBFTTA";

    public static string Build(string bankBin, string accountNumber, decimal amount, string description)
    {
        var amountText = decimal.Round(amount, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
        var beneficiary = Field("00", bankBin) + Field("01", accountNumber);
        var merchant = Field("00", NapasGuid) + Field("01", beneficiary) + Field("02", TransferToAccount);
        var payload = new StringBuilder()
            .Append(Field("00", "01"))            // Payload format indicator
            .Append(Field("01", "12"))            // QR động: mỗi hóa đơn một số tiền
            .Append(Field("38", merchant))
            .Append(Field("53", "704"))           // VND
            .Append(Field("54", amountText))
            .Append(Field("58", "VN"))
            .Append(Field("62", Field("08", description)))
            .Append("6304")
            .ToString();
        return payload + Crc16(payload).ToString("X4", CultureInfo.InvariantCulture);
    }

    /// <summary>Nội dung chuyển khoản ngắn, không dấu để mọi ngân hàng giữ nguyên: "EZRENT HD12 T102026".</summary>
    public static string Description(int invoiceId, string period)
    {
        var suffix = VietnamTime.TryParsePeriod(period, out var day) ? $" T{day:MMyyyy}" : "";
        return $"EZRENT HD{invoiceId}{suffix}";
    }

    public static byte[]? Png(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload.Length > 1000) return null;
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(8);
    }

    private static string Field(string id, string value)
    {
        if (value.Length > 99) throw new ArgumentException("Giá trị VietQR quá dài.", nameof(value));
        return id + value.Length.ToString("00", CultureInfo.InvariantCulture) + value;
    }

    // CRC-16/CCITT-FALSE (đa thức 0x1021, giá trị đầu 0xFFFF) theo chuẩn EMVCo.
    private static ushort Crc16(string text)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.ASCII.GetBytes(text))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }
}
