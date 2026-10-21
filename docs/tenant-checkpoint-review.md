# Checkpoint Tenant P3 — 17/10/2026

Nhánh nguồn: `feature/tenant-p3`. Nhánh đích để review: `main`. Dương yêu cầu Đức review và duyệt trước khi gộp; không tự merge hoặc bật auto-merge.

## Kết quả có thể review

- Tenant chỉ xem/thao tác bản ghi của mình; role và Identity ID bắt buộc. Hợp đồng/hóa đơn chuyển về chỉ đọc, bỏ CRUD tài chính của Tenant. POST còn lại dùng anti-forgery và kiểm tra trạng thái.
- Tìm phòng công khai hợp lệ, bộ lọc/sắp xếp/phân trang số, chi tiết và lưu/bỏ lưu của mình.
- Home thiên về lịch/thông báo/hướng dẫn, danh sách/chi tiết/hủy lịch của mình, thông báo riêng theo tài khoản.
- Header dùng chung từ 768px, đồng bộ phông/kích thước/menu Tin nhắn; giao diện Tin nhắn có desktop/tablet/mobile, đọc Conversation/Message nguyên bản, giữ bản nháp và xem trước hỗ trợ.
- Đặt lịch từ đúng phòng có giao diện ngày/giờ và reader seam. Nguồn giờ dùng chung chưa được nối trong production; không tạo lịch giả.

## Chưa hoàn tất nghiệp vụ

- Nguồn khung giờ Landlord, giữ độc quyền, hạn phản hồi 2 giờ và quy tắc báo trước 6 giờ phía server. Các action Create/Edit lịch cũ còn tồn tại và cần phối hợp chuyển sang cùng quy tắc; UI mới chưa gửi thật.
- Gửi tin, đánh dấu đã đọc, tạo/gửi hỗ trợ và phản hồi phía Landlord/Admin.
- Ký hợp đồng, VietQR tự gạch nợ và lưu video sự cố.
- Khoảng hở phân quyền tài chính ở route Landlord/global nằm ngoài gói Tenant; không coi phân quyền toàn hệ thống là hoàn chỉnh.

Không thay model dùng chung, DbContext, migration, code Landlord/Admin hoặc database nhóm.

## Kiểm tra và chạy thử

`dotnet test tests/TenantSecurity.Tests --no-restore`: **133/133 đạt** trong lần kiểm tra checkpoint. Build thành công. Bộ test dùng MVC TestServer và SQLite trong bộ nhớ, không chạy Program.cs production hoặc SQL Server của nhóm. Identity login/OAuth và database SQL Server thực tế chưa nằm trong bộ kiểm thử này.

`dotnet run --project tests/TenantSecurity.Tests -- --preview`, mở `http://127.0.0.1:8770/review/tenant`. Chọn Desktop/Tablet/Mobile; thử Trang chủ → Tìm phòng → Chi tiết → Đặt lịch; lịch của mình; lưu/bỏ lưu; thông báo; Tin nhắn và bản nháp/xem trước hỗ trợ. Dữ liệu mẫu riêng, reset khi host khởi động lại. Bản review mô phỏng viewport, không phải kiểm chứng Safari/bàn phím ảo trên thiết bị thật.

Tài liệu triển khai chính: tenant-security-review.md, tenant-discovery-plan.md, tenant-home-mvc.md, tenant-desktop-shell.md, tenant-booking-mvc-ui.md, tenant-appointment-data-contract.md và tenant-messages-mvc.md. Các bản HTML khác trong docs là lịch sử thiết kế/prototype, không thay thế hành vi MVC hiện tại.

## Phạm vi Git

Không đưa thay đổi cấu hình đăng nhập có sẵn trong Program.cs vào checkpoint Tenant; file local được giữ nguyên. Không đưa .impeccable/design.json, kết quả test, log, bin/obj, node_modules hoặc bí mật cấu hình vào commit. Tài nguyên ảnh/phông dùng lại có nguồn và giấy phép kèm theo.
