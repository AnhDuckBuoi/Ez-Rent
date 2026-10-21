# EZ Rent — product context

<!-- impeccable:product-schema 1 -->

## Gửi tin Tenant — 21/10/2026

Dương duyệt nhắn chủ nhà từ phòng, lưu văn bản và cập nhật tin nhận đã nhìn thấy, giữ UI ba kích thước. Dùng Conversation/Message gốc, không migration; chỉ tạo hội thoại ở lần gửi đầu. Chặn trang cũ khi chủ phòng thay đổi, giữ nội dung khi mất phản hồi. Tải tin mới bằng GET; chưa realtime hoặc UI trả lời Landlord/Admin, TenantAdmin vẫn chỉ đọc. Xem docs/tenant-messages-write.md. Dương đã duyệt commit gói trên feature/tenant-messages để Đức review; chưa gộp nhánh chính hoặc ghi database nhóm.

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
- UC09/UC10: đã ghép trang Tin nhắn MVC responsive, đọc Conversation/Message của thành viên từ model gốc, gửi văn bản cho chủ nhà và đánh dấu tin nhận đã hiển thị. Giữ bản nháp và xem trước hỗ trợ; chưa gửi hỗ trợ hoặc có giao diện trả lời Landlord/Admin. Xem docs/tenant-messages-write.md và lịch sử giao diện tại docs/tenant-messages-mvc.md.
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

## Phòng đang ở — 17/10/2026

Dương đã duyệt tổng quan phòng đang thuê, thiết kế lại hợp đồng/hóa đơn/sửa chữa và ghép menu ba kích thước. Dùng các model và quy tắc sẵn có, chưa cần migration. Hợp đồng/hóa đơn chỉ đọc; sửa chữa dùng luồng gửi/sửa/hủy thật, chỉ đổi khi còn chờ, giữ lịch sử. Chưa triển khai ký hợp đồng hay thanh toán VietQR. Chi tiết đọc mã và QA: docs/tenant-my-room-mvc.md.

## Bản thử tài liệu thuê phòng — 17/10/2026

Dương duyệt thử giao diện tự điền mẫu hợp đồng, in/lưu PDF và lưu bản được ký bên ngoài; tạm hoãn ký trên web. Hóa đơn cần giữ ảnh điện/nước theo chỉ số đúng phòng/kỳ. Bản thử riêng chỉ dùng dữ liệu minh họa và tệp trong bộ nhớ trình duyệt, chưa triển khai lưu trữ thật hoặc sửa dữ liệu chung. Xem docs/tenant-documents-preview.md; mẫu nội dung cần Dương và Đức chốt.

## Ghép tài liệu MVC và BM03 — 17/10/2026

Dương duyệt ghép xem/lưu tài liệu, cung cấp mẫu BM03 và chốt nước theo m³. Tenant nay đọc/tải tài liệu riêng, xem lịch sử, xác nhận đối chiếu theo phiên bản và xem ảnh/chỉ số đã lưu cho hóa đơn; có dịch vụ ghi dùng chung chờ Đức ghép Landlord. Bản nháp BM03 tự điền dữ liệu đang có, để trống thông tin chưa có, không ký trên web. Migration mã nguồn đã thêm, chưa áp dụng database nhóm. Xem docs/tenant-documents-mvc.md về phần đã có và các bước phát hành/thông tin cá nhân còn cần ghép.


## Bổ sung thông tin BM03 — 17/10/2026

Dương duyệt form thông tin Tenant và lịch sử bản nháp. Mỗi lần lưu tạo bản chụp riêng cho hợp đồng, gồm thông tin cá nhân và thỏa thuận do server đọc; không sửa hồ sơ hay tài liệu đã lưu. Bản cũ chỉ đọc; có kiểm tra tab cũ và nguồn hợp đồng thay đổi. Điều khoản hợp đồng được giữ riêng trong phụ lục đối chiếu khi in. Chưa ký trên web hoặc bổ sung dữ liệu Landlord còn thiếu. Xem docs/tenant-contract-draft.md; migration chỉ ở mã nguồn, chưa áp dụng database nhóm.
