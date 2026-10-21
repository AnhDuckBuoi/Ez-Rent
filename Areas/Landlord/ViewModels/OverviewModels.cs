namespace EZ_Rent.Areas.Landlord.ViewModels;

public sealed class OverviewModel
{
    public string Name { get; init; } = "";
    public string Period { get; init; } = "";
    public int Rooms { get; init; }
    public int Available { get; init; }
    public int Rented { get; init; }
    public int Hidden { get; init; }
    public int WaitingApproval { get; init; }
    public int NeedsAttention { get; init; }
    public int PendingViewings { get; init; }
    public int ActiveContracts { get; init; }
    public int ContractsWaiting { get; init; }
    public int MissingReadings { get; init; }
    public int OpenInvoices { get; init; }
    public int AwaitingPayment { get; init; }
    public int OverdueInvoices { get; init; }
    public decimal CollectedThisPeriod { get; init; }
    public decimal OutstandingAmount { get; init; }
    public int OpenRepairs { get; init; }
    public int UnreadMessages { get; init; }
    public bool BankConfigured { get; init; }
    public bool ViewingHoursConfigured { get; init; }
    public int SupportReplies { get; init; }
    public IReadOnlyList<ViewingRow> NextViewings { get; init; } = [];
    public IReadOnlyList<ViewingRow> PendingViewingList { get; init; } = [];
    public IReadOnlyList<RepairRow> UrgentRepairs { get; init; } = [];
    public IReadOnlyList<InvoiceRow> InvoicesToCheck { get; init; } = [];
    public IReadOnlyList<RoomRow> RoomsToFix { get; init; } = [];
}
