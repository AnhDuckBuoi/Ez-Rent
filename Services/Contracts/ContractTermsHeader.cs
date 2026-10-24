namespace EZ_Rent.Services.Contracts;

/// <summary>
/// BM03 có "Hình thức thanh toán" và "Nơi lập hợp đồng" nhưng RentalContract chưa có cột riêng (không thêm migration).
/// Hai dòng này được đặt ở đầu RentalContract.Terms, nên cũng hiện trong phụ lục "Điều khoản đang lưu" của bản nháp phía Tenant.
/// </summary>
public static class ContractTermsHeader
{
    private const string PaymentPrefix = "Hình thức thanh toán: ";
    private const string PlacePrefix = "Nơi lập hợp đồng: ";

    public static readonly IReadOnlyDictionary<string, string> PaymentMethods = new Dictionary<string, string>
    {
        ["transfer"] = "Chuyển khoản theo mã VietQR trên hóa đơn",
        ["cash"] = "Tiền mặt",
        ["both"] = "Chuyển khoản theo mã VietQR hoặc tiền mặt"
    };

    public static string Compose(string paymentKey, string? place, string body)
    {
        var lines = new List<string> { PaymentPrefix + PaymentMethods.GetValueOrDefault(paymentKey, PaymentMethods["both"]) };
        if (!string.IsNullOrWhiteSpace(place)) lines.Add(PlacePrefix + place.Trim());
        return string.Join('\n', lines) + "\n\n" + body.Trim();
    }

    /// <summary>Tách lại các dòng đầu khi mở form sửa; nội dung không có tiêu đề được trả nguyên vẹn.</summary>
    public static (string PaymentKey, string? Place, string Body) Parse(string? terms)
    {
        var text = (terms ?? "").Replace("\r\n", "\n");
        var payment = "both"; string? place = null;
        var lines = text.Split('\n').ToList();
        var consumed = 0;
        foreach (var line in lines)
        {
            if (line.StartsWith(PaymentPrefix, StringComparison.Ordinal))
            {
                var value = line[PaymentPrefix.Length..];
                payment = PaymentMethods.FirstOrDefault(p => p.Value == value).Key ?? "both";
                consumed++;
            }
            else if (line.StartsWith(PlacePrefix, StringComparison.Ordinal)) { place = line[PlacePrefix.Length..]; consumed++; }
            else break;
        }
        if (consumed == 0) return (payment, null, text.Trim());
        return (payment, place, string.Join('\n', lines.Skip(consumed)).Trim());
    }

    public static string PaymentLabel(string? terms) => PaymentMethods.GetValueOrDefault(Parse(terms).PaymentKey, PaymentMethods["both"]);
}
