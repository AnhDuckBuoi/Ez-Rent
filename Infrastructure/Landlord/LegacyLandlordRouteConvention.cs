using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EZ_Rent.Infrastructure.Landlord;

/// <summary>
/// Các controller CRUD tự sinh cũ của Landlord (namespace EZ_Rent.Areas_Landlord_Controllers) không kiểm tra đăng nhập,
/// vai trò hay quyền sở hữu, nhưng menu chủ trọ trong _Layout chung vẫn trỏ tới chúng. Không sửa các file đó;
/// convention này bắt buộc vai trò Landlord và chuyển từng route cũ sang trang mới tương ứng đã kiểm tra quyền.
/// Dashboard/Home cũ của Landlord cũng chuyển sang trang Tổng quan mới để chủ trọ luôn thấy một giao diện thống nhất.
/// </summary>
public sealed class LegacyLandlordRouteConvention : IApplicationModelConvention
{
    public const string LegacyNamespace = "EZ_Rent.Areas_Landlord_Controllers";

    public static readonly HashSet<string> ReplacedControllers = new(StringComparer.Ordinal)
    {
        "EZ_Rent.Areas.Landlord.Controllers.DashboardController",
        "EZ_Rent.Areas.Landlord.Controllers.HomeController"
    };

    public void Apply(ApplicationModel application)
    {
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireRole("Landlord").Build();
        foreach (var controller in application.Controllers)
        {
            var legacy = string.Equals(controller.ControllerType.Namespace, LegacyNamespace, StringComparison.Ordinal);
            if (!legacy && !ReplacedControllers.Contains(controller.ControllerType.FullName ?? "")) continue;
            controller.Filters.Add(new AuthorizeFilter(policy));
            controller.Filters.Add(new LegacyLandlordRedirectFilter(controller.ControllerName));
        }
    }
}

/// <summary>Chuyển route cũ sang trang mới (giữ id khi cùng loại dữ liệu), không phân biệt hoa thường.</summary>
public sealed class LegacyLandlordRedirectFilter(string legacyController) : IResourceFilter
{
    private static readonly Dictionary<string, string> Targets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Post"] = "Rooms", ["RoomImage"] = "Rooms", ["Service"] = "Rooms",
        ["ViewingAppointment"] = "Viewings", ["Contract"] = "Contracts", ["MeterReading"] = "MeterReadings",
        ["Invoice"] = "Invoices", ["RepairRequest"] = "Repairs"
    };

    private static readonly HashSet<string> SameEntity = new(StringComparer.OrdinalIgnoreCase)
    { "Post", "ViewingAppointment", "Contract", "MeterReading", "Invoice", "RepairRequest" };

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var target = Targets.GetValueOrDefault(legacyController, "Overview");
        var action = (context.RouteData.Values["action"] as string ?? "Index").ToLowerInvariant();
        object routeValues = new { area = "Landlord" };
        var targetAction = "Index";
        if (action is "details" or "edit" or "delete" && SameEntity.Contains(legacyController) &&
            int.TryParse(context.RouteData.Values["id"]?.ToString(), out var id) && id > 0)
        {
            targetAction = "Details";
            routeValues = new { area = "Landlord", id };
        }
        else if (action == "create" && legacyController is "Post" or "Contract")
        {
            targetAction = "Create";
        }
        context.Result = new RedirectToActionResult(targetAction, target, routeValues);
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
