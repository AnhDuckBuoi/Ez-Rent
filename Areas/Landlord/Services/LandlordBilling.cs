using EZ_Rent.Models;

namespace EZ_Rent.Areas.Landlord.Services;

public sealed record ServiceCharge(string Name, decimal UnitPrice, int Quantity, string Unit)
{
    public decimal Amount => UnitPrice * Quantity;
}

/// <summary>Bảng tính hóa đơn. Dùng chung cho xem trước bằng AJAX và lúc lưu, để hai con số luôn khớp.</summary>
public sealed class InvoiceBreakdown
{
    public decimal RoomRent { get; init; }
    public double ElectricUsage { get; init; }
    public decimal ElectricPrice { get; init; }
    public decimal ElectricAmount { get; init; }
    public double WaterUsage { get; init; }
    public decimal WaterPrice { get; init; }
    public decimal WaterAmount { get; init; }
    public decimal FixedFee { get; init; }
    public IReadOnlyList<ServiceCharge> Services { get; init; } = [];
    public decimal ServiceFee => FixedFee + Services.Sum(s => s.Amount);
    public decimal Adjustment { get; init; }
    public decimal Total => RoomRent + ElectricAmount + WaterAmount + ServiceFee + Adjustment;
    public bool HasMeter { get; init; }

    public string ServiceNote()
    {
        var parts = new List<string>();
        if (FixedFee > 0) parts.Add($"Phí dịch vụ chung {LandlordFormat.Number(FixedFee)}");
        parts.AddRange(Services.Select(s => $"{s.Name} {LandlordFormat.Number(s.UnitPrice)} x{s.Quantity}"));
        return parts.Count == 0 ? "" : "Dịch vụ: " + string.Join("; ", parts) + ".";
    }
}

public static class LandlordBilling
{
    public const double MaxUsage = 100_000;

    public static double Usage(double previous, double current) => Math.Round(current - previous, 2, MidpointRounding.AwayFromZero);

    public static decimal Amount(double usage, decimal unitPrice) =>
        decimal.Round((decimal)usage * unitPrice, 0, MidpointRounding.AwayFromZero);

    public static InvoiceBreakdown Compute(RentalContract contract, MeterReading? reading, decimal fixedFee,
        IReadOnlyList<ServiceCharge> services, decimal adjustment) => new()
    {
        RoomRent = contract.MonthlyRent,
        HasMeter = reading != null,
        ElectricUsage = reading?.ElectricUsage ?? 0,
        ElectricPrice = contract.AgreedElectricityPrice,
        ElectricAmount = reading == null ? 0 : Amount(reading.ElectricUsage, contract.AgreedElectricityPrice),
        WaterUsage = reading?.WaterUsage ?? 0,
        WaterPrice = contract.AgreedWaterPrice,
        WaterAmount = reading == null ? 0 : Amount(reading.WaterUsage, contract.AgreedWaterPrice),
        FixedFee = fixedFee,
        Services = services,
        Adjustment = adjustment
    };
}
