using EZ_Rent.Areas.Landlord.Helpers;
using EZ_Rent.Services.Billing;
using EZ_Rent.Services.Common;
using EZ_Rent.Services.Notifications;
using EZ_Rent.Services.Payments;
using EZ_Rent.Services.Uploads;
using EZ_Rent.Services.Viewings;
using EZ_Rent.Areas.Landlord.ViewModels.Account;
using EZ_Rent.Areas.Landlord.ViewModels.Contracts;
using EZ_Rent.Areas.Landlord.ViewModels.Invoices;
using EZ_Rent.Areas.Landlord.ViewModels.Meters;
using EZ_Rent.Areas.Landlord.ViewModels.Overview;
using EZ_Rent.Areas.Landlord.ViewModels.Payments;
using EZ_Rent.Areas.Landlord.ViewModels.Repairs;
using EZ_Rent.Areas.Landlord.ViewModels.Rooms;
using EZ_Rent.Areas.Landlord.ViewModels.Shared;
using EZ_Rent.Areas.Landlord.ViewModels.Support;
using EZ_Rent.Areas.Landlord.ViewModels.Viewings;
using EZ_Rent.Data;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>Trang tổng quan của chủ trọ: số liệu chính và các việc cần làm ngay (điểm vào của luồng Landlord).</summary>
[Area("Landlord")]
[Authorize(Roles = "Landlord")]
public sealed class OverviewController(AppDbContext db, TimeProvider? clock = null) : LandlordAreaController(db, clock)
{
    [HttpGet]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index()
    {
        var me = CurrentUserId;
        var now = VietnamNow;
        var today = VietnamToday;
        var period = VietnamTime.Period(now);
        var posts = MyPosts.AsNoTracking();
        var invoices = MyInvoices.AsNoTracking();
        var openInvoices = invoices.Where(i => i.PaymentStatus == PaymentStatus.Unpaid || i.PaymentStatus == PaymentStatus.AwaitingConfirmation || i.PaymentStatus == PaymentStatus.Overdue);

        var viewingRows = (await MyViewings.AsNoTracking()
            .Where(a => (a.Status == ViewingAppointmentStatus.Confirmed && a.ViewingDate >= now) || a.Status == ViewingAppointmentStatus.Pending)
            .OrderBy(a => a.ViewingDate).Take(20)
            .Select(a => new { a.ViewingAppointmentId, a.RentalPostId, a.RentalPost.Title, TenantName = a.Tenant.FullName, a.ViewingDate, a.Status, a.CreatedAt })
            .ToListAsync())
            .Select(a => new ViewingRow
            {
                Id = a.ViewingAppointmentId, RoomId = a.RentalPostId, RoomTitle = a.Title, TenantName = a.TenantName,
                ViewingDate = a.ViewingDate, Status = a.Status, CreatedAt = a.CreatedAt, IsPast = a.ViewingDate <= now
            }).ToList();

        var model = new OverviewModel
        {
            Name = (ViewData[LandlordMenuCounts.Key] as LandlordMenuCounts)?.Name ?? "",
            Period = period,
            Rooms = await posts.CountAsync(),
            Available = await posts.CountAsync(p => p.RoomStatus == RoomStatus.Available && p.ApprovalStatus == ApprovalStatus.Approved),
            Rented = await posts.CountAsync(p => p.RoomStatus == RoomStatus.Rented),
            Hidden = await posts.CountAsync(p => p.RoomStatus == RoomStatus.Hidden),
            WaitingApproval = await posts.CountAsync(p => p.ApprovalStatus == ApprovalStatus.Pending),
            NeedsAttention = await posts.CountAsync(p => p.ApprovalStatus == ApprovalStatus.NeedRevision || p.ApprovalStatus == ApprovalStatus.Rejected),
            PendingViewings = await MyViewings.CountAsync(a => a.Status == ViewingAppointmentStatus.Pending),
            ActiveContracts = await MyContracts.CountAsync(c => c.Status == ContractStatus.Active),
            ContractsWaiting = await MyContracts.CountAsync(c => c.Status == ContractStatus.PendingLandlord || c.Status == ContractStatus.PendingTenant),
            MissingReadings = await posts.CountAsync(p => p.RentalContracts.Any(c => c.Status == ContractStatus.Active) && !p.MeterReadings.Any(m => m.Period == period)),
            OpenInvoices = await openInvoices.CountAsync(),
            AwaitingPayment = await invoices.CountAsync(i => i.PaymentStatus == PaymentStatus.AwaitingConfirmation),
            OverdueInvoices = await openInvoices.CountAsync(i => i.PaymentStatus != PaymentStatus.AwaitingConfirmation && i.DueDate < today),
            CollectedThisPeriod = await invoices.Where(i => i.BillingPeriod == period && i.PaymentStatus == PaymentStatus.Paid).SumAsync(i => (decimal?)i.TotalAmount) ?? 0,
            OutstandingAmount = await openInvoices.SumAsync(i => (decimal?)i.TotalAmount) ?? 0,
            OpenRepairs = await MyRepairs.CountAsync(r => r.Status == RepairRequestStatus.Pending || r.Status == RepairRequestStatus.InProgress),
            UnreadMessages = await Db.Conversations.Where(c => (c.InitiatorId == CurrentUserId || c.RecipientId == CurrentUserId) && c.ConversationType == ConversationType.TenantLandlord).SelectMany(c => c.Messages).CountAsync(m => !m.IsRead && m.SenderId != me),
            BankConfigured = await BankAccountStore.ReadAsync(Db, me) != null,
            ViewingHoursConfigured = (await ViewingHoursStore.ReadAsync(Db, me))?.HasAnyTime == true,
            SupportReplies = (ViewData[LandlordMenuCounts.Key] as LandlordMenuCounts)?.SupportReplies ?? 0,
            PendingViewingList = viewingRows.Where(a => a.Status == ViewingAppointmentStatus.Pending).Take(5).ToList(),
            NextViewings = viewingRows.Where(a => a.Status == ViewingAppointmentStatus.Confirmed).Take(5).ToList(),
            UrgentRepairs = await MyRepairs.AsNoTracking()
                .Where(r => r.Status == RepairRequestStatus.Pending || r.Status == RepairRequestStatus.InProgress)
                .OrderByDescending(r => r.Priority).ThenBy(r => r.CreatedAt).Take(5)
                .Select(r => new RepairRow
                {
                    Id = r.RepairRequestId, RoomId = r.RentalPostId, RoomTitle = r.RentalPost.Title, TenantName = r.Tenant.FullName,
                    ProblemType = r.ProblemType, Priority = r.Priority, Status = r.Status, CreatedAt = r.CreatedAt
                }).ToListAsync(),
            InvoicesToCheck = await openInvoices
                .Where(i => i.PaymentStatus == PaymentStatus.AwaitingConfirmation || i.DueDate < today)
                .OrderBy(i => i.PaymentStatus == PaymentStatus.AwaitingConfirmation ? 0 : 1).ThenBy(i => i.DueDate).Take(5)
                .Select(i => new InvoiceRow
                {
                    Id = i.InvoiceId, Period = i.BillingPeriod, RoomTitle = i.RentalContract.RentalPost.Title, TenantName = i.RentalContract.Tenant.FullName,
                    Total = i.TotalAmount, DueDate = i.DueDate, Status = i.PaymentStatus, TenantConfirmedAt = i.TenantConfirmedAt,
                    Overdue = i.PaymentStatus != PaymentStatus.AwaitingConfirmation && i.DueDate < today
                }).ToListAsync(),
            RoomsToFix = await posts.Where(p => p.ApprovalStatus == ApprovalStatus.NeedRevision || p.ApprovalStatus == ApprovalStatus.Rejected)
                .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt).Take(5)
                .Select(p => new RoomRow { Id = p.RentalPostId, Title = p.Title, Approval = p.ApprovalStatus, ApprovalNote = p.ApprovalNote }).ToListAsync()
        };
        return View("Index", model);
    }
}
