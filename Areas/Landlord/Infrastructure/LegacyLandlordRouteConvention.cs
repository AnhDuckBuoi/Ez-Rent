using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EZ_Rent.Areas.Landlord.Infrastructure;

/// <summary>
/// Các controller CRUD tự sinh cũ của Landlord (namespace EZ_Rent.Areas_Landlord_Controllers) không kiểm tra
/// đăng nhập, vai trò hay quyền sở hữu. Không sửa các file đó; convention này chỉ bổ sung:
/// 1) bắt buộc vai trò Landlord, 2) chuyển hướng mọi route cũ sang trang mới tương ứng đã kiểm tra quyền.
/// Nhờ vậy menu cũ trong _Layout vẫn dùng được mà không còn đường ghi dữ liệu không an toàn.
/// Dashboard cũ và /Landlord cũng được chuyển sang trang Tổng quan mới (Overview).
/// </summary>
public sealed class LegacyLandlordRouteConvention : IApplicationModelConvention
{
    public const string LegacyNamespace = "EZ_Rent.Areas_Landlord_Controllers";

    /// <summary>
    /// Trang Dashboard cũ (và /Landlord → Dashboard) vẫn dùng layout Bootstrap chung. Theo yêu cầu của nhóm,
    /// chuyển sang trang Tổng quan mới để chủ trọ luôn thấy cùng một giao diện. File Dashboard cũ không bị sửa.
    /// </summary>
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

public sealed class LegacyLandlordRedirectFilter(string legacyController) : IResourceFilter
{
    private static readonly Dictionary<string, string> Targets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Post"] = "Rooms", ["RoomImage"] = "Rooms", ["Service"] = "Rooms",
        ["ViewingAppointment"] = "Viewings", ["Contract"] = "Contracts", ["MeterReading"] = "MeterReadings",
        ["Invoice"] = "Invoices", ["RepairRequest"] = "Repairs"
    };

    // Chỉ những controller cũ có trang chi tiết cùng loại dữ liệu mới giữ lại id khi chuyển hướng.
    private static readonly HashSet<string> SameEntity = new(StringComparer.OrdinalIgnoreCase)
    { "Post", "ViewingAppointment", "Contract", "MeterReading", "Invoice", "RepairRequest" };

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var target = Targets.GetValueOrDefault(legacyController, "Overview");
        var action = context.RouteData.Values["action"] as string ?? "Index";
        object? routeValues = new { area = "Landlord" };
        string targetAction = "Index";
        if (string.Equals(action, "Details", StringComparison.OrdinalIgnoreCase) && SameEntity.Contains(legacyController)
            && int.TryParse(context.RouteData.Values["id"] as string, out var id) && id > 0)
        {
            targetAction = "Details";
            routeValues = new { area = "Landlord", id };
        }
        else if (string.Equals(action, "Create", StringComparison.OrdinalIgnoreCase) && legacyController is "Post" or "Contract")
        {
            targetAction = "Create";
        }
        context.Result = new RedirectToActionResult(targetAction, target, routeValues);
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
