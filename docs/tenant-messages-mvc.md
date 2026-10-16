# Tin nhắn Tenant — ghép MVC, 16/10/2026

Dương đã duyệt ghép thiết kế Tin nhắn riêng vào ứng dụng, cả desktop/tablet/mobile, và yêu cầu ưu tiên tái sử dụng mã gốc. Bản này thay placeholder tại `/Tenant/Dashboard/Messages`; chưa triển khai gửi tin hoặc gửi yêu cầu hỗ trợ.

## Phần đã ghép và tái sử dụng

- `Conversation`, `Message`, `ConversationType` và DbSet/quan hệ nguyên bản: đọc hội thoại TenantLandlord/TenantAdmin mà Tenant hiện tại là Initiator hoặc Recipient. Không thêm bảng, enum, migration hoặc đăng ký dịch vụ vào Program.cs.
- `TenantControllerBase`: giữ yêu cầu Identity ID và role Tenant. `DashboardController.Messages` nhận bộ lọc được kiểm tra, gọi `TenantMessagesReader`, trả ViewModel cho Razor; không đưa entity hoặc ID tài khoản vào JavaScript.
- `TenantRentalDiscovery.Card`, `RentalPresentation.SafeMediaUrl` và placeholder: dùng lại quy tắc phòng công khai/còn trống, chọn ảnh và chặn URL ảnh nguy hiểm. Tin ẩn, nháp hoặc đã thuê được che thông tin phòng; lịch sử tin nhắn của thành viên vẫn xem được.
- `AppointmentModel` và luồng lịch hẹn đã có: đọc lịch xác nhận sắp tới của đúng Tenant và đúng phòng. Ngày xem phòng giữ cách lưu giờ địa phương hiện có; `Message.SentAt` theo UTC nguyên bản, hiển thị UTC+7.
- `TenantDesktopHeader`, `_HomeIcon`, `_TenantPagination`, `_TenantFeedback` và `tenant-shell.js`: thêm lối Tin nhắn trên header dùng chung, giữ các mục/active/bell hiện tại. Tái sử dụng thông báo nổi đã thống nhất với mẫu Admin khi tải hộp thư thất bại. Home đã có shortcut Tin nhắn, tiếp tục dùng route đó.
- CSS và tương tác responsive kế thừa bản thử được thích, chuyển vào `wwwroot`; font Be Vietnam Pro tự lưu kèm SIL OFL, chỉ tải trong layout Tin nhắn. Không dùng tài nguyên từ thư mục docs khi chạy MVC.

## Hành vi hiện tại

Desktop trên 1240px có ba cột. Tablet 768–1240px có hộp thư/chat và dialog thông tin bên phải. Mobile có hai màn riêng, điều hướng dưới ở hộp thư, nút quay lại ở chat. Điện thoại ngang dưới 1024px và cao tối đa 600px với pointer coarse dùng một màn. VisualViewport, safe-area, tìm kiếm khi viewport thấp và các nút cảm ứng theo bản đã duyệt.

Danh sách 10 hội thoại/trang, lịch sử 40 tin/trang, phân trang số dùng component chung. Trang lịch sử 1 chứa tin mới nhất; từng trang hiển thị theo thứ tự thời gian tăng dần. Sắp xếp có ID làm tiêu chí phụ khi trùng giờ. Chỉ đọc preview cuối mỗi hội thoại, không tải toàn bộ lịch sử. Tìm kiếm GET theo tên hoặc tiêu đề phòng công khai, tối đa 100 ký tự; phụ thuộc collation của database, không tuyên bố tìm không dấu như prototype trong bộ nhớ.

`ConversationId`, `Query`, `Filter`, `Page`, `MessagePage`, `View` nằm trong URL; browser Back/Forward và link thường vẫn dùng được. JavaScript tải GET/Razor để thay nội dung, giữ bản nháp riêng từng hội thoại và nội dung/nhãn phòng của bản nháp hỗ trợ trong bộ nhớ tab. Không đưa tin nhắn hoặc nội dung hỗ trợ vào URL/localStorage. Bản nháp mất khi tải lại hoặc rời tab sau khi chấp nhận cảnh báo.

Ô soạn cho nhập và dùng gợi ý bản nháp; Enter xuống dòng. Nút gửi vô hiệu và mô tả rõ chưa thể gửi. Không tự thêm tin, trả lời, biên nhận, trạng thái online hoặc thành công giả. Form hỗ trợ có validation, sửa lỗi cạnh trường, focus vào lỗi đầu, xem trước bằng textContent, chỉnh sửa/đóng; không tạo mã yêu cầu. Form dùng method dialog để không đưa nội dung lên URL nếu JavaScript tắt. Hội thoại TenantAdmin có sẵn vẫn đọc như dữ liệu gốc.

## Quyền và ranh giới ghi dữ liệu

Kiểm tra thành viên và loại hội thoại trước khi đọc ID. ID hội thoại người khác, loại không dành cho Tenant hoặc không tồn tại đều trả 404; thiếu Identity ID/sai role bị chặn. Message có sender không thuộc hai thành viên không được dùng làm preview, tính unread hoặc hiển thị. Razor encode nội dung và tên. Trang đặt Cache-Control no-store. Bộ lọc/số trang sai trả 400 có đường phục hồi.

GET không đánh dấu đã đọc, sửa UpdatedAt hoặc tạo tin/hội thoại/yêu cầu. Số chưa đọc lấy IsRead gốc và giữ nguyên sau khi mở. Không có POST Messages; gửi tay trả 405. Chưa tái sử dụng Admin Support như một hộp hỗ trợ tổng quát vì controller đó đang thao tác RepairRequest, là nghiệp vụ sửa chữa khác.

Để mở gửi thật ở gói sau cần phối hợp hai vai trò: tạo/tìm hội thoại, POST gửi CSRF và kiểm tra thành viên, đánh dấu đã đọc bằng thao tác ghi rõ ràng, phản hồi Admin/Landlord, luồng tạo/yêu cầu hỗ trợ và xử lý lỗi. Không tự thêm các bước đó trong gói giao diện này.

## Xác minh

- Build đạt; 16 cảnh báo nullable cũ ở EmailSender/Admin/Landlord/các view ngoài gói.
- 133/133 kiểm thử HTTP/SQLite đạt, gồm 14 trường hợp mới: quyền, IDOR, sender lạ, loại hội thoại, dữ liệu không bị ghi, POST 405, bộ lọc/pager, tin ẩn và ảnh không an toàn, UTC+7, XSS, input sai. File kết quả `tests/TenantSecurity.Tests/TestResults/tenant-messages.trx`.
- 10 nhóm kiểm tra trình duyệt đạt: hội thoại thật từ SQLite preview, bản nháp qua chuyển trang/Back, gửi bị khóa, modal/focus, hỗ trợ validation/xem trước, giữ phòng của bản nháp hỗ trợ qua hội thoại khác, tìm kiếm viewport thấp, lỗi mạng dùng feedback chung, bố cục/ảnh/font/đường dẫn/reduced motion. Không lỗi JS, asset hoặc request ghi trong luồng kiểm tra.
- Kích thước kiểm tra: 1440×1000, 834×1112, 390×844, 320×700, 844×390 và viewport thấp 390×430. Chưa xác minh Safari/bàn phím ảo trên thiết bị vật lý.
- Bằng chứng tại artifact của chat: `tenant-messages-mvc-functional.json`, `mvc-final-*.png`. Một phép so sánh datetime trong test đã được sửa để decode dấu cộng do Razor encode; dữ liệu giờ không thay đổi. Selector tự động mở hỗ trợ cũng được giới hạn vào dialog đang mở thay vì trúng nút hộp thư bị ẩn.

## Review và dữ liệu mẫu

Chạy test host độc lập bằng `dotnet run --project tests/TenantSecurity.Tests -- --preview`; mở `http://127.0.0.1:8770/Tenant/Dashboard/Messages`. Chỉ test host có hội thoại và danh tính mẫu trong SQLite bộ nhớ. Production đọc dữ liệu đã có của người đăng nhập; không tự seed mẫu và không chạy Program.cs/database nhóm để kiểm tra gói này.

Preview có ba hội thoại, phòng và lịch liên quan. Ảnh ở `/images/tenant-preview/living.jpg` là ảnh minh họa chỉ gắn bởi fixture preview, có nhãn và nguồn Caleb Oquendo/Pexels trong SOURCE.md. Không gán ảnh này cho tin phòng production.

Nhánh `feature/tenant-p3`, không commit/push. SHA256 Program.cs giữ nguyên `A7C65A326EE5C6703B7A21ADE59B4009AAB6D98BD9440357A48C105465D1E64E`. Không sửa model/DbContext/migration/Landlord/Admin/database nhóm. Bản thử docs được giữ để đối chiếu; MVC hiện đã ghép phần đọc và giao diện theo phạm vi ở trên.


## Đồng bộ chính xác menu trên — 17/10/2026

Dương phản ánh menu Tin nhắn hơi lệch các trang khác. Nguyên nhân là CSS Tin nhắn ghi đè max-width header thành 1440px thay vì 1280px, đổi kích thước account, màu/logo và các quy tắc 768–1023px (ẩn chuông, đổi gap/cỡ chữ); header còn kế thừa Be Vietnam Pro từ nội dung Tin nhắn.

Đã bỏ các ghi đè này khỏi tenant-messages.css. tenant-shell.css định nghĩa font Segoe UI 15px/1.55 và màu chữ trực tiếp cho header dùng chung, không phụ thuộc font nội dung từng trang. Khung chat vẫn giữ phông, chiều rộng và bố cục đã duyệt. Không đổi controller/Razor/dữ liệu.

Đã so sánh 4 trang (Home, Tìm phòng, Lịch hẹn, Tin nhắn) × 6 chiều rộng 768/834/938/1024/1440/1920: 24 trường hợp đạt. Header, inner container, logo, chuông và account có cùng hình học/kiểu chữ/màu; khoảng cách và font các mục menu giống nhau. Giữ chữ đậm cho mục active theo thiết kế chung, nên độ rộng nét chữ active có thể khác nhẹ. Kiểm tra thêm mobile 390px, mở chat, đổi sang tablet rồi quay lại vẫn giữ bản nháp và quy tắc ẩn header trong chat mobile. Không lỗi JS. Chỉ CSS, không cần build lại hoặc chạy lại bộ C#.

Bằng chứng artifact: tenant-messages-header-alignment.json, tenant-messages-header-fixed-938.png.
