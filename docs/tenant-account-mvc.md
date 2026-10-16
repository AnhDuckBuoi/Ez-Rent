# Tài khoản Tenant

## Phạm vi

Nhánh `feature/tenant-account`, phát triển tiếp từ checkpoint `afb7083` của `feature/tenant-p3`. PR #2 của gói trước vẫn chờ Đức review. Gói tài khoản được lưu riêng trên nhánh tính năng; chờ Đức review và duyệt trước khi ghép vào nhánh chính.

- Cá nhân: xem email và trạng thái xác minh; sửa họ tên, số điện thoại; tải/gỡ ảnh đại diện.
- Bảo mật: đổi mật khẩu bằng mật khẩu hiện tại; tạo mật khẩu nếu trước đó chỉ đăng nhập ngoài; liên kết/gỡ Google khi phù hợp; đăng xuất.
- Menu Tenant dùng chung dẫn đến `/Tenant/Account`. Desktop/tablet từ 768 px dùng header trên, mobile giữ điều hướng hiện có.
- Tái sử dụng `ApplicationUser`, `UserManager`, `SignInManager`, feedback chung và `_TenantHomeLayout`. Không thêm bảng, migration hoặc provider đăng nhập.
- Không sửa `Program.cs` đang có thay đổi cục bộ từ trước. Không chạy ứng dụng chính hoặc SQL Server của nhóm.

## Đọc mã theo thứ tự

1. `Areas/Tenant/ViewModels/AccountModels.cs`: các trường form được phép gửi và validation. Ba input riêng cho hồ sơ, ảnh, mật khẩu; không bind `ApplicationUser` từ request.
2. `Areas/Tenant/Controllers/AccountController.cs`: từng action thực hiện một thao tác. Danh tính lấy từ phiên hiện tại; mọi thao tác ghi dùng POST và anti-forgery. Kiểm tra `IdentityResult` trước khi báo thành công.
3. `Areas/Tenant/Services/TenantAvatarStorage.cs`: kiểm tra dung lượng thực, chữ ký PNG/JPEG và kích thước; đặt tên ngẫu nhiên; chỉ xóa đường dẫn nội bộ có tên do ứng dụng sinh.
4. `Areas/Tenant/Views/Account`: hai trang và partial tab điều hướng. CSS/JS riêng chỉ được tải ở trang tài khoản.
5. `tests/TenantSecurity.Tests/TenantAccountTests.cs` và `TenantAccountCookieTests.cs`: kiểm tra dữ liệu, ownership, overposting, lỗi Identity, upload, mật khẩu và phiên đăng nhập thật.

## Các quyết định cần biết

- Số điện thoại không bắt buộc. Khi đổi số, đặt `PhoneNumberConfirmed = false`; chưa thêm luồng OTP. Email chỉ đọc vì đổi email cần luồng xác minh riêng.
- Đổi/tạo mật khẩu dùng chính sách Identity đang cấu hình. Không lưu mật khẩu thô, không điền lại mật khẩu vào HTML khi lỗi. Thành công sẽ refresh phiên hiện tại.
- Google chỉ hiển thị nút liên kết nếu scheme thật đã được đăng ký. Callback dùng kiểm tra XSRF của Identity. Không cho gỡ phương thức ngoài cuối cùng nếu chưa có mật khẩu hoặc phương thức khác.
- Chưa kiểm thử OAuth trực tiếp với Google vì test host không chứa thông tin xác thực Google. Các ca chưa cấu hình, callback thiếu danh tính ngoài và bảo vệ cách đăng nhập cuối cùng đã có kiểm thử.
- Ảnh tối đa 2 MB, mỗi chiều tối đa 4.096 px. Tên/mime do client gửi không quyết định tên hoặc phần mở rộng lưu trên server. Không nhận SVG. Lưu file mới trước, cập nhật Identity sau; nếu cập nhật thất bại, dọn file mới; thành công mới dọn file cũ.
- Bộ kiểm tra ảnh đọc chữ ký/header và kích thước, không phải bộ giải mã ảnh đầy đủ hay máy quét malware. File nằm tại `wwwroot/uploads/avatars`, được bỏ qua bởi Git. Khi triển khai nhiều máy hoặc dùng filesystem tạm, cần đổi storage sang nơi lưu bền vững dùng chung và áp dụng chính sách upload của môi trường đó.
- Các trang Identity gốc vẫn dùng cho vai trò khác. Tenant vào màn mới qua menu; không thay đổi toàn bộ UI đăng nhập/đăng ký của nhóm.

## Review trên trình duyệt

Chạy test host độc lập:

```powershell
dotnet run --project tests/TenantSecurity.Tests --no-build -- --preview
```

Mở `/review/tenant?page=account`, chọn Desktop / Tablet / Mobile. Chọn Bảo mật để xem trang thứ hai. Đây là mô phỏng viewport trình duyệt, chưa thay thế kiểm tra trên thiết bị vật lý.

1. Sửa tên, số điện thoại rồi Lưu thay đổi; tải lại và mở Trang chủ để kiểm tra tên.
2. Thử nhập số điện thoại sai hoặc bỏ trống tên; không được lưu và phải hiện lỗi cạnh trường.
3. Chọn PNG/JPEG nhỏ, xem trước rồi Lưu ảnh đại diện; tải lại; thử Gỡ ảnh hiện tại.
4. Ở Bảo mật, nhập sai mật khẩu hiện tại rồi thử lại đúng. Mật khẩu demo ban đầu là `TenantDemo@123`, email `minhanh@example.test` — chỉ có trong SQLite tạm của test host.
5. Đăng xuất rồi truy cập lại trang Cá nhân: phải về Login; đăng nhập bằng mật khẩu vừa đổi. Nếu đã đổi mật khẩu trong buổi review, dùng mật khẩu mới hoặc khởi động lại host để reset dữ liệu demo.

Preview dùng cookie Identity thật. Cookie marker chỉ tự đăng nhập demo một lần mỗi browser session; không tự đăng nhập lại sau Logout. Nếu restart host làm mất SQLite nhưng browser vẫn giữ cookie cũ, xóa cookie localhost hoặc mở cửa sổ mới để bắt đầu lại.

## Kiểm tra

```powershell
dotnet test tests/TenantSecurity.Tests --no-restore
```

Kết quả ngày 17/10/2026:

- Build thành công; 16 cảnh báo nullable có sẵn ở EmailSender và các view Admin/Landlord/global, không có cảnh báo mới ở tài khoản.
- Toàn bộ suite: **152/152 qua** tại `tenant-account-final.trx`. Sau khi bổ sung ca JPEG thật và chuẩn hóa mã, chạy riêng nhóm tài khoản: **19/19 qua** tại `tenant-account-confirmation.trx` (suite hiện có 153 ca, thêm một ca so với lần chạy toàn bộ).
- Trình duyệt Chrome: **13 kiểm tra qua**, không có lỗi JavaScript. Hai trang × bốn độ rộng (1440, 834, 390, 320 px) không tràn ngang; tất cả input hiển thị có label; desktop/tablet dùng menu trên, mobile dùng menu hiện có. Năm kiểm tra luồng gồm lưu hồ sơ và tên trên Home, upload/gỡ ảnh thật, lỗi mật khẩu không điền lại bí mật, đổi mật khẩu → logout → login mới.
- Ảnh review ba kích thước và JSON: `C:/Users/ASUS/.codex/visualizations/2026/10/13/01a135d7-a110-7673-955a-c339104538cc/tenant-account-{desktop,tablet,mobile}.png`, các ảnh có hậu tố `-security`, và `tenant-account-browser.json`.
- Test host chỉ dùng SQLite trong bộ nhớ, không chạy `Program.cs` của ứng dụng. Thay đổi cục bộ `Program.cs` trước gói được giữ nguyên SHA256 `A7C65A326EE5C6703B7A21ADE59B4009AAB6D98BD9440357A48C105465D1E64E`.
