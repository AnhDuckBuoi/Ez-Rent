using Microsoft.AspNetCore.Html;

namespace EZ_Rent.Helpers;

/// <summary>
/// Bộ icon nét (SVG nội tuyến) cho thanh điều hướng và menu theo vai trò, cùng phong cách nét mảnh, bo tròn với icon phía Tenant (_HomeIcon).
/// Không cần thư viện hay CDN; màu theo màu chữ (currentColor) nên tự đổi khi mục được chọn.
/// Dùng trong Razor: @NavIcon.Svg("search")
/// </summary>
public static class NavIcon
{
    private static readonly Dictionary<string, string> Paths = new()
    {
        ["home"] = "M3 11 12 4l9 7M5 9.5V20h14V9.5M10 20v-6h4v6",
        ["search"] = "M21 21l-4.8-4.8M18 10.5a7.5 7.5 0 1 1-15 0 7.5 7.5 0 0 1 15 0",
        ["dashboard"] = "M4 4h7v8H4zM13 4h7v5h-7zM13 11h7v9h-7zM4 14h7v6H4z",
        ["building"] = "M4 21V7l8-4 8 4v14M2 21h20M9 10h1m4 0h1M9 14h1m4 0h1M10 21v-3h4v3",
        ["plus"] = "M12 5v14M5 12h14",
        ["calendar"] = "M4 6h16v15H4zM8 3v5m8-5v5M4 11h16",
        ["clock"] = "M12 7v5l3 2M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0",
        ["contract"] = "M14 3H6v18h12V7zM14 3v4h4M9 12h6M9 16h6M9 8h2",
        ["bolt"] = "M13 2 4.5 13.5H11L10 22l8.5-11.5H12z",
        ["receipt"] = "M6 3h12v18l-3-2-3 2-3-2-3 2zM9 8h6M9 12h6M9 16h3",
        ["bank"] = "M3 10 12 4l9 6M5 10v8m4.67-8v8m4.66-8v8M19 10v8M3 21h18",
        ["chat"] = "M21 12a8 8 0 0 1-11.6 7.1L4 20.5l1.4-4.6A8 8 0 1 1 21 12",
        ["wrench"] = "M14.5 6.5a4 4 0 0 0-5.3 5.3L3.5 17.5l3 3 5.7-5.7a4 4 0 0 0 5.3-5.3l-2.6 2.6-2.3-.7-.7-2.3z",
        ["support"] = "M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0M16 12a4 4 0 1 1-8 0 4 4 0 0 1 8 0M5.6 5.6l3.6 3.6m5.6 5.6 3.6 3.6m0-12.8-3.6 3.6m-5.6 5.6-3.6 3.6",
        ["bell"] = "M18 9a6 6 0 0 0-12 0c0 6.5-2.5 7.5-2.5 8.5h17c0-1-2.5-2-2.5-8.5M10 21h4",
        ["heart"] = "M20.5 5.5c-2.3-2.4-5.8-1.9-8.5 1.2-2.7-3.1-6.2-3.6-8.5-1.2-2.6 2.8-1.6 7.1 8.5 14.5 10.1-7.4 11.1-11.7 8.5-14.5",
        ["key"] = "M16 7.5a4 4 0 1 1-8 0 4 4 0 0 1 8 0M12 11.5V22M12 16.5h3.5M12 19.5h2.5",
        ["user"] = "M5 21v-2a6 6 0 0 1 12 0v2M15 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0",
        ["users"] = "M2 21v-2a5 5 0 0 1 10 0v2M11 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0M16 21v-2a5 5 0 0 0-2-4M16 3.5a4 4 0 0 1 0 7M22 21v-2a5 5 0 0 0-3-4.5",
        ["user-plus"] = "M3 21v-2a6 6 0 0 1 9-5.2M13 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0M19 13v8M15 17h8",
        ["shield"] = "M12 21.5s8-3.8 8-10V5.5L12 2.5l-8 3v6c0 6.2 8 10 8 10",
        ["check-square"] = "M9 11.5l2.5 2.5L20 5.5M20 12v7a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h10",
        ["database"] = "M12 9c4.4 0 8-1.3 8-3s-3.6-3-8-3-8 1.3-8 3 3.6 3 8 3M4 6v12c0 1.7 3.6 3 8 3s8-1.3 8-3V6M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3",
        ["chart"] = "M4 20V4M4 20h16M8 16v-4m4 4V8m4 8v-6",
        ["logout"] = "M10 4H6a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h4M15 16l4-4-4-4M19 12H9",
        ["login"] = "M14 4h4a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-4M9 16l4-4-4-4M13 12H3"
    };

    public static IHtmlContent Svg(string name, int size = 18) =>
        new HtmlString(Paths.TryGetValue(name, out var d)
            ? $"<svg class=\"ez-nav-icon\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" " +
              $"stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\" focusable=\"false\" style=\"vertical-align:-3px;margin-right:6px;flex-shrink:0\"><path d=\"{d}\"/></svg>"
            : "");

    public static IReadOnlyCollection<string> Names => Paths.Keys;
}
