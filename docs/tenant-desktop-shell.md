# Đồng bộ desktop Tenant — 16/10/2026

Dương đã duyệt: thanh điều hướng desktop dùng chung, phân trang bằng số, thông báo sau thao tác theo mẫu Admin của Đức. Nhánh `feature/tenant-p3`; chưa commit/push.

## Phạm vi và thiết kế

Áp dụng thanh điều hướng trên dùng chung từ 768px (đã sửa mốc cũ 1024px) cho Dashboard, Help, Messages, RentalPost danh sách/chi tiết, Favorite danh sách, ViewingAppointment danh sách/chi tiết/hủy và Notification danh sách/chi tiết. Các màn hợp đồng/hóa đơn/sửa chữa còn dùng layout cũ nằm ngoài gói này. Bản thử thiết kế v4 vẫn riêng, chưa thay giao diện MVC bằng v4.

Thanh trên: EZ Rent | Trang chủ · Tìm phòng · Lịch hẹn · Đã lưu · Tin nhắn | Chuông · Tài khoản. Cùng chiều cao 85px, nội dung tối đa 1280px, lề ngang 30px, ghim top 0, đánh dấu đúng mục khi vào chi tiết. Ẩn thanh dưới và header cũ từ 768px. Khoảng 768–1023px dùng chiều cao 76px, lề 20px và khoảng cách menu gọn hơn. Dưới 768px giữ điều hướng mobile. Kích thước nội dung trang vẫn đổi ở mốc cũ 1024px.

Giữ bảng màu của MVC hiện tại: nền #f6f7f9, bề mặt #ffffff, chữ #1f2f40, chữ phụ #586779, chính #356797, đường viền #e0e4e9; Segoe UI đang có trong dự án. Trọng tâm là khung sử dụng ổn định khi chuyển trang, không đưa thêm nội dung trang chủ hay ảnh vào gói này.

## Luồng dữ liệu và tái sử dụng

Request → controller/service hiện tại → ViewModel → layout → `TenantDesktopHeaderViewComponent` → header dùng chung. Component chỉ đọc số thông báo chưa đọc theo ID người dùng đăng nhập, không nhận ID từ trình duyệt. Những truy vấn và kiểm tra quyền của chức năng hiện có được giữ nguyên.

`TenantPaginationModel` + `_TenantPagination` dùng cho Tìm phòng/Đã lưu/Lịch hẹn/Thông báo. Chọn số trang gửi yêu cầu lấy đúng trang hiện tại, không nối thêm kết quả theo cuộn. Giữ bộ lọc và sắp xếp; đánh dấu trang hiện tại, có Trước/Sau và dấu … cho khoảng trang dài, tối đa 7 số. Liên kết hoạt động khi không có JavaScript. Tìm phòng/Đã lưu tiếp tục thay nội dung qua AJAX, cập nhật URL và hỗ trợ Back.

Mẫu tham chiếu: `Areas/Admin/Views/Shared/_StatusMessage.cshtml` (Bootstrap alert vàng nhạt, có nút đóng, không tự ẩn). Tenant dùng `_TenantFeedback`, `tenant-shell.css/js` tái hiện mẫu này, không tải toàn bộ Bootstrap vào các layout Tenant. Thông báo POST–redirect và kết quả AJAX dùng cùng hình thức; lỗi dùng màu đỏ. Lỗi nhập trường vẫn nằm trong form. Nội dung Razor được mã hóa, AJAX gán bằng textContent; giữ CSRF và kiểm tra quyền hiện có. Đóng bằng bàn phím đưa focus về main.

## Xác minh

- Bộ kiểm tra độc lập dùng SQLite trong bộ nhớ: **107/107 đạt** (`tenant-desktop-shell.trx`); gồm 102 kiểm tra trước đó và 5 trường hợp mới về header theo quyền và giới hạn số trang lớn.
- Build qua; còn 16 cảnh báo nullable có sẵn ngoài phần Tenant thay đổi. Bản preview cũ đã được dừng để giải phóng DLL trước khi chạy bản mới.
- Trình duyệt: 9 trang × desktop 1024/1440/1920 = 27; Dashboard/Tìm phòng × 390/834/1023 = 6 kiểm tra hồi quy. Tổng **33 trường hợp đạt**, không tràn ngang, ID trùng, lỗi JavaScript. Desktop chỉ có header mới; màn nhỏ giữ header/menu cũ. Sticky được kiểm tra trên trang có đủ nội dung cuộn.
- Luồng: chọn trang 2 giữ sort, Back trở lại trang 1; bỏ lưu/lưu lại phòng; đóng thông báo bằng chuột và Enter; focus sau đóng; nội dung HTML hiển thị dạng văn bản; thông báo còn sau hơn 4 giây; ReadAll redirect hiển thị phản hồi và cập nhật badge; phản hồi 503 mô phỏng có thông báo lỗi và cho thử lại.
- Đã xem ảnh Tìm phòng 1024px và Thông báo 1440px. Ảnh và `checks.json` lưu trong thư mục artifact `tenant-desktop-shell` của chat.

Chỉ chạy host preview độc lập tại localhost:8770; không chạy Program.cs chính hoặc ghi database nhóm. Không đổi schema, Landlord hay Admin. Hash Program.cs giữ nguyên `A7C65A326EE5C6703B7A21ADE59B4009AAB6D98BD9440357A48C105465D1E64E`.

## Review

Mở `/Tenant/Dashboard` rồi chuyển Tìm phòng → Lịch hẹn → Đã lưu → chuông. Trên Tìm phòng, xuống cuối danh sách chọn số 2. Bấm tim lưu phòng để xem khối phản hồi và nút × phía trên nội dung. Dữ liệu ở localhost:8770 là minh họa trong bộ nhớ.


## Sửa lệch điều hướng khi cửa sổ máy tính hẹp

Dương phản ánh chuyển từ Trang chủ sang Tìm phòng/Lịch hẹn lại thấy menu dưới. Đã tái hiện ở viewport 938px: Home dùng header cũ ở trên và không có bottom nav, trong khi Discovery dùng tenant-nav cố định dưới. Nguyên nhân là shell dùng chung chỉ bật từ 1024px, còn hai layout có quy tắc khác nhau ở phía dưới mốc này. Đây là thiếu sót của lần đồng bộ trước, không phải đổi thiết kế có chủ đích.

Đã đổi ngưỡng thanh trên dùng chung xuống 768px trong tenant-shell.css, có kiểu gọn 768–1023px. Giữ mốc thay đổi kích thước nội dung từ 1024px và hành vi mobile dưới 768px. Tái sử dụng header/component và các liên kết hiện tại, không thêm cách nhận diện thiết bị theo tên trình duyệt.

Đã kiểm tra trực tiếp 9 trang × 4 chiều rộng 768/938/1024/1440px và 4 trang tại mobile 390px: 40 trường hợp đạt. Thêm 4 lần chuyển trang bằng menu để xác nhận mục active, menu dưới luôn ẩn và header ghim khi cuộn. Không lỗi JS. Ảnh tenant-nav-938-fixed.png và tenant-navigation-fix.json nằm trong artifact của chat. Chỉ sửa CSS/tài liệu, không cần build lại hoặc chạy lại bộ C# 133 test đã đạt ở gói trước.
