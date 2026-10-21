using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Data;
using EZ_Rent.Models;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.Services;

/// <summary>Reads owned drafts and appends immutable snapshots using only allowed personal fields.</summary>
public sealed class TenantContractDraftService(AppDbContext db)
{
    private IQueryable<RentalContract> OwnedContracts(string tenantId) => db.RentalContracts.AsNoTracking()
        .Where(c => c.TenantId == tenantId).Include(c => c.Tenant).Include(c => c.Landlord).Include(c => c.RentalPost);

    private IQueryable<ContractTenantDraft> Drafts(int contractId, string tenantId) => db.ContractTenantDrafts.AsNoTracking()
        .Where(d => d.RentalContractId == contractId && d.TenantId == tenantId);

    public async Task<ContractDraftPage?> GetAsync(int contractId, string tenantId, int? revision = null,
        int historyPage = 1, ContractTenantDraftInput? attempted = null, CancellationToken ct = default)
    {
        var contract = await OwnedContracts(tenantId).SingleOrDefaultAsync(c => c.RentalContractId == contractId, ct);
        if (contract == null) return null;
        var query = Drafts(contractId, tenantId);
        var latest = await query.OrderByDescending(d => d.Revision).FirstOrDefaultAsync(ct);
        var selected = revision.HasValue ? await query.SingleOrDefaultAsync(d => d.Revision == revision, ct) : latest;
        if (revision.HasValue && selected == null) return null;
        var snapshot = selected ?? Capture(contract);
        var pages = TenantRentalPresentation.Pages(await query.CountAsync(ct));
        historyPage = Math.Min(historyPage, pages);
        var history = await query.OrderByDescending(d => d.Revision).Skip((historyPage - 1) * TenantRentalPresentation.PageSize)
            .Take(TenantRentalPresentation.PageSize).ToListAsync(ct);
        var input = attempted ?? new ContractTenantDraftInput
        {
            ExpectedRevision = latest?.Revision ?? 0, ContractStamp = Stamp(contract), FullName = snapshot.FullName,
            PhoneNumber = snapshot.PhoneNumber, DateOfBirth = snapshot.DateOfBirth, PermanentAddress = snapshot.PermanentAddress,
            DocumentKind = snapshot.DocumentKind, DocumentNumber = snapshot.DocumentNumber,
            DocumentIssuedOn = snapshot.DocumentIssuedOn, DocumentIssuedAt = snapshot.DocumentIssuedAt
        };
        return new(contract, snapshot, input, history, latest?.Revision ?? 0, historyPage, pages,
            Missing(snapshot), selected != null && selected.ContractStamp != Stamp(contract));
    }

    public async Task<DraftSaveResult> SaveAsync(int contractId, string tenantId, ContractTenantDraftInput input,
        CancellationToken ct = default)
    {
        // Keep the source agreement stable between checking the form stamp and copying it.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var contract = await OwnedContracts(tenantId).SingleOrDefaultAsync(c => c.RentalContractId == contractId, ct);
        if (contract == null) return new(DraftSaveStatus.Missing);
        var latest = await Drafts(contractId, tenantId).OrderByDescending(d => d.Revision).FirstOrDefaultAsync(ct);
        if ((latest?.Revision ?? 0) != input.ExpectedRevision || Stamp(contract) != input.ContractStamp)
            return new(DraftSaveStatus.Conflict);
        var errors = ValidateDates(input);
        if (errors.Count > 0) return new(DraftSaveStatus.Invalid, Errors: errors);
        var draft = Capture(contract);
        draft.Revision = input.ExpectedRevision + 1;
        draft.FullName = input.FullName.Trim();
        draft.PhoneNumber = Clean(input.PhoneNumber);
        draft.DateOfBirth = input.DateOfBirth;
        draft.PermanentAddress = Clean(input.PermanentAddress);
        draft.DocumentKind = input.DocumentKind;
        draft.DocumentNumber = Clean(input.DocumentNumber);
        draft.DocumentIssuedOn = input.DocumentIssuedOn;
        draft.DocumentIssuedAt = Clean(input.DocumentIssuedAt);
        db.ContractTenantDrafts.Add(draft);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            db.Entry(draft).State = EntityState.Detached;
            if (await Drafts(contractId, tenantId).AnyAsync(d => d.Revision == draft.Revision, ct))
                return new(DraftSaveStatus.Conflict);
            throw;
        }
        await transaction.CommitAsync(ct);
        return new(DraftSaveStatus.Saved, draft.Revision);
    }

    private static ContractTenantDraft Capture(RentalContract contract) => new()
    {
        RentalContractId = contract.RentalContractId, TenantId = contract.TenantId, ContractStamp = Stamp(contract),
        FullName = contract.Tenant.FullName, PhoneNumber = contract.Tenant.PhoneNumber,
        LandlordName = contract.Landlord.FullName, LandlordPhone = contract.Landlord.PhoneNumber,
        RoomAddress = $"{contract.RentalPost.Address}, {contract.RentalPost.District}, {contract.RentalPost.City}",
        StartDate = contract.StartDate, EndDate = contract.EndDate, MonthlyRent = contract.MonthlyRent,
        DepositAmount = contract.DepositAmount, ElectricityPrice = contract.AgreedElectricityPrice,
        WaterPrice = contract.AgreedWaterPrice, ContractTerms = contract.Terms
    };

    // Agreement fields already readable by this Tenant. This is a stale-form check, not a signature.
    private static string Stamp(RentalContract contract)
    {
        var source = new
        {
            contract.RentalContractId, contract.TenantId, contract.LandlordId, contract.RentalPostId,
            contract.StartDate, contract.EndDate, contract.MonthlyRent, contract.DepositAmount,
            contract.AgreedElectricityPrice, contract.AgreedWaterPrice, contract.Terms,
            contract.CurrentDocumentVersion, contract.Status, LandlordName = contract.Landlord.FullName,
            LandlordPhone = contract.Landlord.PhoneNumber, contract.RentalPost.Address,
            contract.RentalPost.District, contract.RentalPost.City
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(source))));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Dictionary<string, string> ValidateDates(ContractTenantDraftInput input)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var errors = new Dictionary<string, string>();
        if (input.DateOfBirth > today) errors[nameof(input.DateOfBirth)] = "Ngày sinh không được ở tương lai.";
        if (input.DocumentIssuedOn > today) errors[nameof(input.DocumentIssuedOn)] = "Ngày cấp không được ở tương lai.";
        if (input.DocumentIssuedOn.HasValue && input.DateOfBirth.HasValue && input.DocumentIssuedOn < input.DateOfBirth)
            errors[nameof(input.DocumentIssuedOn)] = "Ngày cấp không được trước ngày sinh.";
        return errors;
    }

    private static IReadOnlyList<string> Missing(ContractTenantDraft draft)
    {
        var fields = new List<string>();
        if (string.IsNullOrWhiteSpace(draft.FullName)) fields.Add("Họ và tên");
        if (string.IsNullOrWhiteSpace(draft.PhoneNumber)) fields.Add("Số điện thoại");
        if (!draft.DateOfBirth.HasValue) fields.Add("Ngày sinh");
        if (string.IsNullOrWhiteSpace(draft.PermanentAddress)) fields.Add("Địa chỉ thường trú");
        if (!draft.DocumentKind.HasValue) fields.Add("Loại giấy tờ");
        if (string.IsNullOrWhiteSpace(draft.DocumentNumber)) fields.Add("Số giấy tờ");
        if (!draft.DocumentIssuedOn.HasValue) fields.Add("Ngày cấp");
        if (string.IsNullOrWhiteSpace(draft.DocumentIssuedAt)) fields.Add("Nơi cấp");
        return fields;
    }

    public static string DocumentLabel(IdentityDocumentKind? kind) => kind switch
    {
        IdentityDocumentKind.CitizenIdentity => "CCCD / Căn cước",
        IdentityDocumentKind.LegacyIdentity => "CMND",
        IdentityDocumentKind.Passport => "Hộ chiếu",
        _ => "Giấy tờ định danh"
    };
}
