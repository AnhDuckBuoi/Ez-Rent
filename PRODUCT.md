# EZ Rent — product context

<!-- impeccable:product-schema 1 -->

## Đồng bộ về thư mục gốc và dataset nhóm — 25/10/2026

Bản làm việc hiện ở `D:\Workspace\University\WebNC\BTL\Ez Rent`, nhánh `feature/tenant-integration`. Đã ghép Landlord PR #6 vào nền Tenant và giữ sửa chat/đăng nhập local; chờ review trước khi commit/push. Demo dùng DbSeeder gốc trên SQL Server LocalDB riêng, không dùng dữ liệu hardcode của host SQLite. Giữ UI Tenant của Dương, Trang chủ công khai của nhóm và RecommendationService của Đức; dùng chung bộ lọc và điều hướng Tenant về một luồng. Xem [hướng dẫn chạy và bàn giao](docs/tenant-team-dataset.md). Các mục bên dưới là checkpoint trước.

## Chat tự cập nhật và đã xem — 25/10/2026

Dương duyệt tối ưu đúng phạm vi Tenant–Landlord: tự nhận tin, tự cập nhật Đã xem theo tin thực sự hiển thị, giữ các tín hiệu đang chờ và đồng bộ sau kết nối lại. Không thêm presence/đang nhập/model/migration. Giữ layout chung và SignalR hiện tại; nút tải chỉ là khôi phục khi lỗi hoặc JavaScript không hoạt động. 275/275 kiểm thử và 16 kiểm tra trình duyệt đạt; bản sửa chờ review, chưa commit tiếp. Xem [quy tắc và cách thử](docs/tenant-chat-live.md). Các checkpoint bên dưới là lịch sử.

## Bản tích hợp sau review của Đức — 21/10/2026

Trạng thái hiện tại nằm trên `feature/tenant-integration`, checkout riêng dựa trên `origin/main` tại `19d0900`. Tenant dùng chính `Views/Shared/_Layout.cshtml` và menu chung của nhóm; CSS được giới hạn trong vùng nội dung Tenant. Tìm phòng tái sử dụng `RecommendationService`/SearchHistory của main. Chat Tenant–Landlord đã có gửi/nhận realtime qua SignalR/WebSocket, tái sử dụng phần chat từ nhánh Landlord, giữ bảng Conversation/Message gốc và POST có CSRF. Hỗ trợ TenantAdmin có gửi mới/gửi bổ sung, chưa có UI trả lời Admin. 274/274 kiểm thử và 38 kiểm tra trình duyệt đạt. Bản review mới ở cổng 8771; Dương đã duyệt commit/push nhánh feature để Đức review, chưa merge main hoặc thay database nhóm. Xem [bàn giao tích hợp](docs/tenant-integration-checkpoint.md); các mục bên dưới ghi lại phạm vi từng checkpoint cũ, không thay thế trạng thái mới này.

## Hỗ trợ/phản ánh Tenant — 21/10/2026

Dương duyệt tiếp tục hỗ trợ/phản ánh, dùng mã gốc. Gói trên feature/tenant-support dùng Conversation/Message loại TenantAdmin, mỗi yêu cầu một hội thoại; gửi văn bản, lọc và gửi bổ sung thật. Chọn người nhận theo vai trò Admin hoạt động trên server; không có người nhận thì chặn gửi. Không đưa vào RepairRequest vì Admin/Support tại origin/main 19d0900 vẫn là sửa chữa. Chưa ghép màn trả lời Admin hoặc quy trình trạng thái xử lý. Không model/migration/Program.cs mới, chưa commit/push, chưa ghi database nhóm. Xem docs/tenant-support-mvc.md.

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
- UC09/UC10: đã ghép trang Tin nhắn MVC responsive, đọc Conversation/Message của thành viên từ model gốc, gửi văn bản cho chủ nhà, gửi yêu cầu hỗ trợ/gửi bổ sung và đánh dấu tin nhận đã hiển thị. Chưa có giao diện trả lời Landlord/Admin. Xem docs/tenant-support-mvc.md, docs/tenant-messages-write.md và lịch sử giao diện tại docs/tenant-messages-mvc.md.
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
