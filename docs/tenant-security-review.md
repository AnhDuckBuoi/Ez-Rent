# Bàn giao gói 2 — Bảo vệ dữ liệu Tenant

Ngày hoàn tất kiểm tra: 14/10/2026. Nhánh làm việc: `feature/tenant-p3`. Gói này đã được Dương duyệt trước khi triển khai; chưa commit hoặc push.

## Kết quả

Năm controller Favorite, ViewingAppointment, Contract, Invoice và RepairRequest trong Area Tenant yêu cầu vai trò `Tenant`. Tài khoản đã xác thực nhưng thiếu ID Identity bị từ chối. Danh sách và từng thao tác theo ID đều truy vấn kèm điều kiện người sở hữu; ID không tồn tại hoặc thuộc Tenant khác đều trả 404.

| Tài nguyên | Quyền và hành vi hiện tại |
|---|---|
| Yêu thích | Chỉ danh sách của mình. Tạo/sửa chỉ nhận `RentalPostId`; tin phải được duyệt và còn trống. Tạo trùng trả về danh sách, không thêm bản ghi. Sửa thành phòng đã yêu thích trả lỗi form. Bỏ yêu thích chỉ xóa bản ghi của mình. |
| Lịch hẹn | Tạo/sửa nhận phòng, thời gian và ghi chú Tenant. Server đặt Tenant và trạng thái `Pending`. Sửa khi còn `Pending`; hủy khi `Pending` hoặc `Confirmed`. Hủy chuyển `Cancelled`, giữ lịch sử và ghi chú Landlord. |
| Hợp đồng | Chỉ xem danh sách/chi tiết hợp đồng của mình. Không còn action hoặc view CRUD Create/Edit/Delete. Chưa thêm chức năng ký hoặc xác nhận. |
| Hóa đơn | Chỉ xem hóa đơn qua hợp đồng của mình. Không còn action hoặc view CRUD Create/Edit/Delete. Không nhận số tiền, trạng thái thanh toán hoặc `PaidAt` từ Tenant. Chưa triển khai VietQR tự gạch nợ. |
| Sửa chữa | Tạo cho phòng thuộc hợp đồng `Active` của mình. Chỉ sửa/hủy yêu cầu `Pending`; không đổi phòng của yêu cầu đã gửi. Nội dung xử lý và thời điểm hoàn tất thuộc server/Landlord. Hủy chuyển `Cancelled`, giữ lịch sử. |

Các form Create/Edit dùng ba input ViewModel riêng; không có trường nhập tài khoản, trạng thái, ghi chú Landlord hoặc thời gian hệ thống. ID bản ghi lấy từ route, không lấy từ hidden entity ID do trình duyệt gửi. Timestamps lúc tạo dựa vào entity/server, lúc cập nhật dùng UTC.

Với sửa/hủy lịch hẹn và sửa chữa, câu lệnh cập nhật chứa cả owner và trạng thái được phép. Nếu Landlord xử lý bản ghi giữa lúc đọc và ghi, cập nhật không ghi đè trạng thái mới và trả 409. `ExecuteUpdateAsync` không chạy hook `SaveChanges`, nên `UpdatedAt` được gán UTC ngay trong câu lệnh này. Quy tắc về thời hạn đặt/hủy lịch, giờ địa phương và trùng lịch sẽ được chốt trong gói lịch hẹn; gói này chưa hoàn thiện những quy tắc đó.

Các POST còn lại đều giữ `[ValidateAntiForgeryToken]`; form Razor sinh token. `ModelState` kiểm tra dữ liệu trước khi ghi. Nội dung người dùng hiển thị qua Razor được encode; truy vấn EF Core dùng LINQ có tham số. Hóa đơn không có chỉ số công tơ vẫn xem được.

## Luồng dữ liệu để đọc code

Trình duyệt gửi form → MVC định tuyến đến controller Tenant → kiểm tra đăng nhập/vai trò và ID Identity → bind những trường có trong input ViewModel → truy vấn EF Core có owner và kiểm tra tham chiếu/trạng thái → ghi những trường được phép → redirect về danh sách. Nếu dữ liệu đầu vào sai, controller dựng lại danh sách phòng hợp lệ rồi trả form với lỗi.

ViewModel input giúp tránh việc binder tạo một entity với cả quyền sở hữu và trạng thái do trình duyệt quyết định. Server nạp bản ghi của người đang đăng nhập để kiểm tra, rồi chỉ sửa các cột được phép; không dùng `_context.Update(entity)` với entity từ form.

Điểm bắt đầu đọc:

- `Areas/Tenant/Controllers/TenantControllerBase.cs`: lấy Identity ID, từ chối principal thiếu ID, truy vấn phòng công khai/phòng đang thuê.
- `Areas/Tenant/Controllers/*Controller.cs`: quyền sở hữu và thao tác theo tài nguyên.
- `Areas/Tenant/ViewModels/*InputModel.cs`: dữ liệu Tenant được phép gửi và validation.
- `Areas/Tenant/Views`: form đã giới hạn input, link tài chính đã bỏ, nút hủy giữ lịch sử.
- `tests/TenantSecurity.Tests`: kiểm thử HTTP qua MVC, Razor, authorization, anti-forgery và EF Core.

## Bằng chứng kiểm tra

Build thành công. Lần biên dịch sau khi sửa các view hóa đơn không còn warning ở phần Tenant vừa chỉnh; các warning nullable của EmailSender, Admin, Landlord và RentalPost công khai vẫn nằm ngoài gói này.

Bộ kiểm thử cuối cùng: **42 đạt, 0 lỗi, 0 bỏ qua**. Có kiểm tra cả đường đi bị từ chối và thao tác hợp lệ:

- Anonymous, sai role và principal thiếu ID bị từ chối.
- Danh sách/chi tiết chỉ chứa dữ liệu Tenant hiện tại; Tenant A không xem/sửa/hủy dữ liệu của B.
- POST truy cập chéo vẫn bị chặn khi có CSRF token hợp lệ; POST thiếu token trả 400.
- Gửi giả user, entity ID, trạng thái, timestamp và ghi chú Landlord không thay đổi những trường server quản lý.
- Hợp đồng/hóa đơn không còn action ghi; gửi giả số tiền, trạng thái Paid và `PaidAt` không thay đổi dữ liệu.
- Tin nháp, ẩn, đã thuê và ID không tồn tại không được dùng để tạo yêu thích/lịch hẹn.
- Yêu cầu sửa chữa không được gửi cho phòng của Tenant khác hoặc phòng không có hợp đồng Active của mình.
- Tạo yêu thích trùng, sửa vào phòng đã yêu thích, sửa/xóa hợp lệ được xử lý đúng.
- Lịch hẹn/sửa chữa đã xử lý không thể bị Tenant sửa/hủy; hủy giữ bản ghi và phản hồi.
- Enum sai và mô tả quá dài không được lưu; nội dung `<script>` được hiển thị như văn bản.

Kiểm thử chạy bằng host MVC riêng và SQLite trong bộ nhớ. Nó không chạy `Program.cs`, không sử dụng cấu hình kết nối SQL Server hoặc database nhóm. Role/Identity trong kiểm thử do test handler cấp; đây chưa phải kiểm thử đăng nhập Google hoặc email/mật khẩu thật. Chưa chạy kiểm thử đầu cuối với SQL Server và trình duyệt trên ứng dụng chính.

Lệnh chạy lại tại thư mục dự án:

```powershell
dotnet build EZ-Rent.csproj
dotnet test tests/TenantSecurity.Tests/TenantSecurity.Tests.csproj
```

`EZ-Rent.csproj` chỉ bổ sung loại trừ thư mục `tests` khỏi compile/content của ứng dụng chính. Thư viện TestHost, SQLite và xUnit chỉ nằm trong project kiểm thử. Kết quả `.trx`, log, bin và obj được `.gitignore` loại trừ.

## Giới hạn cần Đức/nhóm xử lý

**Chưa thể coi toàn ứng dụng đã được bảo vệ:** controller hợp đồng/hóa đơn phía Landlord vẫn không có `[Authorize]` và chưa kiểm tra owner. `Program.cs` cũng không có fallback authorization policy. Vì vậy bảo vệ các route `/Tenant/...` chưa ngăn được một người dùng truy cập dữ liệu qua route `/Landlord/...` hiện có. Đây là điểm cần sửa trước khi đánh giá phân quyền toàn hệ thống hoặc dùng dữ liệu thật.

Những controller đó thuộc phần phối hợp với người phụ trách Landlord/Đức; gói này không tự chỉnh ngoài phạm vi đã duyệt. Cần duyệt một gói riêng để bổ sung vai trò và quyền sở hữu phía Landlord, rà soát Admin và CRUD tin đăng công khai. Chỉ thêm kiểm tra role là chưa đủ: Landlord cũng phải giới hạn dữ liệu theo chính tài khoản của mình.

Không thay model, enum, DbContext, migration hoặc database nhóm. `Program.cs` giữ nguyên nội dung trước gói này, đã đối chiếu SHA256. Không thêm nghiệp vụ ký hợp đồng, thanh toán thật, upload ảnh/video hoặc chat vào gói bảo vệ dữ liệu.

## Kế hoạch tiếp theo để Dương review

Ưu tiên phối hợp chặn đường truy cập tài chính qua Landlord trước khi coi phân quyền là hoàn chỉnh. Nếu Dương tiếp tục gói giao diện Tenant, đề xuất gói 3:

1. Dựng trang tìm phòng và chi tiết theo P3 bằng dữ liệu thật, chỉ lấy tin công khai hợp lệ; thêm bộ lọc và phân trang.
2. Ghép yêu thích vào danh sách/chi tiết, tái sử dụng kiểm tra quyền đã có.
3. Dựng luồng chọn ngày/khung giờ và danh sách lịch hẹn của mình. Dương đã chốt ngày 15/10/2026: khung do chủ nhà mở, báo trước ít nhất 6 giờ và vẫn chờ chủ nhà xác nhận. Chốt thêm thời lượng/khoảng nghỉ, xử lý khung Pending và hạn phản hồi, phối hợp nguồn dữ liệu khung giờ với Landlord/Đức trước khi ghép chức năng thật. Xem `tenant-appointment-decisions.md`.
4. Kiểm tra luồng trên mobile; trình outline tablet/desktop để review riêng.

Kế hoạch này chưa triển khai. Ký hợp đồng, onboarding/yêu cầu thuê, VietQR, chat và lưu video vẫn theo các mốc đã thống nhất trong `tenant-implementation-plan.md`.
