using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace EZ_Rent.Areas.Landlord.Helpers;

/// <summary>Đoạn HTML nhỏ dùng lại nhiều nơi, cùng kiểu badge với trang Admin; mọi chữ đều được HtmlEncoder mã hóa.</summary>
public static class LandlordUi
{
    public static IHtmlContent Badge(StatusLabel label)
    {
        var dark = label.Tone is "warning" or "info" ? " text-dark" : "";
        return new HtmlString($"<span class=\"badge bg-{label.Tone}{dark}\">{HtmlEncoder.Default.Encode(label.Text)}</span>");
    }

    /// <summary>Một dòng của phiếu tính tiền: nhãn ...... giá trị (giá trị có thể được AJAX cập nhật qua data-ll-out).</summary>
    public static IHtmlContent BillRow(string label, string? detail, string value, string? outKey = null, string? detailKey = null)
    {
        var e = HtmlEncoder.Default;
        var detailHtml = detail == null && detailKey == null ? ""
            : $"<small class=\"d-block text-muted\"{(detailKey == null ? "" : $" data-ll-out=\"{e.Encode(detailKey)}\"")}>{e.Encode(detail ?? "")}</small>";
        var outAttr = outKey == null ? "" : $" data-ll-out=\"{e.Encode(outKey)}\"";
        return new HtmlString($"<div class=\"ll-bill-row\"><span>{e.Encode(label)}{detailHtml}</span><span class=\"ll-leader\" aria-hidden=\"true\"></span><span class=\"fw-semibold text-end\"{outAttr}>{e.Encode(value)}</span></div>");
    }
}
