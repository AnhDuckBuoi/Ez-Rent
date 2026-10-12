namespace EZ_Rent.Models.Enums;

public enum PaymentStatus
{
    Unpaid = 1,
    AwaitingConfirmation = 2,
    Paid = 3,
    Overdue = 4,
    Cancelled = 5
}