using EZ_Rent.Models;

namespace EZ_Rent.Areas.Tenant.ViewModels;

public sealed record ContractDetailsModel(RentalContract Contract, ContractDocument? CurrentDocument,
    IReadOnlyList<ContractDocument> Documents, int DocumentPage, int DocumentPages);

public sealed record InvoiceDetailsModel(Invoice Invoice, InvoiceMeterEvidence? Evidence);
