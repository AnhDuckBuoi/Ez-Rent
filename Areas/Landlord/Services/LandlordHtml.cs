using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace EZ_Rent.Areas.Landlord.Services;

/// <summary>Đoạn HTML nhỏ dùng lại nhiều nơi; mọi chữ đều được HtmlEncoder mã hóa.</summary>
public static class LandlordHtml
{
    public static IHtmlContent Badge(StatusLabel label) =>
        new HtmlString($"<span class=\"ll-badge is-{label.Tone}\">{HtmlEncoder.Default.Encode(label.Text)}</span>");

    /// <summary>Một dòng của phiếu tính tiền: nhãn ...... giá trị (giá trị có thể được AJAX cập nhật qua data-ll-out).</summary>
    public static IHtmlContent BillRow(string label, string? detail, string value, string? outKey = null, string? detailKey = null)
    {
        var e = HtmlEncoder.Default;
        var detailHtml = detail == null && detailKey == null ? ""
            : $"<small{(detailKey == null ? "" : $" data-ll-out=\"{e.Encode(detailKey)}\"")}>{e.Encode(detail ?? "")}</small>";
        var outAttr = outKey == null ? "" : $" data-ll-out=\"{e.Encode(outKey)}\"";
        return new HtmlString($"<div class=\"ll-bill-row\"><span class=\"label\">{e.Encode(label)}{detailHtml}</span><span class=\"leader\" aria-hidden=\"true\"></span><span class=\"value\"{outAttr}>{e.Encode(value)}</span></div>");
    }

    private static readonly Dictionary<string, string> Icons = new()
    {
        ["overview"] = "<rect x=\"3\" y=\"3\" width=\"7\" height=\"9\" rx=\"1.5\"/><rect x=\"14\" y=\"3\" width=\"7\" height=\"5\" rx=\"1.5\"/><rect x=\"14\" y=\"12\" width=\"7\" height=\"9\" rx=\"1.5\"/><rect x=\"3\" y=\"16\" width=\"7\" height=\"5\" rx=\"1.5\"/>",
        ["rooms"] = "<path d=\"M5 21V4a1 1 0 0 1 1-1h12a1 1 0 0 1 1 1v17\"/><path d=\"M3 21h18\"/><circle cx=\"15\" cy=\"12\" r=\"1\"/>",
        ["calendar"] = "<rect x=\"3\" y=\"5\" width=\"18\" height=\"16\" rx=\"2\"/><path d=\"M16 3v4M8 3v4M3 10h18\"/>",
        ["chat"] = "<path d=\"M21 12a8 8 0 0 1-11.6 7.1L4 20l1.1-4.2A8 8 0 1 1 21 12z\"/>",
        ["contract"] = "<path d=\"M14 3H6a1 1 0 0 0-1 1v16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V8z\"/><path d=\"M14 3v5h5M9 13h6M9 17h6\"/>",
        ["bolt"] = "<path d=\"M13 2 4 14h7l-1 8 9-12h-7z\"/>",
        ["drop"] = "<path d=\"M12 3s6 6.5 6 11a6 6 0 0 1-12 0c0-4.5 6-11 6-11z\"/>",
        ["receipt"] = "<path d=\"M6 3h12v18l-3-2-3 2-3-2-3 2z\"/><path d=\"M9 8h6M9 12h6\"/>",
        ["wrench"] = "<path d=\"M14.7 6.3a4 4 0 0 0-5.4 5.4L3 18l3 3 6.3-6.3a4 4 0 0 0 5.4-5.4l-2.5 2.5-2.5-.5-.5-2.5z\"/>",
        ["bank"] = "<path d=\"M3 10 12 4l9 6M5 10v8M19 10v8M9.5 10v8M14.5 10v8M3 21h18\"/>",
        ["user"] = "<circle cx=\"12\" cy=\"8\" r=\"4\"/><path d=\"M4 21a8 8 0 0 1 16 0\"/>",
        ["logout"] = "<path d=\"M15 4h3a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1h-3M10 17l5-5-5-5M15 12H4\"/>",
        ["home"] = "<path d=\"M3 11 12 4l9 7\"/><path d=\"M5 10v10h14V10\"/>",
        ["help"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M9.5 9.5a2.5 2.5 0 1 1 3.5 2.3c-.6.3-1 .9-1 1.6v.6M12 17h.01\"/>",
        ["plus"] = "<path d=\"M12 5v14M5 12h14\"/>",
        ["print"] = "<path d=\"M7 9V3h10v6M7 17H5a2 2 0 0 1-2-2v-4a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v4a2 2 0 0 1-2 2h-2\"/><rect x=\"7\" y=\"14\" width=\"10\" height=\"7\"/>"
    };

    /// <summary>Biểu tượng SVG nội tuyến (trang trí, ẩn với trình đọc màn hình).</summary>
    public static IHtmlContent Icon(string name) => new HtmlString(
        "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\" focusable=\"false\">"
        + Icons.GetValueOrDefault(name, "") + "</svg>");
}
