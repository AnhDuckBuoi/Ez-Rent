using EZ_Rent.Data;
using EZ_Rent.Models;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Services;

public sealed class RentalDocumentConflictException(string message) : Exception(message);
public enum DocumentAcknowledgement { Missing, StaleVersion, Acknowledged }

/// <summary>Shared operations for the future Landlord issuing flow and the Tenant reader.</summary>
public sealed class RentalDocumentService(AppDbContext db, RentalPrivateFileStore files)
{
    // Caller supplies the authenticated Landlord ID, never an ID posted by a form.
    public async Task<ContractDocument> AppendContractAsync(int contractId, string landlordId, int expectedVersion,
        IFormFile file, CancellationToken ct = default)
    {
        var contract = await db.RentalContracts.AsNoTracking()
            .SingleOrDefaultAsync(c => c.RentalContractId == contractId && c.LandlordId == landlordId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng thuộc chủ nhà.");
        if (expectedVersion < 0 || contract.CurrentDocumentVersion != expectedVersion)
            throw new RentalDocumentConflictException("Tài liệu đã thay đổi. Hãy tải lại trước khi gửi bản mới.");
        var stored = await files.SaveAsync(file, allowPdf: true, ct);
        var commitStarted = false;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var updated = await db.RentalContracts.Where(c => c.RentalContractId == contractId &&
                    c.LandlordId == landlordId && c.TenantId == contract.TenantId && c.CurrentDocumentVersion == expectedVersion)
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.CurrentDocumentVersion, expectedVersion + 1), ct);
            if (updated != 1) throw new RentalDocumentConflictException("Đã có tài liệu mới hơn. Hãy tải lại hợp đồng.");
            var document = new ContractDocument
            {
                RentalContractId = contractId, Version = expectedVersion + 1, TenantId = contract.TenantId,
                StorageKey = stored.Key, FileName = stored.FileName, ContentType = stored.ContentType,
                ByteLength = stored.Length, Sha256 = stored.Sha256, UploadedById = landlordId
            };
            db.ContractDocuments.Add(document);
            await db.SaveChangesAsync(ct);
            commitStarted = true;
            await transaction.CommitAsync(ct);
            return document;
        }
        catch
        {
            // A connection loss during commit has an unknown outcome; retain that file for recovery.
            if (!commitStarted) files.DeleteUncommitted(stored.Key);
            throw;
        }
    }

    public async Task<DocumentAcknowledgement> AcknowledgeAsync(int contractId, int documentId, string tenantId,
        CancellationToken ct = default)
    {
        var document = await db.ContractDocuments.AsNoTracking().Include(d => d.RentalContract)
            .SingleOrDefaultAsync(d => d.ContractDocumentId == documentId && d.RentalContractId == contractId &&
                d.RentalContract.TenantId == tenantId && d.TenantId == tenantId, ct);
        if (document == null) return DocumentAcknowledgement.Missing;
        if (document.Version != document.RentalContract.CurrentDocumentVersion) return DocumentAcknowledgement.StaleVersion;
        var changed = await db.ContractDocuments.Where(d => d.ContractDocumentId == documentId &&
                d.RentalContract.TenantId == tenantId && d.TenantId == tenantId && d.Version == d.RentalContract.CurrentDocumentVersion &&
                d.TenantAcknowledgedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(d => d.TenantAcknowledgedAt, DateTime.UtcNow)
                .SetProperty(d => d.TenantAcknowledgedById, tenantId), ct);
        if (changed == 1) return DocumentAcknowledgement.Acknowledged;
        var stillCurrent = await db.ContractDocuments.AsNoTracking().AnyAsync(d => d.ContractDocumentId == documentId &&
            d.RentalContract.TenantId == tenantId && d.TenantId == tenantId && d.Version == d.RentalContract.CurrentDocumentVersion &&
            d.TenantAcknowledgedById == tenantId && d.TenantAcknowledgedAt != null, ct);
        return stillCurrent ? DocumentAcknowledgement.Acknowledged : DocumentAcknowledgement.StaleVersion;
    }

    // Call when issuing an invoice, not from GET or from the Tenant area.
    // A unique InvoiceId retains the first capture; correction needs a separate issuing workflow.
    public async Task<InvoiceMeterEvidence> CaptureInvoiceAsync(int invoiceId, string landlordId,
        IFormFile? electricImage, IFormFile? waterImage, CancellationToken ct = default)
    {
        var invoice = await db.Invoices.AsNoTracking().Include(i => i.RentalContract).Include(i => i.MeterReading)
            .SingleOrDefaultAsync(i => i.InvoiceId == invoiceId && i.RentalContract.LandlordId == landlordId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn thuộc chủ nhà.");
        if (await db.InvoiceMeterEvidence.AnyAsync(e => e.InvoiceId == invoiceId, ct))
            throw new RentalDocumentConflictException("Hóa đơn đã lưu bộ chỉ số và ảnh đối chiếu.");
        var reading = invoice.MeterReading;
        if (reading == null || reading.RentalPostId != invoice.RentalContract.RentalPostId || reading.Period != invoice.BillingPeriod)
            throw new InvalidDataException("Chỉ số phải thuộc đúng phòng và kỳ hóa đơn.");
        ValidateIndices(reading.PrevElectricIndex, reading.CurrElectricIndex, reading.ElectricUsage);
        ValidateIndices(reading.PrevWaterIndex, reading.CurrWaterIndex, reading.WaterUsage);
        if (reading.ElectricUsage != invoice.ElectricUsage || reading.WaterUsage != invoice.WaterUsage)
            throw new InvalidDataException("Lượng tiêu thụ trên hóa đơn không khớp với chỉ số điện nước.");
        if (electricImage == null || waterImage == null)
            throw new InvalidDataException("Cần cả ảnh công tơ điện và ảnh đồng hồ nước trước khi lưu đối chiếu.");
        StoredRentalFile? electric = null, water = null;
        var commitStarted = false;
        try
        {
            electric = await files.SaveAsync(electricImage, allowPdf: false, ct);
            water = await files.SaveAsync(waterImage, allowPdf: false, ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var evidence = new InvoiceMeterEvidence
            {
                InvoiceId = invoiceId, TenantId = invoice.RentalContract.TenantId, RentalPostId = reading.RentalPostId, Period = reading.Period, ReadingDate = reading.ReadingDate,
                PrevElectricIndex = reading.PrevElectricIndex, CurrElectricIndex = reading.CurrElectricIndex, ElectricUsage = reading.ElectricUsage,
                PrevWaterIndex = reading.PrevWaterIndex, CurrWaterIndex = reading.CurrWaterIndex, WaterUsage = reading.WaterUsage,
                ElectricityPrice = invoice.RentalContract.AgreedElectricityPrice, WaterPrice = invoice.RentalContract.AgreedWaterPrice,
                ElectricStorageKey = electric.Key, ElectricContentType = electric.ContentType, ElectricSha256 = electric.Sha256,
                WaterStorageKey = water.Key, WaterContentType = water.ContentType, WaterSha256 = water.Sha256, CapturedById = landlordId
            };
            db.InvoiceMeterEvidence.Add(evidence);
            await db.SaveChangesAsync(ct);
            commitStarted = true;
            await transaction.CommitAsync(ct);
            return evidence;
        }
        catch
        {
            if (!commitStarted)
            {
                if (electric != null) files.DeleteUncommitted(electric.Key);
                if (water != null) files.DeleteUncommitted(water.Key);
            }
            throw;
        }
    }

    private static void ValidateIndices(double previous, double current, double usage)
    {
        if (!double.IsFinite(previous) || !double.IsFinite(current) || !double.IsFinite(usage) ||
            previous < 0 || current < previous || Math.Abs(current - previous - usage) > 0.000001)
            throw new InvalidDataException("Chỉ số điện nước và lượng tiêu thụ không hợp lệ.");
    }
}
