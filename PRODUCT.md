# EZ Rent — product context

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Người thuê tìm phòng, lưu lựa chọn, đặt lịch xem và theo dõi phản hồi. Dương phụ trách Tenant; Đức chốt dữ liệu dùng chung. Các vai trò khác là Landlord và Admin.

## Product Purpose

Hỗ trợ hành trình tìm và thuê phòng. Trang chủ Tenant có thông báo, lịch hẹn và nội dung hữu ích. Danh sách phòng đầy đủ nằm ở Tìm phòng; trang chủ chỉ có phần phòng nhỏ và lối vào Tìm phòng.

## Operating Context

Dự án môn Lập trình Web Nâng cao, ASP.NET Core MVC .NET 8, Razor, CSS/JavaScript, EF Core và SQL Server. Thiết kế P3 Figma là căn cứ cho Tenant. Thiết kế responsive phục vụ trình duyệt mobile, tablet và desktop, không phải ứng dụng native.

## Capabilities and Constraints

- Đăng nhập hiện có bằng tài khoản và Google khi được cấu hình. Không thêm nhà cung cấp đăng nhập trong gói Tenant.
- Xem phòng: chọn giờ đến do chủ nhà mở, ít nhất 6 giờ báo trước; ẩn giờ không đủ điều kiện. Yêu cầu chờ giữ riêng giờ tối đa 2 giờ từ khi gửi. Cần xác nhận trước khi đến. Thời lượng xem do hai bên sắp xếp.
- Dương tự kết hợp Tenant và Landlord. Không tự đổi mô hình, migration, dữ liệu chung hoặc quy tắc phía Landlord trong gói giao diện này.
- Ký hợp đồng đang chờ phương án; yêu cầu thuê khi mới đăng nhập để tương lai.
- UC09/UC10: đã ghép trang Tin nhắn MVC responsive, đọc Conversation/Message của thành viên từ model gốc, giữ bản nháp và xem trước hỗ trợ. Chưa có xử lý gửi tin/gửi hỗ trợ/đánh dấu đã đọc; không báo thành công giả. Xem docs/tenant-messages-mvc.md.
- Dương đã đồng ý ghép Home v3 vào MVC. Dashboard đọc tên, lịch xác nhận, thông báo và phòng đã lưu của Tenant từ database; Notification có luồng đọc riêng. Test host tại 8770 chạy MVC với SQLite và dữ liệu mẫu riêng, không kết nối database nhóm. Figma chưa thay đổi.
- Góc người thuê trong MVC dùng hướng dẫn cố định, chưa có CMS. Chưa ghép nguồn phát sinh thông báo từ Landlord; không tự tạo thông báo giả khi tải Home.

## Brand Commitments

Tên EZ Rent, tiếng Việt, màu xanh kế thừa P3. Người dùng muốn giao diện bớt rập khuôn và bớt đơn điệu. Chưa có yêu cầu thay nhận diện toàn sản phẩm.

## Evidence on Hand

docs/tenant-home-mvc.md, docs/tenant-home-plan.md, docs/tenant-responsive-plan.md, docs/tenant-appointment-decisions.md và các bản xem trước liên quan. Chưa chốt nguồn bài viết/CMS. Không suy diễn tin tức hay số liệu hoạt động minh họa thành dữ liệu thật.

## Product Principles

- Trình kế hoạch trước phần việc mới, làm trên nhánh feature/tenant-*, không tự commit/push vào nhánh chính.
- Trang chủ giúp nắm tình hình và tìm đúng thao tác nhanh.
- Trạng thái chờ xác nhận không được trình bày như lịch đã có thể đến xem.
- Câu chữ chỉ rõ hành động, trạng thái và bước tiếp theo.

## Tài khoản Tenant — 17/10/2026

Dương đã duyệt hoàn thiện web Tenant, bắt đầu với Cá nhân/Bảo mật. Gói tài khoản dùng dữ liệu ApplicationUser và Identity sẵn có, không cần migration; sửa hồ sơ/ảnh, đổi hoặc tạo mật khẩu, liên kết Google theo cấu hình và đăng xuất. Email chỉ đọc. Xem docs/tenant-account-mvc.md. Các dữ liệu xem thử vẫn tách khỏi database nhóm.
