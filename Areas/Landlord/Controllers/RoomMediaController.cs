using EZ_Rent.Areas.Landlord.Helpers;
using EZ_Rent.Data;
using EZ_Rent.Models.Enums;
using EZ_Rent.Services.Rooms;
using EZ_Rent.Services.Uploads;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>
/// UC04 (phần ảnh/video của tin phòng, BM01 "Hình ảnh hoặc video"): tải lên, xóa, đặt ảnh đại diện, đổi thứ tự.
/// Mọi thao tác chỉ trên tin của chủ trọ đang đăng nhập; file được kiểm tra chữ ký (UploadStorage).
/// Thêm ảnh/video cho tin đã duyệt sẽ đưa tin về "Chờ duyệt" để quản trị viên kiểm tra lại (P02).
/// </summary>
[Area("Landlord")]
[Authorize(Roles = "Landlord")]
public sealed class RoomMediaController(AppDbContext db, IWebHostEnvironment env, TimeProvider? clock = null)
    : LandlordAreaController(db, clock)
{
    private string RoomPage(int id) => (Url.Action("Details", "Rooms", new { id }) ?? "/Landlord/Rooms") + "#media";
    private string RoomList => Url.Action("Index", "Rooms") ?? "/Landlord/Rooms";

    [HttpPost]
    [RequestSizeLimit(160 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 160 * 1024 * 1024)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload([FromRoute] int id, List<IFormFile> files)
    {
        var back = RoomPage(id);
        var post = await MyPosts.Include(p => p.RoomImages).FirstOrDefaultAsync(p => p.RentalPostId == id);
        if (post == null) return NotFoundPage("tin phòng", RoomList);
        files = files.Where(f => f.Length > 0).ToList();
        if (files.Count == 0) return Fail(StatusCodes.Status400BadRequest, "Chọn ít nhất một ảnh hoặc video.", back);
        if (post.RoomImages.Count + files.Count > RoomMediaWriter.MaxMediaPerRoom)
            return Fail(StatusCodes.Status400BadRequest, $"Mỗi tin tối đa {RoomMediaWriter.MaxMediaPerRoom} ảnh/video (hiện có {post.RoomImages.Count}).", back);
        var uploads = new List<CheckedUpload>();
        foreach (var file in files)
        {
            var (upload, error) = await UploadStorage.CheckAsync(file, allowVideo: true, null, HttpContext.RequestAborted);
            if (error != null) return Fail(StatusCodes.Status400BadRequest, error, back);
            uploads.Add(upload!);
        }
        var order = post.RoomImages.Count == 0 ? 0 : post.RoomImages.Max(i => i.DisplayOrder) + 1;
        var saved = await RoomMediaWriter.AddAsync(Db, env, id, uploads, order, post.RoomImages.Any(i => i.IsPrimary && i.MediaType == MediaType.Image), UtcNow, HttpContext.RequestAborted);
        // Ảnh mới cần được kiểm tra đúng thực tế (P02) nên tin đã duyệt quay về chờ duyệt.
        var resubmit = post.ApprovalStatus is ApprovalStatus.Approved or ApprovalStatus.Rejected or ApprovalStatus.NeedRevision;
        if (resubmit) { post.ApprovalStatus = ApprovalStatus.Pending; post.ApprovedAt = null; post.ApprovedById = null; }
        await Db.SaveChangesAsync();
        return Done($"Đã thêm {saved} file." + (resubmit ? " Tin được gửi duyệt lại vì có ảnh/video mới." : ""), back);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete([FromRoute] int id, [FromForm] int mediaId)
    {
        var back = RoomPage(id);
        var media = await Db.RoomImages.Where(i => i.RentalPostId == id && i.RentalPost.OwnerId == CurrentUserId)
            .OrderBy(i => i.DisplayOrder).ThenBy(i => i.RoomImageId).ToListAsync();
        var target = media.FirstOrDefault(i => i.RoomImageId == mediaId);
        if (target == null) return NotFoundPage("ảnh", back);
        Db.RoomImages.Remove(target);
        if (target.IsPrimary)
        {
            var next = media.FirstOrDefault(i => i.RoomImageId != mediaId && i.MediaType == MediaType.Image);
            if (next != null) next.IsPrimary = true;
        }
        await Db.SaveChangesAsync();
        UploadStorage.TryDelete(env, target.MediaUrl);
        return Done("Đã xóa file.", back);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimary([FromRoute] int id, [FromForm] int mediaId)
    {
        var back = RoomPage(id);
        var media = await Db.RoomImages.Where(i => i.RentalPostId == id && i.RentalPost.OwnerId == CurrentUserId).ToListAsync();
        var target = media.FirstOrDefault(i => i.RoomImageId == mediaId);
        if (target == null) return NotFoundPage("ảnh", back);
        if (target.MediaType != MediaType.Image) return Fail(StatusCodes.Status400BadRequest, "Ảnh đại diện phải là ảnh, không dùng video.", back);
        foreach (var item in media) item.IsPrimary = item.RoomImageId == mediaId;
        await Db.SaveChangesAsync();
        return Done("Đã đặt ảnh đại diện.", back);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move([FromRoute] int id, [FromForm] int mediaId, [FromForm] string direction)
    {
        var back = RoomPage(id);
        var media = await Db.RoomImages.Where(i => i.RentalPostId == id && i.RentalPost.OwnerId == CurrentUserId)
            .OrderBy(i => i.DisplayOrder).ThenBy(i => i.RoomImageId).ToListAsync();
        var index = media.FindIndex(i => i.RoomImageId == mediaId);
        if (index < 0) return NotFoundPage("ảnh", back);
        var swap = direction == "up" ? index - 1 : index + 1;
        if (swap >= 0 && swap < media.Count) (media[index], media[swap]) = (media[swap], media[index]);
        for (var i = 0; i < media.Count; i++) media[i].DisplayOrder = i;
        await Db.SaveChangesAsync();
        return Done("Đã đổi thứ tự hiển thị.", back);
    }
}
