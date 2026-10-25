# Trang chủ Tenant — outline bản 03 (Impeccable)

Ngày: 15/10/2026. Nhánh `feature/tenant-p3`. Căn cứ: Dương muốn Trang chủ riêng, ưu tiên tin/thông báo và nội dung hữu ích; phòng chỉ là một phần nhỏ, danh sách đầy đủ nằm ở Tìm phòng. Dương cũng yêu cầu giao diện bớt đơn điệu. Đã trình kế hoạch trước khi sửa outline.

Cập nhật sau khi Dương đồng ý kế hoạch: đã ghép vào MVC. Xem [triển khai và kiểm thử Home MVC](tenant-home-mvc.md). Nội dung bên dưới ghi lịch sử bản outline v3.

## Kết quả để review

Mở `http://127.0.0.1:8766/tenant-responsive-outline.html?v=3`. Trang chủ được chọn mặc định; có thể đổi 390/834/1024/1440 px và chuyển sang các màn cũ. Canvas riêng: `tenant-responsive-canvas.html?view=home&v=3`.

Thứ tự ưu tiên bản 03:

1. Lời chào và nút Tìm phòng; không còn banner minh họa lớn.
2. Lịch hẹn tiếp theo **đã xác nhận**, hiển thị rõ ngày/giờ đến, địa chỉ và nút mở đúng lịch. Pending chỉ nằm trong yêu cầu/thông báo.
3. Hai thông báo gần đây, mở danh sách đầy đủ qua chuông hoặc Xem tất cả.
4. Góc người thuê: bộ lọc chủ đề, bài đọc mở ngay trong trang. Các bài mặc định thu gọn. Checklist năm mục dùng checkbox thực, giữ trạng thái trong lần xem thử.
5. Lối tắt dạng hàng, tối đa hai phòng đã lưu và Hỗ trợ ở cột tiện ích desktop; tablet dưới 1000px/mobile đưa xuống cùng luồng đọc.

## Cách áp dụng Impeccable

Đã cài skill gốc pbakaus/impeccable. Đã đọc SKILL.md, new-work, operate, craft-floor, document và hướng dẫn review trực tiếp. Launcher context gặp lỗi Windows `cache_directory_failed`; dùng tài liệu và code dự án làm ngữ cảnh, không báo rằng gate tự động đã chạy. Chỉ giữ nhận diện P3 và thiết kế lại cấu trúc Home, không đổi toàn bộ thương hiệu. PRODUCT.md ghi sự thật đã chốt; tenant-home-design-brief.md ghi phạm vi; DESIGN.md và .impeccable/design.json ghi các quy tắc quan sát từ bản dựng, chỉ áp cho Home preview.

Bỏ các hình SVG mô phỏng cảnh phòng/checklist, mảng màu pastel cho lối tắt, nhãn chữ hoa trên tiêu đề và dialog đọc bài. Dùng chữ lớn hơn, đường phân cách, khoảng cách nhóm và trạng thái chức năng để phân cấp. Desktop có cột tiện ích hẹp; dưới 1000px dựng DOM theo thứ tự đọc thực tế. Không có tải font/ảnh/thư viện ngoài.

## Tương tác trong outline

- Thông báo chưa đọc, đánh dấu tất cả đã đọc và mở đúng lịch; giữ trong bộ nhớ khi chuyển màn/đổi thiết bị.
- Bài viết mở/thu gọn trực tiếp, aria-expanded/aria-controls; lọc chủ đề, giữ checkbox khi chuyển màn/đổi kích thước. Tải lại trang khôi phục dữ liệu mẫu.
- Tin nhắn/Hỗ trợ vẫn là lối vào minh họa, chưa phải UC09/UC10 hoàn chỉnh. Hỗ trợ có thể dẫn đến đúng bài ngay trong trang.
- Phòng đã lưu dùng chung trạng thái với Tìm phòng/chi tiết; mở từ Home rồi quay lại vẫn về Home. Bỏ lưu hết hiển thị lời hướng dẫn rỗng.
- Bản mẫu thể hiện người đang tìm phòng. Chưa thiết kế hoặc ghép dữ liệu hóa đơn/phòng đang ở cho người có hợp đồng.

## Dữ liệu thật cần nối sau khi duyệt

Source có `Models/Notification.cs` và DbSet, gồm UserId, Title, Content, Link, Type, IsRead, CreatedAt. Dashboard hiện chưa đọc Notification; gói này không sửa controller hoặc ghi trạng thái đọc vào database. Khi ghép cần lọc đúng Tenant, encode nội dung và kiểm tra đường dẫn/quyền tại trang đích; thao tác đánh dấu đọc cần CSRF và owner check.

Chưa tìm thấy nguồn Article/News được triển khai trong model hiện tại. Các bài trên outline là nội dung mẫu, không phải bản tin đã xuất bản hoặc tin từ bên thứ ba. Cần thống nhất nguồn nội dung biên tập/Admin trước khi đưa thành chức năng thật; không tự thêm model/migration hoặc quy trình quản trị từ việc dựng outline.

Mọi thông báo, tên, bài, phòng và thời gian trong bản này là minh họa. Đồng hồ cố định 07:00 ngày 15/10/2026 theo Việt Nam; không dựa vào ngày máy chạy thử. Không gửi tin nhắn, email, request ghi dữ liệu hoặc đến chủ nhà thật.

## Kiểm tra

- Lượt đầu: Chromium kiểm tra cả năm màn trên 320/390/768/834/999/1024/1199/1440px (40 tổ hợp), không tràn ngang, ID trùng hay lỗi JavaScript; không có request ghi trong các luồng đã thử.
- Đã thử checklist và trạng thái giữ qua breakpoint, lọc/đọc bài inline, mở đúng lịch xác nhận/chờ, đánh dấu đọc, Escape và focus, Hỗ trợ, phòng đã lưu/quay lại, đồng bộ tab ngoài và giữ trạng thái trong iframe khi đổi thiết bị.
- Sau kiểm tra ảnh, thu gọn bài mặc định để tránh kéo dài Home; chốt lại tám chiều rộng Home và chụp desktop/tablet/mobile. Sửa đường dẫn trợ giúp để luôn mở bài ngay cả khi trước đó bài đã mở.
- Kiểm tra riêng chưa có lịch/chưa có phòng lưu, chỉ có Pending không hiện thành Confirmed, dialog ở 320px và thay đổi thiết bị trong bảng review.
- Không sửa backend, Figma, model/migration hoặc dữ liệu thật. Không chạy lại test C# cho thay đổi HTML/CSS/JS tĩnh này. Chưa commit/push.
